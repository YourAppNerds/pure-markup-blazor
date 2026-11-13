using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace PureMarkup.Blazor.Generators.Components;

/// <summary>How a component parameter is set from markup.</summary>
internal enum ParamKind
{
    /// <summary>Any other type: one method taking the declared type (expressions included).</summary>
    Value,
    /// <summary><c>EventCallback</c>: <c>Action</c>, <c>Func&lt;Task&gt;</c>, <c>EventCallback</c>.</summary>
    Event,
    /// <summary><c>EventCallback&lt;T&gt;</c>: four handler shapes plus <c>EventCallback&lt;T&gt;</c>.</summary>
    EventOf,
    /// <summary><c>RenderFragment</c>: <c>Func&lt;Node&gt;</c>.</summary>
    Fragment,
    /// <summary><c>RenderFragment&lt;T&gt;</c>: <c>Func&lt;T, Node&gt;</c>.</summary>
    FragmentOf,
}

/// <param name="Name">Parameter (property) name.</param>
/// <param name="Method">Generated method name (differs from <paramref name="Name"/> on a clash with a <c>ComponentNode</c> member).</param>
/// <param name="Type">Declared type for <see cref="ParamKind.Value"/>; the argument type for EventOf/FragmentOf.</param>
/// <param name="ParamAttributes">Attributes copied onto the method parameter (<c>DynamicallyAccessedMembers</c>).</param>
internal sealed record ParamInfo(string Name, string Method, ParamKind Kind, string Type, string ParamAttributes);

/// <param name="Name">Simple name, for the extension class name.</param>
/// <param name="FullType">Fully qualified type, with its own type parameters (<c>global::X.InputNumber&lt;TValue&gt;</c>).</param>
/// <param name="TypeParameters">Method type parameter list, attributes included, or empty.</param>
/// <param name="Constraints">The <c>where</c> clauses, or empty.</param>
/// <param name="CapturesUnmatched">Has a <c>CaptureUnmatchedValues</c> parameter, so it gets <c>.Attr(name, value)</c>.</param>
/// <param name="HasAnnotatedMembers">A member takes a <c>DynamicallyAccessedMembers</c>-annotated value (e.g. <c>Router.NotFoundPage</c>), which
/// makes every <c>ComponentNode&lt;T&gt;</c> mention warn IL2111.</param>
internal sealed record ComponentInfo(
    string Name, string Namespace, string FullType, string TypeParameters, string Constraints,
    EquatableArray<ParamInfo> Params, bool CapturesUnmatched, bool HasAnnotatedMembers);

/// <summary>Reads components and their <c>[Parameter]</c> properties from symbols. Shared by the pack-time and (M2b) consumer-time generators.</summary>
internal static class ComponentModel
{
    public const string ComponentNodeMetadataName = "PureMarkup.Blazor.ComponentNode`1";

    private static readonly SymbolDisplayFormat Fq = SymbolDisplayFormat.FullyQualifiedFormat
        .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <summary>Method names that a generated parameter method must not take: members of <c>ComponentNode&lt;T&gt;</c>, object members and <c>Attr</c>.</summary>
    public static HashSet<string> ReservedNames(Compilation compilation)
    {
        var set = new HashSet<string>(StringComparer.Ordinal) { "Attr", "Equals", "GetHashCode", "ToString", "GetType" };
        if (compilation.GetTypeByMetadataName(ComponentNodeMetadataName) is { } node)
            foreach (var m in node.GetMembers())
                if (m.DeclaredAccessibility == Accessibility.Public && m.CanBeReferencedByName) set.Add(m.Name);
        return set;
    }

    /// <summary>True for a public, concrete, non-obsolete class that implements IComponent.</summary>
    public static bool IsComponent(INamedTypeSymbol t, INamedTypeSymbol iComponent) =>
        t.TypeKind == TypeKind.Class && !t.IsAbstract && !t.IsStatic && IsPublic(t) && !IsObsolete(t)
        && t.AllInterfaces.Contains(iComponent, SymbolEqualityComparer.Default);

    public static ComponentInfo? Read(INamedTypeSymbol type, Compilation compilation, HashSet<string> reserved, CancellationToken ct)
    {
        var parameterAttr = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.ParameterAttribute");
        var cascadingBase = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.CascadingParameterAttributeBase");
        var renderFragment = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.RenderFragment");
        var renderFragmentOf = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.RenderFragment`1");
        var eventCallback = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.EventCallback");
        var eventCallbackOf = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.EventCallback`1");
        if (parameterAttr is null) return null;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var list = new List<ParamInfo>();
        var captures = false;
        var annotated = false;
        // Most derived first, so a parameter redeclared with 'new' wins over the base one.
        for (var t = type; t is not null; t = t.BaseType)
        {
            ct.ThrowIfCancellationRequested();
            foreach (var m in t.GetMembers())
            {
                if (!annotated && m.DeclaredAccessibility == Accessibility.Public && HasDam(m)) annotated = true;
                if (m is not IPropertySymbol p || p.IsStatic || p.IsIndexer || p.DeclaredAccessibility != Accessibility.Public) continue;
                if (p.SetMethod is null || IsObsolete(p)) continue;
                AttributeData? param = null;
                var cascading = false;
                foreach (var a in p.GetAttributes())
                {
                    if (SymbolEqualityComparer.Default.Equals(a.AttributeClass, parameterAttr)) param = a;
                    else if (cascadingBase is not null && InheritsFrom(a.AttributeClass, cascadingBase)) cascading = true;
                }
                if (param is null || cascading || !seen.Add(p.Name)) continue;
                if (!IsAccessible(p.Type)) continue;

                if (param.NamedArguments.Any(kv => kv.Key == "CaptureUnmatchedValues" && kv.Value.Value is true)) captures = true;

                var named = p.Type as INamedTypeSymbol;
                var def = named?.OriginalDefinition;
                ParamKind kind;
                string typeText;
                if (def is not null && Eq(def, eventCallback)) { kind = ParamKind.Event; typeText = ""; }
                else if (def is not null && Eq(def, eventCallbackOf)) { kind = ParamKind.EventOf; typeText = named!.TypeArguments[0].ToDisplayString(Fq); }
                else if (def is not null && Eq(def, renderFragment)) { kind = ParamKind.Fragment; typeText = ""; }
                else if (def is not null && Eq(def, renderFragmentOf)) { kind = ParamKind.FragmentOf; typeText = named!.TypeArguments[0].ToDisplayString(Fq); }
                else { kind = ParamKind.Value; typeText = p.Type.ToDisplayString(Fq); }

                string method;
                if (p.Name == "ChildContent" && kind == ParamKind.Fragment) continue; // .Children(...) covers it
                else if (p.Name == "ChildContent" && kind == ParamKind.FragmentOf) method = "ChildContent"; // typed twin of ChildContent<TContext>
                else method = reserved.Contains(p.Name) ? p.Name + "Param" : p.Name;

                list.Add(new ParamInfo(p.Name, Escape(method), kind, typeText, DamAttributes(p.GetAttributes())));
            }
        }

        var typeParams = "";
        if (type.TypeParameters.Length > 0)
            typeParams = "<" + string.Join(", ", type.TypeParameters.Select(tp => DamAttributes(tp.GetAttributes()) + tp.Name)) + ">";
        var ns = type.ContainingNamespace.IsGlobalNamespace ? "" : type.ContainingNamespace.ToDisplayString();
        return new ComponentInfo(type.Name, ns, type.ToDisplayString(Fq), typeParams, Constraints(type), new EquatableArray<ParamInfo>(list.ToArray()), captures, annotated);
    }

    private static bool HasDam(ISymbol m) => m switch
    {
        IPropertySymbol p => DamAttributes(p.GetAttributes()).Length > 0 || (p.SetMethod is { } s && HasDam(s)),
        IMethodSymbol ms => DamAttributes(ms.GetReturnTypeAttributes()).Length > 0 || ms.Parameters.Any(x => DamAttributes(x.GetAttributes()).Length > 0),
        _ => false,
    };

    private static bool Eq(ISymbol a, ISymbol? b) => b is not null && SymbolEqualityComparer.Default.Equals(a, b);

    private static bool InheritsFrom(INamedTypeSymbol? t, INamedTypeSymbol baseType)
    {
        for (; t is not null; t = t.BaseType) if (SymbolEqualityComparer.Default.Equals(t, baseType)) return true;
        return false;
    }

    private static bool IsObsolete(ISymbol s) =>
        s.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "System.ObsoleteAttribute");

    private static bool IsPublic(INamedTypeSymbol t)
    {
        for (ISymbol? s = t; s is INamedTypeSymbol n; s = n.ContainingType)
            if (n.DeclaredAccessibility != Accessibility.Public) return false;
        return true;
    }

    /// <summary>A parameter whose type mentions a non-public type (or a pointer) cannot appear in a public method.</summary>
    private static bool IsAccessible(ITypeSymbol t) => t switch
    {
        ITypeParameterSymbol => true,
        IArrayTypeSymbol a => IsAccessible(a.ElementType),
        INamedTypeSymbol n => IsPublic(n) && n.TypeArguments.All(IsAccessible),
        _ => false,
    };

    private static string DamAttributes(IEnumerable<AttributeData> attributes)
    {
        var sb = new StringBuilder();
        foreach (var a in attributes)
        {
            if (a.AttributeClass?.ToDisplayString() != "System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute") continue;
            if (a.ConstructorArguments.Length != 1 || a.ConstructorArguments[0].Value is not int v) continue;
            sb.Append("[global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers((global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes)(")
              .Append(v).Append("))] ");
        }
        return sb.ToString();
    }

    private static string Constraints(INamedTypeSymbol t)
    {
        var sb = new StringBuilder();
        foreach (var tp in t.TypeParameters)
        {
            var parts = new List<string>();
            if (tp.HasReferenceTypeConstraint) parts.Add(tp.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated ? "class?" : "class");
            if (tp.HasUnmanagedTypeConstraint) parts.Add("unmanaged");
            else if (tp.HasValueTypeConstraint) parts.Add("struct");
            if (tp.HasNotNullConstraint) parts.Add("notnull");
            parts.AddRange(tp.ConstraintTypes.Select(c => c.ToDisplayString(Fq)));
            if (tp.HasConstructorConstraint && !tp.HasValueTypeConstraint) parts.Add("new()");
            if (parts.Count > 0) sb.Append(" where ").Append(tp.Name).Append(" : ").Append(string.Join(", ", parts));
        }
        return sb.ToString();
    }

    private static string Escape(string name) => SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;
}

/// <summary>An immutable array with value equality, so incremental generator steps cache.</summary>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T> where T : IEquatable<T>
{
    private readonly T[]? _items;

    public EquatableArray(T[] items) => _items = items;

    public int Length => _items?.Length ?? 0;

    public bool Equals(EquatableArray<T> other) => (_items ?? Array.Empty<T>()).SequenceEqual(other._items ?? Array.Empty<T>());

    public override bool Equals(object? obj) => obj is EquatableArray<T> o && Equals(o);

    public override int GetHashCode()
    {
        var h = 17;
        foreach (var i in _items ?? Array.Empty<T>()) h = h * 31 + (i?.GetHashCode() ?? 0);
        return h;
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)(_items ?? Array.Empty<T>())).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

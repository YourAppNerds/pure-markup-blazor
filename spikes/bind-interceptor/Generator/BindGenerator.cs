using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PureMarkup.Generator;

/// <summary>
/// Intercepts <c>node.Bind(() => a.b.c)</c> and emits typed get/set code for the member chain.
/// The interceptor only receives the Expression (it cannot see lambda captures), so the root object
/// is read from the expression at runtime: <c>this</c> is a ConstantExpression (free), a captured local
/// is one FieldInfo.GetValue on the closure. Everything after the root is typed code; private members
/// go through [UnsafeAccessor].
/// </summary>
[Generator]
public sealed class BindGenerator : IIncrementalGenerator
{
    // Body uses {S} as a placeholder for the site index, filled in at emit time.
    sealed record Site(string Attribute, string NodeType, string ExprType, string Body, string Accessors);

    static readonly SymbolDisplayFormat Fq = SymbolDisplayFormat.FullyQualifiedFormat;

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var sites = context.SyntaxProvider.CreateSyntaxProvider(
                static (n, _) => n is InvocationExpressionSyntax
                {
                    Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Bind" },
                    ArgumentList.Arguments.Count: 1,
                },
                static (c, ct) => Analyze(c, ct))
            .Where(static s => s is not null)
            .Select(static (s, _) => s!);

        context.RegisterSourceOutput(sites.Collect(), Emit);
    }

    static Site? Analyze(GeneratorSyntaxContext c, CancellationToken ct)
    {
        var inv = (InvocationExpressionSyntax)c.Node;
        var model = c.SemanticModel;
        if (model.GetSymbolInfo(inv, ct).Symbol is not IMethodSymbol { Name: "Bind", ReducedFrom: { } from } m) return null;
        if (from.ContainingType.ToDisplayString() != "PureMarkup.BindExtensions") return null;
        if (m.TypeArguments.Any(t => t is ITypeParameterSymbol)) return null;
        if (inv.ArgumentList.Arguments[0].Expression is not ParenthesizedLambdaExpressionSyntax
            { ParameterList.Parameters.Count: 0, ExpressionBody: { } body }) return null;

        var compilation = model.Compilation;
        bool Accessible(ISymbol s) => compilation.IsSymbolAccessibleWithin(s, compilation.Assembly);

        // Walk the chain right to left: a.b.c -> members [b, c] with root a.
        var members = new List<ISymbol>();
        string? rootType = null; // cast target for BindRuntime.Root; null for a static root
        string? staticRoot = null;
        var e = body;
        while (true)
        {
            while (e is PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.SuppressNullableWarningExpression } p) e = p.Operand;
            while (e is ParenthesizedExpressionSyntax pe) e = pe.Expression;

            if (e is MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } ma)
            {
                var sym = model.GetSymbolInfo(ma, ct).Symbol;
                if (!IsMember(sym)) return null;
                members.Insert(0, sym!);
                if (model.GetSymbolInfo(ma.Expression, ct).Symbol is INamedTypeSymbol t)
                {
                    if (!sym!.IsStatic || !Accessible(t)) return null;
                    staticRoot = t.ToDisplayString(Fq);
                    break;
                }
                e = ma.Expression;
                continue;
            }
            if (e is ThisExpressionSyntax)
            {
                rootType = EnclosingType(model, inv, ct);
                break;
            }
            if (e is IdentifierNameSyntax id)
            {
                var sym = model.GetSymbolInfo(id, ct).Symbol;
                if (sym is ILocalSymbol { Type: var lt } && lt.IsReferenceType && Accessible(lt)) { rootType = lt.ToDisplayString(Fq); break; }
                if (sym is IParameterSymbol { Type: var pt } && pt.IsReferenceType && Accessible(pt)) { rootType = pt.ToDisplayString(Fq); break; }
                if (!IsMember(sym)) return null;
                members.Insert(0, sym!);
                if (sym!.IsStatic)
                {
                    if (!Accessible(sym.ContainingType)) return null;
                    staticRoot = sym.ContainingType.ToDisplayString(Fq);
                }
                else rootType = EnclosingType(model, inv, ct);
                break;
            }
            return null;
        }
        if (members.Count == 0) return null;
        if (staticRoot is null && rootType is null) return null;

        // Intermediate members must be reference types (a write through a struct copy is lost),
        // the last member must be writable, and every owner type must be nameable for [UnsafeAccessor].
        for (var i = 0; i < members.Count; i++)
        {
            var s = members[i];
            var type = s is IPropertySymbol ps ? ps.Type : ((IFieldSymbol)s).Type;
            if (i < members.Count - 1 && !type.IsReferenceType) return null;
            if (!Accessible(s.ContainingType) || s.ContainingType.IsGenericType) return null;
            if (s.ContainingType.IsValueType) return null;
            if (staticRoot is not null && !Accessible(s)) return null; // accessors need an instance
        }
        var last = members[members.Count - 1];
        if (last is IPropertySymbol { SetMethod: null or { IsInitOnly: true } }) return null;
        if (last is IFieldSymbol { IsReadOnly: true } or IFieldSymbol { IsConst: true }) return null;

        var accessors = new StringBuilder();
        string Read(ISymbol s, string target, int i)
        {
            if (s is IPropertySymbol p)
            {
                if (Accessible(p.GetMethod!)) return $"{target}.{p.Name}";
                accessors.AppendLine($"        [global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = \"get_{p.Name}\")]");
                accessors.AppendLine($"        static extern {p.Type.ToDisplayString(Fq)} __s{{S}}_{i}_get({p.ContainingType.ToDisplayString(Fq)} o);");
                return $"__s{{S}}_{i}_get({target})";
            }
            var f = (IFieldSymbol)s;
            if (Accessible(f)) return $"{target}.{f.Name}";
            accessors.AppendLine($"        [global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = \"{f.Name}\")]");
            accessors.AppendLine($"        static extern ref {f.Type.ToDisplayString(Fq)} __s{{S}}_{i}_ref({f.ContainingType.ToDisplayString(Fq)} o);");
            return $"__s{{S}}_{i}_ref({target})";
        }

        var cur = staticRoot ?? "__root";
        for (var i = 0; i < members.Count - 1; i++) cur = Read(members[i], cur, i);
        var n = members.Count - 1;
        var value = Read(last, cur, n);
        string set;
        if (last is not IPropertySymbol lp) set = $"{value} = v"; // a field accessor returns ref, so this covers private fields
        else if (Accessible(lp.SetMethod!)) set = $"{cur}.{lp.Name} = v";
        else
        {
            accessors.AppendLine($"        [global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = \"set_{lp.Name}\")]");
            accessors.AppendLine($"        static extern void __s{{S}}_{n}_set({lp.ContainingType.ToDisplayString(Fq)} o, {lp.Type.ToDisplayString(Fq)} v);");
            set = $"__s{{S}}_{n}_set({cur}, v)";
        }

        var valueType = m.TypeArguments[1].ToDisplayString(Fq);
        var sb = new StringBuilder();
        if (staticRoot is null)
            sb.AppendLine($"            var __root = ({rootType})global::PureMarkup.BindRuntime.Root(expr, {members.Count})!;");
        sb.Append($"            return global::PureMarkup.BindExtensions.BindCore(node, expr, {value}, ({valueType} v) => {set});");

        var location = model.GetInterceptableLocation(inv, ct);
        if (location is null) return null;
        return new Site(
            location.GetInterceptsLocationAttributeSyntax(),
            $"global::PureMarkup.ComponentNode<{m.TypeArguments[0].ToDisplayString(Fq)}>",
            $"global::System.Linq.Expressions.Expression<global::System.Func<{valueType}>>",
            sb.ToString(),
            accessors.ToString());
    }

    static bool IsMember(ISymbol? s) => s is IFieldSymbol or IPropertySymbol { IsIndexer: false, Parameters.Length: 0 };

    static string? EnclosingType(SemanticModel model, SyntaxNode at, CancellationToken ct)
    {
        var s = model.GetEnclosingSymbol(at.SpanStart, ct);
        var t = s as INamedTypeSymbol ?? s?.ContainingType;
        if (t is null || t.IsGenericType || t.IsValueType) return null;
        return model.Compilation.IsSymbolAccessibleWithin(t, model.Compilation.Assembly) ? t.ToDisplayString(Fq) : null;
    }

    static void Emit(SourceProductionContext spc, ImmutableArray<Site> sites)
    {
        if (sites.IsEmpty) return;
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable disable"); // signatures must match the call site; nullability is not part of the match
        sb.AppendLine("namespace System.Runtime.CompilerServices");
        sb.AppendLine("{");
        sb.AppendLine("    [global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = true)]");
        sb.AppendLine("    file sealed class InterceptsLocationAttribute : global::System.Attribute");
        sb.AppendLine("    {");
        sb.AppendLine("        public InterceptsLocationAttribute(int version, string data) { }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("namespace PureMarkup.Generated");
        sb.AppendLine("{");
        sb.AppendLine("    file static class BindInterceptors");
        sb.AppendLine("    {");
        for (var i = 0; i < sites.Length; i++)
        {
            var s = sites[i];
            var idx = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            sb.AppendLine($"        {s.Attribute}");
            sb.AppendLine($"        public static {s.NodeType} Bind_{idx}(this {s.NodeType} node, {s.ExprType} expr)");
            sb.AppendLine("        {");
            sb.AppendLine(s.Body.Replace("{S}", idx));
            sb.AppendLine("        }");
            if (s.Accessors.Length > 0) sb.Append(s.Accessors.Replace("{S}", idx));
            sb.AppendLine();
        }
        sb.AppendLine("    }");
        sb.AppendLine("}");
        spc.AddSource("BindInterceptors.g.cs", sb.ToString());
    }
}

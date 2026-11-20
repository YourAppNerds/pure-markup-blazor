using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace PureMarkup.Blazor.Generators.Analyzers;

/// <summary>
/// <c>.Prop("name", value)</c> with a constant name: PMCSS020 (warning) when the name is not a w3c/webref property or legacy
/// alias (the <c>prop</c>, <c>alias</c> and <c>descriptors</c> lines of <c>Css/css.data</c>, embedded at build time), a custom property
/// (<c>--x</c>) or vendor-prefixed (<c>-webkit-x</c>), nor an <c>@font-face</c> descriptor inside <c>Css.FontFace</c>, with a suggestion for a near miss; PMCSS021 (info) when the property has
/// a typed method. Non-constant names are not checked.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CssPropertyNameAnalyzer : DiagnosticAnalyzer
{
    private const string Category = "PureMarkup.Styling";

    internal static readonly DiagnosticDescriptor Unknown = new(
        "PMCSS020", "Unknown CSS property",
        "'{0}' is not a CSS property (w3c/webref), a custom property (--name) or vendor-prefixed (-webkit-...){1}",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor HasTypedMethod = new(
        "PMCSS021", "CSS property has a typed method",
        "'{0}' has a typed method: use .{1}(...)",
        Category, DiagnosticSeverity.Info, isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Unknown, HasTypedMethod);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var properties = start.Compilation.GetTypeByMetadataName("PureMarkup.Blazor.Styling.CssProperties`1");
            if (properties is null) return;
            var css = start.Compilation.GetTypeByMetadataName("PureMarkup.Blazor.Styling.Css");
            start.RegisterOperationAction(ctx =>
            {
                var call = (IInvocationOperation)ctx.Operation;
                if (call.TargetMethod.Name != "Prop" || !SymbolEqualityComparer.Default.Equals(call.TargetMethod.ContainingType.OriginalDefinition, properties)) return;
                var arg = call.Arguments.FirstOrDefault(a => a.Parameter?.Name == "name");
                if (arg?.Value.ConstantValue is not { HasValue: true, Value: string name }) return;
                var location = arg.Value.Syntax.GetLocation();
                switch (Check(name, InFontFace(call, css)))
                {
                    case (true, var suggestion): ctx.ReportDiagnostic(Diagnostic.Create(Unknown, location, name, suggestion)); break;
                    case (false, var method): ctx.ReportDiagnostic(Diagnostic.Create(HasTypedMethod, location, name, method)); break;
                }
            }, OperationKind.Invocation);
        });
    }

    /// <summary>For a constant property name: null (fine), (false, typed method) or (true, suggestion text) for an unknown name.</summary>
    internal static (bool Unknown, string Arg)? Check(string name, bool fontFace = false)
    {
        if (name.StartsWith("-", StringComparison.Ordinal) && name.Length > 1) return null;   // --custom or -vendor-
        var data = Data.Value;
        var lower = name.Trim().ToLowerInvariant();
        if (fontFace && data.FontFaceDescriptors.Contains(lower)) return null;
        if (data.Methods.TryGetValue(lower, out var method)) return (false, method);
        if (data.Aliases.TryGetValue(lower, out var target))
            return data.Methods.TryGetValue(target, out var standard) ? (false, standard) : null;
        return (true, Suggest(lower, data) is { } s ? $"; did you mean '{s}' (.{data.Methods[s]}(...))?" : "");
    }

    /// <summary>True inside the lambda of <c>Css.FontFace(f => ...)</c>, where <c>@font-face</c> descriptors (<c>src</c>, ...) are valid.</summary>
    private static bool InFontFace(IOperation op, INamedTypeSymbol? css)
    {
        for (var o = op.Parent; o is not null; o = o.Parent)
        {
            if (o is not IAnonymousFunctionOperation) continue;
            for (var p = o.Parent; p is not null; p = p.Parent)
            {
                if (p is IInvocationOperation i)
                    return i.TargetMethod.Name == "FontFace" && SymbolEqualityComparer.Default.Equals(i.TargetMethod.ContainingType, css);
                if (p is not (IDelegateCreationOperation or IArgumentOperation or IConversionOperation)) return false;
            }
            return false;
        }
        return false;
    }

    private static string? Suggest(string name, Names data)
    {
        // backgroundColor, BackgroundColor, background_color
        var squashed = new string(name.Where(char.IsLetterOrDigit).ToArray());
        if (data.BySquashedMethod.TryGetValue(squashed, out var byMethod)) return byMethod;
        if (name.Length < 4) return null;
        string? best = null;
        var bestDistance = name.Length < 8 ? 2 : 3;
        foreach (var candidate in data.Methods.Keys)
        {
            if (Math.Abs(candidate.Length - name.Length) >= bestDistance) continue;
            var d = Distance(name, candidate);
            if (d < bestDistance || (d == bestDistance && best is null)) { best = candidate; bestDistance = d; }
        }
        return best;
    }

    private static int Distance(string a, string b)
    {
        var prev = new int[b.Length + 1];
        var cur = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) prev[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            cur[0] = i;
            for (var j = 1; j <= b.Length; j++)
                cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            (prev, cur) = (cur, prev);
        }
        return prev[b.Length];
    }

    internal sealed class Names
    {
        public Dictionary<string, string> Methods { get; } = new(StringComparer.Ordinal);   // css name -> C# method
        public Dictionary<string, string> Aliases { get; } = new(StringComparer.Ordinal);   // legacy alias -> css name
        public Dictionary<string, string> BySquashedMethod { get; } = new(StringComparer.Ordinal);   // backgroundcolor -> background-color
        public HashSet<string> FontFaceDescriptors { get; } = new(StringComparer.Ordinal);
    }

    internal static readonly Lazy<Names> Data = new(() =>
    {
        var names = new Names();
        using var stream = typeof(CssPropertyNameAnalyzer).Assembly.GetManifestResourceStream("PureMarkup.Blazor.Generators.css.names")
            ?? throw new InvalidOperationException("css.names resource missing");
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line)
        {
            var parts = line.Split(' ');
            if (parts.Length > 2 && parts[0] == "descriptors" && parts[1] == "@font-face")
            {
                foreach (var d in parts.Skip(2)) names.FontFaceDescriptors.Add(d);
                continue;
            }
            if (parts.Length < 2) continue;
            var eq = parts[1].IndexOf('=');
            if (eq < 0) continue;
            string css = parts[1].Substring(0, eq), value = parts[1].Substring(eq + 1);
            if (parts[0] == "prop")
            {
                names.Methods[css] = value;
                names.BySquashedMethod[value.ToLowerInvariant()] = css;
            }
            else if (parts[0] == "alias") names.Aliases[css] = value;
        }
        return names;
    });
}

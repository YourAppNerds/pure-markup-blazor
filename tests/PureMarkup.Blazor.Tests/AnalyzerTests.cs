extern alias gen;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using gen::PureMarkup.Blazor.Generators.Analyzers;

namespace PureMarkup.Blazor.Tests;

/// <summary>Stylesheet purity (PMCSS010-013) and <c>.Prop</c> names (PMCSS020-021), on in-memory compilations.</summary>
public class AnalyzerTests
{
    private static readonly MetadataReference[] Refs = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
        .Append(typeof(ComponentNode<>).Assembly.Location)
        .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToArray();

    private const string Usings = """
        global using System;
        global using System.IO;
        global using System.Linq;
        global using System.Collections.Generic;
        global using System.Net.Http;
        global using Microsoft.AspNetCore.Components;
        global using Microsoft.AspNetCore.Components.Web;
        global using Microsoft.JSInterop;
        global using PureMarkup.Blazor;
        global using PureMarkup.Blazor.Styling;
        global using static PureMarkup.Blazor.H;
        global using static PureMarkup.Blazor.Styling.CssUnits;
        global using RouteData = Microsoft.AspNetCore.Components.RouteData;
        """;

    private static Compilation Compile(IEnumerable<string> sources) =>
        CSharpCompilation.Create("App", sources.Prepend(Usings).Select(s => CSharpSyntaxTree.ParseText(s, new CSharpParseOptions(LanguageVersion.Preview))), Refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

    private static ImmutableArray<Diagnostic> Analyze(Compilation c) =>
        c.WithAnalyzers([new StylesheetPurityAnalyzer(), new CssPropertyNameAnalyzer()]).GetAnalyzerDiagnosticsAsync().Result;

    private static List<string> Run(string source)
    {
        var c = Compile([source]);
        Assert.Empty(c.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()));
        return Analyze(c).OrderBy(d => d.Location.SourceSpan.Start)
            .Select(d => $"{d.Id} {d.Location.SourceTree!.GetText().ToString(d.Location.SourceSpan)}: {d.GetMessage()}").ToList();
    }

    [Fact]
    public void Purity_DirectViolations_InStylesheet()
    {
        var d = Run("""
            [Stylesheet]
            public static class BadCss
            {
                [Inject] public static NavigationManager? Nav { get; set; }
                public static IJSRuntime? Js;
                public static Rule Card { get; } = Css.Rule(r => r
                    .Width(Px(File.ReadAllText("w.txt").Length))
                    .Content(Environment.GetEnvironmentVariable("X") ?? "")
                    .Content(DateTime.Now.ToString())
                    .ZIndex(new Random().Next())
                    .Content(Environment.NewLine));
                public static readonly HttpClient Http = new();
            }
            """);
        Assert.Collection(d,
            x => Assert.StartsWith("PMCSS010 Nav: 'BadCss.Nav' uses '[Inject]'", x),
            x => Assert.StartsWith("PMCSS010 Js: 'BadCss.Js' uses 'IJSRuntime'", x),
            x => Assert.StartsWith("PMCSS011 File.ReadAllText(\"w.txt\"): 'BadCss.Card' uses 'File.ReadAllText'", x),
            x => Assert.StartsWith("PMCSS011 Environment.GetEnvironmentVariable(\"X\"): 'BadCss.Card' uses 'Environment.GetEnvironmentVariable'", x),
            x => Assert.StartsWith("PMCSS012 DateTime.Now: 'BadCss.Card' uses 'DateTime.Now'", x),
            x => Assert.StartsWith("PMCSS012 new Random(): 'BadCss.Card' uses 'Random'", x),
            x => Assert.StartsWith("PMCSS010 Http: 'BadCss.Http' uses 'HttpClient'", x));
    }

    [Fact]
    public void Purity_Indirect_ThroughMethodsAndStaticInitializers()
    {
        var d = Run("""
            public static class Helper
            {
                public static readonly string Loaded = File.ReadAllText("x.css");
                public static string Pure(int n) => "a" + n;
                public static string Load() => Deeper();
                private static string Deeper() => Environment.MachineName;
                public static string Stamp => DateTime.UtcNow.Ticks.ToString();
            }
            [Stylesheet]
            public static class UsesHelper
            {
                public static Rule A { get; } = Css.Rule(r => r.Content(Helper.Load()));
                public static Rule B { get; } = Css.Rule(r => r.Content(Helper.Stamp));
                public static Rule C { get; } = Css.Rule(r => r.Content(Helper.Pure(1)));
            }
            """);
        Assert.Collection(d,
            x => Assert.Equal("PMCSS013 Helper.Load(): 'UsesHelper.A' reaches 'Environment.MachineName' through 'Helper.Load'; the build runs stylesheets and presets in a separate process, with no services, JS or app environment", x),
            x => Assert.StartsWith("PMCSS013 Helper.Load(): 'UsesHelper.A' reaches 'File.ReadAllText' through 'Helper'", x),
            x => Assert.StartsWith("PMCSS013 Helper.Stamp: 'UsesHelper.B' reaches 'DateTime.UtcNow' through 'Helper.Stamp'", x),
            x => Assert.StartsWith("PMCSS013 Helper.Stamp: 'UsesHelper.B' reaches 'File.ReadAllText' through 'Helper'", x),
            x => Assert.StartsWith("PMCSS013 Helper.Pure(1): 'UsesHelper.C' reaches 'File.ReadAllText' through 'Helper'", x));
    }

    [Fact]
    public void Purity_Presets_AreRoots_EvenOutsideStylesheets()
    {
        var d = Run("""
            public sealed class Page : MarkupComponent
            {
                private static readonly string Machine = Environment.MachineName;
                public static Preset<ButtonElement> Stamped => Preset.Once<ButtonElement>(p => p.Title(DateTime.Now.ToString()));
                protected override Node Render() => Button().Use(Stamped).Text(File.ReadAllText("ok-at-runtime.txt"));
            }
            """);
        Assert.Collection(d,
            x => Assert.StartsWith("PMCSS013 Stamped: 'Page.Stamped' reaches 'Environment.MachineName' through 'Page'", x),
            x => Assert.StartsWith("PMCSS012 DateTime.Now: 'Page.Stamped' uses 'DateTime.Now'", x));
    }

    [Fact]
    public void Purity_PureCode_NoDiagnostics()
    {
        var d = Run("""
            public static class Sizes { public const int Base = 4; public static Length Step(int n) => Px(Base * n); }
            [Stylesheet]
            public static class Tokens2
            {
                public static Token<Color> Surface { get; } = Css.Token("surface", light: Hex("#fff"), dark: Hex("#111"));
            }
            [Stylesheet]
            public static class GoodCss
            {
                private static readonly string[] Fonts = ["Inter", "system-ui"];
                public static Rule Card { get; } = Css.Rule(r => r
                    .Padding(Sizes.Step(2)).Background(Tokens2.Surface)
                    .FontFamily(string.Join(", ", Fonts.Select(f => f.ToLowerInvariant())))
                    .Width(Px(Math.Round(10.4))));
                public static Var<Length> Fill { get; } = Css.Var<Length>(Percent(0));
            }
            public sealed class Runtime : MarkupComponent
            {
                [Inject] public IJSRuntime Js { get; set; } = default!;
                protected override Node Render() => Div().Class(GoodCss.Card).Text(DateTime.Now.ToString());
            }
            """);
        Assert.Empty(d);
    }

    [Fact]
    public void PropNames()
    {
        var d = Run("""
            [Stylesheet]
            public static class P
            {
                private const string Known = "colr";
                public static Rule A { get; } = Css.Rule(r => r
                    .Prop("colr", "red")
                    .Prop("backgroundColor", "red")
                    .Prop("totally-made-up", "1")
                    .Prop("--brand", "red")
                    .Prop("-webkit-line-clamp", 3)
                    .Prop("color", "red")
                    .Prop("word-wrap", "break-word")
                    .Prop("border", Px(1), "solid", "red")
                    .Prop(Known, "x")
                    .Prop(Dynamic(), "x"));
                private static string Dynamic() => "colr";
                public static FontFace Inter { get; } = Css.FontFace(f => f.FontFamily("Inter").Prop("src", "url(/inter.woff2)").Prop("unicode-range", "U+0-FF"));
                public static Rule NotFontFace { get; } = Css.Rule(r => r.Prop("src", "x"));
            }
            """);
        Assert.Equal([
            "PMCSS020 \"colr\": 'colr' is not a CSS property (w3c/webref), a custom property (--name) or vendor-prefixed (-webkit-...); did you mean 'color' (.Color(...))?",
            "PMCSS020 \"backgroundColor\": 'backgroundColor' is not a CSS property (w3c/webref), a custom property (--name) or vendor-prefixed (-webkit-...); did you mean 'background-color' (.BackgroundColor(...))?",
            "PMCSS020 \"totally-made-up\": 'totally-made-up' is not a CSS property (w3c/webref), a custom property (--name) or vendor-prefixed (-webkit-...)",
            "PMCSS021 \"color\": 'color' has a typed method: use .Color(...)",
            "PMCSS021 \"word-wrap\": 'word-wrap' has a typed method: use .OverflowWrap(...)",
            "PMCSS021 \"border\": 'border' has a typed method: use .Border(...)",
            "PMCSS020 Known: 'colr' is not a CSS property (w3c/webref), a custom property (--name) or vendor-prefixed (-webkit-...); did you mean 'color' (.Color(...))?",
            "PMCSS020 \"src\": 'src' is not a CSS property (w3c/webref), a custom property (--name) or vendor-prefixed (-webkit-...)",
        ], d);
    }

    public static TheoryData<string, string> Apps => new()
    {
        { "samples/WebApp", "WebApp.Client.Theme.Tokens" },
        { "templates/PureMarkup.Blazor.Templates/content/puremarkup-blazor", "PureMarkupApp.Theme.Tokens" },
    };

    /// <summary>The sample and the template stay free of PMCSS warnings and errors (info suggestions are fine).</summary>
    [Theory]
    [MemberData(nameof(Apps))]
    public void SampleAndTemplate_HaveNoPmcssWarnings(string dir, string tokensType)
    {
        // tests/PureMarkup.Blazor.Tests/bin/{Configuration}/net10.0/
        var repo = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Parent!.Parent!.Parent!.Parent!.Parent!.FullName;
        var root = Path.Combine(repo, dir);
        var files = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(f => Path.GetFileName(f) is not ("Program.cs" or "GlobalUsings.cs"))
            .Select(File.ReadAllText).ToList();
        var c = Compile(files.Append("global using WebApp.Client.Theme; namespace WebApp.Client.Theme { } global using PureMarkupApp.Theme; namespace PureMarkupApp.Theme { }"));
        // The analyzers only see what resolves: the stylesheet attribute must bind.
        var tokens = c.GetTypeByMetadataName(tokensType);
        Assert.NotNull(tokens);
        Assert.Contains(tokens.GetAttributes(), a => a.AttributeClass?.Name == "StylesheetAttribute");
        var bad = Analyze(c).Where(d => d.Severity >= DiagnosticSeverity.Warning).Select(d => d.ToString()).ToList();
        Assert.Empty(bad);
    }
}

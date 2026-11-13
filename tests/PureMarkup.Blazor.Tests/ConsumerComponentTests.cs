extern alias gen;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using gen::PureMarkup.Blazor.Generators.Components;

namespace PureMarkup.Blazor.Tests;

/// <summary>A C# component in this project: it gets typed methods without an attribute.</summary>
public sealed class Panel : MarkupComponent
{
    [Parameter] public string? Title { get; set; }
    [Parameter] public RenderFragment? Header { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }

    protected override Node Render() => Section().Children(H2().Text(Title), Content(Header), Content(ChildContent));
}

public sealed class UsesPanel : MarkupComponent
{
    protected override Node Render() => Fragment(
        Comp<Panel>().Title("t1").Header("plain <text>").Children("body"),
        Comp<Panel>().Title("t2").Header(() => B().Text("bold")));
}

/// <summary>A component derived from a framework one: <c>ComponentNode&lt;T&gt;</c> is invariant, so it needs its own (flattened) methods.</summary>
public sealed class UsesDerived : MarkupComponent
{
    public Model M { get; } = new() { Name = "Ann" };

    protected override Node Render() => Comp<EditForm>().Model(M).ChildContent(_ =>
        Comp<ProbeInput>().Value(M.Name).ValueExpression(() => M.Name).DisplayName("Name").Attr("id", "name"));
}

public class ConsumerComponentTests
{
    [Fact]
    public async Task LocalComponent_TypedMethods_IncludingStringFragment()
    {
        var html = HtmlOut.Normalize(await HtmlOut.RenderAsync<UsesPanel>());
        Assert.Equal("<section><h2>t1</h2>plain &lt;text&gt;body</section><section><h2>t2</h2><b>bold</b></section>", html);
    }

    [Fact]
    public async Task DerivedFromFrameworkComponent_GetsFlattenedMethods()
    {
        var html = await HtmlOut.RenderAsync<UsesDerived>();
        Assert.Contains("id=\"name\"", html);
        Assert.Contains("value=\"Ann\"", html);
    }

    // ---- generator driven on in-memory compilations ----

    private const string Library = """
        using Microsoft.AspNetCore.Components;
        using Microsoft.AspNetCore.Components.Forms;
        namespace Lib;
        public class Tag : ComponentBase
        {
            [Parameter] public string? Label { get; set; }
            [Parameter] public RenderFragment? Footer { get; set; }
        }
        public class Grid<TItem> : ComponentBase
        {
            [Parameter] public System.Collections.Generic.IEnumerable<TItem>? Items { get; set; }
            [Parameter] public RenderFragment<TItem>? Row { get; set; }
            [Parameter] public EventCallback<TItem> OnSelect { get; set; }
        }
        public class FancyInput : InputText { [Parameter] public bool Fancy { get; set; } }
        internal class Hidden : ComponentBase { [Parameter] public int X { get; set; } }
        public abstract class Base : ComponentBase { [Parameter] public int Y { get; set; } }
        """;

    private static readonly MetadataReference[] Refs = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
        .Append(typeof(ComponentNode<>).Assembly.Location)
        .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToArray();

    private static readonly CSharpParseOptions Parse = new(LanguageVersion.Preview);

    private static CSharpCompilation Compile(string name, string source, params MetadataReference[] extra) =>
        CSharpCompilation.Create(name, [CSharpSyntaxTree.ParseText(source, Parse)], Refs.Concat(extra),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

    private static MetadataReference LibraryReference()
    {
        var lib = Compile("Lib", Library);
        using var ms = new MemoryStream();
        var emit = lib.Emit(ms);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
        return MetadataReference.CreateFromImage(ms.ToArray());
    }

    private static (Compilation Output, IReadOnlyList<Diagnostic> GeneratorDiagnostics, IReadOnlyList<string> Hints) Run(string source)
    {
        var c = Compile("App", source, LibraryReference());
        var driver = CSharpGeneratorDriver.Create([new ConsumerComponentGenerator().AsSourceGenerator()], parseOptions: Parse)
            .RunGeneratorsAndUpdateCompilation(c, out var output, out var diagnostics);
        var hints = driver.GetRunResult().Results.Single().GeneratedSources.Select(s => s.HintName).ToList();
        return (output, diagnostics, hints);
    }

    private static void AssertNoErrors(Compilation c) =>
        Assert.Empty(c.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()));

    [Fact]
    public void Attribute_SingleTypes_Compile_InternalClasses()
    {
        var (output, diags, hints) = Run("""
            using PureMarkup.Blazor;
            [assembly: GenerateMarkup(typeof(Lib.Tag))]
            [assembly: GenerateMarkup(typeof(Lib.Grid<>))]
            [assembly: GenerateMarkup(typeof(Lib.Grid<int>))]
            [assembly: GenerateMarkup(typeof(Lib.FancyInput))]
            namespace App;
            public sealed class Use : MarkupComponent
            {
                public object? Picked;
                protected override Node Render() => H.Fragment(
                    H.Comp<Lib.Tag>().Label("a").Footer("text").Footer(() => H.Text("node")),
                    H.Comp<Lib.Grid<string>>().Items(["x"]).Row(s => H.Text(s)).OnSelect(s => Picked = s),
                    H.Comp<Lib.FancyInput>().Fancy(true).Value("v").ValueExpression(() => "v").Attr("id", "f"));
            }
            """);
        Assert.Empty(diags);
        AssertNoErrors(output);
        Assert.Equal(["PureMarkup.Blazor.Lib_FancyInputParameters.g.cs", "PureMarkup.Blazor.Lib_GridOf1Parameters.g.cs", "PureMarkup.Blazor.Lib_TagParameters.g.cs"],
            hints.Where(h => !h.Contains("Counts")).Order());
        Assert.All(output.SyntaxTrees.Where(t => t.FilePath.Contains("Lib_")), t => Assert.Contains("internal static partial class", t.ToString()));
    }

    [Fact]
    public void Attribute_WholeAssembly_SkipsAbstractAndInternal()
    {
        var (output, diags, hints) = Run("""
            [assembly: PureMarkup.Blazor.GenerateMarkup(typeof(Lib.Tag), WholeAssembly = true, Namespace = "App.Markup")]
            """);
        Assert.Empty(diags);
        AssertNoErrors(output);
        Assert.Equal(["App.Markup.Lib_FancyInputParameters.g.cs", "App.Markup.Lib_GridOf1Parameters.g.cs", "App.Markup.Lib_TagParameters.g.cs"],
            hints.Where(h => !h.Contains("Counts")).Order());
    }

    [Fact]
    public void Attribute_Diagnostics()
    {
        var (_, diags, hints) = Run("""
            using PureMarkup.Blazor;
            [assembly: GenerateMarkup(typeof(string))]
            [assembly: GenerateMarkup(typeof(Lib.Base))]
            [assembly: GenerateMarkup(typeof(Microsoft.AspNetCore.Components.Forms.InputText))]
            """);
        Assert.Equal(["PMCOMP001", "PMCOMP001", "PMCOMP003"], diags.Select(d => d.Id));
        Assert.All(diags, d => Assert.True(d.Location.IsInSource));
        Assert.Empty(hints);
    }

    [Fact]
    public void LocalComponents_AutomaticallyCovered_RazorFallbackUnaffected()
    {
        var (output, diags, hints) = Run("""
            using Microsoft.AspNetCore.Components;
            using PureMarkup.Blazor;
            namespace App;
            public sealed class Local : MarkupComponent
            {
                [Parameter] public int Count { get; set; }
                protected override Node Render() => H.Text(Count.ToString());
            }
            internal sealed class Derived : Lib.FancyInput { }
            public sealed class NoParams : MarkupComponent { protected override Node Render() => H.Text("x"); }
            public sealed class Use : MarkupComponent
            {
                protected override Node Render() => H.Fragment(H.Comp<Local>().Count(2), H.Comp<Derived>().Fancy(true).Value("v"));
            }
            """);
        Assert.Empty(diags);
        AssertNoErrors(output);
        Assert.Equal(["PureMarkup.Blazor.App_DerivedParameters.g.cs", "PureMarkup.Blazor.App_LocalParameters.g.cs"],
            hints.Where(h => !h.Contains("Counts")).Order());
    }
}

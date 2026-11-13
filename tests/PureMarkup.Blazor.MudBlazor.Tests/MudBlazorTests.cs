extern alias gen;

using Bunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Xunit.Abstractions;
using gen::PureMarkup.Blazor.Generators.Components;

[assembly: GenerateMarkup(typeof(MudButton))]
[assembly: GenerateMarkup(typeof(MudTextField<>))]
[assembly: GenerateMarkup(typeof(MudSelect<>))]
[assembly: GenerateMarkup(typeof(MudSelectItem<>))]
[assembly: GenerateMarkup(typeof(MudTable<>))]
[assembly: GenerateMarkup(typeof(MudTh))]
[assembly: GenerateMarkup(typeof(MudTd))]

namespace PureMarkup.Blazor.MudBlazor.Tests;

public sealed record Fruit(string Name, int Stock);

public sealed class MudPage : MarkupComponent
{
    public int Clicks;
    public string Name = "Ann";
    public string Pick = "Pear";
    public static readonly Fruit[] Fruits = [new("Apple", 3), new("Pear", 5)];

    protected override Node Render() => Fragment(
        Comp<MudPopoverProvider>(),
        Comp<MudButton>().Variant(Variant.Filled).Color(Color.Primary).OnClick(() => Clicks++).Children($"Clicked {Clicks}"),
        Comp<MudTextField<string>>().Label("Name").Value(Name).ValueChanged(v => Name = v ?? "").HelperText("Your name"),
        Comp<MudSelect<string>>().Label("Fruit").Value(Pick).ValueChanged(v => Pick = v ?? "").Children(() => Fragment(
            Comp<MudSelectItem<string>>().Value("Apple").Children("Apple"),
            Comp<MudSelectItem<string>>().Value("Pear").Children("Pear"))),
        Comp<MudTable<Fruit>>().Items(Fruits).Dense(true)
            .HeaderContent(() => Fragment(Comp<MudTh>().Children("Name"), Comp<MudTh>().Children("Stock")))
            .RowTemplate(f => Fragment(Comp<MudTd>().DataLabel("Name").Children(f.Name), Comp<MudTd>().DataLabel("Stock").Children(f.Stock.ToString())))
            .NoRecordsContent("Nothing"));
}

public class MudBlazorTests(ITestOutputHelper log)
{
    // MudBlazor registers IAsyncDisposable-only services, so the context must be disposed asynchronously.
    private static BunitContext Context()
    {
        var ctx = new BunitContext();
        ctx.Services.AddMudServices();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        return ctx;
    }

    [Fact]
    public async Task TypedParameters_Render_AndHandleEvents()
    {
        await using var ctx = Context();
        var cut = ctx.Render<MudPage>();
        var button = cut.Find("button.mud-button-filled");
        Assert.Contains("mud-button-filled-primary", button.ClassName);
        Assert.Contains("Clicked 0", button.TextContent);
        button.Click();
        Assert.Contains("Clicked 1", cut.Find("button.mud-button-filled").TextContent);

        Assert.Contains("Your name", cut.Markup);
        Assert.Equal("Ann", cut.Find("input.mud-input-root").GetAttribute("value"));
        Assert.Contains("Pear", cut.Markup);

        var cells = cut.FindAll("td").Select(td => (td.GetAttribute("data-label"), td.TextContent)).ToList();
        Assert.Equal([("Name", "Apple"), ("Stock", "3"), ("Name", "Pear"), ("Stock", "5")], cells);
        Assert.Equal(["Name", "Stock"], cut.FindAll("th").Select(th => th.TextContent.Trim()));
    }

    [Fact]
    public async Task TextFieldValueChanged_UpdatesTheOwner()
    {
        await using var ctx = Context();
        var cut = ctx.Render<MudPage>();
        cut.Find("input.mud-input-root").Change("Bob");
        Assert.Equal("Bob", cut.Instance.Name);
    }

    [Fact]
    public void WholeAssembly_GeneratesCompilingMethods_ForEveryComponent()
    {
        var refs = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(ComponentNode<>).Assembly.Location).Append(typeof(MudButton).Assembly.Location).Distinct()
            .Select(p => MetadataReference.CreateFromFile(p));
        var parse = new CSharpParseOptions(LanguageVersion.Preview);
        var c = CSharpCompilation.Create("App",
            [CSharpSyntaxTree.ParseText("[assembly: PureMarkup.Blazor.GenerateMarkup(typeof(MudBlazor.MudButton), WholeAssembly = true)]", parse)],
            refs, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var driver = CSharpGeneratorDriver.Create([new ConsumerComponentGenerator().AsSourceGenerator()], parseOptions: parse)
            .RunGeneratorsAndUpdateCompilation(c, out var output, out var generatorDiagnostics);
        Assert.Empty(generatorDiagnostics);
        var problems = output.GetDiagnostics().Where(d => d.Severity >= DiagnosticSeverity.Warning).Select(d => d.ToString()).ToList();
        Assert.Empty(problems);

        var sources = driver.GetRunResult().Results.Single().GeneratedSources;
        var counts = sources.Single(s => s.HintName.StartsWith("ConsumerComponents.Counts", StringComparison.Ordinal)).SourceText.ToString();
        log.WriteLine(counts);

        // Which public concrete components got nothing, and why.
        var generated = sources.Select(s => s.SourceText.ToString())
            .SelectMany(t => t.Split('\n').Where(l => l.StartsWith("/// <summary>Typed parameters of", StringComparison.Ordinal)))
            .Select(l => l.Split("global::")[1].Split("&lt;")[0].Split("</c>")[0]).ToHashSet();
        var all = typeof(MudButton).Assembly.GetExportedTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(Microsoft.AspNetCore.Components.IComponent).IsAssignableFrom(t)).ToList();
        var missing = all.Where(t => !generated.Contains(t.FullName!.Split('`')[0]))
            .Select(t => t.Name + (t.IsDefined(typeof(ObsoleteAttribute), false) ? " (obsolete)" : "")).Order().ToList();
        log.WriteLine($"public concrete components: {all.Count}, generated: {generated.Count}; without methods (no settable parameters or obsolete): {string.Join(", ", missing)}");
        Assert.True(generated.Count >= 150, counts);
    }
}

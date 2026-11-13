extern alias gen;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using gen::PureMarkup.Blazor.Generators.Components;

namespace PureMarkup.Blazor.Tests;

/// <summary>The shared component model and emitter, on a component compiled here (the consumer-time path M2b will use).</summary>
public class ComponentModelTests
{
    private const string Source = """
        using System.Collections.Generic;
        using Microsoft.AspNetCore.Components;
        namespace App;
        public class Probe<TItem> : ComponentBase where TItem : class
        {
            [Parameter] public int Count { get; set; }
            [Parameter] public TItem? Item { get; set; }
            [Parameter] public string? Key { get; set; }
            [Parameter] public RenderFragment? ChildContent { get; set; }
            [Parameter] public RenderFragment<TItem>? Row { get; set; }
            [Parameter] public EventCallback<TItem> OnPick { get; set; }
            [Parameter] public EventCallback OnPing { get; set; }
            [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? Rest { get; set; }
            [CascadingParameter] public string? Theme { get; set; }
            [SupplyParameterFromQuery] public int Page { get; set; }
            [SupplyParameterFromForm] public string? Form { get; set; }
        }
        """;

    private static CSharpCompilation Compile(params string[] sources)
    {
        var refs = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(ComponentNode<>).Assembly.Location)
            .Select(p => MetadataReference.CreateFromFile(p));
        return CSharpCompilation.Create("App", sources.Select(s => CSharpSyntaxTree.ParseText(s, new CSharpParseOptions(LanguageVersion.Preview))),
            refs, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }

    private static ComponentInfo Read(Compilation c) =>
        ComponentModel.Read(c.GetTypeByMetadataName("App.Probe`1")!, c, ComponentModel.ReservedNames(c), default)!;

    [Fact]
    public void Reads_Parameters_SkipsCascading_RenamesClashes()
    {
        var info = Read(Compile(Source));
        Assert.Equal(
            ["Count:Count:Value", "Item:Item:Value", "Key:KeyParam:Value", "Row:Row:FragmentOf", "OnPick:OnPick:EventOf", "OnPing:OnPing:Event", "Rest:Rest:Value"],
            info.Params.Select(p => $"{p.Name}:{p.Method}:{p.Kind}"));
        Assert.True(info.CapturesUnmatched);
        Assert.Contains("where TItem : class", info.Constraints);
    }

    [Fact]
    public void EmittedMethods_Compile_AndInferTheTypeArgument()
    {
        var c = Compile(Source);
        var (_, generated, methods) = Assert.Single(ComponentEmitter.Emit([Read(c)], "App.Markup", "test"));
        Assert.Equal(1 + 1 + 1 + 1 + 5 + 3 + 1 + 1, methods); // Count Item KeyParam Row OnPick×5 OnPing×3 Rest Attr
        const string use = """
            using PureMarkup.Blazor;
            using App.Markup;
            namespace App;
            public sealed class Use : MarkupComponent
            {
                public string? Picked;
                protected override Node Render() => H.Comp<Probe<string>>()
                    .Count(1).Item("a").KeyParam("k").Row(s => H.Text(s)).OnPick(s => Picked = s).OnPing(() => { })
                    .Attr("id", "p").Key(1);
            }
            """;
        var errors = c.AddSyntaxTrees(CSharpSyntaxTree.ParseText(generated, new CSharpParseOptions(LanguageVersion.Preview)),
                CSharpSyntaxTree.ParseText(use, new CSharpParseOptions(LanguageVersion.Preview)))
            .GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.Empty(errors);
    }
}

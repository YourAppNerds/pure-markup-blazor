using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using Baseline;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Bench;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

if (args.Contains("--verify"))
{
    Verify.Run();
    return;
}
BenchmarkSwitcher.FromAssembly(typeof(RenderBenchmarks).Assembly).Run(args, DefaultConfig.Instance);

namespace Bench
{
    /// <summary>Dispatcher that runs inline, so renders happen synchronously on the benchmark thread.</summary>
    sealed class InlineDispatcher : Dispatcher
    {
        public override bool CheckAccess() => true;
        public override Task InvokeAsync(Action workItem) { workItem(); return Task.CompletedTask; }
        public override Task InvokeAsync(Func<Task> workItem) => workItem();
        public override Task<T> InvokeAsync<T>(Func<T> workItem) => Task.FromResult(workItem());
        public override Task<T> InvokeAsync<T>(Func<Task<T>> workItem) => workItem();
    }

    /// <summary>TestRenderer-style renderer: BuildRenderTree + diff, no output.</summary>
    sealed class BenchRenderer() : Renderer(Services, NullLoggerFactory.Instance)
    {
        static readonly IServiceProvider Services = new ServiceCollection().BuildServiceProvider();
        public override Dispatcher Dispatcher { get; } = new InlineDispatcher();
        public int Edits;
        protected override void HandleException(Exception exception) => ExceptionDispatchInfo.Capture(exception).Throw();
        protected override Task UpdateDisplayAsync(in RenderBatch batch)
        {
            for (var i = 0; i < batch.UpdatedComponents.Count; i++) Edits += batch.UpdatedComponents.Array[i].Edits.Count;
            return Task.CompletedTask;
        }
        public void RenderRoot(IComponent c)
        {
            var id = AssignRootComponentId(c);
            var t = RenderRootComponentAsync(id);
            if (!t.IsCompletedSuccessfully) t.GetAwaiter().GetResult();
        }
    }

    [MemoryDiagnoser]
    [ShortRunJob]
    public class RenderBenchmarks
    {
        public enum Kind { Razor, Markup, MarkupStatic }

        [ParamsAllValues] public Kind Impl;
        [Params("Table1000", "Page200")] public string Case = "";

        IBenchTarget _target = null!;
        readonly RenderTreeBuilder _builder = new();

        internal static IBenchTarget Create(string c, Kind k) => (c, k) switch
        {
            ("Table1000", Kind.Razor) => new Table1000(),
            ("Table1000", _) => new MarkupTable1000(),
            ("Page200", Kind.Razor) => new Page200(),
            ("Page200", Kind.Markup) => new MarkupPage200(),
            _ => new MarkupPage200Static(),
        };

        [GlobalSetup]
        public void Setup()
        {
            _target = Create(Case, Impl);
            new BenchRenderer().RenderRoot((IComponent)_target);
        }

        /// <summary>BuildRenderTree alone, into a reused builder (the gate metric).</summary>
        [Benchmark]
        public int BuildOnly()
        {
            _builder.Clear();
            _target.BuildPublic(_builder);
            return _builder.GetFrames().Count;
        }

        /// <summary>Interactive re-render: BuildRenderTree + diff against the previous tree; 2 rows/sections change.</summary>
        [Benchmark]
        public void Rerender()
        {
            _target.Toggle();
            _target.Rerender();
        }

        /// <summary>First render in a fresh renderer: new component, build, diff against empty, child components.</summary>
        [Benchmark]
        public void FirstRender() => new BenchRenderer().RenderRoot((IComponent)Create(Case, Impl));
    }

    static class Verify
    {
        public static void Run()
        {
            var services = new ServiceCollection().AddLogging().BuildServiceProvider();
            using var html = new HtmlRenderer(services, NullLoggerFactory.Instance);
            foreach (var c in new[] { "Table1000", "Page200" })
            {
                string? razor = null;
                foreach (var k in Enum.GetValues<RenderBenchmarks.Kind>())
                {
                    if (c == "Table1000" && k == RenderBenchmarks.Kind.MarkupStatic) continue;
                    var type = RenderBenchmarks.Create(c, k).GetType();
                    var output = html.Dispatcher.InvokeAsync(async () =>
                        (await html.RenderComponentAsync(type, ParameterView.Empty)).ToHtmlString()).GetAwaiter().GetResult();
                    // Razor writes static void tags as markup ("<img ...>"); element frames serialize as "<img ... />".
                    var norm = Regex.Replace(output, @">\s+<", "><").Replace(" />", ">").Trim();
                    var b = new RenderTreeBuilder();
                    var t = RenderBenchmarks.Create(c, k);
                    t.BuildPublic(b);
                    var frames = b.GetFrames();
                    int markup = 0, elements = 0;
                    for (var i = 0; i < frames.Count; i++)
                    {
                        if (frames.Array[i].FrameType == RenderTreeFrameType.Markup) markup++;
                        if (frames.Array[i].FrameType == RenderTreeFrameType.Element) elements++;
                    }
                    razor ??= norm;
                    Console.WriteLine($"{c,-10} {k,-13} html={norm.Length,7} chars  equalToRazor={norm == razor}  frames={frames.Count} (elements {elements}, markup {markup})");
                    if (norm != razor)
                    {
                        var i = 0;
                        while (i < norm.Length && i < razor.Length && norm[i] == razor[i]) i++;
                        Console.WriteLine($"  first diff at {i}:\n  razor:  {razor.Substring(Math.Max(0, i - 60), Math.Min(140, razor.Length - Math.Max(0, i - 60)))}\n  markup: {norm.Substring(Math.Max(0, i - 60), Math.Min(140, norm.Length - Math.Max(0, i - 60)))}");
                    }
                }
            }
        }
    }
}

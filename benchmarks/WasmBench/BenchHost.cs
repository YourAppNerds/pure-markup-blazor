using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Baseline;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Bench;

/// <summary>
/// Renders each Table1000 implementation through the WebAssembly renderer and times it with a Stopwatch.
/// A re-render (Toggle + StateHasChanged) runs synchronously: build, diff, and the batch applied to the DOM by
/// blazor.webassembly.js (layout and paint are not included). Rounds alternate the implementations.
/// Query: ?rounds=7&amp;warmup=10&amp;iterations=30. The result is JSON in #results.
/// </summary>
public sealed class BenchHost : ComponentBase
{
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private static readonly (string Name, Type Type)[] Impls = [("Razor", typeof(Table1000)), ("Markup", typeof(MarkupTable1000))];

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    private Type? current;
    private IBenchTarget? target;
    private string results = "running";

    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        b.OpenElement(0, "pre");
        b.AddAttribute(1, "id", "results");
        b.AddContent(2, results);
        b.CloseElement();
        if (current is not null)
        {
            b.OpenComponent(3, current);
            b.AddComponentReferenceCapture(4, r => target = (IBenchTarget)r);
            b.CloseComponent();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        await Task.Yield();
        try
        {
            results = JsonSerializer.Serialize(Run(), new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception e)
        {
            results = "error: " + e;
        }
        Show(null);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Both implementations are referenced by typeof in Impls.")]
    private void Show(Type? type)
    {
        current = type;
        target = null;
        StateHasChanged();
    }

    private Dictionary<string, object> Run()
    {
        var q = new Uri(Nav.Uri).Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=')).ToDictionary(p => p[0], p => int.Parse(p[1]));
        int rounds = q.GetValueOrDefault("rounds", 7), warmup = q.GetValueOrDefault("warmup", 10), iterations = q.GetValueOrDefault("iterations", 30);
        var js = (IJSInProcessRuntime)JS;
        var rerender = Impls.ToDictionary(i => i.Name, _ => new List<double>());
        var build = Impls.ToDictionary(i => i.Name, _ => new List<double>());
        var first = Impls.ToDictionary(i => i.Name, _ => new List<double>());

        for (var round = 0; round < rounds; round++)
        {
            foreach (var (name, type) in Impls)
            {
                var sw = Stopwatch.StartNew();
                Show(type); // the host renders and the new table renders inside the same synchronous batch
                first[name].Add(sw.Elapsed.TotalMicroseconds);
                var t = target ?? throw new InvalidOperationException($"{name} did not render synchronously");

                for (var i = 0; i < warmup; i++) { t.Toggle(); t.Rerender(); }
                sw.Restart();
                for (var i = 0; i < iterations; i++) { t.Toggle(); t.Rerender(); }
                rerender[name].Add(sw.Elapsed.TotalMicroseconds / iterations);

                // The DOM already shows the last toggle: the re-render, batch and DOM update ran inside Rerender().
                var selected = js.Invoke<string>("eval", "document.querySelector('tr.selected')?.dataset.id ?? ''");
                var expected = (warmup + iterations) % 2 == 0 ? "10" : "20";
                if (selected != expected) throw new InvalidOperationException($"{name}: DOM shows row {selected}, expected {expected}");

                var builder = new RenderTreeBuilder();
                for (var i = 0; i < warmup; i++) { t.BuildPublic(builder); builder.Clear(); }
                sw.Restart();
                for (var i = 0; i < iterations; i++) { t.BuildPublic(builder); builder.Clear(); }
                build[name].Add(sw.Elapsed.TotalMicroseconds / iterations);

                Show(null);
            }
        }

        static double Median(List<double> v) { var s = v.Order().ToArray(); return Math.Round(s.Length % 2 == 1 ? s[s.Length / 2] : (s[s.Length / 2 - 1] + s[s.Length / 2]) / 2, 1); }
        return new()
        {
            ["runtime"] = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            ["rounds"] = rounds, ["warmup"] = warmup, ["iterations"] = iterations,
            ["firstRenderUs"] = first.ToDictionary(p => p.Key, p => Median(p.Value)),
            ["rerenderUs"] = rerender.ToDictionary(p => p.Key, p => Median(p.Value)),
            ["buildRenderTreeUs"] = build.ToDictionary(p => p.Key, p => Median(p.Value)),
        };
    }
}

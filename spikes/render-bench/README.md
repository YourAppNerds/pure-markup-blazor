# Spike: render cost of a node tree vs Razor (risk row "Render performance")

**Result: 1,000-row table passes the 2x gate (1.27x re-render, 1.35x build). The 200-line static-heavy page fails naively (2.6x re-render) and needs `Static(...)` (1.55x); allocations are the real gap.**
Release, BenchmarkDotNet 0.15.4 ShortRun (3 iterations, so error bars are wide), .NET 10.0.14 x64, Windows 11.

| Case | Metric | Razor | Markup | Ratio | Markup+Static | Ratio |
|---|---|--:|--:|--:|--:|--:|
| Table 1,000 rows | BuildRenderTree only | 240 us | 324 us | 1.35x | | |
| | Re-render (build + diff, 2 rows change) | 550 us | 696 us | **1.27x** | | |
| | First render (fresh renderer) | 2,249 us | 2,510 us | 1.12x | | |
| | Alloc per re-render | 349 KB | 1,262 KB | 3.6x | | |
| | Alloc first render | 1,851 KB | 2,109 KB | 1.14x | | |
| Page ~200 lines | BuildRenderTree only | 3.5 us | 9.8 us | 2.78x | 7.3 us | 2.08x |
| | Re-render | 5.3 us | 13.8 us | **2.58x** | 8.3 us | **1.55x** |
| | First render | 8.0 us | 15.2 us | 1.91x | 11.6 us | 1.46x |
| | Alloc per re-render | 0.8 KB | 38 KB | 48x | 21 KB | 27x |

Output equality (`dotnet run -c Release --project Bench -- --verify`): normalized HTML from `HtmlRenderer` is identical for every variant
(whitespace between tags removed; Razor writes static `<img>` as markup, element frames as `<img />`).
Frames: table Razor 17,005 (3,001 whitespace markup frames) vs Markup 14,015; page Razor 334 (80 markup) vs Markup 542 (0 markup), Markup+Static 335.

## Why
- Razor folds static HTML into one markup frame at compile time and allocates almost nothing per render (closures for `@onclick` only).
  The prototype allocates per element: the `Element`, its attribute `List`, the `params` children array, boxed ints.
- Rows are dynamic in both, so the table is close; static-heavy pages are where Razor's compile-time folding wins.
- `Static(static () => ...)` (cached per call site, keyed by the compiler-cached delegate) recovers most of it.

## Takeaways for the design
- Gate at 1,000 rows: **pass**. 200-line page: **fail without `Static`**, pass for re-render with it; absolute cost is ~10 us.
- Allocation is the thing to fix: struct or pooled attribute storage, no `List` per element, typed `AddContent(int)`/`AddAttribute` overloads,
  and a generator that turns constant subtrees into markup automatically (like Razor) rather than relying on users writing `Static`.
- `ASP0006` (sequence number not a literal) fires on the library; it computes sequences at runtime by design, so suppress it there.
- Sequence numbers: depth-first counter per region; `If` and `ForEach` open regions so surrounding static structure keeps stable numbers.

## Pending
- WebAssembly run (interpreter and AOT): not done. GC pressure matters more there, so expect a larger gap than on CoreCLR.

## Files
- `Markup/Node.cs`: prototype (element, attribute, text, component, `If`, `ForEach` with key, `onclick`, `Static`).
- `Baseline/`: Razor baseline (`Table1000.razor`, `Page200.razor`, `Badge.razor` used by both).
- `Bench/`: markup equivalents, `RenderBenchmarks` (inline-dispatcher `Renderer` subclass), `--verify` mode.
- `gen.mjs`: generates `Page200.razor` and `MarkupPage200*.cs` from one spec. Run: `dotnet run -c Release --project Bench -- --filter *`.

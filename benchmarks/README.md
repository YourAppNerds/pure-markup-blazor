# Benchmarks: PureMarkup.Blazor vs Razor

Spike A (`spikes/render-bench`) ported to the library. `Baseline/` holds the same pages as `.razor`.
Run: `dotnet run -c Release --project benchmarks/PureMarkup.Blazor.Benchmarks -- --filter '*'` (add `--verify` to compare HTML).

Results (2026-11-12): BenchmarkDotNet 0.15.4, 15 iterations, .NET 10.0.14 x64, Ryzen 5 9600X, Windows 11.
Ratios are Markup / Razor. Spike A's numbers are in brackets.

| Case | Metric | Razor | Markup | Ratio [spike] | Markup+Static | Ratio [spike] |
|---|---|--:|--:|--:|--:|--:|
| Table 1,000 rows | Re-render (build + diff) | 489 us | 554 us | **1.13x** [1.27x] | | |
| | Alloc per re-render | 349 KB | 301 KB | **0.86x** [3.6x] | | |
| | BuildRenderTree only | 199 us | 296 us | 1.49x [1.35x] | | |
| | First render | 2,312 us | 649 us | 0.28x [1.12x] | | |
| Page ~200 lines | Re-render | 6.9 us | 17.2 us | **2.50x** [2.58x] | 10.2 us | **1.49x** [1.55x] |
| | Alloc per re-render | 792 B | 1,224 B | **1.55x** [48x] | 1,224 B | 1.55x [27x] |
| | BuildRenderTree only | 3.2 us | 11.1 us | 3.46x [2.78x] | 6.6 us | 2.06x [2.08x] |

`--verify`: HTML identical to Razor for every variant. Frames: table 17,005 Razor vs 14,015; page 334 vs 533 (326 with `Static`).

## CSS in C# (2026-11-13, M1 CSS core)
`MarkupRules` is the table with `.Class(rule)` instead of class strings (`Page200` has no rules variant and reruns `Markup`).
Rule bodies never run at render; `.Class(rule)` adds the precomputed class string.

| Table 1,000 rows | M1a Markup | Markup now | MarkupRules |
|---|--:|--:|--:|
| Re-render (3 iterations) | 554 us | 545 us | 582 us |
| BuildRenderTree only (15 iterations) | 296 us | 299 us | 332 us |
| Alloc per re-render | 300,864 B | 300,864 B | 300,864 B |

No regression from the merged `class`/`style` attributes. Rules allocate the same; their time difference is inside this
machine's noise: in the same 15-iteration run, `Page200` `MarkupRules` (identical code to `Markup`) measured 12.9 vs 8.4 us.

## Notes
- Nodes are struct handles into per-render arrays that are pooled per thread, so building allocates nothing after warm-up.
  What is left is what Razor also allocates: handler closures, boxed keys and attribute values, `ToString` of numbers.
- The table's first render is lower than Razor's on this machine and has gen-2 collections in both; treat it as noise until a longer run explains it.
- The page gap is frame count: Razor folds static HTML into markup frames at compile time. `Static(...)` recovers it by hand;
  a generator pass that folds constant subtrees into `H.Markup(...)` is the planned fix.
- Not measured: WebAssembly (interpreter, AOT).

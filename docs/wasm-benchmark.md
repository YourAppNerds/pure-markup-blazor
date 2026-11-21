# WebAssembly benchmark

Back to the [README](../README.md). Server-side numbers (BenchmarkDotNet) are in [benchmarks/README.md](../benchmarks/README.md).

## What it measures
The Table 1,000 rows page (4 cells per row, a keyed row with a click handler, a link per row), written twice: as
`.razor` (`benchmarks/Baseline/Table1000.razor`) and in PureMarkup (`benchmarks/PureMarkup.Blazor.Benchmarks/MarkupTable1000.cs`).
These are the same files the server benchmark uses.

`benchmarks/WasmBench` is a standalone Blazor WebAssembly app. It renders each table through the real WebAssembly renderer
and times it with a `Stopwatch` inside the browser:
- **Re-render**: toggle the selected row, then `StateHasChanged`. This runs synchronously: build, diff, and the batch
  applied to the DOM by `blazor.webassembly.js`. Layout and paint are not included. The page checks that the DOM shows
  the new selection afterwards.
- **BuildRenderTree only**: build into a `RenderTreeBuilder`, no diff and no DOM.
- **First render**: mount the table (build, diff, create 1,000 rows in the DOM).

Rounds alternate Razor and Markup. Each round warms up 10 times and averages 30 runs; the result is the median of the rounds.

## Run it
```
node benchmarks/wasm-bench.mjs                 # Release publish (trimmed), 7 rounds
node benchmarks/wasm-bench.mjs --no-publish    # reuse the last publish
node benchmarks/wasm-bench.mjs --aot           # needs the wasm-tools workload
```
Options: `--rounds`, `--warmup`, `--iterations`. The script publishes to the temp folder, serves it from a small static
server, opens it in headless Chromium (Playwright from `samples/WebApp`) and prints JSON and a Markdown table.

## Results (2026-11-21)
Interpreter (no AOT), trimmed Release publish, .NET 10.0.14, Chromium 158, Ryzen 5 9600X, Windows 11. Two runs of 7 rounds:

| Table 1,000 rows | Razor | Markup | Ratio |
|---|--:|--:|--:|
| Re-render (build + diff + DOM) | 11.3 / 11.7 ms | 13.7 / 14.4 ms | **1.22x / 1.23x** |
| BuildRenderTree only | 3.8 / 4.4 ms | 7.8 / 7.3 ms | 2.02x / 1.67x |
| First render (+ DOM) | 53.9 / 54.5 ms | 27.4 / 30.1 ms | 0.51x / 0.55x |

- The interpreter is about 20 to 25 times slower than the server JIT (Razor re-render 489 us on the server).
- The ratio is close to the server's (1.13x re-render). Building the tree costs more than Razor's generated code, which
  writes frames directly; the diff and DOM work are the same, so the gap shrinks in the full re-render.
- First render is lower for Markup, as on the server. Neither number has been explained yet; treat it with care.
- Not measured: AOT (workload missing, see [trimming-aot.md](trimming-aot.md#webassembly-aot)), the `Page200` page,
  and rules or presets (`MarkupRules`, `MarkupPresets`, which need the CSS build step).

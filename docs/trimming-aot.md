# Trimming, AOT and Production publish

Back to the [README](../README.md). Checked 2026-11-21 with .NET SDK 10.0.403 (runtime 10.0.14), Windows 11.

## Summary
- Trimmed WebAssembly publish: **0 warnings from PureMarkup**. The rest come from the framework, and a stock
  `dotnet new blazor` app gets the same ones.
- `PureMarkup.Blazor` shrinks from 531 KB to 44 to 54 KB after trimming (16 to 20 KB Brotli).
- WebAssembly AOT: not run. The `wasm-tools` workload is not installed on this machine (see below).
- Server Native AOT: not supported by Blazor Server, so not tried.
- A Release publish run in Production works for Server, WebAssembly and Auto. The first check found a template bug
  (WebAssembly pages trimmed away); it is fixed and now covered by `scripts/verify-template.mjs`.

## Trim warnings
A Blazor WebAssembly publish hides trim warnings by default (`SuppressTrimAnalysisWarnings=true`). To see them:

```
dotnet publish -c Release -p:SuppressTrimAnalysisWarnings=false -p:TrimmerSingleWarn=false
```

| App (Release publish, default `TrimMode=partial`) | Framework warnings | `Comp<Router>()` in your code | PureMarkup |
|---|--:|--:|--:|
| `samples/WebApp` | 6 | 0 | 0 |
| Template, `--interactivity WebAssembly --sample-content` | 6 | 0 | 0 |
| Template, same plus `--all-interactive` | 6 | 6 (2 IL2110, 4 IL2111) | 0 (was 9) |
| Stock `dotnet new blazor --interactivity WebAssembly --all-interactive` | 6 | 5 in `Routes.razor` | n/a |

- Framework warnings: IL2065 and IL2111 in `Microsoft.JSInterop.Infrastructure.DotNetDispatcher`, and 4 IL2072 in
  `Microsoft.AspNetCore.Components` (`CascadingParameterState`, `ComponentFactory`, `ComponentProperties`).
- `Comp<Router>()`: the framework annotates `Router.NotFoundPage` with `[DynamicallyAccessedMembers]`. Opening a component
  keeps all its members, so the trimmer warns, exactly as for `<Router>` in a `.razor` file. Only an app whose router runs
  in WebAssembly sees it (`--all-interactive` with WebAssembly or Auto).
- Fixed: PureMarkup's generated `RouterParameters` methods repeated IL2110 9 times (once per method and member). The
  generated class already suppressed IL2111 for this case, with a justification; it now suppresses IL2110 the same way.
  Your own `Comp<Router>()` call still reports it once, so nothing is hidden from the app.
- The library builds with `IsTrimmable` and `IsAotCompatible`; their analyzers report nothing.

## Sizes
WebAssembly files (Webcil `.wasm`) in `wwwroot/_framework`, bytes:

| Assembly | Before trim | After trim | Brotli |
|---|--:|--:|--:|
| `PureMarkup.Blazor` in `samples/WebApp` | 543,509 | 50,453 | 18,172 |
| `PureMarkup.Blazor` in template WebAssembly app | 543,509 | 44,821 | 16,806 |
| `PureMarkup.Blazor` in template, `--all-interactive` | 543,509 | 55,061 | 20,120 |

"Before" is the untrimmed file of a Release build. The `.Client` assembly itself is not trimmed (see the next section).

## Do not mark `.Client` as trimmable
The server tells the browser which component to start by type name, so the trimmer cannot see that the pages are used.
With `IsTrimmable` on the `.Client` project, or `TrimMode=full`, the trimmer removed every page (the template's `.Client`
went from 14 KB to 6 KB and `PureMarkup.Blazor` was dropped). In the browser:
`Root component type 'PmWasmSample.Client.Pages.Counter' could not be found in the assembly 'PmWasmSample.Client'`.
Razor apps have the same rule. The template now sets `EnableTrimAnalyzer` instead, which keeps the build-time warnings.

## WebAssembly AOT
```
dotnet publish samples/WebApp/WebApp -c Release -p:RunAOTCompilation=true
error NETSDK1147: To build this project, the following workloads must be installed: wasm-tools
error NETSDK1147: To install these workloads, run the following command: dotnet workload restore
```

To do (Kyle, admin shell; the workloads on this machine came with Visual Studio, so the Visual Studio Installer's
".NET WebAssembly Build Tools" component works too):
```
dotnet workload install wasm-tools
```
Then rerun the publish above and `node benchmarks/wasm-bench.mjs --aot` (see [wasm-benchmark.md](wasm-benchmark.md)).

## Server Native AOT
Not tried. ASP.NET Core's Native AOT supports minimal APIs and gRPC, but not Blazor Server (interactive server rendering
needs runtime code generation and reflection that Native AOT does not allow). A Blazor Web App server project therefore
publishes as a normal framework-dependent or self-contained app.

## Production publish
Known risk: an app with no `.razor` files only gets `blazor.web.js` with `RequiresAspNetWebAssets`. Earlier, `dotnet run`
in Production without launchSettings served it as 200 with 0 bytes.

Check: template apps with `--sample-content` in Server, WebAssembly and Auto (and WebAssembly `--all-interactive`),
`dotnet publish -c Release`, run `dotnet <App>.dll` from the publish folder with `ASPNETCORE_ENVIRONMENT=Production`.

| Mode | `/_framework/blazor.web.js` | Fingerprinted URL | Counter clicks |
|---|---|---|---|
| Server | 200, 200,645 bytes | 200, same content | Yes |
| WebAssembly | 200, 200,645 bytes | 200, same content | Yes (failed before the `.Client` fix) |
| Auto | 200, 200,645 bytes | 200, same content | Yes, on Server and then on WebAssembly (second load failed before the fix) |
| WebAssembly, `--all-interactive` | 200, 200,645 bytes | 200, same content | Yes |

The 0-byte script did not appear with a real publish: the published `wwwroot` holds the file. `dotnet run` in Production
from the project folder is not a supported way to run an app (static web assets from packages are only mapped in
Development), so use the publish output.

`node scripts/verify-template.mjs` now runs this check for those four cases.

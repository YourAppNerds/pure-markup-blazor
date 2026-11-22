# Getting started

Back to the [README](../README.md).

## Install the templates
The packages are on nuget.org as a preview. Install the templates:

```
dotnet new install PureMarkup.Blazor.Templates@0.1.0-preview.2
```

### Use a local build
To try changes from this repo, build both packages into the local feed `artifacts/packages` and install from there:

```
dotnet pack src/PureMarkup.Blazor -c Release                      # library, generator, build files, CSS tool
dotnet pack templates/PureMarkup.Blazor.Templates -c Release      # both go to artifacts/packages (local feed)
dotnet new install artifacts/packages/PureMarkup.Blazor.Templates.0.1.0-preview.2.nupkg
dotnet nuget add source <repo>/artifacts/packages -n puremarkup-local
```

A local build has the same version as the nuget.org package, and NuGet keeps whichever copy it cached first. After each
repack, delete `puremarkup.blazor/0.1.0-preview.2` from the global packages folder
(`dotnet nuget locals global-packages --list` shows where it is).

## Create an app
```
dotnet new puremarkup-blazor -o MyApp --interactivity Auto --sample-content
dotnet new puremarkup-blazor -o MyApp --interactivity Auto --all-interactive   # every page interactive
dotnet new puremarkup-blazor-page -n Orders -o Components/Pages        # inside a restored project
```

`puremarkup-blazor` makes a Blazor Web App like `dotnet new blazor`, with no `.razor` or `.html` files (and no `app.css` or
Bootstrap). You get `App.cs`, `Routes.cs`, `Program.cs`, `GlobalUsings.cs` (the `_Imports.razor` lines),
`Layout/{MainLayout,NavMenu}.cs`, `Pages/{Home,NotFound,Error}.cs` and `Theme/{Tokens,Global,AppPresets}.cs`
(light and dark tokens, the stock look written as C# rules).

Options:
- `--interactivity None|Server|WebAssembly|Auto` (default `Server`). `WebAssembly` and `Auto` add a `.Client` project.
  Interactivity is per page, through `[InteractiveServer]`, `[InteractiveWebAssembly]` or `[InteractiveAuto]`, as in the stock template.
- `--all-interactive` (`-ai`): one render mode for the whole app. See [render-modes.md](render-modes.md).
- `--sample-content`: a Counter page (interactive modes only) and a Weather page (stream rendering).
- `--framework net10.0|net11.0` (net11.0 is wired but untested). `--no-restore`.

`puremarkup-blazor-page` adds one `MarkupComponent` page. Options: `--route` (default `/<name in lower case>`),
`--namespace` (default: the root namespace of the restored project in the output folder), `--interactivity`.

Not yet: standalone WebAssembly, `--auth`, PWA, the reconnect modal and nav icons.

## Run and publish
`dotnet run` or `dotnet watch` from the server project, as with any Blazor Web App. `dotnet watch` applies edits to
rules and presets without a restart (see [styling.md](styling.md#hot-reload)).

`dotnet publish -c Release` works for every mode. WebAssembly code is trimmed; see [trimming-aot.md](trimming-aot.md).

## Without the template
Reference `PureMarkup.Blazor` (the package brings the generator and build files). Then add to the project file:

```xml
<InterceptorsNamespaces>$(InterceptorsNamespaces);PureMarkup.Blazor.Generated</InterceptorsNamespaces>
<RequiresAspNetWebAssets>true</RequiresAspNetWebAssets> <!-- server project of a Blazor Web App with no .razor files -->
```

and `global using static PureMarkup.Blazor.H;` (plus `using PureMarkup.Blazor.Styling;` for CSS in C#).

`RequiresAspNetWebAssets` matters: the SDK adds `blazor.web.js` only when the project has a `.razor` file. Without it, pages
render but never become interactive. Inside this repo, project references import the build targets by hand, as
`samples/Directory.Build.targets` does. See `samples/WebApp` for a full app (static SSR, Server, WebAssembly, Auto).

## Check the templates
`node scripts/verify-template.mjs` packs both packages and installs the templates into a throwaway hive
(`--debug:custom-hive`). It creates every mode with and without sample content, plus `--all-interactive` for Auto (with and
without sample content), Server and WebAssembly (with): 12 cases. Each one is built and run, and the script checks:
- `/`, `/_framework/blazor.web.js`, the `.pm.css` links and the 404 page;
- zero `.razor`/`.html` files, file placement and render-mode attributes;
- Counter clicks (Playwright, installed in `samples/WebApp`) and item-template pages;
- for `ServerSample`, `WasmSample`, `AutoSample` and `WasmGlobalSample`, a real `dotnet publish -c Release` run from the
  publish folder in Production: `blazor.web.js` has its content and Counter still clicks (Auto: also on WebAssembly).

Packages restore into a scratch `NUGET_PACKAGES`, so a repack of the same version is always picked up.
Options: `--only <case>,...`, `--scratch <dir>`, `--no-pack`.

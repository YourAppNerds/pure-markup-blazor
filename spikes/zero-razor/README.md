# Spike: zero-Razor Blazor Web App (M0 spike 1)

**Result: works.** A Blazor Web App with no `.razor`, `.cshtml` or `.html` file runs in every render mode.
All components are hand-written `BuildRenderTree` code (no markup library). Checked on net10.0, SDK 10.0.403, packages 10.0.14.

| Mode | Page | Result (Playwright, `verify.mjs`) |
|---|---|---|
| Static SSR | `/` | Renders `Renderer: Static`; `EditForm` + `FormName="greet"` + `[SupplyParameterFromForm]` POST binds and greets |
| InteractiveServer | `/counter-server` | Becomes `Server; interactive: True`; counter increments |
| InteractiveWebAssembly | `/counter-wasm` (Client) | Becomes `WebAssembly; interactive: True`; counter increments; `dotnet.js` loads |
| InteractiveAuto | `/counter-auto` (Client) | Increments; picked WebAssembly because the runtime was already cached |

No 4xx/5xx responses and no console errors.

## Required csproj properties
- Server: `<RequiresAspNetWebAssets>true</RequiresAspNetWebAssets>`. **Trap confirmed.** Without it the build is clean, `Microsoft.AspNetCore.App.Internal.Assets` is missing from `project.assets.json`, the endpoints manifest has no `blazor.web.js`, and `GET /_framework/blazor.web.js` returns **404**. `blazor.webassembly.js` (from the Client/WASM package) is still served. The Client project does not need the property.
- Client: the template's `StaticWebAssetProjectMode=Default` and `NoDefaultLaunchSettingsFile=true`. Nothing Razor-specific.

## Files
- `ZeroRazor/Program.cs`: standard host (`AddRazorComponents`, both interactive modes, `MapStaticAssets`, `MapRazorComponents<App>`, `AddAdditionalAssemblies`).
- `ZeroRazor/Components/App.cs`: host page. `AddMarkupContent("<!DOCTYPE html>")`, `html/head/body`, `ResourcePreloader`, `ImportMap`, `HeadOutlet`, `Routes`, `<script src=Assets["_framework/blazor.web.js"]>`.
- `ZeroRazor/Components/Routes.cs`: `Router` with `AppAssembly`, `AdditionalAssemblies`, `Found` -> `RouteView` (`DefaultLayout`) + `FocusOnNavigate`.
- `ZeroRazor/Components/Layout/MainLayout.cs`: `LayoutComponentBase` with `NavLink`s and `Body`.
- `ZeroRazor/Components/Pages/Home.cs`: static SSR form (`EditForm`, `InputText` with `Value`/`ValueChanged`/`ValueExpression`).
- `ZeroRazor/Components/Pages/CounterServer.cs`, `ZeroRazor.Client/Pages/CounterWasm.cs`, `CounterAuto.cs`: `[Route]` + render-mode attribute over `CounterBase`.
- `ZeroRazor.Client/RenderModes/RenderModeAttributes.cs`: public `InteractiveServer`/`InteractiveWebAssembly`/`InteractiveAuto` attributes deriving from the abstract `RenderModeAttribute`. They live in Client so both projects can use them.
- `ZeroRazor/Properties/launchSettings.json`: Development environment (see surprises).
- `verify.mjs` + `package.json`: Playwright check. Run `dotnet run --project ZeroRazor`, then `npm i`, `npx playwright install chromium`, `node verify.mjs`.

## Generated output
`bin/` and `obj/` contain no `.html`, `.razor` or `.cshtml`. The only generated C# is `*.GlobalUsings.g.cs`; the Razor source generator emits nothing. The HTML is produced at runtime by `App`.

## Surprises
- **Empty `blazor.web.js` without launchSettings.** `dotnet run` with no launch profile runs in Production. Static web assets are then off, so `MapStaticAssets` still routes `_framework/blazor.web.js` but returns **200 with 0 bytes**, and the page silently stays static (StaticAssetsInvoker warning 17). Interactive modes looked broken until `ASPNETCORE_ENVIRONMENT=Development` was set. This has nothing to do with Razor, but a fresh hand-built project hits it because it has no template launchSettings.
- `RouteData` is ambiguous in a Web SDK project (`Microsoft.AspNetCore.Components` vs `Microsoft.AspNetCore.Routing`), which Razor's imports hide. A generator or global usings must alias it.
- `[Route]` is not inherited, as section 4 says; a shared `CounterBase` with routed subclasses works.
- Package 10.0.5 raises NU1903 (high-severity advisories); use 10.0.14 or later.

## Against design note section 4
No contradictions. Rows now verified at runtime:
- "Web App host page | **No file needed** | ... `MapRazorComponents<App>` needs only an `IComponent` (source; **unverified at runtime**)": verified at runtime.
- `@rendermode` row: confirmed. Custom subclasses of `RenderModeAttribute` work for Server, WebAssembly and Auto.
- `RequiresAspNetWebAssets` trap: confirmed (404) and fixed by the csproj property. The claim that a package `build/*.props` cannot set it was not tested.

# Render modes

Back to the [README](../README.md).

PureMarkup uses Blazor's render modes as they are. Only the syntax differs from Razor.

| Razor | PureMarkup |
|---|---|
| `@rendermode InteractiveServer` on a page | `[InteractiveServer]` on the class |
| `@rendermode InteractiveWebAssembly` | `[InteractiveWebAssembly]` |
| `@rendermode InteractiveAuto` | `[InteractiveAuto]` |
| `<Counter @rendermode="InteractiveServer" />` | `Comp<Counter>().RenderMode(RenderMode.InteractiveServer)` |
| `<Routes @rendermode="..." />` in `App.razor` | `Comp<Routes>().RenderMode(...)` in `App.cs` |

A page with no attribute renders as static SSR.

## Per page or global
- Per page (template default): each interactive page carries its attribute.
- Global (`--all-interactive`): the render mode is set once, on `Routes` and `HeadOutlet` in `App.cs`, and pages carry
  no attribute. With `WebAssembly` or `Auto`, `Routes`, the layout and every page move to the `.Client` project.
  `App` and `Error` (which reads `HttpContext`) stay on the server. Ignored with `--interactivity None`.

## What goes in `.Client`
With WebAssembly or Auto, every component that runs in the browser goes in `.Client`: an interactive page and everything
it renders (its components, presets and `[Stylesheet]` classes). With `--all-interactive`, the router and layout go there too.
The server project references `.Client` and can use all of it. `.Client` cannot see the server project.
Static (SSR) pages and server-only code (`HttpContext`, databases, secrets) stay on the server.

Each project writes its own stylesheet (`{Assembly}.pm.css`). `StyleLink()` in `App.cs` links both.

## Crossing into an interactive component
Its parameters must be serializable. A static parent cannot pass a `Node`, a `RenderFragment` (`.Children(...)`, typed
fragment methods) or a callback to a child with a render mode. This is the same rule as in Razor: the framework throws at
render time. Pass data instead, or move the parent into the interactive part.

## Auto
The first visit runs on Server while the browser downloads the WebAssembly runtime. Later visits run on WebAssembly.
So an Auto page must work in both places: put it in `.Client` and keep server-only code out of it.

## `blazor.web.js`
The server project needs `<RequiresAspNetWebAssets>true</RequiresAspNetWebAssets>`. The SDK only adds `blazor.web.js` when
a `.razor` file exists, and a PureMarkup app has none. The template sets it. A Release publish run in Production serves the
full script (checked for Server, WebAssembly and Auto, see [trimming-aot.md](trimming-aot.md#production-publish)).

# PureMarkup.Blazor

C# markup for Blazor: write components, pages and layouts as plain C#, with no `.razor` files.
An independent project for Blazor; not affiliated with Microsoft.

```csharp
[Route("/counter")]
[InteractiveServer]
public sealed class Counter : MarkupComponent
{
    private int count;

    protected override Node Render() => Fragment(
        Comp<PageTitle>().Children("Counter"),
        H1().Text("Counter"),
        P().Class("count").Text($"Current count: {count}"),
        Button().Type(ButtonType.Button).OnClick(() => count++).Text("Click me"),
        If(count > 5, () => P().Text("That is a lot of clicks.")));
}
```

## What is in the box
- `Div()`, `Input()`, ... for 112 HTML elements, typed attribute methods (`A().Href(...)` compiles, `Div().Href(...)` does not),
  enums for closed value sets, and `On<Event>` methods for all 96 Blazor events. Generated from `src/PureMarkup.Blazor/Html/html.data`.
- `Comp<T>()` for components: `.Param`, `.Children(() => ...)`, `.ChildContent<TContext>(...)`, `.Callback`, `.Key`, `.Ref`, `.RenderMode`.
- `If`, `ForEach(items, key, body)`, `Fragment`, `Content(RenderFragment)`, `Static(static () => ...)`, `Markup`, `Doctype`.
- `.Bind(() => model.Name)` on inputs, with no `Expression.Compile()`: a source generator intercepts the call; other shapes use a reflection walk.
  `.Bind(() => v, x => v = x)` on elements.
- `MarkupComponent`, `MarkupLayout`, and `[InteractiveServer]`, `[InteractiveWebAssembly]`, `[InteractiveAuto]`.
- Trimming and AOT analyzers are on and report nothing.

## How it renders
`Render()` builds a tree of struct handles in per-render pooled arrays, which is then written to the `RenderTreeBuilder`.
A child's sequence number is its slot in its parent, and slots never move, so diffs match Razor's.
Nodes are only valid during the render that built them.

## Setup
Reference `PureMarkup.Blazor`, add the generator as an analyzer, then add:

```xml
<InterceptorsNamespaces>$(InterceptorsNamespaces);PureMarkup.Blazor.Generated</InterceptorsNamespaces>
<RequiresAspNetWebAssets>true</RequiresAspNetWebAssets> <!-- server project of a Blazor Web App with no .razor files -->
```

and `global using static PureMarkup.Blazor.H;`. See `samples/WebApp` for a full Blazor Web App (static SSR, Server, WebAssembly, Auto).

## Repository and license
`src/` library and generators, `tests/` xUnit, `benchmarks/` against Razor (see its README), `samples/`, `spikes/` M0 experiments.
MIT, see `LICENSE`.

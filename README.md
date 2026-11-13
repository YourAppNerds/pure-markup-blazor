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
- `Comp<T>()` for components, with typed parameter methods for every framework component (see below), plus `.Children(() => ...)`,
  `.Key`, `.Ref`, `.RenderMode`, and the untyped `.Param`, `.Fragment`, `.Callback`.
- `If`, `ForEach(items, key, body)`, `Fragment`, `Content(RenderFragment)`, `Static(static () => ...)`, `Markup`, `Doctype`.
- `.Bind(() => model.Name)` on inputs, with no `Expression.Compile()`: a source generator intercepts the call; other shapes use a reflection walk.
  `.Bind(() => v, x => v = x)` on elements.
- `MarkupComponent`, `MarkupLayout`, and `[InteractiveServer]`, `[InteractiveWebAssembly]`, `[InteractiveAuto]`.
- Trimming and AOT analyzers are on and report nothing.

## Components
```csharp
Comp<EditForm>().Model(Draft).FormName("draft").OnValidSubmit(Save).ChildContent(ctx => Fragment(
    Comp<InputNumber<int>>().Value(Draft.Age).ValueChanged(v => Draft.Age = v).ValueExpression(() => Draft.Age).Attr("id", "age"),
    Button().Type(ButtonType.Submit).Text("Save")))
Comp<AuthorizeView>().Authorized(s => P().Text($"Hi {s.User.Identity!.Name}")).NotAuthorized(_ => P().Text("Sign in"))
```
- Components cannot be `new`ed, so `Comp<T>()` describes one and parameters are extension methods on it, one per `[Parameter]`
  (inherited ones included). On a generic component the type argument comes from the receiver: `Comp<InputNumber<int>>().Value(5)`.
- Generated when the library is built, from the framework assemblies it references: `Microsoft.AspNetCore.Components`
  (`.Routing`, `.Sections`, ...), `.Web`, `.Forms`, `.Authorization`. 34 components, 183 methods on net10.0.
- `EventCallback<T>`: `Action`, `Action<T>`, `Func<Task>`, `Func<T, Task>` (the component rendering the markup is the receiver,
  so it re-renders, as with `On<Event>`), or an `EventCallback<T>` to forward. `EventCallback`: `Action`, `Func<Task>`, `EventCallback`.
- `RenderFragment`: `Func<Node>`; `RenderFragment<T>`: `Func<T, Node>`. Both are built when the child renders the fragment.
  There is no `Node` overload: a node belongs to the render that built it, and the child renders the fragment later.
  To pass an existing `RenderFragment`, use `.Param("Name", fragment)`.
- `ChildContent`: `.Children(() => ...)` / `.Children("text")` for `RenderFragment`; a typed `.ChildContent(ctx => ...)` for `RenderFragment<T>`.
- `.Attr(name, value)` on components with a `CaptureUnmatchedValues` parameter (`InputText`, `NavLink`, `EditForm`, ...).
- `ValueExpression`-style parameters take `Expression<Func<T>>`. `[CascadingParameter]` and `[SupplyParameterFrom*]` get no method.
- Name clash rule: a parameter named like a `ComponentNode<T>` member (`Key`, `Ref`, `RenderMode`, `Param`, `Children`, `Attr`, ...)
  gets the suffix `Param`: a `Key` parameter is `.KeyParam(...)`, while `.Key(...)` stays Razor's `@key`.
- Values are stored like `.Param` (value types are boxed, as Razor's `AddComponentParameter(int, string, object)` does).
- Your own and third-party components: `.Param("Name", value)`, `.Fragment`, `.Callback` until consumer-side generation lands.

## Styling: CSS in C#
```csharp
[Stylesheet]
public static class CardCss
{
    public static Rule Card { get; } = Css.Rule(r => r
        .Padding(Rem(1)).Background(Tokens.Surface)                  // Tokens: Css.Token("surface", light: ..., dark: ...)
        .Hover(h => h.BoxShadow("0 2px 8px rgb(0 0 0 / .2)"))
        .Media(Media.MaxWidth(Px(640)), m => m.Padding(Rem(0.5))));
    public static Var<Length> Progress { get; } = Css.Var<Length>(Percent(0));
}
Div().Class("raw").Class(CardCss.Card).Set(CardCss.Progress, Percent(40))   // classes are additive
```
- Class names are compile-time constants, `{Type}_{Member}-{hash}` (`pm-{hash}` in Release): the generator intercepts `Css.Rule`/`Css.Var`.
  Static SSR, prerender and WebAssembly write the same class. Rule bodies run only at build time; rendering adds a class string.
- After compile, `PureMarkup.Blazor.CssTool` loads the built assembly in a separate process and writes `{Assembly}.styles.css`,
  added as a fingerprinted static web asset (server and `.Client` projects). `StyleLink()` in `Head()` links each, dependencies first.
- File order: `@layer reset, vendor, pm.base, pm.preset, pm.local, pm.preset-state, pm.local-state, utilities;`. Rules go in
  `pm.local`, their state blocks (`.Hover`, `.Active`, `.FocusVisible`, `.Disabled`, `.Checked`, `.When`) in `pm.local-state`.
- 747 typed properties with keyword types (`Display.Flex`) from w3c/webref (`Css/css.data`, `tools/cssdata`); `.Prop(name, value)` for the rest.
- Also `.Nest`, `.Is`, `.Before`, `.Container`, `.Supports`, `Css.Global`, `Css.FontFace`, `Css.ImportLayer`, and `.Style(s => ...)` inline.
- Setup: import `build/PureMarkup.Blazor.targets` (as `samples/Directory.Build.targets` does) and `using PureMarkup.Blazor.Styling;`.

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
`src/` library, generators and the CSS build tool, `tests/` xUnit, `benchmarks/` against Razor (see its README), `samples/`, `spikes/` M0 experiments.
MIT, see `LICENSE`.

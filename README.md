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
- `Comp<T>()` for components, with typed parameter methods for framework, third-party and your own C# components (see below), plus `.Children(() => ...)`,
  `.Key`, `.Ref`, `.RenderMode`, and the untyped `.Param`, `.Fragment`, `.Callback`.
- Presets: `Preset.Once<ButtonElement>(p => p.Class(...))`, `.Use`, `.With`, `.New`, `.ClassReplace` ([docs/styling.md](docs/styling.md#presets)).
- `If`, `ForEach(items, key, body)`, `Fragment`, `Content(RenderFragment)`, `Static(static () => ...)`, `Markup`, `Doctype`.
- `.Bind(() => model.Name)` on inputs, with no `Expression.Compile()`: a source generator intercepts the call; other shapes use a reflection walk.
  `.Bind(() => v, x => v = x)` on elements.
- `MarkupComponent`, `MarkupLayout`, and `[InteractiveServer]`, `[InteractiveWebAssembly]`, `[InteractiveAuto]`.
- Trimming: no trim or AOT warnings from PureMarkup; a trimmed WebAssembly publish keeps about 45 to 55 KB of it ([trimming-aot.md](docs/trimming-aot.md)).

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
  (`.Routing`, `.Sections`, ...), `.Web`, `.Forms`, `.Authorization`. 34 components, 187 methods on net10.0.
- `EventCallback<T>`: `Action`, `Action<T>`, `Func<Task>`, `Func<T, Task>` (the component rendering the markup is the receiver,
  so it re-renders, as with `On<Event>`), or an `EventCallback<T>` to forward. `EventCallback`: `Action`, `Func<Task>`, `EventCallback`.
- `RenderFragment`: `Func<Node>` or `string` (text); `RenderFragment<T>`: `Func<T, Node>`. Built when the child renders the fragment.
  There is no `Node` overload: a node belongs to the render that built it, and the child renders the fragment later.
  To pass an existing `RenderFragment`, use `.Param("Name", fragment)`.
- `ChildContent`: `.Children(() => ...)` / `.Children("text")` for `RenderFragment`; a typed `.ChildContent(ctx => ...)` for `RenderFragment<T>`.
- `.Attr(name, value)` on components with a `CaptureUnmatchedValues` parameter (`InputText`, `NavLink`, `EditForm`, ...).
- `ValueExpression`-style parameters take `Expression<Func<T>>`. `[CascadingParameter]` and `[SupplyParameterFrom*]` get no method.
- Name clash rule: a parameter named like a `ComponentNode<T>` member (`Key`, `Ref`, `RenderMode`, `Param`, `Children`, `Attr`, `Use`, `Also`, ...)
  gets the suffix `Param`: a `Key` parameter is `.KeyParam(...)`, while `.Key(...)` stays Razor's `@key`.
- Values are stored like `.Param` (value types are boxed, as Razor's `AddComponentParameter(int, string, object)` does).

### Third-party and your own components
```csharp
[assembly: GenerateMarkup(typeof(MudButton))]                            // one component (typeof(MudSelect<>) for generic ones)
[assembly: GenerateMarkup(typeof(MudButton), WholeAssembly = true)]      // every public component in MudBlazor
Comp<MudTextField<string>>().Label("Name").Value(Name).ValueChanged(v => Name = v)
```
- The generator runs in the project that carries the attribute (same shape as PureMarkup.Maui's `GenerateMarkup`: `Type`,
  `WholeAssembly`, `Namespace`). MudBlazor 9.12 whole-assembly: 166 components, 5021 methods, no warnings
  (6 components have no settable parameters: `MudSpacer`, `MudPopoverProvider`, ...).
- C# components declared in the project (`MarkupComponent` or any `ComponentBase` subclass, public or internal) get methods
  automatically, with no attribute: like Razor, where a component in the project is just usable. Components with no parameters get nothing.
- Derived components: `ComponentNode<T>` is invariant, so `class MyInput : InputText` gets its own flattened methods (inherited parameters
  included): automatically when it is in the project, through the attribute when it comes from a package.
- Output: `internal` classes `{Namespace}_{Name}Parameters` in namespace `PureMarkup.Blazor` (or `Namespace = "..."`), so the
  usual `using PureMarkup.Blazor;` is all you need, and two assemblies generating for the same type never clash.
- Types the library already covers are skipped. Diagnostics: `PMCOMP001` not a concrete component, `PMCOMP002` not accessible,
  `PMCOMP003` already covered (remove the attribute); see [docs/diagnostics.md](docs/diagnostics.md).
- `.razor` components of the same project come from another source generator, which this one cannot see: use `.Param("Name", value)`,
  `.Fragment`, `.Callback` for them.
- Trimming: `Comp<Router>()` warns IL2111 (and IL2110 in a trimmed publish) for `Router.NotFoundPage` in a project with the trim
  analyzer on, exactly as `<Router>` in a `.razor` file does; it comes from the framework's annotations. Generated methods add no warnings.
- Scoped CSS from packages (QuickGrid) works: the SDK's `{Assembly}.styles.css` bundle and the generated `{Assembly}.pm.css` have
  different names, and `StyleLink()` links both (see Styling).

## Styling: CSS in C#
```csharp
[Stylesheet]
public static class CardCss
{
    public static Rule Card { get; } = Css.Rule(r => r
        .Padding(Rem(1)).Background(Tokens.Surface)                  // Tokens: Css.Token("surface", light: ..., dark: ...)
        .Hover(h => h.BoxShadow("0 2px 8px rgb(0 0 0 / .2)"))
        .Media(Media.MaxWidth(Px(640)), m => m.Padding(Rem(0.5))));
}
public static Preset<ButtonElement> PrimaryButton => Preset.Once<ButtonElement>(p => p.Class(ButtonCss.Primary).Type(ButtonType.Button));
Div().Class("raw").Class(CardCss.Card)                                // classes are additive
Button().Use(PrimaryButton).Type(ButtonType.Submit).Text("Save")      // local writes beat the preset's
```
- Rule bodies run only at build time. The build writes `{Assembly}.pm.css` (a fingerprinted static web asset) and
  `StyleLink()` in `Head()` links it. Rendering adds a constant class string.
- Cascade layers decide who wins (local beats preset, state beats base), light/dark tokens, 747 typed properties, and
  analyzers that keep stylesheets pure. Presets are reusable, recorded writes for any element or component.
- `dotnet watch` applies edited rule bodies and preset lambdas without a restart.

Details: [docs/styling.md](docs/styling.md).

## How it renders
`Render()` builds a tree of struct handles in per-render pooled arrays, which is then written to the `RenderTreeBuilder`.
A child's sequence number is its slot in its parent, and slots never move, so diffs match Razor's.
Nodes are only valid during the render that built them.


## Get started
```
dotnet new install PureMarkup.Blazor.Templates@0.1.0-preview.1
dotnet new puremarkup-blazor -o MyApp --interactivity Auto --sample-content
```
A Blazor Web App like `dotnet new blazor`, with no `.razor` or `.html` files. Interactivity is per page
(`[InteractiveServer]`, `[InteractiveWebAssembly]`, `[InteractiveAuto]`) or global (`--all-interactive`).
Packing, options, the item template, setup without the template and the template check:
[docs/getting-started.md](docs/getting-started.md). Render modes and the `.Client` project: [docs/render-modes.md](docs/render-modes.md).

## Docs
- [Getting started](docs/getting-started.md): templates, setup, `scripts/verify-template.mjs`
- [Styling](docs/styling.md): CSS in C#, presets, Hot Reload
- [Render modes](docs/render-modes.md): per page or global, what goes in `.Client`, crossing boundaries
- [Diagnostics](docs/diagnostics.md): PMCOMP001-003, PMCSS001, PMCSS010-013, PMCSS020-021
- [Trimming, AOT and Production publish](docs/trimming-aot.md)
- [WebAssembly benchmark](docs/wasm-benchmark.md); server benchmarks in [benchmarks/README.md](benchmarks/README.md)

## Repository and license
`src/` library, generators and the CSS build tool, `templates/` dotnet new templates, `scripts/` template check, `tests/` xUnit,
`benchmarks/` against Razor (server and WebAssembly), `samples/`, `docs/`, `spikes/` M0 experiments.
MIT, see `LICENSE`.

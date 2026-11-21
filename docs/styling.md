# Styling: CSS in C#, presets and Hot Reload

Back to the [README](../README.md). Analyzer IDs are explained in [diagnostics.md](diagnostics.md).

## CSS in C#
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
- After compile, `PureMarkup.Blazor.CssTool` loads the built assembly in a separate process and writes `{Assembly}.pm.css`,
  added as a fingerprinted static web asset (server and `.Client` projects). `StyleLink()` in `Head()` links each, dependencies first.
  The name is not `{Assembly}.styles.css` because the SDK writes its scoped CSS bundle (`.razor.css` files, packages such as
  QuickGrid) under that name.
- When the app has that SDK bundle, `StyleLink()` links it first, as `App.razor` in a Razor template does (needs the asset map from
  `MapStaticAssets`). `StyleLink(scopedCss: false)` leaves it out, for an app that links or imports it itself.
- Scoped CSS is unlayered, and unlayered CSS beats every layer, so it wins over `pm.*` rules on the same element and property.
  Its selectors carry `[b-xxxx]` scope attributes and hit only that component's own elements, so a clash needs a rule aimed at those
  elements. To let `pm.*` rules win there, put the bundle in the `vendor` layer: `Css.ImportLayer("vendor", "WebApp.styles.css")`
  plus `StyleLink(scopedCss: false)` (not fingerprinted then; **unverified** in the sample).
- File order: `@layer reset, vendor, pm.base, pm.preset, pm.local, pm.preset-state, pm.local-state, utilities;`. Every rule goes
  in `pm.local`, its state blocks (`.Hover`, `.Active`, `.FocusVisible`, `.Disabled`, `.Checked`, `.When`) in `pm.local-state`.
  Rules applied in a preset are also written to `pm.preset` / `pm.preset-state` under a separate class (see Presets).
  A later layer beats an earlier one per property, whatever the specificity or source order:

| On one element | Winner |
|---|---|
| Preset rule vs local rule (base values) | Local |
| Preset `:hover` (any state block) vs local base value | Preset state: every state layer follows every base layer |
| Local `:hover` vs preset `:hover` | Local |
| `.With` derived preset's rule vs its base's | Derived (written later in `pm.preset`) |
| Inline `.Style(...)` vs any rule | Inline |

- Theme: `Css.Token("surface", light: ..., dark: ...)` writes the light value on `:root` (with `color-scheme: light dark`), the
  dark one under `@media (prefers-color-scheme: dark)` unless `<html>` has `pm-light`, and both as classes: `.pm-dark` and
  `.pm-light` on any element pin its subtree to one scheme (custom properties inherit, so the nearest themed ancestor wins).
  `Html().Theme(ThemeMode.Dark)` / `Div().Theme(ThemeMode.Light)` writes the class (`ThemeMode.Auto` writes none;
  `ThemeClass.Dark` / `.Light` are the names). Switching at runtime needs no re-render: toggle the class from JS,
  `document.documentElement.classList.toggle('pm-dark')`, and every token-based value follows.
- 747 typed properties with keyword types (`Display.Flex`) from w3c/webref (`Css/css.data`, `tools/cssdata`); `.Prop(name, value)` for the rest.
  A constant name is checked against webref (properties, legacy aliases such as `word-wrap`, and `@font-face` descriptors
  inside `Css.FontFace`): `PMCSS020` (warning) for an unknown name, with a near-miss suggestion (`"colr"`, `"backgroundColor"`);
  `--custom` and `-vendor-` names and non-constant names are not checked. `PMCSS021` (hidden by default, so it stays out of the error list;
  raise it with `dotnet_diagnostic.PMCSS021.severity = suggestion` in `.editorconfig`) when a typed method exists.
- Purity: the build runs `[Stylesheet]` types and static `Preset<T>` members in a separate process with no DI, JS or app
  environment, so analyzers flag, in those types and members (rule bodies included): `PMCSS010` (error) DI, JS or network
  (`[Inject]`, `IServiceProvider`, `Microsoft.JSInterop`, `NavigationManager`, `HttpClient`, `Microsoft.AspNetCore.Http`,
  `Microsoft.Extensions.{DependencyInjection,Configuration,Logging,Options,Hosting}`, ...), also as a member's type;
  `PMCSS011` (error) the build machine (`File`, `Directory`, `FileStream`, `Environment` except `NewLine`, `Process`, registry);
  `PMCSS012` (warning) a value that changes per build (`DateTime.Now`/`UtcNow`/`Today`, `Guid.NewGuid`, `Random`, `Stopwatch`,
  `TimeProvider`). `PMCSS013` (warning, full builds only, not live in the IDE) when they reach such code through methods,
  properties or static initializers of other types in the project. APIs are matched by name; not seen: code inside other
  assemblies, virtual or interface calls, delegates passed in, reflection. A `Preset<T>` in an ordinary class also counts that
  class's static initializers.
- Also `.Nest`, `.Is`, `.Before`, `.Container`, `.Supports`, `Css.Global`, `Css.FontFace`, `Css.ImportLayer`, and `.Style(s => ...)` inline.
- Setup: the package imports `build/PureMarkup.Blazor.targets` itself (project references in the repo import it, as
  `samples/Directory.Build.targets` does); add `using PureMarkup.Blazor.Styling;`.

## Presets
```csharp
public static class AppPresets
{
    public static Preset<ButtonElement> PrimaryButton => Preset.Once<ButtonElement>(p => p
        .Class(ButtonCss.Primary).Type(ButtonType.Button));
    public static Preset<ButtonElement> DangerButton => PrimaryButton.With(p => p.Class(ButtonCss.Danger));
    public static Preset<ComponentNode<InputText>> Field => Preset.Once<ComponentNode<InputText>>(p => p.Class(FormCss.Field));
}
Button().Use(AppPresets.PrimaryButton).Type(ButtonType.Submit).Text("Save")      // Submit beats the preset's Button
Button().Use(AppPresets.PrimaryButton).Class(PageCss.Wide).Style(s => s.MarginTop(0))
AppPresets.DangerButton.New().Text("Delete")
Comp<InputText>().Use(AppPresets.Field).Bind(() => Model.Name)
```
- A preset is recorded writes: the lambda gets a node and uses the normal generated methods (no separate preset API), so it
  works on any element (`Preset<ButtonElement>`, or `Preset<Element>` for global attributes on every element) and any component
  with typed methods (`Preset<ComponentNode<T>>`, MudBlazor included). The writes are recorded once, into the preset's own
  storage, the first time it is used; `Preset.Once` returns the same preset per (base, lambda), so a property getter is fine.
  The lambda must capture nothing.
- Merge rules, whatever the call order (`.Type(...)` before or after `.Use` wins either way):

| Write | Preset vs local |
|---|---|
| Attribute or component parameter | Local wins, by name. A local `null` (attribute absent) also wins. |
| `class` | **Additive**: the preset's classes (strings and rules) come first, then the node's own. `.ClassReplace(...)` drops the preset's classes and rules; the node's other classes stay. |
| Inline `.Style(...)` | Merged by CSS property; local wins. |
| Events, children, fragment parameters (`ChildContent`, ...) | Local replaces the preset's. |
| `.Use` twice | The later preset replaces the earlier. Compose with `.With`: base writes first, derived ones override. |

- Components: `.Class(...)` / `.ClassReplace(...)` (string or `Rule`) are generated for components that capture unmatched
  attributes (the `class` attribute: `InputText`, `NavLink`, ...) and, additively, for a string `Class` parameter (MudBlazor's).
- `.Also(n => ...)` inside a preset (MAUI's meaning): runs on the node at every `.Use` / `.New`, in order, for per-render work;
  its writes count as local writes made at the `.Use` call. `.New()` creates the element or component with the preset applied.
- Not in a preset: `.Key` (throws when recorded) and `.Bind` (throws: a preset is shared, a binding is per instance).
  Rules must still be declared in a `[Stylesheet]` class; reference them from the preset (`.Class(ButtonCss.Primary)`).
- Cost per render: `.Use` takes one attribute slot (the preset's attributes share its sequence number, so slots never move)
  and allocates nothing; merging happens when the node is written. Only a class or style set on both sides is concatenated.
- Layers: a rule applied inside a preset writes `{class}-p` (`Rule.PresetClassName`); applied locally, its plain class. The
  build evaluates every static `Preset<T>` property and field of the assembly (any class) and writes each preset rule once more,
  as `.{class}-p`, in `pm.preset` (state blocks in `pm.preset-state`), base presets first (depth-first over `.With` and `.Use`
  inside the lambda), so a derived preset's rules win. All rules keep their `pm.local` copy. See the table in Styling.
- Why a separate class: with one class name in both layers, a rule R used in a preset on one element and locally on another
  has a `pm.local` copy that matches every element carrying R, including one that got R only from its preset. There R would sit
  in `pm.local` beside the element's own local rule L, and source order (not "local wins") would decide R against L. With
  `R-p` the preset copy only ever lives in `pm.preset`, so L beats it per property in any order; `/cascade` in the sample checks this.
- Limits: a rule shared by unrelated presets is written once, at its first use, so two presets that apply the same two rules in
  opposite orders cannot both win (as with any one stylesheet). A preset only reachable from code (not a static member, or used
  on a child inside another preset) is not scanned: its rules have no `-p` copy. Rules in a dependency's preset are written by
  that dependency's own build.
- Hot Reload: a `[MetadataUpdateHandler]` clears the preset cache and every preset's recorded writes, so an edited preset lambda
  applies on the re-render that follows (static-field presets record again too). Then the stylesheets are rewritten (see Hot Reload).

## Hot Reload
Under `dotnet watch`, an edited rule body (`Css.Rule(r => ...)`; `Css.Global` and `Css.FontFace` bodies work the same way) or
preset lambda (a rule newly used in a preset gets its `-p` copy) shows up in the browser without a restart. Checked in the
sample: static SSR and WebAssembly pages, server and `.Client` stylesheets.
- How: a Debug build (`Optimize` not `true`) records the path of `obj/.../pmcss/{Assembly}.pm.css` as
  `[AssemblyMetadata("PureMarkup.Blazor.CssFile", ...)]`. After each update the handler re-evaluates the stylesheets of every loaded
  assembly with a recorded path in-process (an edited lambda runs its new body) and rewrites the file if it changed. The file is a
  static web asset, so `dotnet watch` pushes it to the browser, which reloads its stylesheet links; the dev server serves the new
  content at the same fingerprinted URL. Class names do not change, so the markup stays valid.
- WebAssembly: the browser cannot write the file; the server process loads the `.Client` assembly too, gets the same update and
  rewrites the `.Client` stylesheet.
- Release: no path is recorded, so the handler does nothing (and trimmed apps drop it). `PureMarkupCssHotReload=false` turns it off.
- Needs a restart (Ctrl+R in `dotnet watch`) or a rebuild: a new `Rule`/`Var`/`Token` member (Hot Reload does not run a new static
  initializer, so the member is `null`) and a renamed member (new class name); a new `[Stylesheet]` class is untested. Without
  `dotnet watch` (plain `dotnet run`), rebuild as before. A rule body that throws logs to stderr and leaves the file as it was.

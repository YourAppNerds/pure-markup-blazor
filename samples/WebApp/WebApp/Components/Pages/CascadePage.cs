using WebApp.Client.Theme;

namespace WebApp.Components.Pages;

/// <summary>
/// The preset/local cascade, checked by verify.mjs with computed styles. Each rule sets <c>color</c>, so the winner is visible.
/// Declaration order is chosen so that source order alone would pick the wrong winner.
/// </summary>
[Stylesheet]
public static class CascadeCss
{
    public static Rule Local { get; } = Css.Rule(r => r.Color(Rgb(0, 0, 255)));                      // blue
    public static Rule Derived { get; } = Css.Rule(r => r.Color(Rgb(128, 0, 128)));                  // purple, declared before its base
    public static Rule Shared { get; } = Css.Rule(r => r.Color(Rgb(255, 0, 0)));                     // red: in presets and used locally
    public static Rule LocalHover { get; } = Css.Rule(r => r.Hover(h => h.Color(Rgb(0, 128, 0))));   // green on hover
    public static Rule PresetHover { get; } = Css.Rule(r => r.Hover(h => h.Color(Rgb(255, 165, 0)))); // orange on hover
    public static Rule Panel { get; } = Css.Rule(r => r.Background(Tokens.Surface).Color(Tokens.Text).Padding(Rem(0.5)));
}

public static class CascadePresets
{
    public static Preset<DivElement> Red => Preset.Once<DivElement>(static p => p.Class(CascadeCss.Shared));
    public static Preset<DivElement> RedHover => Preset.Once<DivElement>(static p => p.Class(CascadeCss.Shared).Class(CascadeCss.PresetHover));
    public static Preset<DivElement> Purple => Red.With(static p => p.Class(CascadeCss.Derived));
}

[Route("/cascade")]
public sealed class CascadePage : MarkupComponent
{
    protected override Node Render() => Fragment(
        Comp<PageTitle>().Children("Cascade"),
        H1().Text("Preset and local layers"),
        Div().Id("shared-local").Class(CascadeCss.Shared).Text("Shared rule used locally (red)"),
        Div().Id("preset-vs-local").Use(CascadePresets.Red).Class(CascadeCss.Local).Text("Preset red, local blue: blue"),
        Div().Id("preset-hover").Use(CascadePresets.RedHover).Class(CascadeCss.Local).Text("Local blue; preset hover orange"),
        Div().Id("local-hover").Use(CascadePresets.RedHover).Class(CascadeCss.LocalHover).Text("Preset hover orange; local hover green"),
        Div().Id("derived").Use(CascadePresets.Purple).Text(".With derived purple beats base red"),
        Div().Id("inline").Use(CascadePresets.Red).Class(CascadeCss.Local).Style(s => s.Color(Rgb(1, 2, 3))).Text("Inline beats all"),
        Div().Id("dark-panel").Theme(ThemeMode.Dark).Class(CascadeCss.Panel).Text("Always dark (.Theme(ThemeMode.Dark))"));
}

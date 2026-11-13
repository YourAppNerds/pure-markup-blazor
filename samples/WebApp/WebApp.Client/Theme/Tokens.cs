namespace WebApp.Client.Theme;

/// <summary>Design tokens: CSS custom properties with light and dark values (no app.css anywhere).</summary>
[Stylesheet]
public static class Tokens
{
    public static Token<Color> Surface { get; } = Css.Token("surface", light: Hex("#ffffff"), dark: Hex("#1b1b1f"));
    public static Token<Color> Text { get; } = Css.Token("text", light: Hex("#1b1b1f"), dark: Hex("#e8e8ee"));
    public static Token<Color> Accent { get; } = Css.Token("accent", light: Rgb(37, 99, 235), dark: Rgb(96, 165, 250));
    public static Token<Color> AccentHover { get; } = Css.Token("accent-hover", light: Rgb(29, 78, 216), dark: Rgb(147, 197, 253));
    public static Token<Color> Focus { get; } = Css.Token("focus", Hex("#f59e0b"));
    public static Token<Length> Radius { get; } = Css.Token("radius", Rem(0.5));
}

/// <summary>Page-wide rules, in <c>pm.base</c>.</summary>
[Stylesheet]
public static class Global
{
    public static GlobalRule Body { get; } = Css.Global("body", b => b
        .Margin(0)
        .FontFamily("system-ui, sans-serif")
        .Background(Tokens.Surface)
        .Color(Tokens.Text));

    public static GlobalRule Headings { get; } = Css.Global("h1, h2", h => h.FontWeight(FontWeight.Bold).LineHeight(1.2));
}

/// <summary>The counter button shared by the WebAssembly, Auto and Server counters.</summary>
[Stylesheet]
public static class CounterCss
{
    public static Rule Button { get; } = Css.Rule(r => r
        .Padding(Rem(0.5), Rem(1))
        .Border("none")
        .BorderRadius(Tokens.Radius)
        .Background(Tokens.Accent)
        .Color(Hex("#ffffff"))
        .Cursor("pointer")
        .TransitionProperty("background-color")
        .TransitionDuration(Ms(150))
        .Hover(h => h.Background(Tokens.AccentHover))
        .FocusVisible(f => f.Outline(Px(2), OutlineStyle.Solid, Tokens.Focus))
        .Disabled(d => d.Opacity(0.5).Cursor("not-allowed"))
        .Media(Media.PrefersReducedMotion, m => m.TransitionDuration(Ms(0))));

    /// <summary>Count display; its width follows a per-instance variable.</summary>
    public static Var<Length> Fill { get; } = Css.Var<Length>(Percent(0));

    public static Rule Count { get; } = Css.Rule(r => r
        .FontVariantNumeric("tabular-nums")
        .Nest("& + button", b => b.MarginTop(Rem(0.5))));

    public static Rule Bar { get; } = Css.Rule(r => r
        .Height(Px(4))
        .Width(Fill)
        .Background(Tokens.Accent));
}

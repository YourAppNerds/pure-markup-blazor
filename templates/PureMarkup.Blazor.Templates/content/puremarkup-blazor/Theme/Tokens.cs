namespace PureMarkupApp.Theme;

/// <summary>
/// Design tokens: CSS custom properties with a light and a dark value. The dark one applies when the OS prefers dark,
/// or under an element with <c>.Theme(ThemeMode.Dark)</c>. Everything else styles through these.
/// </summary>
[Stylesheet]
public static class Tokens
{
    public static Token<Color> Surface { get; } = Css.Token("surface", light: Hex("#ffffff"), dark: Hex("#1b1b1f"));
    public static Token<Color> Text { get; } = Css.Token("text", light: Hex("#212529"), dark: Hex("#e8e8ee"));
    public static Token<Color> Link { get; } = Css.Token("link", light: Hex("#006bb7"), dark: Hex("#5aa9ff"));
    public static Token<Color> Primary { get; } = Css.Token("primary", light: Hex("#1b6ec2"), dark: Hex("#2f81d6"));
    public static Token<Color> PrimaryBorder { get; } = Css.Token("primary-border", light: Hex("#1861ac"), dark: Hex("#3a8be0"));
    public static Token<Color> PrimaryHover { get; } = Css.Token("primary-hover", light: Hex("#1861ac"), dark: Hex("#4a95e4"));
    public static Token<Color> TopRow { get; } = Css.Token("top-row", light: Hex("#f7f7f7"), dark: Hex("#26262b"));
    public static Token<Color> Border { get; } = Css.Token("border", light: Hex("#d6d5d5"), dark: Hex("#3a3a40"));
    public static Token<Color> Focus { get; } = Css.Token("focus", Hex("#258cfb"));
    public static Token<Length> Radius { get; } = Css.Token("radius", Rem(0.375));
}
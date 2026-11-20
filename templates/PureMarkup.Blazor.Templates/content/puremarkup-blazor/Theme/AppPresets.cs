namespace PureMarkupApp.Theme;

/// <summary>
/// Presets: recorded writes applied with <c>.Use</c> (or created with <c>.New()</c>). Local writes beat them; classes add up.
/// </summary>
public static class AppPresets
{
    public static Preset<ButtonElement> PrimaryButton => Preset.Once<ButtonElement>(p => p
        .Class(ButtonCss.Primary).Type(ButtonType.Button));
}

[Stylesheet]
public static class ButtonCss
{
    public static Rule Primary { get; } = Css.Rule(r => r
        .Padding(Rem(0.375), Rem(0.75))
        .FontSize(Rem(1))
        .Border(Px(1), "solid", Tokens.PrimaryBorder)
        .BorderRadius(Tokens.Radius)
        .Background(Tokens.Primary)
        .Color(Hex("#ffffff"))
        .Cursor("pointer")
        .Hover(h => h.Background(Tokens.PrimaryHover))
        .FocusVisible(f => f.BoxShadow("0 0 0 0.1rem white, 0 0 0 0.25rem #258cfb")));
}
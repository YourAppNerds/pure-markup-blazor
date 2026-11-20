namespace PureMarkupApp.Theme;

/// <summary>Page-wide rules (the stock template's app.css), written to the <c>pm.base</c> layer.</summary>
[Stylesheet]
public static class Global
{
    public static GlobalRule Body { get; } = Css.Global("html, body", b => b
        .Margin(0)
        .FontFamily("'Helvetica Neue', Helvetica, Arial, sans-serif")
        .Background(Tokens.Surface)
        .Color(Tokens.Text));

    public static GlobalRule Links { get; } = Css.Global("a, .btn-link", a => a.Color(Tokens.Link));

    public static GlobalRule HeadingFocus { get; } = Css.Global("h1:focus", h => h.Outline("none"));

    public static GlobalRule Table { get; } = Css.Global("table", t => t
        .Width(Percent(100))
        .BorderCollapse(BorderCollapse.Collapse)
        .MarginBottom(Rem(1)));

    public static GlobalRule Cells { get; } = Css.Global("th, td", c => c
        .Padding(Rem(0.5))
        .TextAlign(TextAlign.Left)
        .BorderBottom(Px(1), "solid", Tokens.Border));
}
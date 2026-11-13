using WebApp.Client.Theme;

namespace WebApp.Components.Layout;

/// <summary>Layout rules; this file lives in the server assembly, so the server writes its own WebApp.pm.css.</summary>
[Stylesheet]
public static class LayoutCss
{
    public static Rule Nav { get; } = Css.Rule(r => r
        .Display(Display.Flex)
        .FlexWrap(FlexWrap.Wrap)
        .Gap(Rem(1))
        .Padding(Rem(0.75), Rem(1))
        .BorderBottom(Px(1), "solid", Tokens.Accent)
        .Nest("& a", a => a.Color(Tokens.Accent).TextDecoration("none"))
        .Nest("& a.active", a => a.FontWeight(FontWeight.Bold))
        .Media(Media.MaxWidth(Px(480)), m => m.FlexDirection(FlexDirection.Column)));

    public static Rule Main { get; } = Css.Rule(r => r.Padding(Rem(1)).MaxWidth(Px(960)));
}

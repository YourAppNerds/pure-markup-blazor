using Microsoft.AspNetCore.Components.Routing;

namespace PureMarkupApp.Components.Layout;

public sealed class NavMenu : MarkupComponent
{
    private static readonly (string Href, string Text)[] Links =
    [
        ("", "Home"),
#if (SampleContent && (UseServer || UseWebAssembly))
        ("counter", "Counter"),
#endif
#if (SampleContent)
        ("weather", "Weather"),
#endif
    ];

    protected override Node Render() => Fragment(
        Div().Class(NavMenuCss.TopRow).Children(
            A().Class(NavMenuCss.Brand).Href("").Text("PureMarkupApp")),
        Nav().Class(NavMenuCss.Nav).Children(
            ForEach(Links, l => l.Href, l =>
                Div().Class(NavMenuCss.Item).Children(
                    Comp<NavLink>()
                        .Class(NavMenuCss.Link)
                        .Attr("href", l.Href)
                        .Match(l.Href == "" ? NavLinkMatch.All : NavLinkMatch.Prefix)
                        .Children(l.Text)))));
}

/// <summary>NavMenu.razor.css in the stock template (without the icons and the narrow-screen toggler).</summary>
[Stylesheet]
public static class NavMenuCss
{
    public static Rule TopRow { get; } = Css.Rule(r => r
        .MinHeight(Rem(3.5))
        .Display(Display.Flex)
        .AlignItems(AlignItems.Center)
        .Padding(0, Rem(1))
        .Background("rgba(0, 0, 0, 0.4)"));

    public static Rule Brand { get; } = Css.Rule(r => r
        .FontSize(Rem(1.1))
        .Color(Hex("#ffffff"))
        .TextDecoration("none"));

    public static Rule Nav { get; } = Css.Rule(r => r
        .Display(Display.Flex)
        .FlexDirection(FlexDirection.Column)
        .Padding(Rem(1), 0));

    public static Rule Item { get; } = Css.Rule(r => r
        .FontSize(Rem(0.9))
        .Padding(0, Rem(1), Rem(0.5)));

    public static Rule Link { get; } = Css.Rule(r => r
        .Color(Hex("#d7d7d7"))
        .BorderRadius(Px(4))
        .Height(Rem(3))
        .Display(Display.Flex)
        .AlignItems(AlignItems.Center)
        .Padding(0, Rem(1))
        .TextDecoration("none")
        .Hover(h => h.Background("rgba(255, 255, 255, 0.1)").Color(Hex("#ffffff")))
        .Is(".active", a => a.Background("rgba(255, 255, 255, 0.37)").Color(Hex("#ffffff")), state: true));
}
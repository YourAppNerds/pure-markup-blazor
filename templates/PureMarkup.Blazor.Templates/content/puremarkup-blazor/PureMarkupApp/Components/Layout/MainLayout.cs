#if (GlobalClient)
namespace PureMarkupApp.Client.Layout;
#else
namespace PureMarkupApp.Components.Layout;
#endif

public sealed class MainLayout : MarkupLayout
{
    protected override Node Render() => Fragment(
        Div().Class(MainLayoutCss.Page).Children(
            Div().Class(MainLayoutCss.Sidebar).Children(Comp<NavMenu>()),
            Main().Class(MainLayoutCss.Main).Children(
                Div().Class(MainLayoutCss.TopRow).Children(
                    A().Href("https://learn.microsoft.com/aspnet/core/").Target(Target.Blank).Text("About")),
                Article().Class(MainLayoutCss.Content).Children(Content(Body)))),
        // Shown by blazor.web.js on an unhandled error.
        Div().Id("blazor-error-ui").Class(MainLayoutCss.ErrorUi).Attr("data-nosnippet", true).Children(
            Text("An unhandled error has occurred. "),
            A().Href(".").Class("reload").Text("Reload"),
            Span().Class("dismiss").Text("🗙")));
}

/// <summary>MainLayout.razor.css in the stock template.</summary>
[Stylesheet]
public static class MainLayoutCss
{
    private static readonly Media Wide = Media.MinWidth(Px(641));

    public static Rule Page { get; } = Css.Rule(r => r
        .Position(Position.Relative)
        .Display(Display.Flex)
        .FlexDirection(FlexDirection.Column)
        .Media(Wide, m => m.FlexDirection(FlexDirection.Row)));

    public static Rule Main { get; } = Css.Rule(r => r.Flex(1));

    public static Rule Sidebar { get; } = Css.Rule(r => r
        .BackgroundImage("linear-gradient(180deg, rgb(5, 39, 103) 0%, #3a0647 70%)")
        .Media(Wide, m => m
            .Width(Px(250))
            .Height(Vh(100))
            .Position(Position.Sticky)
            .Top(0)));

    public static Rule TopRow { get; } = Css.Rule(r => r
        .Background(Tokens.TopRow)
        .BorderBottom(Px(1), "solid", Tokens.Border)
        .JustifyContent(JustifyContent.FlexEnd)
        .Height(Rem(3.5))
        .Display(Display.Flex)
        .AlignItems(AlignItems.Center)
        .Padding(0, Rem(1.5), 0, Rem(2))
        .Nest("& a", a => a.WhiteSpace(WhiteSpace.Nowrap).MarginLeft(Rem(1.5)).TextDecoration("none"))
        .Nest("& a:hover", a => a.TextDecoration("underline"))
        .Media(Wide, m => m.Position(Position.Sticky).Top(0).ZIndex(1)));

    public static Rule Content { get; } = Css.Rule(r => r
        .Padding(Rem(1.1), Rem(1.5), 0, Rem(2)));

    public static Rule ErrorUi { get; } = Css.Rule(r => r
        .Prop("color-scheme", "light only")
        .Background("lightyellow")
        .Color(Hex("#212529"))
        .Bottom(0)
        .BoxShadow("0 -1px 2px rgba(0, 0, 0, 0.2)")
        .BoxSizing(BoxSizing.BorderBox)
        .Display(Display.None)
        .Left(0)
        .Padding(Rem(0.6), Rem(1.25), Rem(0.7), Rem(1.25))
        .Position(Position.Fixed)
        .Width(Percent(100))
        .ZIndex(1000)
        .Nest("& .dismiss", d => d.Cursor("pointer").Position(Position.Absolute).Right(Rem(0.75)).Top(Rem(0.5))));
}
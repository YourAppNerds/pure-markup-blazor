using Microsoft.AspNetCore.Components.Routing;

namespace WebApp.Components.Layout;

public sealed class MainLayout : MarkupLayout
{
    private static readonly (string Href, string Text)[] Links =
    [
        ("", "Home (SSR form)"),
        ("counter-server", "Server"),
        ("counter-wasm", "WebAssembly"),
        ("counter-auto", "Auto"),
        ("list", "List"),
        ("grid", "Grid"),
        ("cascade", "Cascade"),
    ];

    protected override Node Render() => Fragment(
        Nav().Class(LayoutCss.Nav).Children(
            ForEach(Links, l => l.Href, l =>
                Comp<NavLink>()
                    .Attr("href", l.Href)
                    .Match(l.Href == "" ? NavLinkMatch.All : NavLinkMatch.Prefix)
                    .Children(l.Text))),
        Main().Class(LayoutCss.Main).Children(Content(Body)));
}

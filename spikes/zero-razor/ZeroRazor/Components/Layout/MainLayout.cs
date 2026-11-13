using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Routing;

namespace ZeroRazor.Components.Layout;

public sealed class MainLayout : LayoutComponentBase
{
    private static readonly (string Href, string Text)[] Links =
    [
        ("", "Home (SSR form)"),
        ("counter-server", "Server"),
        ("counter-wasm", "WebAssembly"),
        ("counter-auto", "Auto"),
    ];

    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        b.OpenElement(0, "nav");
        foreach (var (href, text) in Links)
        {
            b.OpenComponent<NavLink>(1);
            b.SetKey(href);
            b.AddAttribute(2, "href", href);
            b.AddAttribute(3, nameof(NavLink.Match), href == "" ? NavLinkMatch.All : NavLinkMatch.Prefix);
            b.AddAttribute(4, nameof(NavLink.ChildContent), (RenderFragment)(c => c.AddContent(0, text)));
            b.CloseComponent();
            b.AddContent(5, " | ");
        }
        b.CloseElement();

        b.OpenElement(6, "main");
        b.AddContent(7, Body);
        b.CloseElement();
    }
}

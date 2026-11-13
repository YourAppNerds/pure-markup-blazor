using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace ZeroRazor.Components;

// Replaces App.razor: the host page, written as a root component.
public sealed class App : ComponentBase
{
    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        b.AddMarkupContent(0, "<!DOCTYPE html>\n");
        b.OpenElement(1, "html");
        b.AddAttribute(2, "lang", "en");

        b.OpenElement(3, "head");
        b.OpenElement(4, "meta");
        b.AddAttribute(5, "charset", "utf-8");
        b.CloseElement();
        b.OpenElement(6, "meta");
        b.AddAttribute(7, "name", "viewport");
        b.AddAttribute(8, "content", "width=device-width, initial-scale=1.0");
        b.CloseElement();
        b.OpenElement(9, "base");
        b.AddAttribute(10, "href", "/");
        b.CloseElement();
        b.OpenComponent<ResourcePreloader>(11);
        b.CloseComponent();
        b.OpenComponent<ImportMap>(12);
        b.CloseComponent();
        b.OpenComponent<HeadOutlet>(13);
        b.CloseComponent();
        b.CloseElement(); // head

        b.OpenElement(14, "body");
        b.OpenComponent<Routes>(15);
        b.CloseComponent();
        b.OpenElement(16, "script");
        b.AddAttribute(17, "src", Assets["_framework/blazor.web.js"]);
        b.CloseElement();
        b.CloseElement(); // body

        b.CloseElement(); // html
    }
}

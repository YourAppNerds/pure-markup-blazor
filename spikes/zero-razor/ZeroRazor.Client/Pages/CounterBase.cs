using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace ZeroRazor.Client.Pages;

// Shared counter body. [Route] is not inherited, so each page declares its own.
public abstract class CounterBase : ComponentBase
{
    private int _count;
    protected abstract string Title { get; }

    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        b.OpenComponent<PageTitle>(0);
        b.AddAttribute(1, nameof(PageTitle.ChildContent), (RenderFragment)(t => t.AddContent(0, Title)));
        b.CloseComponent();

        b.OpenElement(2, "h1");
        b.AddContent(3, Title);
        b.CloseElement();

        b.OpenElement(4, "p");
        b.AddAttribute(5, "id", "renderer");
        b.AddContent(6, $"Renderer: {RendererInfo.Name}; interactive: {RendererInfo.IsInteractive}");
        b.CloseElement();

        b.OpenElement(7, "p");
        b.AddAttribute(8, "id", "count");
        b.AddContent(9, $"Current count: {_count}");
        b.CloseElement();

        b.OpenElement(10, "button");
        b.AddAttribute(11, "id", "inc");
        b.AddAttribute(12, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, () => _count++));
        b.AddContent(13, "Click me");
        b.CloseElement();
    }
}

using Microsoft.AspNetCore.Components.Rendering;
using PureMarkup.Blazor;

namespace Bench;

/// <summary>Exposes BuildRenderTree and StateHasChanged to the harness, as BenchBase does for the Razor pages.</summary>
public abstract class BenchMarkup : MarkupComponent
{
    public void BuildPublic(RenderTreeBuilder builder) => BuildRenderTree(builder);
    public void Rerender() => StateHasChanged();
}

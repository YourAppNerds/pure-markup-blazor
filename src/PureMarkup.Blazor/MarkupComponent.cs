using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using PureMarkup.Blazor.Rendering;

namespace PureMarkup.Blazor;

/// <summary>A component whose markup is a C# node tree returned by <see cref="Render"/>.</summary>
public abstract class MarkupComponent : ComponentBase
{
    /// <summary>Builds this render's markup. Runs on every render; the tree is written to the render tree and discarded.</summary>
    protected abstract Node Render();

    /// <inheritdoc/>
    protected sealed override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var a = RenderArena.Enter(this);
        try { MarkupRenderer.WriteRoot(a, builder, Render()); }
        finally { RenderArena.Exit(a); }
    }
}

/// <summary>A layout whose markup is a C# node tree. Place the page with <c>Content(Body)</c>.</summary>
public abstract class MarkupLayout : LayoutComponentBase
{
    /// <summary>Builds this render's markup.</summary>
    protected abstract Node Render();

    /// <inheritdoc/>
    protected sealed override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var a = RenderArena.Enter(this);
        try { MarkupRenderer.WriteRoot(a, builder, Render()); }
        finally { RenderArena.Exit(a); }
    }
}

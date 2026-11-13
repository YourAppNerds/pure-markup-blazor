using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace PureMarkup.Blazor;

// RenderModeAttribute is abstract and the framework ships no concrete subclass: Razor's `@rendermode X` on a type emits a
// private nested one. These are the public equivalents for components written in C#.

/// <summary>Razor's <c>@rendermode InteractiveServer</c> on a component class.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class InteractiveServerAttribute : RenderModeAttribute
{
    /// <inheritdoc/>
    public override IComponentRenderMode Mode => RenderMode.InteractiveServer;
}

/// <summary>Razor's <c>@rendermode InteractiveWebAssembly</c> on a component class.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class InteractiveWebAssemblyAttribute : RenderModeAttribute
{
    /// <inheritdoc/>
    public override IComponentRenderMode Mode => RenderMode.InteractiveWebAssembly;
}

/// <summary>Razor's <c>@rendermode InteractiveAuto</c> on a component class.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class InteractiveAutoAttribute : RenderModeAttribute
{
    /// <inheritdoc/>
    public override IComponentRenderMode Mode => RenderMode.InteractiveAuto;
}

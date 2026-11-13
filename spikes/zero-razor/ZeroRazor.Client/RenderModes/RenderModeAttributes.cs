using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace ZeroRazor.Client.RenderModes;

// RenderModeAttribute is abstract and the framework ships no concrete subclass.
// Razor's `@rendermode X` on a type emits a private nested subclass; these are the public equivalents.
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class InteractiveServerAttribute : RenderModeAttribute
{
    public override IComponentRenderMode Mode => RenderMode.InteractiveServer;
}

[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class InteractiveWebAssemblyAttribute : RenderModeAttribute
{
    public override IComponentRenderMode Mode => RenderMode.InteractiveWebAssembly;
}

[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class InteractiveAutoAttribute : RenderModeAttribute
{
    public override IComponentRenderMode Mode => RenderMode.InteractiveAuto;
}

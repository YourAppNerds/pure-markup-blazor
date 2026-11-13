using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Routing;
using ZeroRazor.Components.Layout;
using RouteData = Microsoft.AspNetCore.Components.RouteData;

namespace ZeroRazor.Components;

// Replaces Routes.razor.
public sealed class Routes : ComponentBase
{
    private static readonly Assembly[] Additional = [typeof(ZeroRazor.Client.Pages.CounterWasm).Assembly];

    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        b.OpenComponent<Router>(0);
        b.AddAttribute(1, nameof(Router.AppAssembly), typeof(Program).Assembly);
        b.AddAttribute(2, nameof(Router.AdditionalAssemblies), Additional);
        b.AddAttribute(3, nameof(Router.Found), (RenderFragment<RouteData>)(routeData => f =>
        {
            f.OpenComponent<RouteView>(0);
            f.AddAttribute(1, nameof(RouteView.RouteData), routeData);
            f.AddAttribute(2, nameof(RouteView.DefaultLayout), typeof(MainLayout));
            f.CloseComponent();
            f.OpenComponent<FocusOnNavigate>(3);
            f.AddAttribute(4, nameof(FocusOnNavigate.RouteData), routeData);
            f.AddAttribute(5, nameof(FocusOnNavigate.Selector), "h1");
            f.CloseComponent();
        }));
        b.CloseComponent();
    }
}

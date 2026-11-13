using System.Reflection;
using Microsoft.AspNetCore.Components.Routing;
using WebApp.Components.Layout;

namespace WebApp.Components;

/// <summary>Routes.razor in a template.</summary>
public sealed class Routes : MarkupComponent
{
    private static readonly Assembly[] Additional = [typeof(WebApp.Client.Pages.CounterWasm).Assembly];

    protected override Node Render() =>
        Comp<Router>()
            .Param(nameof(Router.AppAssembly), typeof(Program).Assembly)
            .Param(nameof(Router.AdditionalAssemblies), Additional)
            .Fragment<RouteData>(nameof(Router.Found), route => Fragment(
                Comp<RouteView>().Param(nameof(RouteView.RouteData), route).Param(nameof(RouteView.DefaultLayout), typeof(MainLayout)),
                Comp<FocusOnNavigate>().Param(nameof(FocusOnNavigate.RouteData), route).Param(nameof(FocusOnNavigate.Selector), "h1")));
}

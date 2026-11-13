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
            .AppAssembly(typeof(Program).Assembly)
            .AdditionalAssemblies(Additional)
            .Found(route => Fragment(
                Comp<RouteView>().RouteData(route).DefaultLayout(typeof(MainLayout)),
                Comp<FocusOnNavigate>().RouteData(route).Selector("h1")));
}

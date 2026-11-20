#if (UseWebAssembly)
using System.Reflection;
#endif
using Microsoft.AspNetCore.Components.Routing;
using PureMarkupApp.Components.Layout;
using PureMarkupApp.Components.Pages;

namespace PureMarkupApp.Components;

/// <summary>Routes.razor in the stock template.</summary>
public sealed class Routes : MarkupComponent
{
#if (UseWebAssembly)
    // Pages in the .Client project.
    private static readonly Assembly[] Additional = [typeof(Tokens).Assembly];

#endif
    protected override Node Render() =>
        Comp<Router>()
            .AppAssembly(typeof(Program).Assembly)
#if (UseWebAssembly)
            .AdditionalAssemblies(Additional)
#endif
            .NotFoundPage(typeof(NotFound))
            .Found(route => Fragment(
                Comp<RouteView>().RouteData(route).DefaultLayout(typeof(MainLayout)),
                Comp<FocusOnNavigate>().RouteData(route).Selector("h1")));
}
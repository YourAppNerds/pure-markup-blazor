#if (UseWebAssembly && !GlobalClient)
using System.Reflection;
#endif
using Microsoft.AspNetCore.Components.Routing;
#if (GlobalClient)
using PureMarkupApp.Client.Layout;
using PureMarkupApp.Client.Pages;

namespace PureMarkupApp.Client;
#else
using PureMarkupApp.Components.Layout;
using PureMarkupApp.Components.Pages;

namespace PureMarkupApp.Components;
#endif

#if (GlobalClient)
// In the .Client project: App renders it interactively (--all-interactive), so it, the layout and every page it routes
// to must load in the browser.
#endif
/// <summary>Routes.razor in the stock template.</summary>
public sealed class Routes : MarkupComponent
{
#if (UseWebAssembly && !GlobalClient)
    // Pages in the .Client project.
    private static readonly Assembly[] Additional = [typeof(Tokens).Assembly];

#endif
    protected override Node Render() =>
        Comp<Router>()
            .AppAssembly(typeof(Program).Assembly)
#if (UseWebAssembly && !GlobalClient)
            .AdditionalAssemblies(Additional)
#endif
            .NotFoundPage(typeof(NotFound))
            .Found(route => Fragment(
                Comp<RouteView>().RouteData(route).DefaultLayout(typeof(MainLayout)),
                Comp<FocusOnNavigate>().RouteData(route).Selector("h1")));
}
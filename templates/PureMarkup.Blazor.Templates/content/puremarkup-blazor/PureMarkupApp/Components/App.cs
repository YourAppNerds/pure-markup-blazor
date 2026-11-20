#if (GlobalClient)
using PureMarkupApp.Client;

#endif
namespace PureMarkupApp.Components;

/// <summary>The host page (App.razor in the stock template).</summary>
public sealed class App : MarkupComponent
{
#if (GlobalInteractive)
    // Global interactivity (--all-interactive): one render mode for the whole app, on Routes and HeadOutlet
    // (App.razor's @rendermode="..."), so pages carry no render-mode attribute of their own.
#if (Interactivity == "Server")
    private static readonly IComponentRenderMode Mode = RenderMode.InteractiveServer;
#elif (Interactivity == "WebAssembly")
    private static readonly IComponentRenderMode Mode = RenderMode.InteractiveWebAssembly;
#else
    private static readonly IComponentRenderMode Mode = RenderMode.InteractiveAuto;
#endif

#endif
    protected override Node Render() => Fragment(
        Doctype(),
        Html().Lang("en").Children(
            Head().Children(
                Meta().Charset("utf-8"),
                Meta().Name("viewport").Content("width=device-width, initial-scale=1.0"),
                Base().Href("/"),
                Comp<ResourcePreloader>(),
                // The generated stylesheets ({Assembly}.pm.css), dependencies first.
                StyleLink(),
                Comp<ImportMap>(),
#if (GlobalInteractive)
                Comp<HeadOutlet>().RenderMode(Mode)),
            Body().Children(
                Comp<Routes>().RenderMode(Mode),
#else
                Comp<HeadOutlet>()),
            Body().Children(
                Comp<Routes>(),
#endif
                Script().Src(Assets["_framework/blazor.web.js"]))));
}
namespace PureMarkupApp.Components;

/// <summary>The host page (App.razor in the stock template).</summary>
public sealed class App : MarkupComponent
{
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
                Comp<HeadOutlet>()),
            Body().Children(
                Comp<Routes>(),
                Script().Src(Assets["_framework/blazor.web.js"]))));
}
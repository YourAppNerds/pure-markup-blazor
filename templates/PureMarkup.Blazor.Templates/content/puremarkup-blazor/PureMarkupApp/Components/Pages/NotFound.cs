#if (GlobalClient)
using PureMarkupApp.Client.Layout;

namespace PureMarkupApp.Client.Pages;
#else
using PureMarkupApp.Components.Layout;

namespace PureMarkupApp.Components.Pages;
#endif

[Route("/not-found")]
[Layout(typeof(MainLayout))]
public sealed class NotFound : MarkupComponent
{
    protected override Node Render() => Fragment(
        H3().Text("Not Found"),
        P().Text("Sorry, the content you are looking for does not exist."));
}
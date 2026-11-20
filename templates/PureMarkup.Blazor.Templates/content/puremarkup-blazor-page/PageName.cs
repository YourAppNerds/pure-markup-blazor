namespace PureMarkupApp.Pages;

[Route("/route-token")]
#if (Interactivity == "Server")
[InteractiveServer]
#elif (Interactivity == "WebAssembly")
[InteractiveWebAssembly]
#elif (Interactivity == "Auto")
[InteractiveAuto]
#endif
public sealed class PageName : MarkupComponent
{
    protected override Node Render() => Fragment(
        Comp<PageTitle>().Children("PageName"),
        H1().Text("PageName"));
}
#if (UseWebAssembly)
namespace PureMarkupApp.Client.Pages;
#else
namespace PureMarkupApp.Components.Pages;
#endif

[Route("/counter")]
#if (GlobalInteractive)
// No render mode of its own: App renders Routes interactively (--all-interactive).
#elif (Interactivity == "Server")
[InteractiveServer]
#elif (Interactivity == "WebAssembly")
[InteractiveWebAssembly]
#elif (Interactivity == "Auto")
[InteractiveAuto]
#endif
public sealed class Counter : MarkupComponent
{
    private int currentCount;

    private void IncrementCount() => currentCount++;

    protected override Node Render() => Fragment(
        Comp<PageTitle>().Children("Counter"),
        H1().Text("Counter"),
        P().Role("status").Text($"Current count: {currentCount}"),
        Button().Use(AppPresets.PrimaryButton).OnClick(IncrementCount).Text("Click me"));
}
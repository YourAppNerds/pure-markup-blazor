namespace PureMarkupApp.Components.Pages;

[Route("/counter")]
[InteractiveServer]
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
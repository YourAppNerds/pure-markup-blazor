namespace PureMarkupApp.Components.Pages;

[Route("/")]
public sealed class Home : MarkupComponent
{
    protected override Node Render() => Fragment(
        Comp<PageTitle>().Children("Home"),
        H1().Text("Hello, world!"),
        Text("Welcome to your new app."));
}
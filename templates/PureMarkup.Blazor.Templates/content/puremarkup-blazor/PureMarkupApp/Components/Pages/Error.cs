using System.Diagnostics;

namespace PureMarkupApp.Components.Pages;

[Route("/Error")]
public sealed class Error : MarkupComponent
{
    [CascadingParameter]
    private HttpContext? HttpContext { get; set; }

    private string? RequestId { get; set; }
    private bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    protected override void OnInitialized() =>
        RequestId = Activity.Current?.Id ?? HttpContext?.TraceIdentifier;

    protected override Node Render() => Fragment(
        Comp<PageTitle>().Children("Error"),
        H1().Class(ErrorCss.Danger).Text("Error."),
        H2().Class(ErrorCss.Danger).Text("An error occurred while processing your request."),
        If(ShowRequestId, () => P().Children(
            Strong().Text("Request ID:"), Text(" "), Code().Text(RequestId))),
        H3().Text("Development Mode"),
        P().Children(
            Text("Swapping to "), Strong().Text("Development"),
            Text(" environment will display more detailed information about the error that occurred.")),
        P().Children(
            Strong().Text("The Development environment shouldn't be enabled for deployed applications."),
            Text(" It can result in displaying sensitive information from exceptions to end users. For local debugging, enable the "),
            Strong().Text("Development"), Text(" environment by setting the "), Strong().Text("ASPNETCORE_ENVIRONMENT"),
            Text(" environment variable to "), Strong().Text("Development"), Text(" and restarting the app.")));
}

[Stylesheet]
public static class ErrorCss
{
    public static Rule Danger { get; } = Css.Rule(r => r.Color(Hex("#dc3545")));
}
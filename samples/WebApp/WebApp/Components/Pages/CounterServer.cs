using WebApp.Client.Pages;

namespace WebApp.Components.Pages;

[Route("/counter-server")]
[InteractiveServer]
public sealed class CounterServer : CounterBase
{
    protected override string Title => "Counter (InteractiveServer)";
}

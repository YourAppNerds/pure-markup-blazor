using Microsoft.AspNetCore.Components;
using ZeroRazor.Client.Pages;
using ZeroRazor.Client.RenderModes;

namespace ZeroRazor.Components.Pages;

[Route("/counter-server")]
[InteractiveServer]
public sealed class CounterServer : CounterBase
{
    protected override string Title => "Counter (InteractiveServer)";
}

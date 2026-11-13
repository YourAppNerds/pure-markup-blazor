using Microsoft.AspNetCore.Components;
using ZeroRazor.Client.RenderModes;

namespace ZeroRazor.Client.Pages;

[Route("/counter-auto")]
[InteractiveAuto]
public sealed class CounterAuto : CounterBase
{
    protected override string Title => "Counter (InteractiveAuto)";
}

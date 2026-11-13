using Microsoft.AspNetCore.Components;
using ZeroRazor.Client.RenderModes;

namespace ZeroRazor.Client.Pages;

[Route("/counter-wasm")]
[InteractiveWebAssembly]
public sealed class CounterWasm : CounterBase
{
    protected override string Title => "Counter (InteractiveWebAssembly)";
}

using WebApp.Client.Theme;

namespace WebApp.Client.Pages;

/// <summary>Shared counter body. [Route] is not inherited, so each page declares its own.</summary>
public abstract class CounterBase : MarkupComponent
{
    private int _count;
    protected abstract string Title { get; }

    protected override Node Render() => Fragment(
        Comp<PageTitle>().Children(Title),
        H1().Text(Title),
        P().Id("renderer").Text($"Renderer: {RendererInfo.Name}; interactive: {RendererInfo.IsInteractive}"),
        Div().Id("bar").Class(CounterCss.Bar).Set(CounterCss.Fill, Percent(Math.Min(_count * 10, 100))),
        P().Id("count").Class(CounterCss.Count).Text($"Current count: {_count}"),
        Button().Id("inc").Use(AppPresets.PrimaryButton).OnClick(() => _count++).Text("Click me"));
}

[Route("/counter-wasm")]
[InteractiveWebAssembly]
public sealed class CounterWasm : CounterBase
{
    protected override string Title => "Counter (InteractiveWebAssembly)";
}

[Route("/counter-auto")]
[InteractiveAuto]
public sealed class CounterAuto : CounterBase
{
    protected override string Title => "Counter (InteractiveAuto)";
}

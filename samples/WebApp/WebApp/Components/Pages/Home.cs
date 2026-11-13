using Microsoft.AspNetCore.Components.Forms;
using WebApp.Client.Theme;

namespace WebApp.Components.Pages;

/// <summary>Static SSR page with a posted EditForm.</summary>
[Route("/")]
public sealed class Home : MarkupComponent
{
    [SupplyParameterFromForm]
    public GreetModel? Model { get; set; }

    private string? _greeting;

    protected override void OnInitialized() => Model ??= new();

    private void Submit() => _greeting = $"Hello, {Model!.Name}!";

    protected override Node Render() => Fragment(
        Comp<PageTitle>().Children("Home"),
        H1().Text("Home (static SSR)"),
        P().Id("renderer").Text($"Renderer: {RendererInfo.Name}; interactive: {RendererInfo.IsInteractive}"),
        Comp<EditForm>()
            .Model(Model)
            .FormName("greet")
            .OnValidSubmit(Submit)
            .ChildContent(_ => Fragment(
                Comp<InputText>().Use(AppPresets.Field).Attr("id", "name").Bind(() => Model!.Name),
                Button().Id("submit").Use(AppPresets.SubmitButton).Text("Greet"))),
        If(_greeting is not null, () => P().Id("greeting").Text(_greeting)));

    public sealed class GreetModel
    {
        public string? Name { get; set; }
    }
}

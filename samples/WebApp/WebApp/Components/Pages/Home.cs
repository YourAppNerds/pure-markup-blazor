using Microsoft.AspNetCore.Components.Forms;

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
            .Param(nameof(EditForm.Model), Model)
            .Param(nameof(EditForm.FormName), "greet")
            .Callback<EditContext>(nameof(EditForm.OnValidSubmit), Submit)
            .ChildContent<EditContext>(_ => Fragment(
                Comp<InputText>().Param("id", "name").Bind(() => Model!.Name),
                Button().Id("submit").Type(ButtonType.Submit).Text("Greet"))),
        If(_greeting is not null, () => P().Id("greeting").Text(_greeting)));

    public sealed class GreetModel
    {
        public string? Name { get; set; }
    }
}

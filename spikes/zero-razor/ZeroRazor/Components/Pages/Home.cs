using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace ZeroRazor.Components.Pages;

// Static SSR page (no render mode) with an enhanced-less plain POST form.
[Route("/")]
public sealed class Home : ComponentBase
{
    [SupplyParameterFromForm]
    public GreetModel? Model { get; set; }

    private string? _greeting;

    protected override void OnInitialized() => Model ??= new();

    private void Submit() => _greeting = $"Hello, {Model!.Name}!";

    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        b.OpenComponent<PageTitle>(0);
        b.AddAttribute(1, nameof(PageTitle.ChildContent), (RenderFragment)(t => t.AddContent(0, "Home")));
        b.CloseComponent();

        b.OpenElement(2, "h1");
        b.AddContent(3, "Home (static SSR)");
        b.CloseElement();

        b.OpenElement(4, "p");
        b.AddAttribute(5, "id", "renderer");
        b.AddContent(6, $"Renderer: {RendererInfo.Name}; interactive: {RendererInfo.IsInteractive}");
        b.CloseElement();

        b.OpenComponent<EditForm>(7);
        b.AddAttribute(8, nameof(EditForm.Model), Model);
        b.AddAttribute(9, nameof(EditForm.FormName), "greet");
        b.AddAttribute(10, nameof(EditForm.OnValidSubmit), EventCallback.Factory.Create<EditContext>(this, Submit));
        b.AddAttribute(11, nameof(EditForm.ChildContent), (RenderFragment<EditContext>)(_ => f =>
        {
            f.OpenComponent<InputText>(0);
            f.AddAttribute(1, "id", "name");
            f.AddAttribute(2, nameof(InputText.Value), Model!.Name);
            f.AddAttribute(3, nameof(InputText.ValueChanged),
                EventCallback.Factory.Create<string?>(this, v => Model!.Name = v));
            f.AddAttribute(4, nameof(InputText.ValueExpression),
                (System.Linq.Expressions.Expression<Func<string?>>)(() => Model!.Name));
            f.CloseComponent();

            f.OpenElement(5, "button");
            f.AddAttribute(6, "id", "submit");
            f.AddAttribute(7, "type", "submit");
            f.AddContent(8, "Greet");
            f.CloseElement();
        }));
        b.CloseComponent();

        if (_greeting is not null)
        {
            b.OpenElement(12, "p");
            b.AddAttribute(13, "id", "greeting");
            b.AddContent(14, _greeting);
            b.CloseElement();
        }
    }

    public sealed class GreetModel
    {
        public string? Name { get; set; }
    }
}

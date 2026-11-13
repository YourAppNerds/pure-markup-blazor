using Microsoft.AspNetCore.Components.Forms;

namespace WebApp.Components.Pages;

/// <summary>50 keyed rows, each with an InputText bound through the .Bind interceptor (<c>() =&gt; row.Name</c>).</summary>
[Route("/list")]
[InteractiveServer]
public sealed class ListPage : MarkupComponent
{
    private readonly List<Row> _rows = [.. Enumerable.Range(1, 50).Select(i => new Row { Id = i, Name = $"Item {i}" })];

    protected override Node Render() => Fragment(
        Comp<PageTitle>().Children("List"),
        H1().Text("List (InteractiveServer)"),
        P().Id("renderer").Text($"Renderer: {RendererInfo.Name}; interactive: {RendererInfo.IsInteractive}"),
        P().Id("summary").Text(string.Join(", ", _rows.Take(3).Select(r => r.Name))),
        Comp<EditForm>().Model(_rows).ChildContent(_ =>
            Ul().Id("rows").Children(
                ForEach(_rows, r => r.Id, row =>
                    Li().Class(row.Id % 2 == 0 ? "even" : "odd").Children(
                        Span().Class("id").Text(row.Id),
                        " ",
                        Comp<InputText>().Attr("id", $"name-{row.Id}").Bind(() => row.Name))))));

    public sealed class Row
    {
        public int Id { get; init; }
        public string? Name { get; set; }
    }
}

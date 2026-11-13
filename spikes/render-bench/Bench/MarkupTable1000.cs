using Baseline;
using PureMarkup.Prototype;
using static PureMarkup.Prototype.H;

namespace Bench;

public sealed class MarkupTable1000 : MarkupComponent, IBenchTarget
{
    public int SelectedId { get; set; } = 10;
    public void Select(int id) => SelectedId = id;
    public void Toggle() => SelectedId = SelectedId == 10 ? 20 : 10;

    protected override Node Render() =>
        Table().Class("table").Children(
            Thead().Children(Tr().Children(Th().Text("Id"), Th().Text("Name"), Th().Text("Email"), Th().Text("Score"))),
            Tbody().Children(
                ForEach(BenchData.Rows, r => r.Id, r =>
                    Tr().Class(r.Id == SelectedId ? "selected" : null).Attr("data-id", r.Id).OnClick(() => Select(r.Id)).Children(
                        Td().Text(r.Id),
                        Td().Text(r.Name),
                        Td().Children(A().Attr("href", "mailto:" + r.Email).Text(r.Email)),
                        Td().Class("num").Text(r.Score)))));
}

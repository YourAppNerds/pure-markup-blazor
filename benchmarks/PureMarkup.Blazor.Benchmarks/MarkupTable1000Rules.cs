using Baseline;
using PureMarkup.Blazor;
using PureMarkup.Blazor.Styling;
using static PureMarkup.Blazor.H;

namespace Bench;

[Stylesheet]
public static class BenchCss
{
    public static Rule Table { get; } = Css.Rule(r => r.BorderCollapse(BorderCollapse.Collapse));
    public static Rule Selected { get; } = Css.Rule(r => r.Background(CssUnits.Hex("#eef")));
    public static Rule Num { get; } = Css.Rule(r => r.TextAlign(TextAlign.Right));
}

/// <summary>MarkupTable1000 with build-time CSS rules instead of class strings: the render cost should be the same.</summary>
public sealed class MarkupTable1000Rules : BenchMarkup, IBenchTarget
{
    public int SelectedId { get; set; } = 10;
    public void Select(int id) => SelectedId = id;
    public void Toggle() => SelectedId = SelectedId == 10 ? 20 : 10;

    protected override Node Render() =>
        Table().Class(BenchCss.Table).Children(
            Thead().Children(Tr().Children(Th().Text("Id"), Th().Text("Name"), Th().Text("Email"), Th().Text("Score"))),
            Tbody().Children(
                ForEach(BenchData.Rows, r => r.Id, r =>
                    Tr().Class(BenchCss.Selected, r.Id == SelectedId).Attr("data-id", r.Id).OnClick(() => Select(r.Id)).Children(
                        Td().Text(r.Id),
                        Td().Text(r.Name),
                        Td().Children(A().Attr("href", "mailto:" + r.Email).Text(r.Email)),
                        Td().Class(BenchCss.Num).Text(r.Score)))));
}

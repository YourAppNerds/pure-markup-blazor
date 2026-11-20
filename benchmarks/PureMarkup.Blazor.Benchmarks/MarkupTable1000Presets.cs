using Baseline;
using PureMarkup.Blazor;
using static PureMarkup.Blazor.H;

namespace Bench;

public static class BenchPresets
{
    public static Preset<TdElement> Num => Preset.Once<TdElement>(p => p.Class(BenchCss.Num));
    public static Preset<AElement> Mail => Preset.Once<AElement>(p => p.Attr("rel", "nofollow"));
}

/// <summary>MarkupTable1000Rules with presets on the score cell and the mail link (2,000 .Use per render).</summary>
public sealed class MarkupTable1000Presets : BenchMarkup, IBenchTarget
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
                        Td().Children(A().Use(BenchPresets.Mail).Attr("href", "mailto:" + r.Email).Text(r.Email)),
                        Td().Use(BenchPresets.Num).Text(r.Score)))));
}

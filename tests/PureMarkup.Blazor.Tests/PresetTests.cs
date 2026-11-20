using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using System.Reflection;
using PureMarkup.Blazor.Rendering;

namespace PureMarkup.Blazor.Tests;

public static class TestPresets
{
    public static int PresetClicks;
    public static int AlsoRuns;

    public static Preset<ButtonElement> Primary => Preset.Once<ButtonElement>(p => p
        .Class("btn").Class(TestCss.Badge).Type(ButtonType.Button).Title("primary")
        .Style(s => s.MarginTop(Px(4)).Opacity(1)));

    public static Preset<ButtonElement> Danger => Primary.With(p => p.Class("danger").Type(ButtonType.Reset));

    public static Preset<ButtonElement> Replacing => Primary.With(p => p.ClassReplace("only"));

    public static Preset<ButtonElement> Clicky => Preset.Once<ButtonElement>(p => p.OnClick(() => PresetClicks++).Text("preset text"));

    public static Preset<ButtonElement> Small => Preset.Once<ButtonElement>(p => p.Class("small"));

    public static Preset<ButtonElement> Counted => Preset.Once<ButtonElement>(p => p.Class("counted").Also(b => b.Data("run", ++AlsoRuns)));

    public static Preset<Element> Any => Preset.Once<Element>(p => p.Class("any").Title("t"));

    public static Preset<ComponentNode<Badge>> BigBadge => Preset.Once<ComponentNode<Badge>>(p => p.Label("P").Size(1).Class("p").Class(TestCss.Badge));

    public static Preset<ComponentNode<InputText>> Field => Preset.Once<ComponentNode<InputText>>(p => p.Class("field").Attr("placeholder", "Name"));
}

/// <summary>A local component with value parameters and captured attributes (so it gets an additive <c>.Class</c>).</summary>
public sealed class Badge : MarkupComponent
{
    [Parameter] public string? Label { get; set; }
    [Parameter] public int Size { get; set; }
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? Rest { get; set; }

    protected override Node Render() => Span().Attrs(Rest).Text($"{Label}:{Size}");
}

/// <summary>Renders whatever <see cref="Build"/> returns.</summary>
public sealed class Host : MarkupComponent
{
    [Parameter] public Func<Node>? Build { get; set; }
    protected override Node Render() => Build!();
}

public sealed class Toggle : MarkupComponent
{
    public bool UseDanger;
    public void Rerender() => StateHasChanged();

    protected override Node Render() => Div().Children(
        Button().Id("first").Use(UseDanger ? TestPresets.Danger : TestPresets.Small).Type(ButtonType.Submit).Text("go"),
        Span().Id("after").Text("sibling"));
}

public sealed class Clicks : MarkupComponent
{
    public int Local;
    protected override Node Render() => Fragment(
        Button().Use(TestPresets.Clicky).OnClick(() => Local++),
        Button().Use(TestPresets.Clicky));
}

public sealed class FieldForm : MarkupComponent
{
    public Model M { get; } = new() { Name = "Ann" };
    protected override Node Render() => Comp<EditForm>().Model(M).ChildContent(_ =>
        Comp<InputText>().Use(TestPresets.Field).Bind(() => M.Name).Attr("id", "name"));
}

public class PresetTests
{
    private static async Task<string> Html(Func<Node> build) =>
        HtmlOut.Normalize(await HtmlOut.RenderAsync<Host>(new Dictionary<string, object?> { ["Build"] = build }));

    private static string Badge => TestCss.Badge.ClassName;
    private static string BadgeP => TestCss.Badge.PresetClassName; // a rule applied in a preset writes its preset-origin class

    [Fact]
    public async Task Scalar_LocalWins_InEitherOrder()
    {
        // The preset's writes go at the .Use position; what the node sets itself replaces them.
        Assert.Equal($"<button class=\"btn {BadgeP}\" style=\"margin-top: 4px; opacity: 1;\" title=\"primary\" type=\"submit\">x</button>",
            await Html(() => Button().Use(TestPresets.Primary).Type(ButtonType.Submit).Text("x")));
        Assert.Equal($"<button type=\"submit\" class=\"btn {BadgeP}\" style=\"margin-top: 4px; opacity: 1;\" title=\"primary\">x</button>",
            await Html(() => Button().Type(ButtonType.Submit).Use(TestPresets.Primary).Text("x")));
    }

    [Fact]
    public async Task NullLocalAttribute_StillBeatsThePreset()
    {
        var html = await Html(() => Button().Title(null).Use(TestPresets.Primary));
        Assert.DoesNotContain("title", html);
        Assert.DoesNotContain("title", await Html(() => Button().Use(TestPresets.Primary).Title(null)));
    }

    [Fact]
    public async Task Class_IsAdditive_StringsAndRules_EitherOrder()
    {
        Assert.Contains($"class=\"btn {BadgeP} wide {Badge}\"", await Html(() => Button().Use(TestPresets.Primary).Class("wide").Class(TestCss.Badge)));
        Assert.Contains($"class=\"btn {BadgeP} wide\"", await Html(() => Button().Class("wide").Use(TestPresets.Primary)));
    }

    [Fact]
    public async Task ClassReplace_DropsThePresetsClasses_KeepsLocalOnes()
    {
        Assert.Contains("class=\"wide only\"", await Html(() => Button().Class("wide").Use(TestPresets.Primary).ClassReplace("only")));
        Assert.Contains($"class=\"{Badge}\"", await Html(() => Button().ClassReplace(TestCss.Badge).Use(TestPresets.Primary)));
        // Inside a derived preset it drops the base's classes.
        Assert.Contains("class=\"only\"", await Html(() => Button().Use(TestPresets.Replacing)));
    }

    [Fact]
    public async Task Style_MergesByProperty_LocalWins()
    {
        Assert.Contains("style=\"opacity: 1; margin-top: 0;\"", await Html(() => Button().Style(s => s.MarginTop(0)).Use(TestPresets.Primary)));
        Assert.Contains("style=\"opacity: 1; margin-top: 0;\"", await Html(() => Button().Use(TestPresets.Primary).Style(s => s.MarginTop(0))));
    }

    [Fact]
    public void Events_LocalReplacesThePresets()
    {
        TestPresets.PresetClicks = 0;
        var r = new TestRenderer();
        var c = new Clicks();
        var id = r.Render(c);
        r.Fire(id, "onclick", new MouseEventArgs());
        Assert.Equal((1, 0), (c.Local, TestPresets.PresetClicks));
        r.Fire(id, "onclick", new MouseEventArgs(), nth: 1); // the second button has only the preset's handler
        Assert.Equal((1, 1), (c.Local, TestPresets.PresetClicks));
    }

    [Fact]
    public async Task Children_LocalReplacesThePresets()
    {
        Assert.Equal("<button>preset text</button><button>mine</button>",
            await Html(() => Fragment(Button().Use(TestPresets.Clicky), Button().Use(TestPresets.Clicky).Text("mine"))));
    }

    [Fact]
    public async Task With_BaseFirst_DerivedOverrides_New()
    {
        Assert.Equal($"<button class=\"btn {BadgeP} danger\" style=\"margin-top: 4px; opacity: 1;\" title=\"primary\" type=\"reset\">Delete</button>",
            await Html(() => TestPresets.Danger.New().Text("Delete")));
        Assert.Same(TestPresets.Danger, TestPresets.Danger);
    }

    [Fact]
    public async Task LaterUse_ReplacesEarlier()
    {
        Assert.Equal("<button class=\"small\">x</button>", await Html(() => Button().Use(TestPresets.Primary).Use(TestPresets.Small).Text("x")));
    }

    [Fact]
    public async Task ElementPreset_AppliesToAnyElement()
    {
        Assert.Equal("<div class=\"any\" title=\"t\"></div><span title=\"t\" class=\"any x\"></span>",
            await Html(() => Fragment(Div().Use(TestPresets.Any), Span().Use(TestPresets.Any).Class("x"))));
    }

    [Fact]
    public async Task Also_RunsAtEveryUse_AsLocalWrites()
    {
        var before = TestPresets.AlsoRuns;
        var html = await Html(() => Fragment(Button().Use(TestPresets.Counted), Button().Use(TestPresets.Counted)));
        Assert.Equal($"<button class=\"counted\" data-run=\"{before + 1}\"></button><button class=\"counted\" data-run=\"{before + 2}\"></button>", html);
    }

    [Fact]
    public async Task ComponentParameters_Merge_LocalWins_ClassesAdd()
    {
        Assert.Equal($"<span class=\"p {BadgeP} l\">P:2</span>", await Html(() => Comp<Badge>().Use(TestPresets.BigBadge).Size(2).Class("l")));
        Assert.Equal($"<span class=\"l\">Q:1</span>", await Html(() => Comp<Badge>().Label("Q").ClassReplace("l").Use(TestPresets.BigBadge)));
        Assert.Equal($"<span class=\"p {BadgeP}\">P:1</span>", await Html(() => TestPresets.BigBadge.New()));
    }

    [Fact]
    public async Task InputTextPreset_WithBind()
    {
        var html = HtmlOut.Normalize(await HtmlOut.RenderAsync<FieldForm>());
        Assert.Contains("class=\"field valid\"", html); // InputText adds its validation class to ours
        Assert.Contains("placeholder=\"Name\"", html);
        Assert.Contains("value=\"Ann\"", html);
    }

    [Fact]
    public void ApplyingAPreset_DoesNotShiftSlots_AndRerendersStably()
    {
        var r = new TestRenderer();
        var c = new Toggle();
        var id = r.Render(c);
        var before = Seqs(r, id);
        r.Dispatcher.InvokeAsync(c.Rerender);
        Assert.Equal(0, r.LastEdits); // same preset: nothing changes

        c.UseDanger = true; // a preset with more writes
        r.Dispatcher.InvokeAsync(c.Rerender);
        var after = Seqs(r, id);
        Assert.Equal(before["id"], after["id"]);
        Assert.Equal(before["type"], after["type"]);
        Assert.Equal(before["span"], after["span"]);
        Assert.All(r.LastEditTypes, t => Assert.True(t is RenderTreeEditType.SetAttribute or RenderTreeEditType.RemoveAttribute
            or RenderTreeEditType.StepIn or RenderTreeEditType.StepOut, t.ToString())); // attributes only: no element replaced

        static Dictionary<string, int> Seqs(TestRenderer r, int id)
        {
            var d = new Dictionary<string, int>();
            var frames = r.Frames(id);
            for (var i = 0; i < frames.Count; i++)
            {
                ref var f = ref frames.Array[i];
                if (f.FrameType == RenderTreeFrameType.Attribute && f.AttributeName is "id" or "type" && !d.ContainsKey(f.AttributeName)) d[f.AttributeName] = f.Sequence;
                if (f.FrameType == RenderTreeFrameType.Element && f.ElementName == "span") d["span"] = f.Sequence;
            }
            return d;
        }
    }

    [Fact]
    public void Static_HoldsPresets()
    {
        var html = StaticHtml.Render(static () => Div().Children(Button().Use(TestPresets.Danger).Type(ButtonType.Submit).Style(s => s.Opacity(0.5))));
        Assert.Equal($"<div><button class=\"btn {BadgeP} danger\" title=\"primary\" type=\"submit\" style=\"margin-top: 4px; opacity: 0.5;\"></button></div>", html);
    }

    [Fact]
    public void PresetRules_AreRecordedApart_ForThePresetLayer()
    {
        var data = TestPresets.Danger.Data;
        Assert.Equal([TestCss.Badge], data.Rules);
        Assert.Equal($"btn {BadgeP} danger", data.Class);
        Assert.Empty(TestPresets.Replacing.Data.Rules);
    }

    [Fact]
    public void HotReload_DropsPresetsAndTheirRecordedWrites()
    {
        var getter = TestPresets.Danger;
        var data = getter.Data;
        var kept = Preset.Once<ButtonElement>(static p => p.Class("kept")); // as a static field would hold it
        var keptData = kept.Data;
        typeof(Preset).Assembly.GetType("PureMarkup.Blazor.PresetHotReload")!.GetMethod("ClearCache")!.Invoke(null, [null]);
        Assert.NotSame(getter, TestPresets.Danger);
        Assert.NotSame(data, TestPresets.Danger.Data);
        Assert.NotSame(keptData, kept.Data);
        Assert.Equal(data.Class, TestPresets.Danger.Data.Class);
        Assert.Contains(typeof(Preset).Assembly.GetCustomAttributes<System.Reflection.Metadata.MetadataUpdateHandlerAttribute>(),
            a => a.HandlerType.Name == "PresetHotReload");
    }

    [Fact]
    public void Misuse_Throws()
    {
        var local = "x";
        Assert.Throws<ArgumentException>(() => Preset.Once<ButtonElement>(p => p.Class(local)));
        Assert.Throws<ArgumentException>(() => Preset.Once<Node>(p => p));
        var key = Preset.Once<ButtonElement>(static p => p.Key(1));
        Assert.Throws<InvalidOperationException>(() => key.Data);
        var other = Preset.Once<ButtonElement>(static p => Button());
        Assert.Throws<InvalidOperationException>(() => other.Data);
        var bind = Preset.Once<InputElement>(static p => p.Bind(() => "", _ => { }));
        Assert.Contains("Bind", Assert.Throws<InvalidOperationException>(() => bind.Data).Message);
    }
}

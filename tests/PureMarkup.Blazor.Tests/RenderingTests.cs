using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;

namespace PureMarkup.Blazor.Tests;

public sealed class Basic : MarkupComponent
{
    protected override Node Render() =>
        Div().Class("box").Id("b1").Children(
            H1().Text("Title & <more>"),
            P().Children("Hello, ", Strong().Text("world"), "!"),
            Input().Type(InputType.Checkbox).Checked().Disabled(false).Name("agree"),
            A().Href("/x").Target(Target.Blank).Text("link"),
            Img().Src("/a.png").Alt("A \"quoted\" alt"),
            Ul().Children(Li().Text(1), Li().Text(2)),
            Span().Attr("data-x", (string?)null).Data("id", 7).AriaLabel("lbl"));
}

public sealed class WithDoctype : MarkupComponent
{
    protected override Node Render() => Fragment(Doctype(), Html().Lang("en").Children(Body().Children(Markup("<b>raw</b>"))));
}

/// <summary>Toggles an If in front of many siblings: with permanent slots only the toggled element changes.</summary>
public sealed class IfBeforeSiblings : MarkupComponent
{
    public bool Show = true;
    public int Count = 50;
    public void Rerender() => StateHasChanged();

    protected override Node Render() =>
        Div().Children(
            If(Show, () => P().Class("banner").Text("Banner")),
            Siblings());

    private Node Siblings()
    {
        // Built as a fragment of fixed children (one slot), followed by more fixed slots.
        return Fragment(Span().Text("a"), Span().Text("b"), Span().Text("c"), ForEach(Enumerable.Range(0, Count), i => i, i => Span().Text(i)));
    }
}

public sealed class Keyed : MarkupComponent
{
    public List<int> Items = [1, 2, 3, 4];
    public void Rerender() => StateHasChanged();
    protected override Node Render() => Ul().Children(ForEach(Items, i => i, i => Li().Text(i)));
}

public sealed class Counter : MarkupComponent
{
    public int Count;
    public string? LastKey;
    /// <summary>The async handler waits on this; the test completes it.</summary>
    public TaskCompletionSource Gate = new();
    protected override Node Render() => Fragment(
        P().Text($"Count: {Count}"),
        Button().Type(ButtonType.Button).OnClick(() => Count++).Text("+"),
        Button().OnClick(async (MouseEventArgs e) => { await Gate.Task; Count += 10; }).Text("+10"),
        Input().OnKeyDown((KeyboardEventArgs e) => LastKey = e.Key).PreventDefault("onkeydown").StopPropagation("onkeydown"));
}

public sealed class Card : MarkupComponent
{
    [Parameter] public string? Title { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public RenderFragment<int>? Row { get; set; }
    [Parameter] public EventCallback<string> OnPick { get; set; }

    protected override Node Render() => Section().Class("card").Children(
        H2().Text(Title),
        Content(ChildContent),
        If(Row is not null, () => Fragment(Content(Row, 1), Content(Row, 2))),
        Button().OnClick(() => OnPick.InvokeAsync("picked")).Text("pick"));
}

public sealed class UsesCard : MarkupComponent
{
    public string? Picked;
    protected override Node Render() =>
        Comp<Card>().Param("Title", "T").Children(() => P().Text("inner"))
            .Fragment<int>("Row", i => Em().Text($"row {i}"))
            .Callback<string>("OnPick", s => Picked = s);
}

public sealed class TextChild : MarkupComponent
{
    protected override Node Render() => Comp<Card>().Param("Title", "X").Children("plain & text");
}

/// <summary>Razor's <c>&lt;Card @rendermode="..." /&gt;</c>: set before and after parameters, null, and absent.</summary>
public sealed class WithRenderModes : MarkupComponent
{
    protected override Node Render() => Fragment(
        Comp<Card>().RenderMode(RenderMode.InteractiveServer).Param("Title", "A"),
        Comp<Card>().Param("Title", "B").RenderMode(RenderMode.InteractiveAuto),
        Comp<Card>().Param("Title", "C").RenderMode(null),
        Comp<Card>().Param("Title", "D"));
}


public sealed class StaticPageStatic : MarkupComponent
{
    protected override Node Render() => Main().Children(
        Static(static () => Nav().Class("n").Children(A().Href("/a?x=1&y=2").Text("A & B"), Br(), Input().Disabled())),
        P().Text("dynamic"));
}

public sealed class StaticPagePlain : MarkupComponent
{
    protected override Node Render() => Main().Children(
        Nav().Class("n").Children(A().Href("/a?x=1&y=2").Text("A & B"), Br(), Input().Disabled()),
        P().Text("dynamic"));
}

public sealed class CapturingStatic : MarkupComponent
{
    private readonly string _x = "x";
    protected override Node Render() => Static(() => P().Text(_x));
}

public sealed class Stale : MarkupComponent
{
    public static Node Kept;
    public bool Reuse;
    public void Rerender() => StateHasChanged();
    protected override Node Render()
    {
        if (!Reuse) return Kept = P().Text("first");
        return Div().Children(Kept);
    }
}

public sealed class ElementBind : MarkupComponent
{
    public string? Name = "Ann";
    public bool Agree = true;
    public int Age = 30;
    protected override Node Render() => Fragment(
        Input().Bind(() => Name, v => Name = v),
        Input().Type(InputType.Checkbox).Bind(() => Agree, v => Agree = v),
        Input().Type(InputType.Number).BindOn("oninput", () => Age, v => Age = v));
}

public sealed class Shell : MarkupLayout
{
    protected override Node Render() => Div().Class("layout").Children(Header().Text("head"), Content(Body));
}

public sealed class InLayout : MarkupComponent
{
    protected override Node Render() => Comp<LayoutView>().Param("Layout", typeof(Shell)).Children(() => P().Text("page"));
}

public class RenderingTests
{
    [Fact]
    public async Task Elements_Attributes_Text_AreWrittenAndEncoded()
    {
        var html = HtmlOut.Normalize(await HtmlOut.RenderAsync<Basic>());
        Assert.Equal(
            "<div class=\"box\" id=\"b1\"><h1>Title &amp; &lt;more&gt;</h1><p>Hello, <strong>world</strong>!</p>" +
            "<input type=\"checkbox\" checked name=\"agree\"><a href=\"/x\" target=\"_blank\">link</a>" +
            "<img src=\"/a.png\" alt=\"A &quot;quoted&quot; alt\"><ul><li>1</li><li>2</li></ul>" +
            "<span data-id=\"7\" aria-label=\"lbl\"></span></div>", html);
    }

    [Fact]
    public async Task Doctype_And_Markup_AreRaw()
    {
        var html = await HtmlOut.RenderAsync<WithDoctype>();
        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.Contains("<html lang=\"en\"><body><b>raw</b></body></html>", HtmlOut.Normalize(html));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(500)]
    public void If_KeepsItsSlot_SoTogglingIsThreeEdits(int siblings)
    {
        // Port of BlazorCodeFirst's edit-count check: Razor gives 3 edits, a runtime counter 3N+3 (every later sibling shifts).
        var r = new TestRenderer();
        var c = new IfBeforeSiblings { Count = siblings };
        r.Render(c);
        c.Show = false;
        r.Dispatcher.InvokeAsync(c.Rerender);
        Assert.Equal([RenderTreeEditType.StepIn, RenderTreeEditType.RemoveFrame, RenderTreeEditType.StepOut], r.LastEditTypes);
        c.Show = true;
        r.Dispatcher.InvokeAsync(c.Rerender);
        Assert.Equal([RenderTreeEditType.StepIn, RenderTreeEditType.PrependFrame, RenderTreeEditType.StepOut], r.LastEditTypes);
    }

    [Fact]
    public void ForEach_Keys_TurnReorderIntoMoves()
    {
        var r = new TestRenderer();
        var c = new Keyed();
        var id = r.Render(c);
        c.Items = [4, 1, 2, 3];
        r.Dispatcher.InvokeAsync(c.Rerender);
        Assert.Contains(RenderTreeEditType.PermutationListEntry, r.LastEditTypes);
        Assert.DoesNotContain(RenderTreeEditType.UpdateText, r.LastEditTypes);
        var keys = new List<object?>();
        var frames = r.Frames(id);
        for (var i = 0; i < frames.Count; i++)
            if (frames.Array[i].FrameType == RenderTreeFrameType.Element && frames.Array[i].ElementName == "li") keys.Add(frames.Array[i].ElementKey);
        Assert.Equal([4, 1, 2, 3], keys);
    }

    [Fact]
    public async Task Events_ReRenderTheOwner_AllHandlerShapes()
    {
        var r = new TestRenderer();
        var c = new Counter();
        var id = r.Render(c);
        r.Fire(id, "onclick", new MouseEventArgs());
        Assert.Equal(1, c.Count);
        var click = r.FireAsync(id, "onclick", new MouseEventArgs(), nth: 1);
        Assert.False(click.IsCompleted);
        c.Gate.SetResult();
        await click;
        Assert.Equal(11, c.Count);
        Assert.Contains("Count: 11", FrameText(r, id)); // rendered again on completion
        r.Fire(id, "onkeydown", new KeyboardEventArgs { Key = "Enter" });
        Assert.Equal("Enter", c.LastKey);
    }

    [Fact]
    public void PreventDefault_StopPropagation_AreInternalAttributes()
    {
        var r = new TestRenderer();
        var id = r.Render(new Counter());
        var names = new List<string>();
        var frames = r.Frames(id);
        for (var i = 0; i < frames.Count; i++)
            if (frames.Array[i].FrameType == RenderTreeFrameType.Attribute) names.Add(frames.Array[i].AttributeName);
        Assert.Contains("__internal_preventDefault_onkeydown", names);
        Assert.Contains("__internal_stopPropagation_onkeydown", names);
    }

    [Fact]
    public async Task Components_Params_ChildContent_TypedFragments()
    {
        var html = HtmlOut.Normalize(await HtmlOut.RenderAsync<UsesCard>());
        Assert.Equal("<section class=\"card\"><h2>T</h2><p>inner</p><em>row 1</em><em>row 2</em><button>pick</button></section>", html);
        Assert.Contains("<h2>X</h2>plain &amp; text", HtmlOut.Normalize(await HtmlOut.RenderAsync<TextChild>()));
    }

    [Fact]
    public void Component_Callback_ReachesOwner()
    {
        var r = new TestRenderer();
        var owner = new UsesCard();
        r.Render(owner);
        // The Card is the first child component: id 1 (root is 0).
        r.Fire(1, "onclick", new MouseEventArgs());
        Assert.Equal("picked", owner.Picked);
    }

    [Fact]
    public async Task Static_MatchesPlainRendering_AndIsOneMarkupFrame()
    {
        var plain = HtmlOut.Normalize(await HtmlOut.RenderAsync<StaticPagePlain>());
        var stat = HtmlOut.Normalize(await HtmlOut.RenderAsync<StaticPageStatic>());
        Assert.Equal(plain, stat);

        var r = new TestRenderer();
        var id = r.Render(new StaticPageStatic());
        var frames = r.Frames(id);
        var markup = 0;
        for (var i = 0; i < frames.Count; i++) if (frames.Array[i].FrameType == RenderTreeFrameType.Markup) markup++;
        Assert.Equal(1, markup);
    }

    [Fact]
    public async Task Static_RejectsCapturingLambda()
    {
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => HtmlOut.RenderAsync<CapturingStatic>());
        Assert.Contains("captures nothing", ex.ToString());
    }

    [Fact]
    public void Node_FromAnotherRender_Throws()
    {
        var r = new TestRenderer();
        var c = new Stale();
        r.Render(c);
        c.Reuse = true;
        var ex = Assert.Throws<InvalidOperationException>(() => r.Dispatcher.InvokeAsync(c.Rerender).GetAwaiter().GetResult());
        Assert.Contains("another render", ex.Message);
    }

    [Fact]
    public async Task ElementBind_WritesValueAndChecked()
    {
        var html = HtmlOut.Normalize(await HtmlOut.RenderAsync<ElementBind>());
        Assert.Equal("<input value=\"Ann\"><input type=\"checkbox\" checked><input type=\"number\" value=\"30\">", html);
    }

    [Fact]
    public void ElementBind_ChangeEvents_SetTheField()
    {
        var r = new TestRenderer();
        var c = new ElementBind();
        var id = r.Render(c);
        r.Fire(id, "onchange", new ChangeEventArgs { Value = "Bob" });
        Assert.Equal("Bob", c.Name);
        r.Fire(id, "onchange", new ChangeEventArgs { Value = false }, nth: 1);
        Assert.False(c.Agree);
        r.Fire(id, "oninput", new ChangeEventArgs { Value = "41" });
        Assert.Equal(41, c.Age);
    }

    [Fact]
    public async Task MarkupLayout_RendersBody()
    {
        var html = HtmlOut.Normalize(await HtmlOut.RenderAsync<InLayout>());
        Assert.Equal("<div class=\"layout\"><header>head</header><p>page</p></div>", html);
    }

    [Fact]
    public void RenderModeAttributes_MapToFrameworkModes()
    {
        Assert.Same(RenderMode.InteractiveServer, new InteractiveServerAttribute().Mode);
        Assert.Same(RenderMode.InteractiveWebAssembly, new InteractiveWebAssemblyAttribute().Mode);
        Assert.Same(RenderMode.InteractiveAuto, new InteractiveAutoAttribute().Mode);
    }

    [Fact]
    public void RenderMode_OnComponentNode_WritesAComponentRenderModeFrame()
    {
        var r = new TestRenderer();
        var id = r.Render(new WithRenderModes());
        var modes = new List<string>();
        var frames = r.Frames(id);
        for (var i = 0; i < frames.Count; i++)
        {
            ref var f = ref frames.Array[i];
            if (f.FrameType == RenderTreeFrameType.Component) modes.Add("component");
            if (f.FrameType == RenderTreeFrameType.ComponentRenderMode) modes[^1] = f.ComponentRenderMode.GetType().Name;
        }
        Assert.Equal([nameof(InteractiveServerRenderMode), nameof(InteractiveAutoRenderMode), "component", "component"], modes);
        // The renderer resolved the two instances through the render mode, as it does for Razor's @rendermode.
        Assert.Equal([RenderMode.InteractiveServer, RenderMode.InteractiveAuto], r.ResolvedRenderModes);
    }

    [Fact]
    public void Nodes_OutsideRender_Throw()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Div());
        Assert.Contains("while rendering", ex.Message);
    }

    [Fact]
    public void GeneratedCounts()
    {
        Assert.Equal(112, HtmlCounts.Elements);
        Assert.Equal(384, HtmlCounts.EventMethods);
        Assert.True(HtmlCounts.AttributeMethods > 143); // BlazorCodeFirst's yardstick
    }

    private static string FrameText(TestRenderer r, int id)
    {
        var frames = r.Frames(id);
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < frames.Count; i++) if (frames.Array[i].FrameType == RenderTreeFrameType.Text) sb.Append(frames.Array[i].TextContent);
        return sb.ToString();
    }
}

using System.Collections.Concurrent;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace PureMarkup.Prototype;

/// <summary>Per-render state: the builder, the event receiver and the running sequence number.</summary>
public sealed class RenderContext(RenderTreeBuilder builder, IHandleEvent receiver)
{
    public readonly RenderTreeBuilder Builder = builder;
    public readonly IHandleEvent Receiver = receiver;
    public int Seq;
}

/// <summary>
/// Sequence numbers: depth-first counter inside a region. Every dynamic shape (If, ForEach) opens its own
/// region, so the static structure around it keeps stable sequence numbers, as Razor's would.
/// </summary>
public abstract class Node
{
    public abstract void Write(RenderContext ctx);
}

public sealed class TextNode(string? text) : Node
{
    internal readonly string? Value = text;
    public override void Write(RenderContext ctx) => ctx.Builder.AddContent(ctx.Seq++, Value);
}

public sealed class MarkupNode(string markup) : Node
{
    public override void Write(RenderContext ctx) => ctx.Builder.AddMarkupContent(ctx.Seq++, markup);
}

public sealed class Element(string tag) : Node
{
    internal readonly string Tag = tag;
    internal object? KeyValue;
    internal List<KeyValuePair<string, object?>>? Attrs;
    internal Node[]? Kids;

    public Element Attr(string name, object? value)
    {
        if (value is null || value is false) return this;
        (Attrs ??= new(4)).Add(new(name, value));
        return this;
    }

    public Element Class(string? value) => Attr("class", value);
    public Element Id(string value) => Attr("id", value);
    public Element Key(object key) { KeyValue = key; return this; }
    public Element OnClick(Action handler) => Attr("onclick", handler);

    public Element Children(params Node[] children) { Kids = children; return this; }
    public Element Text(string? text) { Kids = [new TextNode(text)]; return this; }
    public Element Text(int value) { Kids = [new TextNode(value.ToString())]; return this; }

    public override void Write(RenderContext ctx)
    {
        var b = ctx.Builder;
        b.OpenElement(ctx.Seq++, Tag);
        if (KeyValue is not null) b.SetKey(KeyValue);
        if (Attrs is not null)
        {
            foreach (var (name, value) in Attrs)
            {
                switch (value)
                {
                    case Action a:
                        b.AddAttribute(ctx.Seq++, name, EventCallback.Factory.Create<MouseEventArgs>(ctx.Receiver, a));
                        break;
                    case string s:
                        b.AddAttribute(ctx.Seq++, name, s);
                        break;
                    case true:
                        b.AddAttribute(ctx.Seq++, name, true);
                        break;
                    default:
                        b.AddAttribute(ctx.Seq++, name, value);
                        break;
                }
            }
        }
        if (Kids is not null)
            foreach (var k in Kids) k.Write(ctx);
        b.CloseElement();
    }
}

public sealed class ComponentNode(Type type) : Node
{
    internal object? KeyValue;
    internal List<KeyValuePair<string, object?>>? Params;

    public ComponentNode Param(string name, object? value) { (Params ??= new(4)).Add(new(name, value)); return this; }
    public ComponentNode Key(object key) { KeyValue = key; return this; }

    public override void Write(RenderContext ctx)
    {
        var b = ctx.Builder;
        b.OpenComponent(ctx.Seq++, type);
        if (KeyValue is not null) b.SetKey(KeyValue);
        if (Params is not null)
            foreach (var (name, value) in Params) b.AddComponentParameter(ctx.Seq++, name, value);
        b.CloseComponent();
    }
}

public sealed class FragmentNode(Node[] children) : Node
{
    public override void Write(RenderContext ctx)
    {
        foreach (var c in children) c.Write(ctx);
    }
}

public sealed class IfNode(bool condition, Func<Node> then) : Node
{
    public override void Write(RenderContext ctx)
    {
        var b = ctx.Builder;
        b.OpenRegion(ctx.Seq++);
        if (condition)
        {
            var saved = ctx.Seq;
            ctx.Seq = 0;
            then().Write(ctx);
            ctx.Seq = saved;
        }
        b.CloseRegion();
    }
}

public sealed class ForEachNode<T>(IEnumerable<T> items, Func<T, object> key, Func<T, Node> body) : Node
{
    public override void Write(RenderContext ctx)
    {
        var b = ctx.Builder;
        b.OpenRegion(ctx.Seq++);
        var saved = ctx.Seq;
        foreach (var item in items)
        {
            ctx.Seq = 0;
            var node = body(item);
            var k = key(item);
            if (node is Element e) e.KeyValue = k;
            else if (node is ComponentNode c) c.KeyValue = k;
            node.Write(ctx);
        }
        ctx.Seq = saved;
        b.CloseRegion();
    }
}

/// <summary>Static(...): render a subtree once to HTML per call site and emit it as one markup frame.</summary>
public sealed class StaticNode : Node
{
    // A non-capturing (static) lambda is cached by the compiler in a static field, so the delegate
    // instance identifies the call site; lookup is a reference hash.
    static readonly ConcurrentDictionary<Func<Node>, string> Cache = new(ReferenceEqualityComparer.Instance);
    readonly string _html;

    internal StaticNode(Func<Node> build) => _html = Cache.GetOrAdd(build, static b => Html.Serialize(b()));

    public override void Write(RenderContext ctx) => ctx.Builder.AddMarkupContent(ctx.Seq++, _html);
}

static class Html
{
    public static string Serialize(Node n) { var sb = new StringBuilder(); Write(sb, n); return sb.ToString(); }

    static void Write(StringBuilder sb, Node n)
    {
        switch (n)
        {
            case Element e:
                sb.Append('<').Append(e.Tag);
                if (e.Attrs is not null)
                    foreach (var (k, v) in e.Attrs)
                    {
                        if (v is Delegate) throw new InvalidOperationException("Static(...) cannot hold event handlers.");
                        sb.Append(' ').Append(k);
                        if (v is not true) sb.Append("=\"").Append(WebUtility.HtmlEncode(Convert.ToString(v))).Append('"');
                    }
                sb.Append('>');
                if (VoidTags.Contains(e.Tag)) return;
                if (e.Kids is not null) foreach (var k in e.Kids) Write(sb, k);
                sb.Append("</").Append(e.Tag).Append('>');
                break;
            default:
                if (n is TextNode t) sb.Append(WebUtility.HtmlEncode(t.Value));
                else throw new InvalidOperationException($"Static(...) cannot hold {n.GetType().Name}.");
                break;
        }
    }


    static readonly HashSet<string> VoidTags = ["area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "source", "track", "wbr"];
}

public static class H
{
    public static Element El(string tag) => new(tag);
    public static Element Div() => new("div");
    public static Element Section() => new("section");
    public static Element H1() => new("h1");
    public static Element H2() => new("h2");
    public static Element P() => new("p");
    public static Element Ul() => new("ul");
    public static Element Li() => new("li");
    public static Element A() => new("a");
    public static Element Img() => new("img");
    public static Element Input() => new("input");
    public static Element Button() => new("button");
    public static Element Span() => new("span");
    public static Element Table() => new("table");
    public static Element Thead() => new("thead");
    public static Element Tbody() => new("tbody");
    public static Element Tr() => new("tr");
    public static Element Th() => new("th");
    public static Element Td() => new("td");
    public static Element Nav() => new("nav");
    public static Element Main() => new("main");
    public static Element Footer() => new("footer");
    public static TextNode Text(string? s) => new(s);
    public static ComponentNode Comp<T>() where T : IComponent => new(typeof(T));
    public static Node If(bool condition, Func<Node> then) => new IfNode(condition, then);
    public static Node ForEach<T>(IEnumerable<T> items, Func<T, object> key, Func<T, Node> body) => new ForEachNode<T>(items, key, body);
    public static Node Fragment(params Node[] children) => new FragmentNode(children);
    public static Node Static(Func<Node> build) => new StaticNode(build);
}

/// <summary>Base class: a component whose Render() returns a node tree, written to the builder each render.</summary>
public abstract class MarkupComponent : ComponentBase
{
    protected abstract Node Render();

    protected override void BuildRenderTree(RenderTreeBuilder builder) => Render().Write(new RenderContext(builder, this));

    public void BuildPublic(RenderTreeBuilder builder) => BuildRenderTree(builder);
    public void Rerender() => StateHasChanged();
}

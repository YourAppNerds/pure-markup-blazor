using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using PureMarkup.Blazor.Rendering;

namespace PureMarkup.Blazor;

/// <summary>
/// Writes a node tree to a <see cref="RenderTreeBuilder"/>. <see cref="MarkupComponent"/> and <see cref="MarkupLayout"/> use it;
/// call it directly from a custom <see cref="IComponent"/> base.
/// </summary>
/// <remarks>
/// Sequence numbers: a child's sequence is its slot in its parent, and slots never move (<see cref="Node.Empty"/> and a false
/// <c>If</c> keep theirs), which matches Razor's static numbering. Attributes are numbered by call order on their element.
/// </remarks>
public static class MarkupRenderer
{
    /// <summary>Builds the tree with <paramref name="build"/> and writes it. <paramref name="receiver"/> receives events (usually the component).</summary>
    public static void Render(RenderTreeBuilder builder, object? receiver, Func<Node> build)
    {
        var a = RenderArena.Enter(receiver);
        try { WriteRoot(a, builder, build()); }
        finally { RenderArena.Exit(a); }
    }

    /// <summary>Same as <see cref="Render(RenderTreeBuilder, object?, Func{Node})"/> with an argument, for <see cref="RenderFragment{T}"/>.</summary>
    public static void Render<T>(RenderTreeBuilder builder, object? receiver, Func<T, Node> build, T arg)
    {
        var a = RenderArena.Enter(receiver);
        try { WriteRoot(a, builder, build(arg)); }
        finally { RenderArena.Exit(a); }
    }

    /// <summary>A <see cref="RenderFragment"/> that builds its nodes when rendered.</summary>
    public static RenderFragment Fragment(object? receiver, Func<Node> build) => b => Render(b, receiver, build);

    /// <summary>A <see cref="RenderFragment{T}"/> that builds its nodes when rendered.</summary>
    public static RenderFragment<T> Fragment<T>(object? receiver, Func<T, Node> build) => arg => b => Render(b, receiver, build, arg);

    internal static void WriteRoot(RenderArena a, RenderTreeBuilder b, Node root)
    {
        var i = a.Check(root);
        if (i == 0) return;
        ref var f = ref a.Nodes[i];
        // A root fragment needs no region: its children are the top level.
        if (f.Kind == NodeKind.Fragment) WriteKids(a, b, f.FirstKid, f.KidCount);
        else WriteNode(a, b, i, 0);
    }

    private static void WriteKids(RenderArena a, RenderTreeBuilder b, int first, int count)
    {
        var kids = a.Kids;
        for (var slot = 0; slot < count; slot++)
        {
            var k = kids[first + slot];
            if (k != 0) WriteNode(a, b, k, slot);
        }
    }

    private static void WriteNode(RenderArena a, RenderTreeBuilder b, int i, int seq)
    {
        ref var f = ref a.Nodes[i];
        switch (f.Kind)
        {
            case NodeKind.Element:
                b.OpenElement(seq, f.Name!);
                if (f.Key is not null) b.SetKey(f.Key);
                if (f.FirstAttr != 0) WriteElementAttrs(a, b, ref f);
                if (f.KidCount != 0) WriteKids(a, b, f.FirstKid, f.KidCount);
                b.CloseElement();
                break;
            case NodeKind.Text:
                b.AddContent(seq, f.Name);
                break;
            case NodeKind.Markup:
                b.AddMarkupContent(seq, f.Name);
                break;
            case NodeKind.Component:
                ((ComponentOpener)f.Obj!).Open(b, seq);
                if (f.Key is not null) b.SetKey(f.Key);
                if (f.FirstAttr != 0) WriteComponentAttrs(a, b, ref f);
                b.CloseComponent();
                break;
            case NodeKind.Fragment:
                b.OpenRegion(seq);
                WriteKids(a, b, f.FirstKid, f.KidCount);
                b.CloseRegion();
                break;
            case NodeKind.Repeat:
                b.OpenRegion(seq);
                var kids = a.Kids;
                for (var n = 0; n < f.KidCount; n++)
                {
                    var k = kids[f.FirstKid + n];
                    if (k != 0) WriteNode(a, b, k, 0);
                }
                b.CloseRegion();
                break;
            case NodeKind.Content:
                b.AddContent(seq, (RenderFragment?)f.Obj);
                break;
        }
    }

    private static void WriteElementAttrs(RenderArena a, RenderTreeBuilder b, ref NodeFrame f)
    {
        var attrs = a.Attrs;
        var receiver = a.Receiver;
        for (var i = f.FirstAttr; i != 0; i = attrs[i].Next)
        {
            ref var at = ref attrs[i];
            switch (at.Kind)
            {
                case AttrKind.String:
                    b.AddAttribute(at.Seq, at.Name, (string?)at.Value);
                    break;
                case AttrKind.Object:
                    b.AddAttribute(at.Seq, at.Name, at.Value);
                    break;
                case AttrKind.Event:
                    ((EventFactory)at.Aux!).AddAttribute(b, at.Seq, at.Name, receiver, at.Value!);
                    break;
                case AttrKind.PreventDefault:
                    b.AddEventPreventDefaultAttribute(at.Seq, at.Name, (bool)at.Value!);
                    break;
                case AttrKind.StopPropagation:
                    b.AddEventStopPropagationAttribute(at.Seq, at.Name, (bool)at.Value!);
                    break;
                case AttrKind.Binder:
                    b.AddAttribute(at.Seq, at.Name, (EventCallback<ChangeEventArgs>)at.Value!);
                    b.SetUpdatesAttributeName((string)at.Aux!);
                    break;
                case AttrKind.Multiple:
                    b.AddMultipleAttributes(at.Seq, (IEnumerable<KeyValuePair<string, object>>?)at.Value);
                    break;
            }
        }
        if (!f.HasTrailing) return;
        for (var i = f.FirstAttr; i != 0; i = attrs[i].Next)
        {
            ref var at = ref attrs[i];
            if (at.Kind == AttrKind.ElementRef) b.AddElementReferenceCapture(at.Seq, (Action<ElementReference>)at.Value!);
            else if (at.Kind == AttrKind.NamedEvent) b.AddNamedEvent(at.Name, (string)at.Value!);
        }
    }

    private static void WriteComponentAttrs(RenderArena a, RenderTreeBuilder b, ref NodeFrame f)
    {
        var attrs = a.Attrs;
        var receiver = a.Receiver;
        for (var i = f.FirstAttr; i != 0; i = attrs[i].Next)
        {
            ref var at = ref attrs[i];
            switch (at.Kind)
            {
                case AttrKind.Param:
                    b.AddComponentParameter(at.Seq, at.Name, at.Value);
                    break;
                case AttrKind.ParamEvent:
                    ((EventFactory)at.Aux!).AddParameter(b, at.Seq, at.Name, receiver, at.Value!);
                    break;
                case AttrKind.ParamFragment:
                    b.AddComponentParameter(at.Seq, at.Name, Fragment(receiver, (Func<Node>)at.Value!));
                    break;
                case AttrKind.ParamFragmentOf:
                    b.AddComponentParameter(at.Seq, at.Name, ((FragmentFactory)at.Aux!).Create(receiver, at.Value!));
                    break;
                case AttrKind.ParamText:
                    var text = (string?)at.Value;
                    b.AddComponentParameter(at.Seq, at.Name, (RenderFragment)(t => t.AddContent(0, text)));
                    break;
                case AttrKind.Multiple:
                    b.AddMultipleAttributes(at.Seq, (IEnumerable<KeyValuePair<string, object>>?)at.Value);
                    break;
            }
        }
        if (!f.HasTrailing) return;
        // Razor order: parameters, reference capture, render mode.
        for (var i = f.FirstAttr; i != 0; i = attrs[i].Next)
            if (attrs[i].Kind == AttrKind.ComponentRef) b.AddComponentReferenceCapture(attrs[i].Seq, (Action<object>)attrs[i].Value!);
        for (var i = f.FirstAttr; i != 0; i = attrs[i].Next)
            if (attrs[i].Kind == AttrKind.RenderMode) b.AddComponentRenderMode((IComponentRenderMode?)attrs[i].Value);
    }
}

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
/// <c>If</c> keep theirs), which matches Razor's static numbering. Attributes are numbered by call order on their element;
/// a preset's attributes all take the number of the <c>.Use</c> call.
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
        if (f.Kind == NodeKind.Fragment) WriteKids(a, b, f.FirstKid, f.KidCount, a.Receiver);
        else WriteNode(a, b, i, 0, a.Receiver);
    }

    // The receiver is passed down rather than read from the arena: a preset's children live in the preset's own arena.
    private static void WriteKids(RenderArena a, RenderTreeBuilder b, int first, int count, object? receiver)
    {
        var kids = a.Kids;
        for (var slot = 0; slot < count; slot++)
        {
            var k = kids[first + slot];
            if (k != 0) WriteNode(a, b, k, slot, receiver);
        }
    }

    private static void WriteNode(RenderArena a, RenderTreeBuilder b, int i, int seq, object? receiver)
    {
        ref var f = ref a.Nodes[i];
        switch (f.Kind)
        {
            case NodeKind.Element:
                b.OpenElement(seq, f.Name!);
                if (f.Key is not null) b.SetKey(f.Key);
                if (f.FirstAttr != 0) WriteElementAttrs(a, b, ref f, receiver);
                if (f.KidCount != 0) WriteKids(a, b, f.FirstKid, f.KidCount, receiver);
                else if (f.Preset is { KidCount: > 0 } p) WriteKids(p.KidsArena!, b, p.FirstKid, p.KidCount, receiver); // local children replace the preset's
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
                if (f.FirstAttr != 0) WriteComponentAttrs(a, b, ref f, receiver);
                b.CloseComponent();
                break;
            case NodeKind.Fragment:
                b.OpenRegion(seq);
                WriteKids(a, b, f.FirstKid, f.KidCount, receiver);
                b.CloseRegion();
                break;
            case NodeKind.Repeat:
                b.OpenRegion(seq);
                var kids = a.Kids;
                for (var n = 0; n < f.KidCount; n++)
                {
                    var k = kids[f.FirstKid + n];
                    if (k != 0) WriteNode(a, b, k, 0, receiver);
                }
                b.CloseRegion();
                break;
            case NodeKind.Content:
                b.AddContent(seq, (RenderFragment?)f.Obj);
                break;
        }
    }

    private static void WriteElementAttrs(RenderArena a, RenderTreeBuilder b, ref NodeFrame f, object? receiver)
    {
        var attrs = a.Attrs;
        var p = f.Preset;
        for (var i = f.FirstAttr; i != 0; i = attrs[i].Next)
        {
            ref var at = ref attrs[i];
            if (p is not null)
            {
                if (i == f.ClassAttr) { b.AddAttribute(at.Seq, at.Name, p.MergedClass(ref f, (string?)at.Value)); continue; }
                if (i == f.StyleAttr) { b.AddAttribute(at.Seq, at.Name, PresetData.MergeStyle(p.Style, (string?)at.Value)); continue; }
                if (at.Kind == AttrKind.Preset)
                {
                    // The preset's writes, at the .Use position, minus what the node sets itself.
                    if (p.ClassAtMarker(a, ref f) is { } cls) b.AddAttribute(at.Seq, p.ClassName!, cls);
                    if (p.Style is not null && f.StyleAttr == 0 && !PresetData.HasLocal(a, ref f, 0, "style")) b.AddAttribute(at.Seq, "style", p.Style);
                    foreach (ref var pa in p.Attrs.AsSpan())
                        if (pa.Kind < AttrKind.ElementRef && !PresetData.IsOverridden(a, ref f, pa)) WriteElementAttr(b, ref pa, at.Seq, receiver);
                    continue;
                }
            }
            WriteElementAttr(b, ref at, at.Seq, receiver);
        }
        if (!f.HasTrailing) return;
        for (var i = f.FirstAttr; i != 0; i = attrs[i].Next)
        {
            ref var at = ref attrs[i];
            if (at.Kind == AttrKind.Preset)
            {
                foreach (ref var pa in p!.Attrs.AsSpan())
                    if (pa.Kind >= AttrKind.ElementRef && !PresetData.IsOverridden(a, ref f, pa)) WriteElementTrailing(b, ref pa, at.Seq);
            }
            else WriteElementTrailing(b, ref at, at.Seq);
        }
    }

    private static void WriteElementAttr(RenderTreeBuilder b, ref AttrFrame at, int seq, object? receiver)
    {
        switch (at.Kind)
        {
            case AttrKind.String:
                if (at.Value is not null) b.AddAttribute(seq, at.Name, (string)at.Value);
                break;
            case AttrKind.Object:
                if (at.Value is not null) b.AddAttribute(seq, at.Name, at.Value);
                break;
            case AttrKind.Event:
                ((EventFactory)at.Aux!).AddAttribute(b, seq, at.Name, receiver, at.Value!);
                break;
            case AttrKind.PreventDefault:
                b.AddEventPreventDefaultAttribute(seq, at.Name, (bool)at.Value!);
                break;
            case AttrKind.StopPropagation:
                b.AddEventStopPropagationAttribute(seq, at.Name, (bool)at.Value!);
                break;
            case AttrKind.Binder:
                b.AddAttribute(seq, at.Name, (EventCallback<ChangeEventArgs>)at.Value!);
                b.SetUpdatesAttributeName((string)at.Aux!);
                break;
            case AttrKind.Multiple:
                b.AddMultipleAttributes(seq, (IEnumerable<KeyValuePair<string, object>>?)at.Value);
                break;
        }
    }

    private static void WriteElementTrailing(RenderTreeBuilder b, ref AttrFrame at, int seq)
    {
        if (at.Kind == AttrKind.ElementRef) b.AddElementReferenceCapture(seq, (Action<ElementReference>)at.Value!);
        else if (at.Kind == AttrKind.NamedEvent) b.AddNamedEvent(at.Name, (string)at.Value!);
    }

    private static void WriteComponentAttrs(RenderArena a, RenderTreeBuilder b, ref NodeFrame f, object? receiver)
    {
        var attrs = a.Attrs;
        var p = f.Preset;
        for (var i = f.FirstAttr; i != 0; i = attrs[i].Next)
        {
            ref var at = ref attrs[i];
            if (p is not null)
            {
                if (i == f.ClassAttr) { b.AddComponentParameter(at.Seq, at.Name, p.MergedClass(ref f, (string?)at.Value)); continue; }
                if (at.Kind == AttrKind.Preset)
                {
                    if (p.ClassAtMarker(a, ref f) is { } cls) b.AddComponentParameter(at.Seq, p.ClassName!, cls);
                    foreach (ref var pa in p.Attrs.AsSpan())
                        if (pa.Kind < AttrKind.ElementRef && !PresetData.IsOverridden(a, ref f, pa)) WriteComponentAttr(b, ref pa, at.Seq, receiver);
                    continue;
                }
            }
            WriteComponentAttr(b, ref at, at.Seq, receiver);
        }
        if (!f.HasTrailing) return;
        // Razor order: parameters, reference capture, render mode.
        for (var pass = 0; pass < 2; pass++)
        {
            var kind = pass == 0 ? AttrKind.ComponentRef : AttrKind.RenderMode;
            for (var i = f.FirstAttr; i != 0; i = attrs[i].Next)
            {
                ref var at = ref attrs[i];
                if (at.Kind == kind) WriteComponentTrailing(b, ref at, at.Seq);
                else if (at.Kind == AttrKind.Preset)
                    foreach (ref var pa in p!.Attrs.AsSpan())
                        if (pa.Kind == kind && !PresetData.IsOverridden(a, ref f, pa)) WriteComponentTrailing(b, ref pa, at.Seq);
            }
        }
    }

    private static void WriteComponentAttr(RenderTreeBuilder b, ref AttrFrame at, int seq, object? receiver)
    {
        switch (at.Kind)
        {
            case AttrKind.Param:
                b.AddComponentParameter(seq, at.Name, at.Value);
                break;
            case AttrKind.ParamEvent:
                ((EventFactory)at.Aux!).AddParameter(b, seq, at.Name, receiver, at.Value!);
                break;
            case AttrKind.ParamFragment:
                b.AddComponentParameter(seq, at.Name, Fragment(receiver, (Func<Node>)at.Value!));
                break;
            case AttrKind.ParamFragmentOf:
                b.AddComponentParameter(seq, at.Name, ((FragmentFactory)at.Aux!).Create(receiver, at.Value!));
                break;
            case AttrKind.ParamText:
                var text = (string?)at.Value;
                b.AddComponentParameter(seq, at.Name, (RenderFragment)(t => t.AddContent(0, text)));
                break;
            case AttrKind.Multiple:
                b.AddMultipleAttributes(seq, (IEnumerable<KeyValuePair<string, object>>?)at.Value);
                break;
        }
    }

    private static void WriteComponentTrailing(RenderTreeBuilder b, ref AttrFrame at, int seq)
    {
        if (at.Kind == AttrKind.ComponentRef) b.AddComponentReferenceCapture(seq, (Action<object>)at.Value!);
        else if (at.Kind == AttrKind.RenderMode) b.AddComponentRenderMode((IComponentRenderMode?)at.Value);
    }
}

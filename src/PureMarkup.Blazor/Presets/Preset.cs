using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using PureMarkup.Blazor.Rendering;
using PureMarkup.Blazor.Styling;

namespace PureMarkup.Blazor;

/// <summary>Creates presets.</summary>
public static class Preset
{
    /// <summary>
    /// A preset for <typeparamref name="T"/>, an element (<c>ButtonElement</c>, or <c>Element</c> for any element) or a
    /// component (<c>ComponentNode&lt;InputText&gt;</c>), written with the normal markup methods:
    /// <c>public static Preset&lt;ButtonElement&gt; Primary => Preset.Once&lt;ButtonElement&gt;(p => p.Class(ButtonCss.Primary).Type(ButtonType.Button));</c>
    /// The writes are recorded once per (base, lambda), so a property getter returns the same preset every time.
    /// The lambda must capture nothing (mark it <c>static</c> if you like) and return the node it was given.
    /// </summary>
    public static Preset<T> Once<T>(Func<T, T> build) where T : struct => PresetCache.Get(null, build);
}

/// <summary>
/// Recorded writes for <typeparamref name="T"/>, applied with <c>.Use(preset)</c> or <c>preset.New()</c>. Local writes beat the
/// preset's in any call order: attributes and parameters by name, inline styles by CSS property, events and children
/// outright. Classes are additive: the preset's classes and the node's own all apply (<c>.ClassReplace</c> drops the preset's).
/// </summary>
public sealed class Preset<T> : IPreset where T : struct
{
    private readonly Preset<T>? _base;
    private readonly Func<T, T> _build;
    private PresetData? _data;

    internal Preset(Preset<T>? @base, Func<T, T> build)
    {
        PresetTarget<T>.Check();
        _base = @base;
        _build = build;
    }

    /// <summary>A derived preset: this preset's writes first, then <paramref name="build"/>'s, which win where both set something.</summary>
    public Preset<T> With(Func<T, T> build) => PresetCache.Get(this, build);

    /// <summary>The recorded writes, flattened (base first). Recorded on first use, on any thread.</summary>
    internal PresetData Data
    {
        get
        {
            var d = Volatile.Read(ref _data);
            if (d is not null) return d;
            d = Record();
            return Interlocked.CompareExchange(ref _data, d, null) ?? d;
        }
    }

    /// <summary>Drops the recorded writes, so the next use records again (Hot Reload).</summary>
    void IPreset.Reset() => Volatile.Write(ref _data, null);

    PresetData IPreset.Data => Data;

    private PresetData Record()
    {
        var a = RenderArena.EnterRecording();
        try
        {
            var node = a.NewNode(PresetTarget<T>.IsComponent ? NodeKind.Component : NodeKind.Element);
            a.RecordingRoot = node.Index;
            if (_base is not null)
            {
                a.UsePreset(node, _base.Data);
                if (_base.Data.Actions.Length > 0) (a.RecordedActions ??= []).AddRange(_base.Data.Actions);
            }
            var result = _build(Unsafe.As<Node, T>(ref node));
            if (Unsafe.As<T, Node>(ref result) != node)
                throw new InvalidOperationException("A preset lambda must return the node it is given: p => p.Class(...).Type(...).");
            return PresetData.Compile(a, node, PresetTarget<T>.Tag);
        }
        finally { RenderArena.ExitRecording(a); }
    }
}

/// <summary>Applying presets.</summary>
public static class PresetExtensions
{
    /// <summary>
    /// Applies <paramref name="preset"/> to this element, then runs its <c>.Also</c> actions. Local writes win in any call
    /// order; classes add up. A later <c>.Use</c> replaces an earlier one (compose presets with <see cref="Preset{T}.With"/>).
    /// </summary>
    public static T Use<T>(this T el, Preset<T> preset) where T : struct, IElement
    {
        ArgumentNullException.ThrowIfNull(preset);
        var data = preset.Data;
        var a = RenderArena.Current;
        a.UsePreset(el.Node, data);
        if (data.Actions.Length == 0) return el;
        if (a.IsRecordingRoot(el.Node)) (a.RecordedActions ??= []).AddRange(data.Actions); // a preset using a preset: run at its uses
        else foreach (var action in data.Actions) ((Action<T>)action)(el);
        return el;
    }

    /// <summary>Applies a preset written for any element (<c>Preset&lt;Element&gt;</c>, global attributes only).</summary>
    public static T Use<T>(this T el, Preset<Element> preset) where T : struct, IElement
    {
        ArgumentNullException.ThrowIfNull(preset);
        var data = preset.Data;
        var a = RenderArena.Current;
        a.UsePreset(el.Node, data);
        if (data.Actions.Length == 0) return el;
        if (a.IsRecordingRoot(el.Node))
            foreach (var action in data.Actions) (a.RecordedActions ??= []).Add((Action<T>)(e => ((Action<Element>)action)(new Element(e.Node))));
        else foreach (var action in data.Actions) ((Action<Element>)action)(new Element(el.Node));
        return el;
    }

    /// <summary>A new element with the preset applied: <c>AppPresets.DangerButton.New().Text("Delete")</c>.</summary>
    public static T New<T>(this Preset<T> preset) where T : struct, IElement
    {
        ArgumentNullException.ThrowIfNull(preset);
        var tag = preset.Data.Tag ?? throw new InvalidOperationException(
            "A Preset<Element> has no tag: use El(tag).Use(preset) or Div().Use(preset).");
        var node = RenderArena.Current.NewNode(NodeKind.Element, tag);
        return Unsafe.As<Node, T>(ref node).Use(preset);
    }

    /// <summary>A new component with the preset applied.</summary>
    public static ComponentNode<TComp> New<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] TComp>(
        this Preset<ComponentNode<TComp>> preset) where TComp : Microsoft.AspNetCore.Components.IComponent =>
        H.Comp<TComp>().Use(preset);

    /// <summary>
    /// Inside a preset: <paramref name="action"/> runs on the element at every <c>.Use</c> / <c>.New</c>, in order, and its writes
    /// count as local writes made at the <c>.Use</c> call (MAUI's <c>.Also</c>). Use it for per-render work. Outside a preset it runs at once.
    /// </summary>
    public static T Also<T>(this T el, Action<T> action) where T : struct, IElement
    {
        ArgumentNullException.ThrowIfNull(action);
        var a = RenderArena.Current;
        if (a.IsRecordingRoot(el.Node)) (a.RecordedActions ??= []).Add(action);
        else action(el);
        return el;
    }

    /// <summary>Adds a class and drops the preset's classes and rules from this element. Other local classes still add up.</summary>
    public static T ClassReplace<T>(this T el, string? value) where T : struct, IElement
    {
        var a = RenderArena.Current;
        a.ReplaceClass(el.Node);
        a.AddClass(el.Node, value);
        return el;
    }

    /// <summary>Adds the rule's class and drops the preset's classes and rules from this element.</summary>
    public static T ClassReplace<T>(this T el, Rule rule) where T : struct, IElement
    {
        ArgumentNullException.ThrowIfNull(rule);
        var a = RenderArena.Current;
        a.ReplaceClass(el.Node);
        a.AddClass(el.Node, rule);
        return el;
    }
}

/// <summary>What a preset may target: a one-field node handle (an element struct or <c>ComponentNode&lt;T&gt;</c>).</summary>
internal static class PresetTarget<T> where T : struct
{
    public static readonly bool IsComponent = typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(ComponentNode<>);
    private static readonly bool Valid = (IsComponent || typeof(IElement).IsAssignableFrom(typeof(T))) && Unsafe.SizeOf<T>() == Unsafe.SizeOf<Node>();
    public static readonly string? Tag = IsComponent ? null : ElementTags.Of(typeof(T));

    public static void Check()
    {
        if (!Valid)
            throw new ArgumentException($"A preset targets an element (ButtonElement, Element, ...) or ComponentNode<T>, not {typeof(T).Name}.");
    }
}

/// <summary>A preset of any target type, for the build tool and Hot Reload.</summary>
internal interface IPreset
{
    PresetData Data { get; }
    void Reset();
}

/// <summary>Presets by (base, lambda), so a property getter returns the same preset. Cleared on Hot Reload.</summary>
internal static class PresetCache
{
    private static readonly ConcurrentDictionary<(object? Base, Delegate Build), object> Cache = new();

    public static Preset<T> Get<T>(Preset<T>? @base, Func<T, T> build) where T : struct
    {
        ArgumentNullException.ThrowIfNull(build);
        if (Cache.TryGetValue((@base, build), out var hit)) return (Preset<T>)hit;
        // A capturing lambda is a new delegate on every call, so it would add a preset per call and never hit.
        if (build.Target is { } t && !IsCachedLambdaClass(t.GetType()))
            throw new ArgumentException("A preset lambda must capture nothing (no locals, no 'this'); mark it 'static'.", nameof(build));
        return (Preset<T>)Cache.GetOrAdd((@base, build), static (_, s) => new Preset<T>(s.@base, s.build), (@base, build));
    }

    private static bool IsCachedLambdaClass(Type t) => t.Name == "<>c" || t.Name.StartsWith("<>c`", StringComparison.Ordinal);

    /// <summary>Every preset created so far.</summary>
    internal static IEnumerable<object> All => Cache.Values;

    /// <summary>
    /// Forgets every preset and its recorded writes: getters create and record them again, and a preset kept in a static
    /// field records again on its next use. Hot Reload calls this after a preset lambda is edited.
    /// </summary>
    internal static void Clear()
    {
        foreach (var p in Cache.Values) ((IPreset)p).Reset();
        Cache.Clear();
    }
}

/// <summary>
/// A preset's writes, flattened: base writes first, derived ones override. Attributes keep their recorded frames
/// (minus the class and style, which are kept apart so the writer can merge them with the node's own).
/// </summary>
internal sealed class PresetData
{
    public AttrFrame[] Attrs = [];
    /// <summary>Name of the class attribute or parameter (<c>class</c>, or a component's <c>Class</c>).</summary>
    public string? ClassName;
    /// <summary>The preset's classes (strings and rules' <see cref="Rule.PresetClassName"/>), space-separated.</summary>
    public string? Class;
    /// <summary>The rules among <see cref="Class"/>, for the build tool.</summary>
    public Rule[] Rules = [];
    public string? Style;
    public RenderArena? KidsArena;
    public int FirstKid;
    public int KidCount;
    public bool HasTrailing;
    public Delegate[] Actions = [];
    /// <summary>Tag for <c>.New()</c>; null for <c>Element</c> and components.</summary>
    public string? Tag;
    /// <summary>The preset this one was recorded on top of (<c>.With</c> base, or a <c>.Use</c> inside the lambda), if any.</summary>
    public PresetData? Base;

    public static PresetData Compile(RenderArena a, Node node, string? tag)
    {
        ref var f = ref a.Nodes[node.Index];
        if (f.Key is not null) throw new InvalidOperationException("Key cannot be part of a preset: set it where the preset is used.");
        var b = f.Preset;
        var attrs = new List<AttrFrame>();
        if (b is not null)
            foreach (var pa in b.Attrs)
                if (!IsOverridden(a, ref f, pa)) attrs.Add(pa);
        for (var i = f.FirstAttr; i != 0; i = a.Attrs[i].Next)
        {
            var at = a.Attrs[i];
            if (i == f.ClassAttr || i == f.StyleAttr || at.Kind == AttrKind.Preset) continue;
            var g = Group(at.Kind);
            attrs.RemoveAll(x => Group(x.Kind) == g && x.Name == at.Name); // last write wins inside a preset too
            attrs.Add(at);
        }

        var d = new PresetData { Attrs = [.. attrs], Tag = tag, Base = b };
        foreach (var at in d.Attrs) if (at.Kind >= AttrKind.ElementRef) d.HasTrailing = true;

        var baseClass = f.ClassReplace ? null : b?.Class;
        var localClass = f.ClassAttr != 0 ? (string?)a.Attrs[f.ClassAttr].Value : null;
        d.Class = JoinClass(baseClass, localClass);
        d.ClassName = f.ClassAttr != 0 ? a.Attrs[f.ClassAttr].Name : b?.ClassName;
        d.Rules = [.. (f.ClassReplace || b is null ? [] : b.Rules), .. a.RecordedRules ?? []];
        d.Style = MergeStyle(b?.Style, f.StyleAttr != 0 ? (string?)a.Attrs[f.StyleAttr].Value : null);

        if (f.KidCount != 0) (d.KidsArena, d.FirstKid, d.KidCount) = (a, f.FirstKid, f.KidCount);
        else if (b is not null) (d.KidsArena, d.FirstKid, d.KidCount) = (b.KidsArena, b.FirstKid, b.KidCount);

        d.Actions = [.. a.RecordedActions ?? []]; // a base's actions were added when it was applied
        return d;
    }

    /// <summary>Attributes and parameters share one name space; the other kinds match by kind and name.</summary>
    internal static int Group(AttrKind kind) => kind switch
    {
        AttrKind.String or AttrKind.Object or AttrKind.Event or AttrKind.Binder or AttrKind.Param or AttrKind.ParamEvent
            or AttrKind.ParamFragment or AttrKind.ParamFragmentOf or AttrKind.ParamText => 0,
        AttrKind.ElementRef or AttrKind.ComponentRef => (int)AttrKind.ElementRef,
        AttrKind.Preset => -1,
        _ => (int)kind,
    };

    /// <summary>True when the node sets <paramref name="pa"/> itself (local beats preset). The node's merged class and style don't count.</summary>
    internal static bool IsOverridden(RenderArena a, ref NodeFrame f, in AttrFrame pa) => HasLocal(a, ref f, Group(pa.Kind), pa.Name);

    internal static bool HasLocal(RenderArena a, ref NodeFrame f, int group, string name)
    {
        var attrs = a.Attrs;
        for (var i = f.FirstAttr; i != 0; i = attrs[i].Next)
        {
            ref var at = ref attrs[i];
            if (i != f.ClassAttr && i != f.StyleAttr && at.Name == name && Group(at.Kind) == group) return true;
        }
        return false;
    }

    /// <summary>The preset's class to write at the <c>.Use</c> position: only when the node has no class of its own (else merged there).</summary>
    internal string? ClassAtMarker(RenderArena a, ref NodeFrame f) =>
        Class is not null && !f.ClassReplace && f.ClassAttr == 0 && !HasLocal(a, ref f, 0, ClassName!) ? Class : null;

    /// <summary>The value of the node's own class attribute, with the preset's classes first.</summary>
    internal string? MergedClass(ref NodeFrame f, string? local) => f.ClassReplace ? local : JoinClass(Class, local);

    internal static string? JoinClass(string? first, string? second) =>
        first is null ? second : second is null ? first : first + " " + second;

    /// <summary>
    /// <paramref name="preset"/>'s declarations whose property <paramref name="local"/> does not set, then <paramref name="local"/>'s.
    /// </summary>
    internal static string? MergeStyle(string? preset, string? local)
    {
        if (preset is null) return local;
        if (local is null) return preset;
        var kept = new System.Text.StringBuilder();
        foreach (var range in preset.AsSpan().Split(';'))
        {
            var decl = preset.AsSpan(range).Trim();
            if (decl.IsEmpty || SetsProperty(local, Property(decl))) continue;
            kept.Append(decl).Append("; ");
        }
        return kept.Append(local).ToString();
    }

    private static ReadOnlySpan<char> Property(ReadOnlySpan<char> decl)
    {
        var colon = decl.IndexOf(':');
        return (colon < 0 ? decl : decl[..colon]).Trim();
    }

    private static bool SetsProperty(string style, ReadOnlySpan<char> property)
    {
        foreach (var range in style.AsSpan().Split(';'))
            if (Property(style.AsSpan(range)).Equals(property, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}

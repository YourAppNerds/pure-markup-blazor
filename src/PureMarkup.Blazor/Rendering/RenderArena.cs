using System.Runtime.CompilerServices;

namespace PureMarkup.Blazor.Rendering;

internal enum NodeKind : byte
{
    Empty,
    Element,
    Text,
    Markup,
    Component,
    /// <summary>Children in a region at the parent's slot (or inline at the root).</summary>
    Fragment,
    /// <summary>ForEach: one region; every item uses sequence 0 and carries its key, like a Razor loop.</summary>
    Repeat,
    /// <summary>A <see cref="Microsoft.AspNetCore.Components.RenderFragment"/> written with AddContent.</summary>
    Content,
}

internal enum AttrKind : byte
{
    String,
    Object,
    /// <summary>Value is a <see cref="EventFactory"/> handler; Aux the factory.</summary>
    Event,
    PreventDefault,
    StopPropagation,
    /// <summary>Value is a boxed <c>EventCallback&lt;ChangeEventArgs&gt;</c>; Aux the attribute it updates.</summary>
    Binder,
    Multiple,
    // Component parameters
    Param,
    ParamEvent,
    /// <summary>Value is <c>Func&lt;Node&gt;</c>.</summary>
    ParamFragment,
    /// <summary>Value is <c>Func&lt;T, Node&gt;</c>; Aux the <see cref="FragmentFactory"/>.</summary>
    ParamFragmentOf,
    ParamText,
    /// <summary>Where a <c>.Use(preset)</c> writes the preset's attributes; the preset is <see cref="NodeFrame.Preset"/>.</summary>
    Preset,
    // Written after all attributes, in this order.
    ElementRef,
    ComponentRef,
    NamedEvent,
    RenderMode,
}

/// <summary>One node. Attributes are a linked list (they can be added after children are created), children a range in <see cref="RenderArena.Kids"/>.</summary>
internal struct NodeFrame
{
    public NodeKind Kind;
    public bool HasTrailing;
    public string? Name;      // tag, text or markup
    public object? Obj;       // ComponentOpener or RenderFragment
    public object? Key;
    public int FirstAttr;     // 1-based into Attrs; 0 = none
    public int LastAttr;
    public int ClassAttr;     // index of the merged class attribute (element) or class parameter (component), 0 = none
    public int StyleAttr;     // index of the merged style attribute, 0 = none
    public PresetData? Preset; // applied with .Use; merged with the local attributes when written
    public bool ClassReplace; // .ClassReplace: drop the preset's classes
    public int AttrSeq;       // next attribute sequence number (counts skipped null attributes too)
    public int FirstKid;
    public int KidCount;
}

internal struct AttrFrame
{
    public AttrKind Kind;
    public int Seq;
    public int Next;
    public string Name;
    public object? Value;
    public object? Aux;
}

/// <summary>
/// Per-render storage for nodes: struct frames in arrays reused across renders, so building a tree allocates nothing
/// once the arrays have grown. One arena per active render scope per thread; scopes nest (Static, HtmlRenderer in a render).
/// </summary>
internal sealed class RenderArena
{
    [ThreadStatic] private static RenderArena? t_current;
    [ThreadStatic] private static RenderArena? t_free;
    private static int s_scopeIds;

    // Above this many frames an arena is dropped after use instead of pooled, so one huge page does not pin memory.
    private const int MaxRetainedNodes = 1 << 16;

    internal NodeFrame[] Nodes = new NodeFrame[64];
    internal int NodeCount = 1; // index 0 is Node.Empty
    internal AttrFrame[] Attrs = new AttrFrame[64];
    internal int AttrCount = 1; // index 0 = no attribute
    internal int[] Kids = new int[64];
    internal int KidCount;
    private int[] _scratch = new int[32];
    private int _scratchTop;

    internal int ScopeId;
    internal object? Receiver;

    /// <summary>Set on an arena that records a preset (<see cref="EnterRecording"/>). Kept alive by the preset, never pooled.</summary>
    internal bool Recording;
    /// <summary>The node a preset records into; <c>.Also</c> and a nested <c>.Use</c> on it are deferred to each use.</summary>
    internal int RecordingRoot;
    /// <summary>Rules applied with <c>.Class(rule)</c> while recording (the preset's rules, for the <c>pm.preset</c> layer).</summary>
    internal List<Styling.Rule>? RecordedRules;
    /// <summary><c>.Also</c> actions recorded while recording.</summary>
    internal List<Delegate>? RecordedActions;
    private RenderArena? _parent;
    private RenderArena? _nextFree;

    internal static RenderArena Current =>
        t_current ?? throw new InvalidOperationException(
            "Markup nodes can only be created while rendering: inside Render(), a fragment lambda, or MarkupRenderer.Render.");

    internal static RenderArena Enter(object? receiver)
    {
        var a = t_free;
        if (a is null) a = new RenderArena();
        else t_free = a._nextFree;
        a._nextFree = null;
        a._parent = t_current;
        a.ScopeId = Interlocked.Increment(ref s_scopeIds);
        a.Receiver = receiver;
        t_current = a;
        return a;
    }

    /// <summary>A fresh arena for recording a preset: nested like a render scope, but owned by the preset afterwards.</summary>
    internal static RenderArena EnterRecording()
    {
        var a = new RenderArena { Recording = true, _parent = t_current, ScopeId = Interlocked.Increment(ref s_scopeIds) };
        t_current = a;
        return a;
    }

    internal static void ExitRecording(RenderArena a)
    {
        if (!ReferenceEquals(t_current, a)) throw new InvalidOperationException("Render scopes exited out of order.");
        t_current = a._parent;
        a._parent = null;
        a.ScopeId = 0; // nodes recorded here are only read through the preset from now on
    }

    internal static void Exit(RenderArena a)
    {
        if (!ReferenceEquals(t_current, a)) throw new InvalidOperationException("Render scopes exited out of order.");
        t_current = a._parent;
        a._parent = null;
        a.Receiver = null;
        a.ScopeId = 0;
        if (a.NodeCount > MaxRetainedNodes) return;
        // Clear references so a pooled arena does not keep closures (and through them, components) alive.
        Array.Clear(a.Nodes, 0, a.NodeCount);
        Array.Clear(a.Attrs, 0, a.AttrCount);
        a.NodeCount = 1;
        a.AttrCount = 1;
        a.KidCount = 0;
        a._scratchTop = 0;
        a._nextFree = t_free;
        t_free = a;
    }

    // ---- building ----

    internal Node NewNode(NodeKind kind, string? name = null, object? obj = null)
    {
        if (NodeCount == Nodes.Length) Array.Resize(ref Nodes, Nodes.Length * 2);
        var i = NodeCount++;
        ref var f = ref Nodes[i];
        f.Kind = kind;
        f.Name = name;
        f.Obj = obj;
        return new Node(i, ScopeId);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref NodeFrame Frame(Node n)
    {
        if (n.Scope != ScopeId || n.Index == 0) ThrowStale(n);
        return ref Nodes[n.Index];
    }

    internal int Check(Node n)
    {
        if (n.Index != 0 && n.Scope != ScopeId) ThrowStale(n);
        return n.Index;
    }

    private static void ThrowStale(Node n)
    {
        if (n.Index == 0) throw new InvalidOperationException("Node.Empty has no attributes or children.");
        throw new InvalidOperationException(
            "This node was created in another render. Nodes are only valid during the render that built them: " +
            "build them inside Render() (or the fragment lambda), or use Static(...) for constant content.");
    }

    /// <summary>Appends an attribute. A null value is written as nothing but still consumes a sequence number so later ones keep theirs.</summary>
    internal void AddAttr(Node n, AttrKind kind, string name, object? value, object? aux = null)
    {
        ref var f = ref Frame(n);
        var seq = f.AttrSeq++;
        // A null attribute is absent, as Razor omits it, but its frame is kept (and skipped when written) so it still
        // beats a preset's value of the same name, whichever comes first.
        Append(ref f, seq, kind, name, value, aux);
    }

    /// <summary>Adds to the element's one <c>class</c> attribute (space-separated), so strings and rules are additive.</summary>
    internal void AddClass(Node n, string? value) => AddMerged(n, AttrKind.String, "class", value, ' ', isClass: true);

    /// <summary>Adds a rule's class, remembering the rule while a preset records.</summary>
    internal void AddClass(Node n, Styling.Rule rule, bool when = true)
    {
        if (when && Recording) (RecordedRules ??= []).Add(rule);
        AddClass(n, when ? rule.ClassName : null);
    }

    /// <summary>A component's class parameter (<c>class</c> unmatched attribute or a <c>Class</c> parameter): additive like an element's.</summary>
    internal void AddParamClass(Node n, string name, string? value) => AddMerged(n, AttrKind.Param, name, value, ' ', isClass: true);

    /// <summary><c>.ClassReplace</c>: the preset's classes are dropped from this node; local classes stay additive.</summary>
    internal void ReplaceClass(Node n) => Frame(n).ClassReplace = true;

    /// <summary>Adds to the element's one <c>style</c> attribute; later declarations win, as in CSS.</summary>
    internal void AddStyle(Node n, string? value) => AddMerged(n, AttrKind.String, "style", value, ';', isClass: false);

    /// <summary>
    /// <c>.Use(preset)</c>: one attribute sequence number, like any call, so later attributes keep theirs. A later
    /// <c>.Use</c> replaces the preset but keeps the first one's position.
    /// </summary>
    internal void UsePreset(Node n, PresetData preset)
    {
        ref var f = ref Frame(n);
        var seq = f.AttrSeq++;
        if (f.Preset is null) Append(ref f, seq, AttrKind.Preset, "", null, null);
        f.Preset = preset;
        if (preset.HasTrailing) f.HasTrailing = true;
    }

    /// <summary>True when <paramref name="n"/> is the node a preset is recording into.</summary>
    internal bool IsRecordingRoot(Node n) => Recording && n.Index == RecordingRoot && n.Scope == ScopeId;

    /// <summary>Throws when a preset is recording: for calls that only make sense on a node in a render.</summary>
    internal void ThrowIfRecording(string what)
    {
        if (Recording)
            throw new InvalidOperationException($"{what} cannot be part of a preset: a preset is built once and shared. Call it where the preset is used.");
    }

    private void AddMerged(Node n, AttrKind kind, string name, string? value, char separator, bool isClass)
    {
        ref var f = ref Frame(n);
        var seq = f.AttrSeq++; // still one sequence number per call, so later attributes keep theirs
        if (string.IsNullOrEmpty(value)) return;
        var existing = isClass ? f.ClassAttr : f.StyleAttr;
        if (existing != 0)
        {
            ref var at = ref Attrs[existing];
            var old = (string)at.Value!;
            at.Value = separator == ';' && old.EndsWith(';') ? old + " " + value : old + (separator == ';' ? "; " : " ") + value;
            return;
        }
        var i = Append(ref f, seq, kind, name, value, null);
        if (isClass) f.ClassAttr = i;
        else f.StyleAttr = i;
    }

    private int Append(ref NodeFrame f, int seq, AttrKind kind, string name, object? value, object? aux)
    {
        if (AttrCount == Attrs.Length) Array.Resize(ref Attrs, Attrs.Length * 2);
        var i = AttrCount++;
        ref var a = ref Attrs[i];
        a.Kind = kind;
        a.Seq = seq;
        a.Next = 0;
        a.Name = name;
        a.Value = value;
        a.Aux = aux;
        if (f.FirstAttr == 0) f.FirstAttr = i;
        else Attrs[f.LastAttr].Next = i;
        f.LastAttr = i;
        if (kind >= AttrKind.ElementRef) f.HasTrailing = true;
        return i;
    }

    /// <summary>Skips one attribute sequence number (an attribute that is absent this render).</summary>
    internal void SkipAttr(Node n) => Frame(n).AttrSeq++;

    internal void SetKey(Node n, object? key)
    {
        if (n.Index == 0) return;
        ref var f = ref Frame(n);
        if (f.Kind is NodeKind.Element or NodeKind.Component) f.Key = key;
    }

    internal void AddKids(Node n, ReadOnlySpan<Node> kids)
    {
        ref var f = ref Frame(n);
        var old = f.KidCount;
        var count = old + kids.Length;
        // Append in place when this node's range is the last one in Kids; otherwise move it to the end.
        var start = old > 0 && f.FirstKid + old == KidCount ? f.FirstKid : KidCount;
        EnsureKids(start + count);
        if (start != f.FirstKid && old > 0) Array.Copy(Kids, f.FirstKid, Kids, start, old);
        for (var i = 0; i < kids.Length; i++) Kids[start + old + i] = Check(kids[i]);
        f.FirstKid = start;
        f.KidCount = count;
        KidCount = start + count;
    }

    internal void AddKid(Node n, Node kid) => AddKids(n, new ReadOnlySpan<Node>(in kid));

    private void EnsureKids(int size)
    {
        if (size <= Kids.Length) return;
        var len = Kids.Length * 2;
        while (len < size) len *= 2;
        Array.Resize(ref Kids, len);
    }

    // Scratch stack for ForEach: items are collected here while nested bodies create their own nodes and kids.
    internal int ScratchMark => _scratchTop;

    internal void PushScratch(int index)
    {
        if (_scratchTop == _scratch.Length) Array.Resize(ref _scratch, _scratch.Length * 2);
        _scratch[_scratchTop++] = index;
    }

    internal Node PopScratchInto(NodeKind kind, int mark)
    {
        var node = NewNode(kind);
        var count = _scratchTop - mark;
        EnsureKids(KidCount + count);
        Array.Copy(_scratch, mark, Kids, KidCount, count);
        ref var f = ref Nodes[node.Index];
        f.FirstKid = KidCount;
        f.KidCount = count;
        KidCount += count;
        _scratchTop = mark;
        return node;
    }
}

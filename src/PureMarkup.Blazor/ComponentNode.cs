using System.ComponentModel;
using IComponent = Microsoft.AspNetCore.Components.IComponent;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;
using PureMarkup.Blazor.Rendering;

namespace PureMarkup.Blazor;

/// <summary>
/// A component in the tree. Components cannot be <c>new</c>ed (the renderer creates them), so this describes one:
/// its parameters, key, content. Framework components get generated typed parameter methods
/// (<c>Comp&lt;EditForm&gt;().Model(m).OnValidSubmit(Save)</c>); they call <see cref="Param"/>, <see cref="Callback(string, Action)"/>
/// and <see cref="Fragment(string, Func{Node})"/>, which stay available for components without generated methods.
/// </summary>
public readonly struct ComponentNode<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T> where T : IComponent
{
    private readonly Node _node;

    internal ComponentNode(Node node) => _node = node;

    /// <summary>The underlying node.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Node Node => _node;

    /// <summary>Converts to a node.</summary>
    public static implicit operator Node(ComponentNode<T> c) => c._node;

    private ComponentNode<T> Add(AttrKind kind, string name, object? value, object? aux = null)
    {
        RenderArena.Current.AddAttr(_node, kind, name, value, aux);
        return this;
    }

    /// <summary>Sets a parameter by name (also for same-project <c>.razor</c> components and unmatched attributes).</summary>
    public ComponentNode<T> Param(string name, object? value) => Add(AttrKind.Param, name, value);

    /// <summary>
    /// Applies <paramref name="preset"/>: parameters merge by name (local wins in any call order), classes add up, a later
    /// <c>.Use</c> replaces an earlier one. Then runs the preset's <c>.Also</c> actions.
    /// </summary>
    public ComponentNode<T> Use(Preset<ComponentNode<T>> preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        var data = preset.Data;
        var a = RenderArena.Current;
        a.UsePreset(_node, data);
        if (data.Actions.Length == 0) return this;
        if (a.IsRecordingRoot(_node)) (a.RecordedActions ??= []).AddRange(data.Actions);
        else foreach (var action in data.Actions) ((Action<ComponentNode<T>>)action)(this);
        return this;
    }

    /// <summary>Inside a preset: runs at every <c>.Use</c> / <c>.New</c> (see <see cref="PresetExtensions.Also{T}"/>). Outside a preset it runs at once.</summary>
    public ComponentNode<T> Also(Action<ComponentNode<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var a = RenderArena.Current;
        if (a.IsRecordingRoot(_node)) (a.RecordedActions ??= []).Add(action);
        else action(this);
        return this;
    }

    /// <summary>
    /// Adds to the class parameter <paramref name="name"/> (<c>class</c> for an unmatched attribute, or a <c>Class</c> parameter):
    /// additive, like an element's class. <paramref name="replace"/> drops a preset's classes. Generated <c>.Class</c> /
    /// <c>.ClassReplace</c> methods call this.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ComponentNode<T> AddClass(string name, string? value, bool replace = false)
    {
        var a = RenderArena.Current;
        if (replace) a.ReplaceClass(_node);
        a.AddParamClass(_node, name, value);
        return this;
    }

    /// <inheritdoc cref="AddClass(string, string?, bool)"/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ComponentNode<T> AddClass(string name, Styling.Rule rule, bool replace = false)
    {
        ArgumentNullException.ThrowIfNull(rule);
        var a = RenderArena.Current;
        if (replace) a.ReplaceClass(_node);
        if (a.Recording) (a.RecordedRules ??= []).Add(rule);
        a.AddParamClass(_node, name, a.RuleClass(rule));
        return this;
    }

    /// <summary>Razor's <c>@key</c>.</summary>
    public ComponentNode<T> Key(object? key)
    {
        RenderArena.Current.SetKey(_node, key);
        return this;
    }

    /// <summary>Razor's <c>@ref</c>.</summary>
    public ComponentNode<T> Ref(Action<T> capture) => Add(AttrKind.ComponentRef, "", (Action<object>)(c => capture((T)c)));

    /// <summary>Razor's <c>@rendermode</c> on a component instance.</summary>
    public ComponentNode<T> RenderMode(IComponentRenderMode? mode) => Add(AttrKind.RenderMode, "", mode);

    /// <summary>Razor's <c>@attributes</c> on a component (needs <c>CaptureUnmatchedValues</c>).</summary>
    public ComponentNode<T> Attrs(IEnumerable<KeyValuePair<string, object>>? attributes) => Add(AttrKind.Multiple, "", attributes);

    /// <summary><c>ChildContent</c>, built when the component renders it (so it runs inside ErrorBoundary, AuthorizeView, ...).</summary>
    public ComponentNode<T> Children(Func<Node> content) => Add(AttrKind.ParamFragment, "ChildContent", content);

    /// <summary><c>ChildContent</c> that is plain text.</summary>
    public ComponentNode<T> Children(string? text) => Add(AttrKind.ParamText, "ChildContent", text);

    /// <summary><c>ChildContent</c> as a <see cref="RenderFragment{TContext}"/>, e.g. <c>EditForm</c>'s <c>EditContext</c>.</summary>
    public ComponentNode<T> ChildContent<TContext>(Func<TContext, Node> content) =>
        Add(AttrKind.ParamFragmentOf, "ChildContent", content, FragmentFactory<TContext>.Instance);

    /// <summary>A named <see cref="RenderFragment"/> parameter.</summary>
    public ComponentNode<T> Fragment(string name, Func<Node> content) => Add(AttrKind.ParamFragment, name, content);

    /// <summary>A named <see cref="RenderFragment"/> parameter that is plain text.</summary>
    public ComponentNode<T> Fragment(string name, string? text) => Add(AttrKind.ParamText, name, text);

    /// <summary>A named <see cref="RenderFragment{TContext}"/> parameter, e.g. <c>Router.Found</c>.</summary>
    public ComponentNode<T> Fragment<TContext>(string name, Func<TContext, Node> content) =>
        Add(AttrKind.ParamFragmentOf, name, content, FragmentFactory<TContext>.Instance);

    /// <summary>An <see cref="EventCallback"/> parameter; the receiver is the component that renders this markup.</summary>
    public ComponentNode<T> Callback(string name, Action handler) => Add(AttrKind.ParamEvent, name, handler, EventFactory0.Instance);

    /// <inheritdoc cref="Callback(string, Action)"/>
    public ComponentNode<T> Callback(string name, Func<Task> handler) => Add(AttrKind.ParamEvent, name, handler, EventFactory0.Instance);

    /// <summary>An <see cref="EventCallback{TArg}"/> parameter.</summary>
    public ComponentNode<T> Callback<TArg>(string name, Action<TArg> handler) => Add(AttrKind.ParamEvent, name, handler, EventFactory<TArg>.Instance);

    /// <inheritdoc cref="Callback{TArg}(string, Action{TArg})"/>
    public ComponentNode<T> Callback<TArg>(string name, Func<TArg, Task> handler) => Add(AttrKind.ParamEvent, name, handler, EventFactory<TArg>.Instance);

    /// <inheritdoc cref="Callback{TArg}(string, Action{TArg})"/>
    public ComponentNode<T> Callback<TArg>(string name, Action handler) => Add(AttrKind.ParamEvent, name, handler, EventFactory<TArg>.Instance);

    /// <inheritdoc cref="Callback{TArg}(string, Action{TArg})"/>
    public ComponentNode<T> Callback<TArg>(string name, Func<Task> handler) => Add(AttrKind.ParamEvent, name, handler, EventFactory<TArg>.Instance);
}

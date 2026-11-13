using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using PureMarkup.Blazor.Rendering;

namespace PureMarkup.Blazor;

/// <summary>
/// Markup factories. Add <c>global using static PureMarkup.Blazor.H;</c> and write <c>Div().Class("x").Children(...)</c>.
/// Element factories (<c>Div()</c>, <c>Input()</c>, ...) are generated from <c>Html/html.data</c>.
/// </summary>
public static partial class H
{
    /// <summary>An element with any tag name, e.g. a custom element.</summary>
    public static Element El(string tag) => new(RenderArena.Current.NewNode(NodeKind.Element, tag));

    /// <summary>A text node (HTML-encoded).</summary>
    public static Node Text(string? text) => RenderArena.Current.NewNode(NodeKind.Text, text);

    /// <summary>A text node from a number, formatted as Razor's <c>@value</c> would (current culture).</summary>
    public static Node Text(int value) => Text(value.ToString(CultureInfo.CurrentCulture));

    /// <summary>A text node from any value's <c>ToString()</c>, as Razor's <c>@value</c>.</summary>
    public static Node Text(object? value) => Text(value?.ToString());

    /// <summary>Raw HTML, written as one markup frame. Not encoded: never pass user input.</summary>
    /// <remarks>Also the hook for the planned generator pass that folds constant subtrees into markup at compile time.</remarks>
    public static Node Markup(string? html) => RenderArena.Current.NewNode(NodeKind.Markup, html);

    /// <summary><c>&lt;!DOCTYPE html&gt;</c>, for a host page written as a component.</summary>
    public static Node Doctype() => Markup("<!DOCTYPE html>\n");

    /// <summary>Several nodes in one slot (a region).</summary>
    public static Node Fragment(params ReadOnlySpan<Node> children)
    {
        var a = RenderArena.Current;
        var n = a.NewNode(NodeKind.Fragment);
        a.AddKids(n, children);
        return n;
    }

    /// <summary>Renders a <see cref="RenderFragment"/>, e.g. a layout's <c>Body</c> or a <c>ChildContent</c> parameter.</summary>
    public static Node Content(RenderFragment? fragment) =>
        fragment is null ? Node.Empty : RenderArena.Current.NewNode(NodeKind.Content, obj: fragment);

    /// <summary>Renders a <see cref="RenderFragment{T}"/> with an argument.</summary>
    public static Node Content<T>(RenderFragment<T>? fragment, T arg) => Content(fragment?.Invoke(arg));

    /// <summary>
    /// <paramref name="then"/> when <paramref name="condition"/> holds, otherwise nothing. Either way the slot is kept,
    /// so later siblings keep their sequence numbers. The lambda only runs when the condition holds.
    /// </summary>
    public static Node If(bool condition, Func<Node> then) => condition ? then() : Node.Empty;

    /// <summary><paramref name="then"/> or <paramref name="else"/>, in one slot.</summary>
    public static Node If(bool condition, Func<Node> then, Func<Node> @else) => condition ? then() : @else();

    /// <summary>
    /// One item per element of <paramref name="items"/>, each keyed with <paramref name="key"/> (Razor's <c>@key</c>).
    /// Takes one slot; items share one sequence number inside it, as a Razor loop does. The key goes on the item's
    /// top element or component.
    /// </summary>
    public static Node ForEach<T>(IEnumerable<T> items, Func<T, object?> key, Func<T, Node> body)
    {
        var a = RenderArena.Current;
        var mark = a.ScratchMark;
        if (items is IReadOnlyList<T> list)
        {
            for (var i = 0; i < list.Count; i++) Add(a, list[i], key, body);
        }
        else
        {
            foreach (var item in items) Add(a, item, key, body);
        }
        return a.PopScratchInto(NodeKind.Repeat, mark);

        static void Add(RenderArena a, T item, Func<T, object?> key, Func<T, Node> body)
        {
            var n = body(item);
            a.SetKey(n, key(item));
            a.PushScratch(a.Check(n));
        }
    }

    /// <summary>One item per element, without keys: for lists that never reorder.</summary>
    public static Node ForEach<T>(IEnumerable<T> items, Func<T, Node> body)
    {
        var a = RenderArena.Current;
        var mark = a.ScratchMark;
        foreach (var item in items) a.PushScratch(a.Check(body(item)));
        return a.PopScratchInto(NodeKind.Repeat, mark);
    }

    /// <summary>A component. Set parameters with <c>.Param</c>, content with <c>.Children</c>.</summary>
    public static ComponentNode<T> Comp<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>() where T : IComponent =>
        new(RenderArena.Current.NewNode(NodeKind.Component, obj: ComponentOpener<T>.Instance));

    private static readonly ConcurrentDictionary<Func<Node>, string> StaticCache = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Content that never changes, rendered to HTML once per call site and written as one markup frame afterwards.
    /// Pass a <c>static</c> lambda: the compiler caches it, so the delegate identifies the call site. Elements, text and
    /// fragments only (no events or components).
    /// </summary>
    public static Node Static(Func<Node> build)
    {
        if (!StaticCache.TryGetValue(build, out var html))
        {
            // A lambda that captures state is a new delegate every render, so it would never hit the cache.
            if (build.Target is { } t && t.GetType().Name != "<>c")
                throw new ArgumentException("Static(...) needs a lambda that captures nothing; mark it 'static'.", nameof(build));
            html = StaticCache.GetOrAdd(build, static b => StaticHtml.Render(b));
        }
        return Markup(html);
    }
}

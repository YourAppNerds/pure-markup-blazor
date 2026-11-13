using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using PureMarkup.Blazor.Rendering;

namespace PureMarkup.Blazor;

/// <summary>
/// Methods every element has. Attribute and event methods (<c>.Class</c>, <c>.Href</c>, <c>.OnClick</c>, ...) are generated
/// into <c>HtmlAttributes</c> and <c>HtmlEvents</c>. All of them can be called in any order.
/// </summary>
public static class ElementExtensions
{
    /// <summary>Any attribute. <see langword="null"/> omits it.</summary>
    public static T Attr<T>(this T el, string name, string? value) where T : struct, IElement
    {
        RenderArena.Current.AddAttr(el.Node, AttrKind.String, name, value);
        return el;
    }

    /// <summary>Any attribute. <see langword="null"/> or <see langword="false"/> omits it; <see langword="true"/> writes it bare.</summary>
    public static T Attr<T>(this T el, string name, object? value) where T : struct, IElement
    {
        RenderArena.Current.AddAttr(el.Node, AttrKind.Object, name, value);
        return el;
    }

    /// <summary>A boolean attribute: written bare when <paramref name="value"/> is true, omitted otherwise.</summary>
    public static T Attr<T>(this T el, string name, bool value) where T : struct, IElement =>
        el.Attr(name, value ? BoxedTrue : null);

    /// <summary><c>data-<paramref name="name"/></c>.</summary>
    public static T Data<T>(this T el, string name, object? value) where T : struct, IElement => el.Attr("data-" + name, value);

    /// <summary><c>aria-<paramref name="name"/></c>.</summary>
    public static T Aria<T>(this T el, string name, string? value) where T : struct, IElement => el.Attr("aria-" + name, value);

    /// <summary>Razor's <c>@attributes</c>: splats a dictionary of attributes.</summary>
    public static T Attrs<T>(this T el, IEnumerable<KeyValuePair<string, object>>? attributes) where T : struct, IElement
    {
        RenderArena.Current.AddAttr(el.Node, AttrKind.Multiple, "", attributes);
        return el;
    }

    /// <summary>Razor's <c>@key</c>.</summary>
    public static T Key<T>(this T el, object? key) where T : struct, IElement
    {
        RenderArena.Current.SetKey(el.Node, key);
        return el;
    }

    /// <summary>Razor's <c>@ref</c>: receives the <see cref="ElementReference"/> after render.</summary>
    public static T Ref<T>(this T el, Action<ElementReference> capture) where T : struct, IElement
    {
        RenderArena.Current.AddAttr(el.Node, AttrKind.ElementRef, "", capture);
        return el;
    }

    /// <summary>Child nodes. Strings become text. Calling it again appends.</summary>
    public static T Children<T>(this T el, params ReadOnlySpan<Node> children) where T : struct, IElement
    {
        RenderArena.Current.AddKids(el.Node, children);
        return el;
    }

    /// <summary>A text child.</summary>
    public static T Text<T>(this T el, string? text) where T : struct, IElement
    {
        var a = RenderArena.Current;
        a.AddKid(el.Node, a.NewNode(NodeKind.Text, text));
        return el;
    }

    /// <summary>A text child from a number (current culture, as Razor).</summary>
    public static T Text<T>(this T el, int value) where T : struct, IElement => el.Text(value.ToString(CultureInfo.CurrentCulture));

    /// <summary>A text child from any value's <c>ToString()</c>.</summary>
    public static T Text<T>(this T el, object? value) where T : struct, IElement => el.Text(value?.ToString());

    /// <summary>Raw HTML child. Not encoded.</summary>
    public static T Html<T>(this T el, string? html) where T : struct, IElement
    {
        var a = RenderArena.Current;
        a.AddKid(el.Node, a.NewNode(NodeKind.Markup, html));
        return el;
    }

    /// <summary>Handles any DOM event with a Blazor-registered event args type (generated <c>OnClick</c> etc. call this).</summary>
    public static T On<T, TArgs>(this T el, string eventName, Action handler) where T : struct, IElement => el.OnCore<T, TArgs>(eventName, handler);

    /// <inheritdoc cref="On{T, TArgs}(T, string, Action)"/>
    public static T On<T, TArgs>(this T el, string eventName, Action<TArgs> handler) where T : struct, IElement => el.OnCore<T, TArgs>(eventName, handler);

    /// <inheritdoc cref="On{T, TArgs}(T, string, Action)"/>
    public static T On<T, TArgs>(this T el, string eventName, Func<Task> handler) where T : struct, IElement => el.OnCore<T, TArgs>(eventName, handler);

    /// <inheritdoc cref="On{T, TArgs}(T, string, Action)"/>
    public static T On<T, TArgs>(this T el, string eventName, Func<TArgs, Task> handler) where T : struct, IElement => el.OnCore<T, TArgs>(eventName, handler);

    internal static T OnCore<T, TArgs>(this T el, string eventName, object handler) where T : struct, IElement
    {
        RenderArena.Current.AddAttr(el.Node, AttrKind.Event, eventName, handler, EventFactory<TArgs>.Instance);
        return el;
    }

    /// <summary>Razor's <c>@onclick:preventDefault</c>; pass the event name, e.g. <c>"onsubmit"</c>.</summary>
    public static T PreventDefault<T>(this T el, string eventName, bool value = true) where T : struct, IElement
    {
        RenderArena.Current.AddAttr(el.Node, AttrKind.PreventDefault, eventName, value ? BoxedTrue : BoxedFalse);
        return el;
    }

    /// <summary>Razor's <c>@onclick:stopPropagation</c>.</summary>
    public static T StopPropagation<T>(this T el, string eventName, bool value = true) where T : struct, IElement
    {
        RenderArena.Current.AddAttr(el.Node, AttrKind.StopPropagation, eventName, value ? BoxedTrue : BoxedFalse);
        return el;
    }

    /// <summary>Razor's <c>@formname</c>: names a plain <c>&lt;form&gt;</c> for static SSR posts.</summary>
    public static T FormName<T>(this T el, string name) where T : struct, IElement
    {
        RenderArena.Current.AddAttr(el.Node, AttrKind.NamedEvent, "onsubmit", name);
        return el;
    }

    /// <summary>
    /// Razor's <c>@bind</c> on an element: <c>value</c> (or <c>checked</c> for <see cref="bool"/>) plus <c>onchange</c>.
    /// </summary>
    public static T Bind<T, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>(this T el, Func<TValue> get, Action<TValue> set) where T : struct, IElement =>
        el.BindOn("onchange", get, set);

    /// <summary>Razor's <c>@bind:event</c>, e.g. <c>.BindOn("oninput", ...)</c> to update on every keystroke.</summary>
    public static T BindOn<T, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>(this T el, string eventName, Func<TValue> get, Action<TValue> set) where T : struct, IElement
    {
        var a = RenderArena.Current;
        a.ThrowIfRecording("Bind");
        var value = get();
        var receiver = a.Receiver ?? throw new InvalidOperationException("Bind needs a receiving component.");
        var attr = typeof(TValue) == typeof(bool) ? "checked" : "value";
        // Same calls Razor emits for @bind: BindConverter.FormatValue and EventCallback.Factory.CreateBinder.
        a.AddAttr(el.Node, AttrKind.Object, attr, typeof(TValue) == typeof(bool) ? value : BindConverter.FormatValue(value));
        var binder = EventCallback.Factory.CreateBinder(receiver, set, value);
        a.AddAttr(el.Node, AttrKind.Binder, eventName, binder, attr);
        return el;
    }

    internal static readonly object BoxedTrue = true;
    internal static readonly object BoxedFalse = false;
}

using PureMarkup.Blazor.Rendering;
using PureMarkup.Blazor.Styling;

namespace PureMarkup.Blazor;

/// <summary>Styling methods every element has. Classes and inline styles are additive: each call adds to the attribute.</summary>
public static class StyleExtensions
{
    /// <summary>Adds the rule's class. Costs one string at render: the rule's CSS is in the build-time stylesheet.</summary>
    public static T Class<T>(this T el, Rule rule) where T : struct, IElement
    {
        RenderArena.Current.AddClass(el.Node, rule);
        return el;
    }

    /// <summary>Adds the rule's class when <paramref name="when"/> holds.</summary>
    public static T Class<T>(this T el, Rule rule, bool when) where T : struct, IElement
    {
        RenderArena.Current.AddClass(el.Node, rule, when);
        return el;
    }

    /// <summary>
    /// Typed inline declarations for per-instance values: <c>.Style(s => s.Width(Percent(p)))</c>. Prefer a <see cref="Var{T}"/>
    /// with <see cref="Set"/> so states and media queries in the rule still apply.
    /// </summary>
    public static T Style<T>(this T el, Action<StyleBuilder> build) where T : struct, IElement
    {
        RenderArena.Current.AddStyle(el.Node, StyleBuilder.Build(build));
        return el;
    }

    /// <summary>Sets a custom property inline: <c>style="--CardCss_Progress-x1y2z3: 40%"</c>.</summary>
    public static T Set<T, TValue>(this T el, Var<TValue> variable, TValue value) where T : struct, IElement where TValue : ICssValue<TValue>
    {
        RenderArena.Current.AddStyle(el.Node, variable.Name + ": " + value.ToString());
        return el;
    }
}

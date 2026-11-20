using PureMarkup.Blazor.Rendering;
using PureMarkup.Blazor.Styling;

namespace PureMarkup.Blazor.Styling
{
    /// <summary>The color scheme an element and its descendants use for <see cref="Token{T}"/> values.</summary>
    public enum ThemeMode
    {
        /// <summary>Follow <c>prefers-color-scheme</c> (or the nearest themed ancestor). Writes no class.</summary>
        Auto,
        /// <summary>Light token values: class <c>pm-light</c>.</summary>
        Light,
        /// <summary>Dark token values: class <c>pm-dark</c>.</summary>
        Dark,
    }

    /// <summary>
    /// The theme classes the generated stylesheet defines. Any element may carry one; its subtree then uses that scheme's
    /// token values, whatever <c>prefers-color-scheme</c> says. Toggle one on <c>&lt;html&gt;</c> from JS
    /// (<c>document.documentElement.classList.toggle('pm-dark')</c>) to switch themes with no re-render.
    /// </summary>
    public static class ThemeClass
    {
        /// <summary><c>pm-dark</c></summary>
        public const string Dark = "pm-dark";

        /// <summary><c>pm-light</c></summary>
        public const string Light = "pm-light";
    }
}

namespace PureMarkup.Blazor
{
    /// <summary>Theme selection on any element.</summary>
    public static class ThemeExtensions
    {
        /// <summary>
        /// Pins the token color scheme of this element and its subtree: <c>Html().Theme(ThemeMode.Dark)</c>, or a dark panel
        /// in a light page. <see cref="ThemeMode.Auto"/> writes nothing. Additive with the element's other classes.
        /// </summary>
        public static T Theme<T>(this T el, ThemeMode mode) where T : struct, IElement
        {
            RenderArena.Current.AddClass(el.Node, mode switch
            {
                ThemeMode.Dark => ThemeClass.Dark,
                ThemeMode.Light => ThemeClass.Light,
                _ => null,
            });
            return el;
        }
    }
}

using System.ComponentModel;

namespace PureMarkup.Blazor.Styling;

/// <summary>
/// Marks a static class whose static <see cref="Rule"/>, <see cref="Var{T}"/>, <see cref="Token{T}"/>, global, font-face and
/// layer-import members the build writes to <c>{Assembly}.styles.css</c>. Members must be pure: no services, no JS.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class StylesheetAttribute : Attribute;

/// <summary>Set by the source generator on assemblies that contain <c>[Stylesheet]</c> types; <c>StyleLink()</c> links their files.</summary>
[AttributeUsage(AttributeTargets.Assembly)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class HasStylesheetsAttribute : Attribute;

/// <summary>The cascade layers of the generated file, in order. A later layer beats an earlier one whatever the specificity.</summary>
public enum CssLayer
{
    /// <summary><c>reset</c></summary>
    Reset,
    /// <summary><c>vendor</c>: third-party CSS imported with <see cref="Css.ImportLayer"/>.</summary>
    Vendor,
    /// <summary><c>pm.base</c>: tokens, variables and globals.</summary>
    Base,
    /// <summary><c>pm.preset</c></summary>
    Preset,
    /// <summary><c>pm.local</c>: rules applied with <c>.Class(rule)</c>.</summary>
    Local,
    /// <summary><c>pm.preset-state</c></summary>
    PresetState,
    /// <summary><c>pm.local-state</c>: state blocks (<c>.Hover</c>, <c>.Disabled</c>, <c>.When</c>, ...) of local rules.</summary>
    LocalState,
    /// <summary><c>utilities</c>: room for utility CSS such as Tailwind.</summary>
    Utilities,
}

/// <summary>A named class whose declarations the build writes to the stylesheet. Apply it with <c>.Class(rule)</c>.</summary>
public sealed class Rule
{
    private readonly string? _name;
    internal readonly Action<RuleBuilder> Build;

    internal Rule(string? name, Action<RuleBuilder> build)
    {
        _name = name;
        Build = build;
    }

    /// <summary>The class name, a compile-time constant (<c>{Type}_{Member}-{hash}</c>).</summary>
    public string ClassName => _name ?? throw new InvalidOperationException(
        "This Rule has no name. Declare it as a static property or field initializer, `= Css.Rule(...)`, in a [Stylesheet] class " +
        "and reference PureMarkup.Blazor.Generators as an analyzer, with PureMarkup.Blazor.Generated in InterceptorsNamespaces.");

    /// <summary><c>.{ClassName}</c></summary>
    public string Selector => "." + ClassName;

    /// <inheritdoc/>
    public override string ToString() => ClassName;
}

/// <summary>A CSS custom property. Use it as a value (<c>var(--name)</c>); set it per element with <c>.Set(var, value)</c>.</summary>
public class Var<T> : ICssVariable where T : ICssValue<T>
{
    private readonly string? _name;

    internal Var(string? name, CssValue? initial)
    {
        _name = name;
        Initial = initial;
    }

    /// <summary>The value written to <c>:root</c>, if any.</summary>
    public CssValue? Initial { get; }

    CssValue? ICssVariable.Dark => (this as Token<T>)?.Dark;

    /// <summary>The property name, including <c>--</c>.</summary>
    public string Name => _name ?? throw new InvalidOperationException(
        "This Var has no name. Declare it as a static property or field initializer, `= Css.Var<T>(...)`, in a [Stylesheet] class.");

    /// <summary><c>var(--name)</c></summary>
    public string Reference => "var(" + Name + ")";

    /// <summary>The variable as a typed value.</summary>
    public static implicit operator T(Var<T> v) => T.FromCss(v.Reference);

    /// <summary>The variable as any value.</summary>
    public static implicit operator CssValue(Var<T> v) => new(v.Reference);

    /// <inheritdoc/>
    public override string ToString() => Reference;
}

internal interface ICssVariable
{
    string Name { get; }
    CssValue? Initial { get; }
    CssValue? Dark { get; }
}

/// <summary>A design token: a custom property with a fixed name and a light and dark value.</summary>
public sealed class Token<T> : Var<T> where T : ICssValue<T>
{
    internal Token(string name, CssValue light, CssValue? dark) : base(name, light) => Dark = dark;

    /// <summary>The value under <c>prefers-color-scheme: dark</c> or <c>.theme-dark</c>, if different.</summary>
    public CssValue? Dark { get; }
}

/// <summary>Declarations for a plain selector (<see cref="Css.Global"/>).</summary>
public sealed class GlobalRule
{
    internal GlobalRule(string selector, CssLayer layer, Action<RuleBuilder> build) => (Selector, Layer, Build) = (selector, layer, build);
    internal string Selector { get; }
    internal CssLayer Layer { get; }
    internal Action<RuleBuilder> Build { get; }
}

/// <summary>An <c>@font-face</c> block (<see cref="Css.FontFace"/>).</summary>
public sealed class FontFace
{
    internal FontFace(Action<RuleBuilder> build) => Build = build;
    internal Action<RuleBuilder> Build { get; }
}

/// <summary>An <c>@import url(...) layer(...)</c> (<see cref="Css.ImportLayer"/>).</summary>
public sealed class LayerImport
{
    internal LayerImport(string layer, string url) => (Layer, Url) = (layer, url);
    internal string Layer { get; }
    internal string Url { get; }
}

/// <summary>Factories for stylesheet members. Use them as initializers of static members of a <c>[Stylesheet]</c> class.</summary>
public static class Css
{
    /// <summary>
    /// A rule. Its name comes from the member it initializes (the generator intercepts this call); the body runs only at build
    /// time, when the stylesheet is written, never while rendering.
    /// </summary>
    public static Rule Rule(Action<RuleBuilder> build) => new(null, build);

    /// <summary>Used by the generated interceptor of <see cref="Rule(Action{RuleBuilder})"/>.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static Rule NamedRule(string className, Action<RuleBuilder> build) => new(className, build);

    /// <summary>A custom property with an initial value on <c>:root</c>. Named like a rule.</summary>
    public static Var<T> Var<T>(T initial) where T : ICssValue<T> => new(null, Value(initial));

    /// <summary>A custom property with no initial value.</summary>
    public static Var<T> Var<T>() where T : ICssValue<T> => new(null, null);

    /// <summary>Used by the generated interceptors of <c>Var</c>.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static Var<T> NamedVar<T>(string name, T initial) where T : ICssValue<T> => new(name, Value(initial));

    /// <summary>Used by the generated interceptors of <c>Var</c>.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static Var<T> NamedVar<T>(string name) where T : ICssValue<T> => new(name, null);

    /// <summary>A token <c>--<paramref name="name"/></c> with one value for both color schemes.</summary>
    public static Token<T> Token<T>(string name, T value) where T : ICssValue<T> => new(TokenName(name), Value(value), null);

    /// <summary>
    /// A token <c>--<paramref name="name"/></c> with a light and a dark value. The dark value applies under
    /// <c>prefers-color-scheme: dark</c> unless <c>&lt;html&gt;</c> has <c>theme-light</c>, and always under <c>theme-dark</c>.
    /// </summary>
    public static Token<T> Token<T>(string name, T light, T dark) where T : ICssValue<T> => new(TokenName(name), Value(light), Value(dark));

    /// <summary>Declarations for any selector, e.g. <c>Css.Global("body", g => g.Margin(0))</c>. Defaults to <c>pm.base</c>.</summary>
    public static GlobalRule Global(string selector, Action<RuleBuilder> build, CssLayer layer = CssLayer.Base) => new(selector, layer, build);

    /// <summary>An <c>@font-face</c>: <c>Css.FontFace(f => f.FontFamily("Inter").Prop("src", "url(/inter.woff2)"))</c>.</summary>
    public static FontFace FontFace(Action<RuleBuilder> build) => new(build);

    /// <summary>
    /// Imports a CSS file into a layer, usually <c>vendor</c>: unlayered CSS would beat every layer.
    /// <c>Css.ImportLayer("vendor", "_content/MudBlazor/MudBlazor.min.css")</c>.
    /// </summary>
    public static LayerImport ImportLayer(string layer, string url) => new(layer, url);

    private static CssValue Value<T>(T v) where T : ICssValue<T> => new(v.ToString() ?? "");

    private static string TokenName(string name) => name.StartsWith("--", StringComparison.Ordinal) ? name : "--" + name;
}

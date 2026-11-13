using System.Text;

namespace PureMarkup.Blazor.Styling;

/// <summary>
/// The typed property surface shared by rules and inline styles. One method per CSS property (generated from
/// <c>Css/css.data</c>, w3c/webref), plus <see cref="Prop(string, CssValue)"/> for anything else, e.g. vendor prefixes.
/// </summary>
public abstract partial class CssProperties<TSelf> where TSelf : CssProperties<TSelf>
{
    private protected CssProperties() { }

    private protected abstract void AddDeclaration(string name, string value);

    /// <summary>Any property by name: <c>.Prop("-webkit-line-clamp", 3)</c>.</summary>
    public TSelf Prop(string name, CssValue value)
    {
        AddDeclaration(name, value.ToString());
        return (TSelf)this;
    }

    /// <summary>Space-separated parts, for shorthands: <c>.Prop("border", Px(1), "solid", color)</c>.</summary>
    public TSelf Prop(string name, CssValue first, CssValue second, params ReadOnlySpan<CssValue> rest) =>
        Prop(name, new CssValue(Join(first, second, rest)));

    /// <summary>Sets a custom property: <c>.Set(CardCss.Progress, Percent(40))</c>.</summary>
    public TSelf Set<T>(Var<T> variable, T value) where T : ICssValue<T> => Prop(variable.Name, value.ToString() ?? "");

    private protected static string Join(CssValue first, CssValue second, ReadOnlySpan<CssValue> rest)
    {
        var sb = new StringBuilder().Append(first.ToString()).Append(' ').Append(second.ToString());
        foreach (var v in rest) sb.Append(' ').Append(v.ToString());
        return sb.ToString();
    }
}

/// <summary>A state for <see cref="RuleBuilder.When"/>: a pseudo-class on the parent rule's element.</summary>
public readonly struct State(string pseudo)
{
    internal string Pseudo { get; } = pseudo;
    /// <summary><c>:hover</c></summary>
    public static State Hover => new(":hover");
    /// <summary><c>:active</c></summary>
    public static State Active => new(":active");
    /// <summary><c>:focus</c></summary>
    public static State Focus => new(":focus");
    /// <summary><c>:focus-visible</c></summary>
    public static State FocusVisible => new(":focus-visible");
    /// <summary><c>:focus-within</c></summary>
    public static State FocusWithin => new(":focus-within");
    /// <summary><c>:disabled</c></summary>
    public static State Disabled => new(":disabled");
    /// <summary><c>:checked</c></summary>
    public static State Checked => new(":checked");
    /// <summary>Any pseudo-class, e.g. <c>":open"</c>.</summary>
    public static implicit operator State(string pseudo) => new(pseudo);
}

/// <summary>A media query: <c>Media.MinWidth(Px(640))</c>, <c>Media.PrefersReducedMotion</c>, or any string.</summary>
public readonly struct Media(string query)
{
    internal string Query { get; } = query;
    /// <summary><c>(min-width: x)</c></summary>
    public static Media MinWidth(Length width) => new($"(min-width: {width})");
    /// <summary><c>(max-width: x)</c></summary>
    public static Media MaxWidth(Length width) => new($"(max-width: {width})");
    /// <summary><c>(prefers-reduced-motion: reduce)</c></summary>
    public static Media PrefersReducedMotion => new("(prefers-reduced-motion: reduce)");
    /// <summary><c>(prefers-color-scheme: dark)</c></summary>
    public static Media PrefersDark => new("(prefers-color-scheme: dark)");
    /// <summary><c>(hover: hover)</c>: devices with a real hover.</summary>
    public static Media CanHover => new("(hover: hover)");
    /// <summary><c>print</c></summary>
    public static Media Print => new("print");
    /// <summary>Both queries.</summary>
    public Media And(Media other) => new(Query + " and " + other.Query);
    /// <summary>Any query text.</summary>
    public static implicit operator Media(string query) => new(query);
    /// <inheritdoc/>
    public override string ToString() => Query;
}

/// <summary>
/// The body of a <see cref="Rule"/> or <see cref="GlobalRule"/>: declarations plus nested selectors, states and queries.
/// Runs at build time only. State blocks go to the state layer above the rule's own layer, so any state beats any base value.
/// </summary>
public sealed partial class RuleBuilder : CssProperties<RuleBuilder>
{
    internal readonly List<KeyValuePair<string, string>> Declarations = [];
    internal readonly List<Nested> Children = [];

    /// <param name="Pattern">Selector with <c>&amp;</c> for the current one; null keeps it.</param>
    /// <param name="State">True for state blocks.</param>
    /// <param name="AtRule">Wrapping at-rule, e.g. <c>@media (...)</c>.</param>
    /// <param name="Body">Declarations and further nesting.</param>
    internal sealed record Nested(string? Pattern, bool State, string? AtRule, RuleBuilder Body);

    internal RuleBuilder() { }

    private protected override void AddDeclaration(string name, string value) => Declarations.Add(new(name, value));

    private RuleBuilder Add(string? pattern, bool state, string? atRule, Action<RuleBuilder> build)
    {
        var body = new RuleBuilder();
        build(body);
        Children.Add(new Nested(pattern, state, atRule, body));
        return this;
    }

    /// <summary><c>:hover</c> (state).</summary>
    public RuleBuilder Hover(Action<RuleBuilder> build) => Add("&:hover", true, null, build);
    /// <summary><c>:active</c> (state).</summary>
    public RuleBuilder Active(Action<RuleBuilder> build) => Add("&:active", true, null, build);
    /// <summary><c>:focus</c> (state).</summary>
    public RuleBuilder Focus(Action<RuleBuilder> build) => Add("&:focus", true, null, build);
    /// <summary><c>:focus-visible</c> (state).</summary>
    public RuleBuilder FocusVisible(Action<RuleBuilder> build) => Add("&:focus-visible", true, null, build);
    /// <summary><c>:focus-within</c> (state).</summary>
    public RuleBuilder FocusWithin(Action<RuleBuilder> build) => Add("&:focus-within", true, null, build);
    /// <summary><c>:disabled</c> (state).</summary>
    public RuleBuilder Disabled(Action<RuleBuilder> build) => Add("&:disabled", true, null, build);
    /// <summary><c>:checked</c> (state).</summary>
    public RuleBuilder Checked(Action<RuleBuilder> build) => Add("&:checked", true, null, build);

    /// <summary>
    /// Styles this element from a state of an ancestor with <paramref name="parent"/>: <c>.parent:hover .this</c> (state).
    /// </summary>
    public RuleBuilder When(Rule parent, State state, Action<RuleBuilder> build) => Add(parent.Selector + state.Pseudo + " &", true, null, build);

    /// <summary><c>::before</c></summary>
    public RuleBuilder Before(Action<RuleBuilder> build) => Add("&::before", false, null, build);
    /// <summary><c>::after</c></summary>
    public RuleBuilder After(Action<RuleBuilder> build) => Add("&::after", false, null, build);
    /// <summary><c>::placeholder</c></summary>
    public RuleBuilder Placeholder(Action<RuleBuilder> build) => Add("&::placeholder", false, null, build);

    /// <summary>
    /// Any pseudo-class or pseudo-element appended to the selector: <c>.Is(":nth-child(2n)", ...)</c>. Pass
    /// <paramref name="state"/> to put it in the state layer.
    /// </summary>
    public RuleBuilder Is(string pseudo, Action<RuleBuilder> build, bool state = false) => Add("&" + pseudo, state, null, build);

    /// <summary>A related selector; <c>&amp;</c> stands for this one: <c>.Nest("&amp; > h2", ...)</c>. Without <c>&amp;</c> it is a descendant.</summary>
    public RuleBuilder Nest(string selector, Action<RuleBuilder> build) =>
        Add(selector.Contains('&') ? selector : "& " + selector, false, null, build);

    /// <summary><c>@media</c></summary>
    public RuleBuilder Media(Media query, Action<RuleBuilder> build) => Add(null, false, "@media " + query.Query, build);
    /// <summary><c>@container</c>, e.g. <c>"(min-width: 400px)"</c> or <c>"card (min-width: 400px)"</c>.</summary>
    public RuleBuilder Container(string query, Action<RuleBuilder> build) => Add(null, false, "@container " + query, build);
    /// <summary><c>@supports</c></summary>
    public RuleBuilder Supports(string condition, Action<RuleBuilder> build) => Add(null, false, "@supports " + condition, build);
}

/// <summary>Inline declarations for <c>.Style(s => ...)</c>. Built at render time, so keep it to per-instance values.</summary>
public sealed partial class StyleBuilder : CssProperties<StyleBuilder>
{
    [ThreadStatic] private static StyleBuilder? t_cached;
    private readonly StringBuilder _sb = new();

    private StyleBuilder() { }

    private protected override void AddDeclaration(string name, string value)
    {
        if (_sb.Length > 0) _sb.Append(' ');
        _sb.Append(name).Append(": ").Append(value).Append(';');
    }

    internal static string Build(Action<StyleBuilder> build)
    {
        var b = t_cached ?? new StyleBuilder();
        t_cached = null; // a nested .Style inside build gets its own builder
        try
        {
            build(b);
            return b._sb.ToString();
        }
        finally
        {
            b._sb.Clear();
            t_cached = b;
        }
    }
}

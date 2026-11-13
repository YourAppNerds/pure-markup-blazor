using System.Globalization;

namespace PureMarkup.Blazor.Styling;

/// <summary>A typed CSS value that can also be built from raw CSS text (custom properties, <c>var()</c>, <c>calc()</c>).</summary>
public interface ICssValue<TSelf> where TSelf : ICssValue<TSelf>
{
    /// <summary>Wraps CSS text as this type, e.g. <c>var(--x)</c>.</summary>
    static abstract TSelf FromCss(string css);
}

/// <summary>Any CSS value. Strings, numbers and every typed value convert to it.</summary>
public readonly struct CssValue : ICssValue<CssValue>
{
    private readonly string? _css;

    /// <summary>Raw CSS text.</summary>
    public CssValue(string css) => _css = css;

    /// <summary><c>inherit</c></summary>
    public static CssValue Inherit => new("inherit");
    /// <summary><c>initial</c></summary>
    public static CssValue Initial => new("initial");
    /// <summary><c>unset</c></summary>
    public static CssValue Unset => new("unset");
    /// <summary><c>revert</c></summary>
    public static CssValue Revert => new("revert");
    /// <summary><c>revert-layer</c></summary>
    public static CssValue RevertLayer => new("revert-layer");
    /// <summary><c>auto</c></summary>
    public static CssValue Auto => new("auto");
    /// <summary><c>none</c></summary>
    public static CssValue None => new("none");

    /// <summary>Raw CSS text.</summary>
    public static implicit operator CssValue(string css) => new(css);
    /// <summary>A number (invariant culture).</summary>
    public static implicit operator CssValue(int n) => new(n.ToString(CultureInfo.InvariantCulture));
    /// <summary>A number (invariant culture).</summary>
    public static implicit operator CssValue(double n) => new(CssUnits.Num(n));

    static CssValue ICssValue<CssValue>.FromCss(string css) => new(css);

    /// <summary>The CSS text.</summary>
    public override string ToString() => _css ?? "";
}

/// <summary>A <c>&lt;length-percentage&gt;</c>: <see cref="CssUnits.Px"/>, <see cref="CssUnits.Rem"/>, <see cref="CssUnits.Percent"/>, ...</summary>
public readonly struct Length : ICssValue<Length>
{
    private readonly string? _css;
    private Length(string css) => _css = css;
    /// <summary><c>0</c></summary>
    public static Length Zero => new("0");
    /// <summary>Wraps CSS text, e.g. <c>calc(100% - 2rem)</c>.</summary>
    public static Length FromCss(string css) => new(css);
    /// <summary>To any value.</summary>
    public static implicit operator CssValue(Length v) => new(v.ToString());
    /// <inheritdoc/>
    public override string ToString() => _css ?? "0";
}

/// <summary>A <c>&lt;color&gt;</c>: <see cref="CssUnits.Hex"/>, <see cref="CssUnits.Rgb(int, int, int)"/>, <see cref="CssUnits.Named"/>, ...</summary>
public readonly struct Color : ICssValue<Color>
{
    private readonly string? _css;
    private Color(string css) => _css = css;
    /// <summary><c>transparent</c></summary>
    public static Color Transparent => new("transparent");
    /// <summary><c>currentcolor</c></summary>
    public static Color CurrentColor => new("currentcolor");
    /// <summary>Wraps CSS text, e.g. <c>oklch(70% 0.1 200)</c>.</summary>
    public static Color FromCss(string css) => new(css);
    /// <summary>To any value.</summary>
    public static implicit operator CssValue(Color v) => new(v.ToString());
    /// <inheritdoc/>
    public override string ToString() => _css ?? "transparent";
}

/// <summary>A <c>&lt;time&gt;</c>: <see cref="CssUnits.Ms"/>, <see cref="CssUnits.Sec"/>.</summary>
public readonly struct Time : ICssValue<Time>
{
    private readonly string? _css;
    private Time(string css) => _css = css;
    /// <summary>Wraps CSS text.</summary>
    public static Time FromCss(string css) => new(css);
    /// <summary>To any value.</summary>
    public static implicit operator CssValue(Time v) => new(v.ToString());
    /// <inheritdoc/>
    public override string ToString() => _css ?? "0s";
}

/// <summary>An <c>&lt;angle&gt;</c>: <see cref="CssUnits.Deg"/>, <see cref="CssUnits.Turn"/>.</summary>
public readonly struct Angle : ICssValue<Angle>
{
    private readonly string? _css;
    private Angle(string css) => _css = css;
    /// <summary>Wraps CSS text.</summary>
    public static Angle FromCss(string css) => new(css);
    /// <summary>To any value.</summary>
    public static implicit operator CssValue(Angle v) => new(v.ToString());
    /// <inheritdoc/>
    public override string ToString() => _css ?? "0deg";
}

/// <summary>Unit and color helpers. Add <c>global using static PureMarkup.Blazor.Styling.CssUnits;</c> and write <c>Px(2)</c>.</summary>
public static class CssUnits
{
    internal static string Num(double n) => n.ToString("0.####", CultureInfo.InvariantCulture);

    /// <summary><c>{n}px</c></summary>
    public static Length Px(double n) => Length.FromCss(Num(n) + "px");
    /// <summary><c>{n}rem</c></summary>
    public static Length Rem(double n) => Length.FromCss(Num(n) + "rem");
    /// <summary><c>{n}em</c></summary>
    public static Length Em(double n) => Length.FromCss(Num(n) + "em");
    /// <summary><c>{n}%</c></summary>
    public static Length Percent(double n) => Length.FromCss(Num(n) + "%");
    /// <summary><c>{n}vw</c></summary>
    public static Length Vw(double n) => Length.FromCss(Num(n) + "vw");
    /// <summary><c>{n}vh</c></summary>
    public static Length Vh(double n) => Length.FromCss(Num(n) + "vh");
    /// <summary><c>{n}ch</c></summary>
    public static Length Ch(double n) => Length.FromCss(Num(n) + "ch");
    /// <summary><c>calc(...)</c></summary>
    public static Length Calc(string expression) => Length.FromCss("calc(" + expression + ")");

    /// <summary><c>{n}ms</c></summary>
    public static Time Ms(double n) => Time.FromCss(Num(n) + "ms");
    /// <summary><c>{n}s</c></summary>
    public static Time Sec(double n) => Time.FromCss(Num(n) + "s");

    /// <summary><c>{n}deg</c></summary>
    public static Angle Deg(double n) => Angle.FromCss(Num(n) + "deg");
    /// <summary><c>{n}turn</c></summary>
    public static Angle Turn(double n) => Angle.FromCss(Num(n) + "turn");

    /// <summary>A hex color; the leading <c>#</c> is optional.</summary>
    public static Color Hex(string hex) => Color.FromCss(hex.StartsWith('#') ? hex : "#" + hex);
    /// <summary><c>rgb(r g b)</c></summary>
    public static Color Rgb(int r, int g, int b) => Color.FromCss(string.Create(CultureInfo.InvariantCulture, $"rgb({r} {g} {b})"));
    /// <summary><c>rgb(r g b / a)</c></summary>
    public static Color Rgb(int r, int g, int b, double alpha) => Color.FromCss(string.Create(CultureInfo.InvariantCulture, $"rgb({r} {g} {b} / {Num(alpha)})"));
    /// <summary>A named color such as <c>rebeccapurple</c>.</summary>
    public static Color Named(string name) => Color.FromCss(name);
}

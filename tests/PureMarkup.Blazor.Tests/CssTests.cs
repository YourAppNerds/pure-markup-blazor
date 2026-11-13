using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using static VerifyXunit.Verifier;

namespace PureMarkup.Blazor.Tests;

[Stylesheet]
public static class TestTokens
{
    public static Token<Color> Text { get; } = Css.Token("text", light: Hex("#111"), dark: Hex("#eee"));
    public static Token<Length> Gap { get; } = Css.Token("gap", Rem(1));
    public static LayerImport Vendor { get; } = Css.ImportLayer("vendor", "_content/Vendor/vendor.min.css");
    public static FontFace Inter { get; } = Css.FontFace(f => f.FontFamily("Inter").Prop("src", "url(/fonts/inter.woff2) format(\"woff2\")"));
    public static GlobalRule Reset { get; } = Css.Global("*, *::before", g => g.BoxSizing(BoxSizing.BorderBox), CssLayer.Reset);
    public static GlobalRule Links { get; } = Css.Global("a, button", g => g.Color(TestTokens.Text).Hover(h => h.TextDecoration("underline")));
}

[Stylesheet]
public static class TestCss
{
    public static Rule Card { get; } = Css.Rule(r => r
        .Padding(Rem(1), Rem(2))
        .Display(Display.Flex)
        .Gap(TestTokens.Gap)
        .Color(TestTokens.Text)
        .Opacity(0.9)
        .Prop("-webkit-line-clamp", 3)
        .Hover(h => h.BoxShadow("0 1px 4px rgb(0 0 0 / 0.2)"))
        .FocusVisible(f => f.Outline(Px(2), OutlineStyle.Solid, TestTokens.Text))
        .Before(b => b.Content("\"\""))
        .Is(":nth-child(2n)", n => n.Background(Hex("#fafafa")))
        .Nest("& > h2", h => h.Margin(0))
        .Media(Media.MaxWidth(Px(640)), m => m.Padding(Rem(0.5)).Hover(h => h.BoxShadow("none")))
        .Container("card (min-width: 400px)", c => c.Gap(Rem(2)))
        .Supports("(display: grid)", s => s.Display(Display.Grid)));

    public static Var<Length> Progress { get; } = Css.Var<Length>(Percent(0));
    public static Var<Color> Tint { get; } = Css.Var<Color>();

    public static Rule Title { get; } = Css.Rule(r => r
        .Width(Progress)
        .Color(Tint)
        .TransitionDuration(Ms(200))
        .Rotate(Deg(3))
        .When(Card, State.Hover, h => h.Color(Hex("#06c")))
        .Disabled(d => d.Opacity(0.5))
        .Checked(c => c.FontWeight(FontWeight.Bold))
        .Active(a => a.Prop("scale", 0.98)));

    // A field works too.
    public static readonly Rule Badge = Css.Rule(r => r.BorderRadius(Px(4)));
}

/// <summary>Not [Stylesheet]: passed to the emitter explicitly to show duplicate names fail.</summary>
public static class DuplicateTokens
{
    public static Token<Color> A { get; } = Css.Token("same", Hex("#000"));
    public static Token<Color> B { get; } = Css.Token("same", Hex("#fff"));
}

public sealed class Styled : MarkupComponent
{
    public int Percent { get; set; } = 40;

    protected override Node Render() =>
        Div().Class("raw").Class(TestCss.Card).Class(TestCss.Badge, when: false).Children(
            H2().Class(TestCss.Title).Set(TestCss.Progress, CssUnits.Percent(Percent)).Style(s => s.MarginTop(Px(4)).Opacity(1)).Text("t"));
}

public class CssTests
{
    private static readonly Type[] Sheets = [typeof(TestTokens), typeof(TestCss)];

    [Fact]
    public Task Emitted_css_matches_snapshot() => Verify(CssEmitter.Emit(Sheets), extension: "css");

    [Fact]
    public void Emit_by_assembly_finds_the_stylesheet_types() =>
        Assert.Equal(CssEmitter.Emit(Sheets), CssEmitter.Emit(typeof(TestCss).Assembly));

    [Fact]
    public void Names_are_compile_time_constants_from_assembly_type_and_member()
    {
#if DEBUG
        const bool readable = true;
#else
        const bool readable = false;
#endif
        var asm = typeof(TestCss).Assembly.GetName().Name!;
        Assert.Equal(CssNames.For(asm, "PureMarkup.Blazor.Tests.TestCss", "TestCss", "Card", readable), TestCss.Card.ClassName);
        Assert.Equal(CssNames.For(asm, "PureMarkup.Blazor.Tests.TestCss", "TestCss", "Badge", readable), TestCss.Badge.ClassName);
        Assert.Equal("--" + CssNames.For(asm, "PureMarkup.Blazor.Tests.TestCss", "TestCss", "Progress", readable), TestCss.Progress.Name);
        Assert.Matches(readable ? "^TestCss_Card-[0-9a-z]{6}$" : "^pm-[0-9a-z]{6}$", TestCss.Card.ClassName);
        Assert.Equal("--text", TestTokens.Text.Name);
    }

    [Fact]
    public void Hash_is_stable() // pinned: changing the algorithm renames every class in every app
    {
        Assert.Equal("5hvxqv", CssNames.Hash("WebApp.Client", "WebApp.Client.Theme.CounterCss", "Button"));
        Assert.Equal(CssNames.Hash("a", "b", "c"), CssNames.Hash("a", "b", "c"));
        Assert.NotEqual(CssNames.Hash("a", "b", "c"), CssNames.Hash("a", "b", "d"));
    }

    [Fact]
    public void Another_load_of_the_assembly_gives_the_same_names()
    {
        // What a WebAssembly client does: load the same compiled assembly fresh and read the rule's name.
        var path = typeof(TestCss).Assembly.Location;
        var alc = new AssemblyLoadContext("second", isCollectible: true);
        alc.Resolving += (c, n) => File.Exists(Path.Combine(Path.GetDirectoryName(path)!, n.Name + ".dll"))
            ? c.LoadFromAssemblyPath(Path.Combine(Path.GetDirectoryName(path)!, n.Name + ".dll")) : null;
        try
        {
            var other = alc.LoadFromAssemblyPath(path);
            Assert.NotSame(typeof(TestCss).Assembly, other);
            var rule = other.GetType(typeof(TestCss).FullName!)!.GetProperty(nameof(TestCss.Card))!.GetValue(null)!;
            Assert.Equal(TestCss.Card.ClassName, (string)rule.GetType().GetProperty(nameof(Rule.ClassName))!.GetValue(rule)!);
        }
        finally { alc.Unload(); }
    }

    [Fact]
    public async Task Prerendered_markup_carries_the_rule_classes_additively()
    {
        var html = await HtmlOut.RenderAsync<Styled>();
        Assert.Equal(
            $"<div class=\"raw {TestCss.Card.ClassName}\"><h2 class=\"{TestCss.Title.ClassName}\" style=\"{TestCss.Progress.Name}: 40%; margin-top: 4px; opacity: 1;\">t</h2></div>",
            html);
    }

    [Fact]
    public void State_blocks_go_to_the_state_layer_and_base_blocks_do_not()
    {
        var css = CssEmitter.Emit(Sheets);
        var layers = Layers(css);
        var card = "." + TestCss.Card.ClassName;
        var title = "." + TestCss.Title.ClassName;
        Assert.StartsWith("/*", css);
        Assert.Contains(CssEmitter.LayerOrder, css);
        Assert.Contains(card + ":hover", layers["pm.local-state"]);
        Assert.Contains(card + ":focus-visible", layers["pm.local-state"]);
        Assert.Contains(card + ":hover " + title, layers["pm.local-state"]); // .When
        Assert.Contains(title + ":disabled", layers["pm.local-state"]);
        Assert.Contains(title + ":checked", layers["pm.local-state"]);
        Assert.Contains(title + ":active", layers["pm.local-state"]);
        Assert.Matches(@"@media \(max-width: 640px\) \{\s*\." + Regex.Escape(TestCss.Card.ClassName) + @":hover", layers["pm.local-state"]);
        Assert.DoesNotContain(":hover", layers["pm.local"]);
        Assert.Contains(card + "::before", layers["pm.local"]);
        Assert.Contains(card + ":nth-child(2n)", layers["pm.local"]);
        Assert.Contains(card + " > h2", layers["pm.local"]);
        Assert.Contains("a:hover, button:hover", layers["pm.base"]); // globals keep their layer
        Assert.Contains("box-sizing: border-box", layers["reset"]);
        Assert.True(css.IndexOf("@import", StringComparison.Ordinal) < css.IndexOf("@layer pm.", StringComparison.Ordinal));
    }

    [Fact]
    public void Duplicate_names_fail_the_build()
    {
        var e = Assert.Throws<InvalidOperationException>(() => CssEmitter.Emit([typeof(DuplicateTokens)]));
        Assert.Contains("--same", e.Message);
    }

    [Fact]
    public void A_rule_outside_a_stylesheet_has_no_name()
    {
#pragma warning disable PMCSS001 // the point of the test
        var rule = Css.Rule(r => r.Margin(0));
#pragma warning restore PMCSS001
        Assert.Throws<InvalidOperationException>(() => rule.ClassName);
    }

    [Fact]
    public void Property_surface_is_generated_from_webref()
    {
        Assert.True(CssCounts.Properties > 700, $"{CssCounts.Properties} properties");
        Assert.True(CssCounts.KeywordTypes > 300, $"{CssCounts.KeywordTypes} keyword types");
    }

    /// <summary>Top-level <c>@layer name { ... }</c> bodies by name.</summary>
    private static Dictionary<string, string> Layers(string css)
    {
        var result = new Dictionary<string, string>();
        foreach (Match m in Regex.Matches(css, @"^@layer ([\w.-]+) \{\n(.*?)^\}", RegexOptions.Multiline | RegexOptions.Singleline))
            result[m.Groups[1].Value] = m.Groups[2].Value;
        return result;
    }
}

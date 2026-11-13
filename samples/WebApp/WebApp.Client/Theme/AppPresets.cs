using Microsoft.AspNetCore.Components.Forms;

namespace WebApp.Client.Theme;

/// <summary>Presets: recorded writes applied with <c>.Use</c>. Local writes beat them; classes add up.</summary>
public static class AppPresets
{
    public static Preset<ButtonElement> PrimaryButton => Preset.Once<ButtonElement>(p => p
        .Class("btn").Class(CounterCss.Button).Type(ButtonType.Button));

    /// <summary>A <c>.With</c> chain: PrimaryButton's writes first, then these, which win.</summary>
    public static Preset<ButtonElement> SubmitButton => PrimaryButton.With(p => p.Type(ButtonType.Submit));

    /// <summary>A preset on a component: InputText captures <c>class</c> and other attributes.</summary>
    public static Preset<ComponentNode<InputText>> Field => Preset.Once<ComponentNode<InputText>>(p => p
        .Class(FormCss.Field).Attr("autocomplete", "off"));
}

[Stylesheet]
public static class FormCss
{
    public static Rule Field { get; } = Css.Rule(r => r
        .Padding(Rem(0.25), Rem(0.5))
        .Border(Px(1), "solid", Tokens.Accent)
        .BorderRadius(Tokens.Radius)
        .MarginRight(Rem(0.5)));
}

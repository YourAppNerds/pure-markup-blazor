using Microsoft.AspNetCore.Components.Forms;

namespace PureMarkup.Blazor.Tests;

public sealed class Model { public string? Name { get; set; } public int Age { get; set; } }
public sealed class Draft { public string? Title { get; set; } }

/// <summary>InputText that records itself so a test can type into it.</summary>
public sealed class ProbeInput : InputText
{
    public static readonly List<ProbeInput> All = [];
    protected override void OnInitialized() { All.Add(this); base.OnInitialized(); }
    public void Type(string s) => CurrentValueAsString = s;
    public FieldIdentifier Field => FieldIdentifier;
}

/// <summary>Captured local inside the EditForm child-content lambda: <c>() => model.Name</c>.</summary>
public sealed class LocalForm : MarkupComponent
{
    public Model State { get; } = new() { Name = "Old" };

    protected override Node Render()
    {
        var model = State;
        return Comp<EditForm>().Param("Model", model).Param("FormName", "local")
            .ChildContent<EditContext>(_ => Comp<ProbeInput>().Bind(() => model.Name));
    }
}

/// <summary>Private nullable property on the component: <c>() => Draft!.Title</c>, via the InputText overload.</summary>
public sealed class DraftForm : MarkupComponent
{
    private Draft? Draft { get; set; } = new() { Title = "T0" };
    public string? Title => Draft?.Title;

    protected override Node Render() =>
        Comp<EditForm>().Param("Model", Draft).Param("FormName", "draft")
            .ChildContent<EditContext>(_ => Comp<InputText>().Bind(() => Draft!.Title));
}

/// <summary>Probe for the private-property path (ProbeInput uses the generic overload).</summary>
public sealed class DraftProbeForm : MarkupComponent
{
    private Draft? Draft { get; set; } = new() { Title = "T0" };
    public string? Title => Draft?.Title;

    protected override Node Render() =>
        Comp<EditForm>().Param("Model", Draft).Param("FormName", "draft")
            .ChildContent<EditContext>(_ => Comp<ProbeInput>().Bind(() => Draft!.Title));
}

/// <summary>Root type is private, so the generator cannot name it: the reflection fallback runs.</summary>
public sealed class FallbackForm : MarkupComponent
{
    private sealed class Hidden { public string? Name { get; set; } = "H0"; }
    private readonly Hidden _hidden = new();
    public string? Name => _hidden.Name;

    protected override Node Render()
    {
        var h = _hidden;
        return Comp<EditForm>().Param("Model", h).Param("FormName", "fb")
            .ChildContent<EditContext>(_ => Comp<ProbeInput>().Bind(() => h.Name));
    }
}

/// <summary>ForEach rows binding a lambda parameter: <c>() => row.Name</c>.</summary>
public sealed class RowsForm : MarkupComponent
{
    public List<Model> Rows { get; } = [new() { Name = "r0" }, new() { Name = "r1" }];

    protected override Node Render() =>
        Comp<EditForm>().Param("Model", this).ChildContent<EditContext>(_ =>
            ForEach(Rows, r => r, row => Comp<ProbeInput>().Bind(() => row.Name)));
}

public class BindTests
{
    static List<ProbeInput> RenderAndFindInputs(Microsoft.AspNetCore.Components.IComponent c)
    {
        ProbeInput.All.Clear();
        new TestRenderer().Render(c);
        return [.. ProbeInput.All];
    }

    [Fact]
    public void CapturedLocal_IsIntercepted_SetterUpdatesModel()
    {
        var before = BindRuntime.FallbackCount;
        var form = new LocalForm();
        var input = Assert.Single(RenderAndFindInputs(form));
        Assert.Equal(before, BindRuntime.FallbackCount); // generated path, not the reflection fallback
        Assert.Equal("Old", input.Value);
        input.Type("Ann");
        Assert.Equal("Ann", form.State.Name);
        Assert.Equal("Name", input.Field.FieldName);
        Assert.Same(form.State, input.Field.Model);
        Assert.Equal("Ann", input.Value); // re-rendered with the new value
    }

    [Fact]
    public void PrivateNullableProperty_IsIntercepted_ViaUnsafeAccessor()
    {
        var before = BindRuntime.FallbackCount;
        var form = new DraftProbeForm();
        var input = Assert.Single(RenderAndFindInputs(form));
        Assert.Equal(before, BindRuntime.FallbackCount);
        Assert.Equal("T0", input.Value);
        input.Type("T1");
        Assert.Equal("T1", form.Title);
        Assert.Equal("Title", input.Field.FieldName);
    }

    [Fact]
    public void UnnameableRoot_FallsBackToReflectionWalk()
    {
        var before = BindRuntime.FallbackCount;
        var form = new FallbackForm();
        var input = Assert.Single(RenderAndFindInputs(form));
        Assert.True(BindRuntime.FallbackCount > before);
        input.Type("H1");
        Assert.Equal("H1", form.Name);
    }

    [Fact]
    public void ForEachRow_LambdaParameter_IsIntercepted()
    {
        var before = BindRuntime.FallbackCount;
        var form = new RowsForm();
        var inputs = RenderAndFindInputs(form);
        Assert.Equal(before, BindRuntime.FallbackCount);
        Assert.Equal(2, inputs.Count);
        inputs[1].Type("changed");
        Assert.Equal("changed", form.Rows[1].Name);
        Assert.Equal("r0", form.Rows[0].Name);
    }

    [Theory]
    [InlineData(typeof(LocalForm), "model.Name", "Old")]
    [InlineData(typeof(DraftForm), "Draft.Title", "T0")]
    public async Task StaticSsr_FieldNameComesFromExpressionText(Type component, string name, string value)
    {
        var render = typeof(HtmlOut).GetMethod(nameof(HtmlOut.RenderAsync))!.MakeGenericMethod(component);
        var markup = await (Task<string>)render.Invoke(null, [null])!;
        Assert.Contains($"name=\"{name}\"", markup);
        Assert.Contains($"value=\"{value}\"", markup);
    }
}

using System.Diagnostics;
using System.Linq.Expressions;
using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PureMarkup;
using Xunit;
using Xunit.Abstractions;
using static PureMarkup.H;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Tests;

public sealed class Model { public string? Name { get; set; } }
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

/// <summary>Private nullable property on the component: <c>() => Draft!.Title</c>.</summary>
public sealed class DraftForm : MarkupComponent
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

sealed class InlineDispatcher : Dispatcher
{
    public override bool CheckAccess() => true;
    public override Task InvokeAsync(Action workItem) { workItem(); return Task.CompletedTask; }
    public override Task InvokeAsync(Func<Task> workItem) => workItem();
    public override Task<T> InvokeAsync<T>(Func<T> workItem) => Task.FromResult(workItem());
    public override Task<T> InvokeAsync<T>(Func<Task<T>> workItem) => workItem();
}

sealed class TestRenderer() : Renderer(new ServiceCollection().BuildServiceProvider(), NullLoggerFactory.Instance)
{
    public override Dispatcher Dispatcher { get; } = new InlineDispatcher();
    protected override void HandleException(Exception exception) => ExceptionDispatchInfo.Capture(exception).Throw();
    protected override Task UpdateDisplayAsync(in RenderBatch batch) => Task.CompletedTask;
    public void Render(IComponent c) => RenderRootComponentAsync(AssignRootComponentId(c)).GetAwaiter().GetResult();
}

public class BindTests(ITestOutputHelper output)
{
    static ProbeInput RenderAndFindInput(IComponent c)
    {
        ProbeInput.All.Clear();
        new TestRenderer().Render(c);
        return Assert.Single(ProbeInput.All);
    }

    [Fact]
    public void CapturedLocal_IsIntercepted_SetterUpdatesModel()
    {
        var before = BindRuntime.FallbackCount;
        var form = new LocalForm();
        var input = RenderAndFindInput(form);
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
        var form = new DraftForm();
        var input = RenderAndFindInput(form);
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
        var input = RenderAndFindInput(form);
        Assert.True(BindRuntime.FallbackCount > before);
        input.Type("H1");
        Assert.Equal("H1", form.Name);
    }

    [Theory]
    [InlineData(typeof(LocalForm), "model.Name", "Old")]
    [InlineData(typeof(DraftForm), "Draft.Title", "T0")]
    public async Task StaticSsr_FieldNameComesFromExpressionText(Type component, string name, string value)
    {
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var html = new HtmlRenderer(services, NullLoggerFactory.Instance);
        var markup = await html.Dispatcher.InvokeAsync(async () =>
            (await html.RenderComponentAsync(component, ParameterView.Empty)).ToHtmlString());
        output.WriteLine(markup);
        Assert.Contains($"name=\"{name}\"", markup);
        Assert.Contains($"value=\"{value}\"", markup);
    }

    [Fact]
    public void Cost_PerBindCall()
    {
        var model = new Model();
        model.Name = Environment.MachineName.Length > 0 ? null : "x"; // maybe-null flow state, as in a real form
        const int n = 200_000;
        double Time(Action a)
        {
            for (var i = 0; i < 20_000; i++) a();
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < n; i++) a();
            return sw.Elapsed.TotalNanoseconds / n;
        }
        // All three build the expression tree at the call site, as Razor's @bind-Value does.
        var intercepted = Time(() => Comp<InputText>().Bind(() => model.Name));
        var reflection = Time(() => BindExtensions.Bind(Comp<InputText>(), () => model.Name)); // static call form: not intercepted
        var handWritten = Time(() => BindExtensions.BindCore(Comp<InputText>(), () => model.Name, model.Name, v => model.Name = v));
        var exprOnly = Time(() => { Expression<Func<string?>> e = () => model.Name; GC.KeepAlive(e); });
        output.WriteLine($"intercepted {intercepted:F0} ns, reflection fallback {reflection:F0} ns, hand-written {handWritten:F0} ns, expression tree alone {exprOnly:F0} ns");
    }
}

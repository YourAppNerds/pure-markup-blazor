using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace PureMarkup.Blazor.Tests;

/// <summary>EditForm through the generated typed methods.</summary>
public sealed class TypedEditForm : MarkupComponent
{
    public readonly Model M = new() { Name = "n" };
    private void Save() { }
    protected override Node Render() =>
        Comp<EditForm>().Model(M).FormName("x").OnValidSubmit(Save).ChildContent(ctx => P().Text(ctx.Model.GetType().Name));
}

/// <summary>The same EditForm through the untyped API.</summary>
public sealed class UntypedEditForm : MarkupComponent
{
    public readonly Model M = new() { Name = "n" };
    private void Save() { }
    protected override Node Render() =>
        Comp<EditForm>().Param("Model", M).Param("FormName", "x").Callback<EditContext>("OnValidSubmit", Save)
            .ChildContent<EditContext>(ctx => P().Text(ctx.Model.GetType().Name));
}

/// <summary>The same EditForm as Razor compiles it.</summary>
public sealed class RazorEditForm : ComponentBase
{
    public readonly Model M = new() { Name = "n" };
    private void Save() { }
    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        b.OpenComponent<EditForm>(0);
        b.AddComponentParameter(0, "Model", M);
        b.AddComponentParameter(1, "FormName", "x");
        b.AddComponentParameter(2, "OnValidSubmit", EventCallback.Factory.Create<EditContext>(this, Save));
        b.AddComponentParameter(3, "ChildContent", (RenderFragment<EditContext>)(ctx => b2 =>
        {
            b2.OpenElement(0, "p");
            b2.AddContent(0, ctx.Model.GetType().Name);
            b2.CloseElement();
        }));
        b.CloseComponent();
    }
}

/// <summary>Generic inference from the receiver: <c>Comp&lt;InputNumber&lt;int&gt;&gt;().Value(...)</c>.</summary>
public sealed class TypedNumber : MarkupComponent
{
    public readonly Model M = new() { Age = 30 };
    protected override Node Render() =>
        Comp<EditForm>().Model(M).ChildContent(_ =>
            Comp<InputNumber<int>>().Value(M.Age).ValueChanged(v => M.Age = v).ValueExpression(() => M.Age).Attr("id", "age"));
}

/// <summary>Every EventCallback&lt;T&gt; overload, one InputText each.</summary>
public sealed class TypedCallbacks : MarkupComponent
{
    public readonly Model M = new() { Name = "n" };
    public int Renders;
    public readonly List<string> Log = [];
    public readonly InputText?[] Inputs = new InputText?[5];

    protected override Node Render()
    {
        Renders++;
        return Comp<EditForm>().Model(M).ChildContent(_ => Fragment(
            Input(0).ValueChanged(() => Log.Add("action")),
            Input(1).ValueChanged((string? v) => Log.Add("action-of:" + v)),
            Input(2).ValueChanged(async () => { await Task.Yield(); Log.Add("func"); }),
            Input(3).ValueChanged(async (string? v) => { await Task.Yield(); Log.Add("func-of:" + v); }),
            Input(4).ValueChanged(EventCallback.Factory.Create<string?>(this, v => Log.Add("callback:" + v)))));
    }

    private ComponentNode<InputText> Input(int i) =>
        Comp<InputText>().Value(M.Name).ValueExpression(() => M.Name).Ref(c => Inputs[i] = c);
}

/// <summary>RenderFragment&lt;T&gt; overloads (Authorized, NotAuthorized) and CascadingValue&lt;T&gt; inference.</summary>
public sealed class TypedAuth : MarkupComponent
{
    [Parameter] public ClaimsPrincipal User { get; set; } = new();

    protected override Node Render() =>
        Comp<CascadingValue<Task<AuthenticationState>>>().Value(Task.FromResult(new AuthenticationState(User))).Children(() =>
            Comp<AuthorizeView>()
                .Authorized(s => P().Text("in " + s.User.Identity!.Name))
                .NotAuthorized(_ => P().Text("out"))
                .Authorizing(() => P().Text("wait")));
}

public class ComponentParameterTests
{
    [Fact]
    public void Counts_CoverTheFramework()
    {
        Assert.True(FrameworkComponentCounts.Components >= 30, $"components: {FrameworkComponentCounts.Components}");
        Assert.True(FrameworkComponentCounts.Methods >= 150, $"methods: {FrameworkComponentCounts.Methods}");
    }

    [Fact]
    public void TypedMethods_WriteTheSameFrames_AsParam_AndRazor()
    {
        var typed = Dump(new TypedEditForm());
        Assert.Equal(Dump(new UntypedEditForm()), typed);
        Assert.Equal(Dump(new RazorEditForm()), typed);
        Assert.Contains("Attribute 2 OnValidSubmit=EventCallback`1", typed);
        Assert.Contains("Attribute 3 ChildContent=RenderFragment`1", typed);
    }

    [Fact]
    public async Task TypedEditForm_RendersTheSameHtml_AsRazor()
    {
        var typed = await HtmlOut.RenderAsync<TypedEditForm>();
        Assert.Equal(await HtmlOut.RenderAsync<RazorEditForm>(), typed);
        Assert.Contains("<p>Model</p>", typed);
    }

    [Fact]
    public async Task GenericComponent_InfersTypeArgument_FromReceiver()
    {
        var html = await HtmlOut.RenderAsync<TypedNumber>();
        Assert.Contains("type=\"number\"", html);
        Assert.Contains("id=\"age\"", html); // .Attr goes to CaptureUnmatchedValues
        Assert.Contains("value=\"30\"", html);
    }

    [Fact]
    public async Task EventCallbackOverloads_InvokeHandler_AndRerenderOwner()
    {
        var owner = new TypedCallbacks();
        new TestRenderer().Render(owner);
        string[] expected = ["action", "action-of:v1", "func", "func-of:v3", "callback:v4"];
        int[] renders = [1, 1, 2, 2, 1]; // async handlers render after the sync part and again on completion, as ComponentBase does
        for (var i = 0; i < 5; i++)
        {
            var before = owner.Renders;
            await owner.Inputs[i]!.ValueChanged.InvokeAsync("v" + i);
            Assert.Equal(expected[i], owner.Log[^1]);
            Assert.Equal(before + renders[i], owner.Renders); // the receiver is the owner, as with Razor's @bind / On*
        }
    }

    [Theory]
    [InlineData(null, "<p>out</p>")]
    [InlineData("Ann", "<p>in Ann</p>")]
    public async Task RenderFragmentOfT_Overloads_Render(string? name, string expected)
    {
        var user = name is null ? new ClaimsPrincipal(new ClaimsIdentity()) : new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, name)], "test"));
        var services = new ServiceCollection().AddLogging().AddAuthorizationCore().BuildServiceProvider();
        await using var html = new HtmlRenderer(services, NullLoggerFactory.Instance);
        var output = await html.Dispatcher.InvokeAsync(async () =>
            (await html.RenderComponentAsync<TypedAuth>(ParameterView.FromDictionary(new Dictionary<string, object?> { ["User"] = user }))).ToHtmlString());
        Assert.Equal(expected, output);
    }

    [Fact]
    public void CascadingParameters_GetNoMethods()
    {
        // Public [CascadingParameter]/[SupplyParameterFrom*] properties of framework components must not appear.
        var asm = typeof(ComponentNode<>).Assembly;
        var generated = asm.GetTypes().Where(t => t.Name.EndsWith("Parameters", StringComparison.Ordinal) && t.IsAbstract && t.IsSealed).ToList();
        Assert.NotEmpty(generated);
        Assert.DoesNotContain(generated.SelectMany(t => t.GetMethods()), m => m.Name is "AuthenticationState" or "CascadedEditContext");
    }

    private static string Dump(IComponent c)
    {
        var r = new TestRenderer();
        var id = r.Render(c);
        var frames = r.Frames(id);
        var sb = new StringBuilder();
        for (var i = 0; i < frames.Count; i++)
        {
            ref var f = ref frames.Array[i];
            sb.Append(f.FrameType).Append(' ').Append(f.Sequence);
            if (f.FrameType == RenderTreeFrameType.Component) sb.Append(' ').Append(f.ComponentType.Name);
            if (f.FrameType == RenderTreeFrameType.Attribute)
                sb.Append(' ').Append(f.AttributeName).Append('=').Append(f.AttributeValue switch
                {
                    null => "null",
                    string s => s,
                    var v => v.GetType().Name,
                });
            sb.AppendLine();
        }
        return sb.ToString();
    }
}

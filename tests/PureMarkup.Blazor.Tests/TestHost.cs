using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace PureMarkup.Blazor.Tests;

/// <summary>Static HTML via the framework's HtmlRenderer.</summary>
public static class HtmlOut
{
    public static async Task<string> RenderAsync<T>(IDictionary<string, object?>? parameters = null) where T : IComponent
    {
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var html = new HtmlRenderer(services, NullLoggerFactory.Instance);
        return await html.Dispatcher.InvokeAsync(async () =>
        {
            var view = parameters is null ? ParameterView.Empty : ParameterView.FromDictionary(parameters);
            return (await html.RenderComponentAsync<T>(view)).ToHtmlString();
        });
    }

    /// <summary>Whitespace between tags removed, self-closing slash dropped.</summary>
    public static string Normalize(string s) => Regex.Replace(s, @">\s+<", "><").Replace(" />", ">").Trim();
}

sealed class InlineDispatcher : Dispatcher
{
    public override bool CheckAccess() => true;
    public override Task InvokeAsync(Action workItem) { workItem(); return Task.CompletedTask; }
    public override Task InvokeAsync(Func<Task> workItem) => workItem();
    public override Task<T> InvokeAsync<T>(Func<T> workItem) => Task.FromResult(workItem());
    public override Task<T> InvokeAsync<T>(Func<Task<T>> workItem) => workItem();
}

/// <summary>Synchronous renderer that records edit counts and exposes frames, for diff and event tests.</summary>
sealed class TestRenderer() : Renderer(new ServiceCollection().BuildServiceProvider(), NullLoggerFactory.Instance)
{
    public override Dispatcher Dispatcher { get; } = new InlineDispatcher();
    public int LastEdits;
    public List<RenderTreeEditType> LastEditTypes = [];

    protected override void HandleException(Exception exception) => ExceptionDispatchInfo.Capture(exception).Throw();

    protected override Task UpdateDisplayAsync(in RenderBatch batch)
    {
        LastEdits = 0;
        LastEditTypes = [];
        for (var i = 0; i < batch.UpdatedComponents.Count; i++)
        {
            var diff = batch.UpdatedComponents.Array[i];
            LastEdits += diff.Edits.Count;
            for (var e = 0; e < diff.Edits.Count; e++) LastEditTypes.Add(diff.Edits.Array[e].Type);
        }
        return Task.CompletedTask;
    }

    public int Render(IComponent c)
    {
        var id = AssignRootComponentId(c);
        RenderRootComponentAsync(id).GetAwaiter().GetResult();
        return id;
    }

    public ArrayRange<RenderTreeFrame> Frames(int componentId) => GetCurrentRenderTreeFrames(componentId);

    /// <summary>Render modes the renderer was asked to resolve (Razor's <c>@rendermode</c>); the component is created as usual.</summary>
    public List<IComponentRenderMode> ResolvedRenderModes = [];

    protected override IComponent ResolveComponentForRenderMode(Type componentType, int? parentComponentId, IComponentActivator componentActivator, IComponentRenderMode renderMode)
    {
        ResolvedRenderModes.Add(renderMode);
        return componentActivator.CreateInstance(componentType);
    }

    /// <summary>Handler id of the <paramref name="nth"/> attribute named <paramref name="eventName"/> in the component's frames.</summary>
    public ulong Handler(int componentId, string eventName, int nth = 0)
    {
        var frames = Frames(componentId);
        for (var i = 0; i < frames.Count; i++)
        {
            ref var f = ref frames.Array[i];
            if (f.FrameType == RenderTreeFrameType.Attribute && f.AttributeName == eventName && f.AttributeEventHandlerId != 0 && nth-- == 0)
                return f.AttributeEventHandlerId;
        }
        throw new InvalidOperationException($"No handler {eventName}");
    }

    public void Fire(int componentId, string eventName, EventArgs args, int nth = 0) =>
        FireAsync(componentId, eventName, args, nth).GetAwaiter().GetResult();

    public Task FireAsync(int componentId, string eventName, EventArgs args, int nth = 0) =>
        DispatchEventAsync(Handler(componentId, eventName, nth), null, args);
}

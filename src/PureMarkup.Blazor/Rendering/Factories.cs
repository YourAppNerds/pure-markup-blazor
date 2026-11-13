using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace PureMarkup.Blazor.Rendering;

/// <summary>Opens a component of a type known at compile time. One instance per type, so nodes store no Type.</summary>
internal abstract class ComponentOpener
{
    public abstract void Open(RenderTreeBuilder b, int seq);
}

internal sealed class ComponentOpener<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T> : ComponentOpener
    where T : IComponent
{
    public static readonly ComponentOpener<T> Instance = new();

    public override void Open(RenderTreeBuilder b, int seq) => b.OpenComponent<T>(seq);
}

/// <summary>Turns a stored handler into a typed <see cref="EventCallback{T}"/> at write time, when the receiver is known.</summary>
internal abstract class EventFactory
{
    public abstract void AddAttribute(RenderTreeBuilder b, int seq, string name, object? receiver, object handler);

    public abstract void AddParameter(RenderTreeBuilder b, int seq, string name, object? receiver, object handler);
}

/// <summary>Untyped <see cref="EventCallback"/> (component parameters of type EventCallback).</summary>
internal sealed class EventFactory0 : EventFactory
{
    public static readonly EventFactory0 Instance = new();

    private static EventCallback Create(object? receiver, object handler) => handler switch
    {
        Action a => EventCallback.Factory.Create(receiver!, a),
        Func<Task> f => EventCallback.Factory.Create(receiver!, f),
        EventCallback e => e,
        _ => throw new InvalidOperationException($"Unsupported handler {handler.GetType()}."),
    };

    public override void AddAttribute(RenderTreeBuilder b, int seq, string name, object? receiver, object handler) =>
        b.AddAttribute(seq, name, Create(receiver, handler));

    public override void AddParameter(RenderTreeBuilder b, int seq, string name, object? receiver, object handler) =>
        b.AddComponentParameter(seq, name, Create(receiver, handler));
}

internal sealed class EventFactory<T> : EventFactory
{
    public static readonly EventFactory<T> Instance = new();

    private static EventCallback<T> Create(object? receiver, object handler) => handler switch
    {
        Action a => EventCallback.Factory.Create<T>(receiver!, a),
        Action<T> a => EventCallback.Factory.Create(receiver!, a),
        Func<Task> f => EventCallback.Factory.Create<T>(receiver!, f),
        Func<T, Task> f => EventCallback.Factory.Create(receiver!, f),
        EventCallback<T> e => e,
        _ => throw new InvalidOperationException($"Unsupported handler {handler.GetType()}."),
    };

    // Same call Razor emits for @onclick: AddAttribute<T>(seq, name, EventCallback<T>).
    public override void AddAttribute(RenderTreeBuilder b, int seq, string name, object? receiver, object handler) =>
        b.AddAttribute(seq, name, Create(receiver, handler));

    public override void AddParameter(RenderTreeBuilder b, int seq, string name, object? receiver, object handler) =>
        b.AddComponentParameter(seq, name, Create(receiver, handler));
}

/// <summary>Wraps a <c>Func&lt;T, Node&gt;</c> as a <see cref="RenderFragment{T}"/>.</summary>
internal abstract class FragmentFactory
{
    public abstract object Create(object? receiver, object body);
}

internal sealed class FragmentFactory<T> : FragmentFactory
{
    public static readonly FragmentFactory<T> Instance = new();

    public override object Create(object? receiver, object body)
    {
        var f = (Func<T, Node>)body;
        return (RenderFragment<T>)(arg => b => MarkupRenderer.Render(b, receiver, f, arg));
    }
}

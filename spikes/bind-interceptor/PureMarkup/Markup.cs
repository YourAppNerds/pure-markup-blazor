using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;

namespace PureMarkup;

// Minimal node tree: just enough to host components with parameters and child content.

public sealed class RenderContext(RenderTreeBuilder builder, IHandleEvent receiver)
{
    public readonly RenderTreeBuilder Builder = builder;
    public readonly IHandleEvent Receiver = receiver;
    public int Seq;
}

public abstract class Node
{
    public abstract void Write(RenderContext ctx);
}

/// <summary>A parameter value that needs the render context (event receiver) to materialize.</summary>
interface IDeferredParam
{
    object Resolve(RenderContext ctx);
}

sealed class DeferredCallback<T>(Action<T> action) : IDeferredParam
{
    public object Resolve(RenderContext ctx) => EventCallback.Factory.Create(ctx.Receiver, action);
}

sealed class DeferredFragment<TArg>(Func<TArg, Node> body) : IDeferredParam
{
    public object Resolve(RenderContext ctx)
    {
        var receiver = ctx.Receiver;
        return (RenderFragment<TArg>)(arg => b => body(arg).Write(new RenderContext(b, receiver)));
    }
}

public sealed class ComponentNode<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T> : Node where T : IComponent
{
    readonly List<KeyValuePair<string, object?>> _params = new(4);

    public ComponentNode<T> Param(string name, object? value) { _params.Add(new(name, value)); return this; }

    public ComponentNode<T> ChildContent<TArg>(Func<TArg, Node> body) => Param("ChildContent", new DeferredFragment<TArg>(body));

    internal ComponentNode<T> Callback<TValue>(string name, Action<TValue> action) => Param(name, new DeferredCallback<TValue>(action));

    public override void Write(RenderContext ctx)
    {
        var b = ctx.Builder;
        b.OpenComponent<T>(ctx.Seq++);
        foreach (var (name, value) in _params)
            b.AddComponentParameter(ctx.Seq++, name, value is IDeferredParam d ? d.Resolve(ctx) : value);
        b.CloseComponent();
    }
}

public static class H
{
    public static ComponentNode<T> Comp<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>() where T : IComponent => new();
}

public abstract class MarkupComponent : ComponentBase
{
    protected abstract Node Render();
    protected override void BuildRenderTree(RenderTreeBuilder builder) => Render().Write(new RenderContext(builder, this));
}

public static class BindExtensions
{
    /// <summary>
    /// Two-way binding for an input. The generator intercepts this call when the lambda is a plain member chain
    /// and emits typed get/set code. When it does not (unsupported shape), this body runs: a reflection walk
    /// over the expression. No Expression.Compile() either way.
    /// </summary>
    public static ComponentNode<TComp> Bind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TComp, TValue>(this ComponentNode<TComp> node, Expression<Func<TValue>> expr)
        where TComp : InputBase<TValue>
    {
        Interlocked.Increment(ref BindRuntime.FallbackCount);
        var (target, member) = BindRuntime.Resolve(expr);
        return BindCore(node, expr, (TValue)BindRuntime.Get(target, member)!, v => BindRuntime.Set(target, member, v));
    }

    /// <summary>What both paths end in: Value, ValueChanged, and the original expression as ValueExpression.</summary>
    public static ComponentNode<TComp> BindCore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TComp, TValue>(ComponentNode<TComp> node, Expression<Func<TValue>> expr, TValue value, Action<TValue> setter)
        where TComp : IComponent =>
        node.Param("Value", value).Callback("ValueChanged", setter).Param("ValueExpression", expr);
}

public static class BindRuntime
{
    public static int FallbackCount;

    /// <summary>Generated code: evaluates what is left of the chain after peeling the <paramref name="depth"/> members it accesses itself.</summary>
    public static object? Root(LambdaExpression expr, int depth)
    {
        var e = expr.Body;
        for (var i = 0; i < depth; i++) e = ((MemberExpression)e).Expression!;
        return Eval(e);
    }

    public static (object? Target, MemberInfo Member) Resolve(LambdaExpression expr)
    {
        if (expr.Body is not MemberExpression m)
            throw new NotSupportedException($"Bind needs a member access like () => model.Name; got {expr.Body.NodeType}.");
        if (m.Member.DeclaringType!.IsValueType)
            throw new NotSupportedException($"Bind cannot write {m.Member.Name}: it lives on a struct, so the write would hit a copy.");
        return (Eval(m.Expression), m.Member);
    }

    public static object? Get(object? target, MemberInfo member) => member switch
    {
        FieldInfo f => f.GetValue(target),
        PropertyInfo p => p.GetValue(target),
        _ => throw new NotSupportedException(member.ToString()),
    };

    public static void Set(object? target, MemberInfo member, object? value)
    {
        switch (member)
        {
            case FieldInfo f: f.SetValue(target, value); break;
            case PropertyInfo p: p.SetValue(target, value); break;
            default: throw new NotSupportedException(member.ToString());
        }
    }

    static object? Eval(Expression? e) => e switch
    {
        null => null, // static member
        ConstantExpression c => c.Value, // closure object or `this`
        MemberExpression m => Get(Eval(m.Expression), m.Member),
        UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.TypeAs } u => Eval(u.Operand),
        _ => throw new NotSupportedException($"Bind cannot evaluate {e.NodeType} without compiling; use .Value/.ValueChanged."),
    };
}

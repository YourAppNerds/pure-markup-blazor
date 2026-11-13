using System.ComponentModel;
using IComponent = Microsoft.AspNetCore.Components.IComponent;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace PureMarkup.Blazor;

/// <summary>
/// Razor's <c>@bind-Value</c> for inputs: <c>Comp&lt;InputText&gt;().Bind(() =&gt; model.Name)</c> sets <c>Value</c>,
/// <c>ValueChanged</c> and <c>ValueExpression</c>. The lambda is passed through as written (forms derive field names from it).
/// </summary>
/// <remarks>
/// The generator intercepts each call whose lambda is a plain member chain and emits typed get/set code
/// (<c>[UnsafeAccessor]</c> for private members). Other shapes run the method bodies here: a reflection walk over the
/// expression. Neither path calls <c>Expression.Compile()</c>.
/// </remarks>
public static class BindExtensions
{
    /// <summary>Binds an <see cref="InputText"/>.</summary>
    public static ComponentNode<InputText> Bind(this ComponentNode<InputText> node, Expression<Func<string?>> expr) => Fallback(node, expr);

    /// <summary>Binds an <see cref="InputTextArea"/>.</summary>
    public static ComponentNode<InputTextArea> Bind(this ComponentNode<InputTextArea> node, Expression<Func<string?>> expr) => Fallback(node, expr);

    /// <summary>Binds an <see cref="InputCheckbox"/>.</summary>
    public static ComponentNode<InputCheckbox> Bind(this ComponentNode<InputCheckbox> node, Expression<Func<bool>> expr) => Fallback(node, expr);

    /// <summary>Binds any <see cref="InputBase{TValue}"/>.</summary>
    public static ComponentNode<TComp> Bind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TComp, TValue>(
        this ComponentNode<TComp> node, Expression<Func<TValue>> expr)
        where TComp : InputBase<TValue> => Fallback(node, expr);

    private static ComponentNode<TComp> Fallback<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TComp, TValue>(
        ComponentNode<TComp> node, Expression<Func<TValue>> expr) where TComp : IComponent
    {
        Rendering.RenderArena.Current.ThrowIfRecording("Bind");
        Interlocked.Increment(ref BindRuntime.FallbackCount);
        var (target, member) = BindRuntime.Resolve(expr);
        return BindCore(node, expr, (TValue)BindRuntime.Get(target, member)!, v => BindRuntime.Set(target, member, v));
    }

    /// <summary>What both paths end in. Called by generated interceptors.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static ComponentNode<TComp> BindCore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TComp, TValue>(
        ComponentNode<TComp> node, Expression<Func<TValue>> expr, TValue value, Action<TValue> setter) where TComp : IComponent
    {
        Rendering.RenderArena.Current.ThrowIfRecording("Bind");
        return node.Param("Value", value).Callback("ValueChanged", setter).Param("ValueExpression", expr);
    }
}

/// <summary>Runtime support for <see cref="BindExtensions"/> and its generated interceptors.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class BindRuntime
{
    /// <summary>How many binds took the reflection path (tests use it to check interception).</summary>
    public static int FallbackCount;

    /// <summary>Evaluates what is left of the chain after peeling the <paramref name="depth"/> members generated code reads itself.</summary>
    public static object? Root(LambdaExpression expr, int depth)
    {
        var e = expr.Body;
        for (var i = 0; i < depth; i++) e = ((MemberExpression)e).Expression!;
        return Eval(e);
    }

    internal static (object? Target, MemberInfo Member) Resolve(LambdaExpression expr)
    {
        var body = expr.Body is UnaryExpression { NodeType: ExpressionType.Convert } c ? c.Operand : expr.Body;
        if (body is not MemberExpression m)
            throw new NotSupportedException($"Bind needs a member access like () => model.Name; got {expr.Body.NodeType}.");
        if (m.Member.DeclaringType!.IsValueType)
            throw new NotSupportedException($"Bind cannot write {m.Member.Name}: it lives on a struct, so the write would hit a copy.");
        return (Eval(m.Expression), m.Member);
    }

    internal static object? Get(object? target, MemberInfo member) => member switch
    {
        FieldInfo f => f.GetValue(target),
        PropertyInfo p => p.GetValue(target),
        _ => throw new NotSupportedException(member.ToString()),
    };

    internal static void Set(object? target, MemberInfo member, object? value)
    {
        switch (member)
        {
            case FieldInfo f: f.SetValue(target, value); break;
            case PropertyInfo p: p.SetValue(target, value); break;
            default: throw new NotSupportedException(member.ToString());
        }
    }

    private static object? Eval(Expression? e) => e switch
    {
        null => null, // static member
        ConstantExpression c => c.Value, // closure object or `this`
        MemberExpression m => Get(Eval(m.Expression), m.Member),
        UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.TypeAs } u => Eval(u.Operand),
        _ => throw new NotSupportedException($"Bind cannot evaluate {e.NodeType} without compiling; set Value/ValueChanged/ValueExpression with Param."),
    };
}

using System.Diagnostics;

namespace PureMarkup.Blazor;

/// <summary>
/// Generates typed parameter methods on <c>Comp&lt;T&gt;()</c> for <see cref="Type"/> (or, with <see cref="WholeAssembly"/>, for every
/// public component in its assembly), in the project that carries the attribute: <c>[assembly: GenerateMarkup(typeof(MudButton))]</c>.
/// Use it for third-party components and for components derived from them. C# components declared in the project itself
/// get methods without it. The generated classes are <c>internal</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
[Conditional("PUREMARKUP_KEEP_ATTRIBUTES")]
public sealed class GenerateMarkupAttribute(Type type) : Attribute
{
    /// <summary>The component to generate methods for (an open generic such as <c>typeof(MudSelect&lt;&gt;)</c> works too).</summary>
    public Type Type { get; } = type;

    /// <summary>Generate for every public component in the assembly that declares <see cref="Type"/>.</summary>
    public bool WholeAssembly { get; set; }

    /// <summary>Namespace of the generated classes. Default: <c>PureMarkup.Blazor</c>, so no extra <c>using</c> is needed.</summary>
    public string? Namespace { get; set; }
}

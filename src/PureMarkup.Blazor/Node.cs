using System.ComponentModel;

namespace PureMarkup.Blazor;

/// <summary>
/// A piece of markup: an element, text, component, fragment or nothing (<see cref="Empty"/>).
/// A node is a handle into the current render's arena, so it is only valid during the render that created it.
/// Build nodes inside <c>Render()</c> (or a fragment lambda); never keep them in fields. For content that never
/// changes, use <see cref="H.Static"/>.
/// </summary>
public readonly struct Node : IEquatable<Node>
{
    internal readonly int Index;
    internal readonly int Scope;

    internal Node(int index, int scope)
    {
        Index = index;
        Scope = scope;
    }

    /// <summary>Writes nothing but keeps its slot, so later siblings keep their sequence numbers.</summary>
    public static Node Empty => default;

    /// <summary>True for <see cref="Empty"/>.</summary>
    public bool IsEmpty => Index == 0;

    /// <summary>A string child is a text node.</summary>
    public static implicit operator Node(string? text) => H.Text(text);

    /// <inheritdoc/>
    public bool Equals(Node other) => Index == other.Index && Scope == other.Scope;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Node n && Equals(n);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Index, Scope);

    /// <summary>Equality.</summary>
    public static bool operator ==(Node left, Node right) => left.Equals(right);

    /// <summary>Inequality.</summary>
    public static bool operator !=(Node left, Node right) => !left.Equals(right);
}

/// <summary>Implemented by every element handle; attribute and event methods are extensions constrained to it.</summary>
public interface IElement
{
    /// <summary>The underlying node.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    Node Node { get; }
}

/// <summary>An element with any tag name (custom elements, SVG until it gets its own table). Global attributes only.</summary>
public readonly struct Element : IElement
{
    private readonly Node _node;

    internal Element(Node node) => _node = node;

    /// <inheritdoc/>
    public Node Node => _node;

    /// <summary>Converts to a node.</summary>
    public static implicit operator Node(Element e) => e._node;
}

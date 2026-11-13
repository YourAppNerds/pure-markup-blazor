using System.Text;
using System.Text.Encodings.Web;

namespace PureMarkup.Blazor.Rendering;

/// <summary>Serializes a node tree to HTML for <see cref="H.Static"/>, encoding as <c>HtmlRenderer</c> does.</summary>
internal static class StaticHtml
{
    public static string Render(Func<Node> build)
    {
        var a = RenderArena.Enter(receiver: null);
        try
        {
            var root = build();
            var sb = new StringBuilder();
            Write(a, sb, a.Check(root));
            return sb.ToString();
        }
        finally { RenderArena.Exit(a); }
    }

    private static void Write(RenderArena a, StringBuilder sb, int i)
    {
        if (i == 0) return;
        var f = a.Nodes[i];
        var enc = HtmlEncoder.Default;
        switch (f.Kind)
        {
            case NodeKind.Element:
                sb.Append('<').Append(f.Name);
                for (var k = f.FirstAttr; k != 0; k = a.Attrs[k].Next)
                {
                    var at = a.Attrs[k];
                    var value = at.Kind switch
                    {
                        AttrKind.String or AttrKind.Object => at.Value,
                        _ => throw new InvalidOperationException($"Static(...) cannot hold '{at.Name}': only plain attributes are allowed."),
                    };
                    if (value is null or false) continue;
                    sb.Append(' ').Append(at.Name);
                    if (value is true) continue;
                    sb.Append("=\"").Append(enc.Encode(Convert.ToString(value, System.Globalization.CultureInfo.CurrentCulture) ?? "")).Append('"');
                }
                sb.Append('>');
                if (HtmlData.IsVoid(f.Name!)) return;
                WriteKids(a, sb, f);
                sb.Append("</").Append(f.Name).Append('>');
                break;
            case NodeKind.Text:
                sb.Append(enc.Encode(f.Name ?? ""));
                break;
            case NodeKind.Markup:
                sb.Append(f.Name);
                break;
            case NodeKind.Fragment:
            case NodeKind.Repeat:
                WriteKids(a, sb, f);
                break;
            default:
                throw new InvalidOperationException($"Static(...) cannot hold a {f.Kind} node: elements, text and fragments only.");
        }
    }

    private static void WriteKids(RenderArena a, StringBuilder sb, NodeFrame f)
    {
        for (var k = 0; k < f.KidCount; k++) Write(a, sb, a.Kids[f.FirstKid + k]);
    }
}

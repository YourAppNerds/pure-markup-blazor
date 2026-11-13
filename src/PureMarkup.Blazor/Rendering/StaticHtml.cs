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
                var p = f.Preset;
                for (var k = f.FirstAttr; k != 0; k = a.Attrs[k].Next)
                {
                    var at = a.Attrs[k];
                    if (p is not null)
                    {
                        if (k == f.ClassAttr) { WriteAttr(sb, at.Name, p.MergedClass(ref f, (string?)at.Value)); continue; }
                        if (k == f.StyleAttr) { WriteAttr(sb, at.Name, PresetData.MergeStyle(p.Style, (string?)at.Value)); continue; }
                        if (at.Kind == AttrKind.Preset)
                        {
                            WriteAttr(sb, p.ClassName ?? "class", p.ClassAtMarker(a, ref f));
                            if (f.StyleAttr == 0 && !PresetData.HasLocal(a, ref f, 0, "style")) WriteAttr(sb, "style", p.Style);
                            foreach (var pa in p.Attrs)
                                if (!PresetData.IsOverridden(a, ref f, pa)) WriteAttr(sb, pa);
                            continue;
                        }
                    }
                    WriteAttr(sb, at);
                }
                sb.Append('>');
                if (HtmlData.IsVoid(f.Name!)) return;
                if (f.KidCount == 0 && p is { KidCount: > 0 }) WriteKids(p.KidsArena!, sb, p.FirstKid, p.KidCount);
                else WriteKids(a, sb, f.FirstKid, f.KidCount);
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
                WriteKids(a, sb, f.FirstKid, f.KidCount);
                break;
            default:
                throw new InvalidOperationException($"Static(...) cannot hold a {f.Kind} node: elements, text and fragments only.");
        }
    }

    private static void WriteAttr(StringBuilder sb, in AttrFrame at)
    {
        if (at.Kind is not (AttrKind.String or AttrKind.Object))
            throw new InvalidOperationException($"Static(...) cannot hold '{at.Name}': only plain attributes are allowed.");
        WriteAttr(sb, at.Name, at.Value);
    }

    private static void WriteAttr(StringBuilder sb, string name, object? value)
    {
        if (value is null or false) return;
        sb.Append(' ').Append(name);
        if (value is true) return;
        sb.Append("=\"").Append(HtmlEncoder.Default.Encode(Convert.ToString(value, System.Globalization.CultureInfo.CurrentCulture) ?? "")).Append('"');
    }

    private static void WriteKids(RenderArena a, StringBuilder sb, int first, int count)
    {
        for (var k = 0; k < count; k++) Write(a, sb, a.Kids[first + k]);
    }
}

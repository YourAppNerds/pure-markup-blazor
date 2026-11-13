using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;

namespace PureMarkup.Blazor.Styling;

/// <summary>
/// Writes the stylesheet for an assembly's <c>[Stylesheet]</c> types. The build runs it (in a separate process, after
/// compile) to produce <c>{Assembly}.pm.css</c>; tests call it directly.
/// </summary>
public static class CssEmitter
{
    /// <summary>The layer order statement every generated file starts with.</summary>
    public const string LayerOrder = "@layer reset, vendor, pm.base, pm.preset, pm.local, pm.preset-state, pm.local-state, utilities;";

    /// <summary>The stylesheet for all <c>[Stylesheet]</c> types in <paramref name="assembly"/>, ordered by full type name.</summary>
    [RequiresUnreferencedCode("Reads every type of the assembly by reflection; build-time only.")]
    public static string Emit(Assembly assembly)
    {
        Type[] types;
        try { types = assembly.GetTypes(); }
        catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t is not null).ToArray()!; }
        return Emit(types.Where(t => t.IsDefined(typeof(StylesheetAttribute), false)));
    }

    /// <summary>The stylesheet for the given <c>[Stylesheet]</c> types, ordered by full type name.</summary>
    [RequiresUnreferencedCode("Reads static members by reflection; build-time only.")]
    public static string Emit(IEnumerable<Type> stylesheets)
    {
        var w = new Writer();
        foreach (var type in stylesheets.OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var members = type.GetFields(flags).Where(f => !f.Name.Contains('<')).Cast<MemberInfo>()
                .Concat(type.GetProperties(flags).Where(p => p.GetIndexParameters().Length == 0))
                .OrderBy(m => m is PropertyInfo p ? BackingOrder(type, p) : m.MetadataToken);
            foreach (var m in members)
            {
                var value = m switch
                {
                    FieldInfo f => f.GetValue(null),
                    PropertyInfo p when p.GetMethod is not null => p.GetValue(null),
                    _ => null,
                };
                if (value is not null) w.Add(type.Name + "." + m.Name, value);
            }
        }
        return w.ToString();
    }

    // Auto-properties sort by their backing field, so properties and fields keep declaration order together.
    private static int BackingOrder([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.NonPublicFields)] Type type, PropertyInfo p) =>
        type.GetField($"<{p.Name}>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)?.MetadataToken ?? p.MetadataToken;

    private sealed class Writer
    {
        private readonly StringBuilder _imports = new();
        private readonly StringBuilder _fonts = new();
        private readonly StringBuilder _root = new();
        private readonly StringBuilder _dark = new();
        private readonly StringBuilder[] _layers = Enumerable.Range(0, 8).Select(_ => new StringBuilder()).ToArray();
        private readonly Dictionary<string, string> _names = new(StringComparer.Ordinal);
        private readonly HashSet<object> _seen = new(ReferenceEqualityComparer.Instance);

        private static readonly string[] LayerNames = ["reset", "vendor", "pm.base", "pm.preset", "pm.local", "pm.preset-state", "pm.local-state", "utilities"];

        public void Add(string source, object value)
        {
            if (!_seen.Add(value)) return;
            switch (value)
            {
                case Rule r:
                    Claim(r.ClassName, source);
                    Block(source, r.Selector, CssLayer.Local, Run(r.Build));
                    break;
                case ICssVariable v:
                    Claim(v.Name, source);
                    if (v.Initial is { } i) _root.Append("    ").Append(v.Name).Append(": ").Append(i.ToString()).AppendLine(";");
                    if (v.Dark is { } dk) _dark.Append("    ").Append(v.Name).Append(": ").Append(dk.ToString()).AppendLine(";");
                    break;
                case GlobalRule g:
                    Block(source, g.Selector, g.Layer, Run(g.Build));
                    break;
                case FontFace f:
                    _fonts.Append("/* ").Append(source).AppendLine(" */").AppendLine("@font-face {");
                    foreach (var d in Run(f.Build).Declarations) _fonts.Append("  ").Append(d.Key).Append(": ").Append(d.Value).AppendLine(";");
                    _fonts.AppendLine("}");
                    break;
                case LayerImport imp:
                    _imports.Append("@import url(\"").Append(imp.Url).Append("\") layer(").Append(imp.Layer).AppendLine(");");
                    break;
            }
        }

        private void Claim(string name, string source)
        {
            if (_names.TryGetValue(name, out var other))
                throw new InvalidOperationException($"Duplicate CSS name '{name}' for {source} and {other}.");
            _names[name] = source;
        }

        private static RuleBuilder Run(Action<RuleBuilder> build)
        {
            var b = new RuleBuilder();
            build(b);
            return b;
        }

        private void Block(string source, string selector, CssLayer layer, RuleBuilder body)
        {
            var sb = _layers[(int)layer];
            sb.Append("  /* ").Append(source).AppendLine(" */");
            Flatten(selector, layer, [], body);
        }

        private void Flatten(string selector, CssLayer layer, List<string> atRules, RuleBuilder body)
        {
            if (body.Declarations.Count > 0)
            {
                var sb = _layers[(int)layer];
                var indent = "  ";
                foreach (var at in atRules)
                {
                    sb.Append(indent).Append(at).AppendLine(" {");
                    indent += "  ";
                }
                sb.Append(indent).Append(selector).AppendLine(" {");
                foreach (var d in body.Declarations) sb.Append(indent).Append("  ").Append(d.Key).Append(": ").Append(d.Value).AppendLine(";");
                sb.Append(indent).AppendLine("}");
                for (var i = atRules.Count - 1; i >= 0; i--)
                {
                    indent = indent[..^2];
                    sb.Append(indent).AppendLine("}");
                }
            }
            foreach (var c in body.Children)
            {
                var sel = c.Pattern is null ? selector : Apply(c.Pattern, selector);
                var l = c.State ? StateLayer(layer) : layer;
                var at = c.AtRule is null ? atRules : [.. atRules, c.AtRule];
                Flatten(sel, l, at, c.Body);
            }
        }

        private static CssLayer StateLayer(CssLayer layer) => layer switch
        {
            CssLayer.Local => CssLayer.LocalState,
            CssLayer.Preset => CssLayer.PresetState,
            _ => layer,
        };

        // Replaces & with each comma-separated part of the selector ("h1, h2" + "&:hover" -> "h1:hover, h2:hover").
        private static string Apply(string pattern, string selector) =>
            string.Join(", ", SplitList(selector).Select(s => pattern.Replace("&", s)));

        private static IEnumerable<string> SplitList(string selector)
        {
            var depth = 0;
            var start = 0;
            for (var i = 0; i < selector.Length; i++)
            {
                var c = selector[i];
                if (c is '(' or '[') depth++;
                else if (c is ')' or ']') depth--;
                else if (c == ',' && depth == 0)
                {
                    yield return selector[start..i].Trim();
                    start = i + 1;
                }
            }
            yield return selector[start..].Trim();
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("/* Generated by PureMarkup.Blazor from [Stylesheet] types. Do not edit. */");
            sb.AppendLine(LayerOrder);
            sb.Append(_imports);
            sb.Append(_fonts);
            if (_root.Length > 0)
            {
                _layers[(int)CssLayer.Base].Insert(0, $"  :root {{\n{_root}  }}\n" + (_dark.Length == 0 ? "" :
                    $"  @media (prefers-color-scheme: dark) {{\n    :root:not(.theme-light) {{\n{Indent(_dark)}    }}\n  }}\n  :root.theme-dark {{\n{_dark}  }}\n"));
            }
            for (var i = 0; i < _layers.Length; i++)
            {
                if (_layers[i].Length == 0) continue;
                sb.Append("@layer ").Append(LayerNames[i]).AppendLine(" {").Append(_layers[i]).AppendLine("}");
            }
            return sb.ToString().Replace("\r\n", "\n");
        }

        private static string Indent(StringBuilder sb) => string.Join("", sb.ToString().Split('\n').Select(l => l.Trim().Length == 0 ? "" : "  " + l.TrimEnd('\r') + "\n"));
    }
}

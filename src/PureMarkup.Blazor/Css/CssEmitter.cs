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

    /// <summary>
    /// The stylesheet for all <c>[Stylesheet]</c> types in <paramref name="assembly"/>, ordered by full type name, plus the
    /// <c>pm.preset</c> copies of the rules its presets (static <c>Preset&lt;T&gt;</c> properties and fields of any type) apply.
    /// </summary>
    [RequiresUnreferencedCode("Reads every type of the assembly by reflection; build-time only.")]
    public static string Emit(Assembly assembly)
    {
        Type[] types;
        try { types = assembly.GetTypes(); }
        catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t is not null).ToArray()!; }
        return Emit(types.Where(t => t.IsDefined(typeof(StylesheetAttribute), false)), types.Where(t => !t.ContainsGenericParameters));
    }

    /// <summary>The stylesheet for the given <c>[Stylesheet]</c> types, ordered by full type name.</summary>
    [RequiresUnreferencedCode("Reads static members by reflection; build-time only.")]
    public static string Emit(IEnumerable<Type> stylesheets) => Emit(stylesheets, []);

    /// <summary>
    /// The stylesheet for the given <c>[Stylesheet]</c> types, plus <c>pm.preset</c> copies of the rules applied by the static
    /// <c>Preset&lt;T&gt;</c> members of <paramref name="presetTypes"/> (types by full name, members in declaration order;
    /// a preset's base before the preset, so a derived preset's rules come later and win).
    /// </summary>
    [RequiresUnreferencedCode("Reads static members by reflection; build-time only.")]
    public static string Emit(IEnumerable<Type> stylesheets, IEnumerable<Type> presetTypes)
    {
        var w = new Writer();
        foreach (var type in stylesheets.OrderBy(t => t.FullName, StringComparer.Ordinal))
            foreach (var m in StaticMembers(type))
                if (Value(m) is { } value) w.Add(type.Name + "." + m.Name, value);

        var presets = new List<(string Source, PresetData Data)>();
        foreach (var type in presetTypes.OrderBy(t => t.FullName, StringComparer.Ordinal))
            foreach (var m in StaticMembers(type))
            {
                var t = m is FieldInfo f ? f.FieldType : ((PropertyInfo)m).PropertyType;
                if (!t.IsGenericType || t.GetGenericTypeDefinition() != typeof(Preset<>)) continue;
                var source = type.Name + "." + m.Name;
                try
                {
                    if (Value(m) is IPreset p) presets.Add((source, p.Data));
                }
                catch (Exception e)
                {
                    var inner = e is TargetInvocationException { InnerException: { } ie } ? ie : e;
                    throw new InvalidOperationException($"Preset {source} failed: {inner.Message}", inner);
                }
            }
        w.AddPresets(presets);
        return w.ToString();
    }

    private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    [RequiresUnreferencedCode("Reads static members by reflection; build-time only.")]
    private static IEnumerable<MemberInfo> StaticMembers(Type type) =>
        type.GetFields(StaticFlags).Where(f => !f.Name.Contains('<')).Cast<MemberInfo>()
            .Concat(type.GetProperties(StaticFlags).Where(p => p.GetIndexParameters().Length == 0 && p.GetMethod is not null))
            .OrderBy(m => m is PropertyInfo p ? BackingOrder(type, p) : m.MetadataToken);

    private static object? Value(MemberInfo m) => m switch
    {
        FieldInfo f => f.GetValue(null),
        PropertyInfo p => p.GetValue(null),
        _ => null,
    };

    // Auto-properties sort by their backing field, so properties and fields keep declaration order together.
    private static int BackingOrder([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.NonPublicFields)] Type type, PropertyInfo p) =>
        type.GetField($"<{p.Name}>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)?.MetadataToken ?? p.MetadataToken;

    private sealed class Writer
    {
        private readonly StringBuilder _imports = new();
        private readonly StringBuilder _fonts = new();
        private readonly StringBuilder _root = new();
        private readonly StringBuilder _dark = new();
        private readonly StringBuilder _light = new();
        private readonly Dictionary<Rule, string> _ruleSources = new(ReferenceEqualityComparer.Instance);
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
                    _ruleSources[r] = source;
                    Block(source, r.Selector, CssLayer.Local, Run(r.Build));
                    break;
                case ICssVariable v:
                    Claim(v.Name, source);
                    if (v.Initial is { } i) _root.Append("    ").Append(v.Name).Append(": ").Append(i.ToString()).AppendLine(";");
                    if (v.Dark is { } dk)
                    {
                        _dark.Append("    ").Append(v.Name).Append(": ").Append(dk.ToString()).AppendLine(";");
                        _light.Append("    ").Append(v.Name).Append(": ").Append(v.Initial?.ToString()).AppendLine(";");
                    }
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

        /// <summary>
        /// Writes each preset rule once to <c>pm.preset</c> under its preset-origin class, bases first (depth-first), so a
        /// <c>.With</c> preset's own rules follow its base's and win on equal specificity.
        /// </summary>
        public void AddPresets(List<(string Source, PresetData Data)> presets)
        {
            var names = new Dictionary<PresetData, string>(ReferenceEqualityComparer.Instance);
            foreach (var (source, data) in presets) names.TryAdd(data, source);
            var done = new HashSet<PresetData>(ReferenceEqualityComparer.Instance);
            var rules = new HashSet<Rule>(ReferenceEqualityComparer.Instance);
            foreach (var (_, data) in presets) Visit(data);

            void Visit(PresetData d)
            {
                if (!done.Add(d)) return;
                if (d.Base is { } b) Visit(b);
                foreach (var r in d.Rules)
                {
                    if (!rules.Add(r)) continue;
                    var preset = names.TryGetValue(d, out var n) ? n : "a preset";
                    var rule = _ruleSources.TryGetValue(r, out var s) ? s : r.ClassName;
                    Block(rule + " in " + preset, "." + r.PresetClassName, CssLayer.Preset, Run(r.Build));
                }
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
                // With dark values: the media query (unless <html> has pm-light), then pm-dark / pm-light on any element. Custom
                // properties inherit and an element's own declaration beats an inherited one, so the nearest themed ancestor wins.
                _layers[(int)CssLayer.Base].Insert(0, _dark.Length == 0 ? $"  :root {{\n{_root}  }}\n" :
                    $"  :root {{\n    color-scheme: light dark;\n{_root}  }}\n" +
                    $"  @media (prefers-color-scheme: dark) {{\n    :root:not(.{ThemeClass.Light}) {{\n{Indent(_dark)}    }}\n  }}\n" +
                    $"  .{ThemeClass.Dark} {{\n    color-scheme: dark;\n{_dark}  }}\n" +
                    $"  .{ThemeClass.Light} {{\n    color-scheme: light;\n{_light}  }}\n");
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

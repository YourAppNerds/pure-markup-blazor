using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using PureMarkup.Blazor.Styling;

namespace PureMarkup.Blazor;

/// <summary>
/// Writes <c>&lt;link rel="stylesheet"&gt;</c> for each build-generated <c>{Assembly}.styles.css</c>, through the app's
/// static asset map so the URL is fingerprinted. Place it in <c>&lt;head&gt;</c> with <c>StyleLink()</c>.
/// </summary>
public sealed class StyleSheetLinks : IComponent
{
    private static string[]? s_discovered;
    private RenderHandle _handle;
    private IReadOnlyList<string>? _assemblies;

    /// <summary>
    /// Assembly names whose stylesheets to link, in cascade order (dependencies first). Default: every assembly with
    /// <c>[Stylesheet]</c> types reachable from the entry assembly, dependencies first.
    /// </summary>
    [Parameter] public IReadOnlyList<string>? Assemblies { get; set; }

    void IComponent.Attach(RenderHandle renderHandle) => _handle = renderHandle;

    Task IComponent.SetParametersAsync(ParameterView parameters)
    {
        _assemblies = parameters.TryGetValue<IReadOnlyList<string>>(nameof(Assemblies), out var a) ? a : null;
        _handle.Render(Build);
        return Task.CompletedTask;
    }

    private void Build(RenderTreeBuilder b)
    {
        var names = _assemblies ?? (s_discovered ??= Discover());
        var assets = _handle.Assets;
        foreach (var name in names)
        {
            b.OpenElement(0, "link");
            b.AddAttribute(1, "rel", "stylesheet");
            b.AddAttribute(2, "href", assets[name + ".styles.css"]);
            b.CloseElement();
        }
    }

    /// <summary>Assemblies with <c>[Stylesheet]</c> types reachable from the entry assembly, dependencies first.</summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification =
        "A reference the trimmer removed belongs to an assembly with no code left, so its stylesheet is not needed.")]
    internal static string[] Discover(Assembly? root = null)
    {
        root ??= Assembly.GetEntryAssembly();
        if (root is null) return [];
        var order = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        Visit(root);
        return [.. order];

        void Visit(Assembly a)
        {
            var name = a.GetName().Name!;
            if (!seen.Add(name)) return;
            // Framework assemblies never hold stylesheets; skipping them keeps the walk small.
            if (name.StartsWith("System", StringComparison.Ordinal) || name.StartsWith("Microsoft.", StringComparison.Ordinal)) return;
            foreach (var r in a.GetReferencedAssemblies())
            {
                if (r.Name is null || seen.Contains(r.Name)) continue;
                Assembly loaded;
                try { loaded = Assembly.Load(r); }
                catch (IOException) { continue; }
                Visit(loaded);
            }
            if (a.IsDefined(typeof(HasStylesheetsAttribute), false)) order.Add(name);
        }
    }
}

public static partial class H
{
    /// <summary>The <c>&lt;link&gt;</c> tags for the build-generated stylesheets (<see cref="StyleSheetLinks"/>); place it in <c>Head()</c>.</summary>
    public static ComponentNode<StyleSheetLinks> StyleLink() => Comp<StyleSheetLinks>();

    /// <summary>The <c>&lt;link&gt;</c> tags for the given assemblies' stylesheets, in this order.</summary>
    public static ComponentNode<StyleSheetLinks> StyleLink(params string[] assemblies) =>
        Comp<StyleSheetLinks>().Param(nameof(StyleSheetLinks.Assemblies), (IReadOnlyList<string>)assemblies);
}

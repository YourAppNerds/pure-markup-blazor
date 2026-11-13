using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using PureMarkup.Blazor.Styling;

namespace PureMarkup.Blazor;

/// <summary>
/// Writes <c>&lt;link rel="stylesheet"&gt;</c> for each build-generated <c>{Assembly}.pm.css</c>, through the app's
/// static asset map so the URL is fingerprinted. Place it in <c>&lt;head&gt;</c> with <c>StyleLink()</c>.
/// When the app has the SDK's scoped CSS bundle (<c>{EntryAssembly}.styles.css</c>, written for <c>.razor.css</c> files and
/// packages with scoped CSS such as QuickGrid), it is linked first, as a Razor template's <c>App.razor</c> would.
/// </summary>
public sealed class StyleSheetLinks : IComponent
{
    private static string[]? s_discovered;
    private RenderHandle _handle;
    private IReadOnlyList<string>? _assemblies;
    private bool _scopedCss;

    /// <summary>
    /// Assembly names whose stylesheets to link, in cascade order (dependencies first). Default: every assembly with
    /// <c>[Stylesheet]</c> types reachable from the entry assembly, dependencies first.
    /// </summary>
    [Parameter] public IReadOnlyList<string>? Assemblies { get; set; }

    /// <summary>
    /// Also link the SDK's scoped CSS bundle <c>{EntryAssembly}.styles.css</c> when the asset map has it. Default <c>true</c>;
    /// set <c>false</c> when the app links it itself or imports it into a layer with <see cref="Css.ImportLayer"/>.
    /// </summary>
    [Parameter] public bool ScopedCss { get; set; } = true;

    void IComponent.Attach(RenderHandle renderHandle) => _handle = renderHandle;

    Task IComponent.SetParametersAsync(ParameterView parameters)
    {
        _assemblies = parameters.TryGetValue<IReadOnlyList<string>>(nameof(Assemblies), out var a) ? a : null;
        _scopedCss = !parameters.TryGetValue<bool>(nameof(ScopedCss), out var s) || s;
        _handle.Render(Build);
        return Task.CompletedTask;
    }

    private void Build(RenderTreeBuilder b)
    {
        var names = _assemblies ?? (s_discovered ??= Discover());
        var scoped = _scopedCss ? Assembly.GetEntryAssembly()?.GetName().Name : null;
        foreach (var href in Hrefs(_handle.Assets, names, scoped))
        {
            b.OpenElement(0, "link");
            b.AddAttribute(1, "rel", "stylesheet");
            b.AddAttribute(2, "href", href);
            b.CloseElement();
        }
    }

    /// <summary>The generated stylesheets' URLs, after <paramref name="scopedBundleOf"/>'s scoped CSS bundle if the app has one.</summary>
    internal static IEnumerable<string> Hrefs(ResourceAssetCollection assets, IReadOnlyList<string> names, string? scopedBundleOf)
    {
        if (scopedBundleOf is not null && HasAsset(assets, scopedBundleOf + ".styles.css"))
            yield return assets[scopedBundleOf + ".styles.css"];
        foreach (var name in names) yield return assets[name + ".pm.css"];
    }

    // The indexer returns the key for a missing asset, so look the label up; an app without fingerprinting has no label.
    private static bool HasAsset(ResourceAssetCollection assets, string key)
    {
        foreach (var asset in assets)
        {
            if (asset.Url == key) return true;
            if (asset.Properties is { } props)
                foreach (var p in props)
                    if (p.Name == "label" && p.Value == key) return true;
        }
        return false;
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
    /// <summary>
    /// The <c>&lt;link&gt;</c> tags for the build-generated stylesheets and the SDK's scoped CSS bundle
    /// (<see cref="StyleSheetLinks"/>); place it in <c>Head()</c>.
    /// </summary>
    public static ComponentNode<StyleSheetLinks> StyleLink() => Comp<StyleSheetLinks>();

    /// <summary>As <see cref="StyleLink()"/>; <paramref name="scopedCss"/> <c>false</c> leaves out the SDK's scoped CSS bundle.</summary>
    public static ComponentNode<StyleSheetLinks> StyleLink(bool scopedCss) =>
        Comp<StyleSheetLinks>().Param(nameof(StyleSheetLinks.ScopedCss), scopedCss);

    /// <summary>The <c>&lt;link&gt;</c> tags for the given assemblies' stylesheets, in this order.</summary>
    public static ComponentNode<StyleSheetLinks> StyleLink(params string[] assemblies) =>
        Comp<StyleSheetLinks>().Param(nameof(StyleSheetLinks.Assemblies), (IReadOnlyList<string>)assemblies);
}

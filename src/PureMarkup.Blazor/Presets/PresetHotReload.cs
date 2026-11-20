using System.Reflection.Metadata;
using PureMarkup.Blazor.Styling;

[assembly: MetadataUpdateHandler(typeof(PureMarkup.Blazor.PresetHotReload))]

namespace PureMarkup.Blazor;

/// <summary>
/// Hot Reload: an edited preset lambda keeps its delegate, but its recorded writes are stale, so every preset is dropped
/// before the app re-renders (Blazor re-renders in its own <c>UpdateApplication</c>, after every <c>ClearCache</c>).
/// Then, in a Debug build, the generated <c>.pm.css</c> files are rewritten (<see cref="CssHotReload"/>); this runs in
/// <c>ClearCache</c> too, after the presets are dropped, so presets record again before their rules are re-evaluated.
/// </summary>
internal static class PresetHotReload
{
    public static void ClearCache(Type[]? updatedTypes)
    {
        PresetCache.Clear();
        CssHotReload.Refresh();
    }
}

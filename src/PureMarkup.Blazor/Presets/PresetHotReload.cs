using System.Reflection.Metadata;

[assembly: MetadataUpdateHandler(typeof(PureMarkup.Blazor.PresetHotReload))]

namespace PureMarkup.Blazor;

/// <summary>
/// Hot Reload: an edited preset lambda keeps its delegate, but its recorded writes are stale, so every preset is dropped
/// before the app re-renders (Blazor re-renders in its own <c>UpdateApplication</c>, after every <c>ClearCache</c>).
/// The generated <c>.pm.css</c> is not rewritten: an edited rule body, or a rule newly used in a preset, needs a rebuild.
/// </summary>
internal static class PresetHotReload
{
    public static void ClearCache(Type[]? updatedTypes) => PresetCache.Clear();
}

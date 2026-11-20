using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PureMarkup.Blazor.Styling;

/// <summary>
/// CSS Hot Reload (development only). A Debug build records the path of each assembly's generated <c>{Assembly}.pm.css</c>
/// (in <c>obj/</c>) as <c>[AssemblyMetadata("PureMarkup.Blazor.CssFile", ...)]</c>. After a Hot Reload update the server
/// re-evaluates the stylesheets in-process (an edited rule lambda runs its new body) and rewrites that file. The file is a
/// static web asset, so <c>dotnet watch</c> sees the change and pushes it to the browser (its refresh script reloads the
/// stylesheet links), and the dev server's static asset handler serves the new content at the same URL.
/// Release builds record no path, so nothing runs; trimmed apps drop the handler.
/// </summary>
internal static class CssHotReload
{
    /// <summary>The metadata key the build targets write in Debug builds.</summary>
    public const string MetadataKey = "PureMarkup.Blazor.CssFile";

    /// <summary>Rewrites the stylesheet of every loaded assembly that has one and a recorded path.</summary>
    public static void Refresh()
    {
        if (OperatingSystem.IsBrowser()) return; // WebAssembly: no file to write; the server process rewrites it.
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!assembly.IsDefined(typeof(HasStylesheetsAttribute), false) || CssFileOf(assembly) is not { } path) continue;
            try
            {
                Rewrite(assembly, path);
            }
            catch (Exception e)
            {
                var inner = e is TargetInvocationException { InnerException: { } ie } ? ie : e;
                Console.Error.WriteLine($"PureMarkup.Blazor CSS Hot Reload: {Path.GetFileName(path)} not updated: {inner.Message}");
            }
        }
    }

    /// <summary>The generated stylesheet's path recorded by a Debug build, if the file exists.</summary>
    public static string? CssFileOf(Assembly assembly)
    {
        foreach (var a in assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
            if (a.Key == MetadataKey && !string.IsNullOrEmpty(a.Value) && File.Exists(a.Value)) return a.Value;
        return null;
    }

    /// <summary>Writes <paramref name="assembly"/>'s stylesheet to <paramref name="path"/>; <c>false</c> when the content is unchanged.</summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification =
        "Hot Reload never runs in a trimmed app (the trimmer removes MetadataUpdateHandler there), and Release builds record no path.")]
    public static bool Rewrite(Assembly assembly, string path)
    {
        var css = CssEmitter.Emit(assembly);
        if (File.Exists(path) && File.ReadAllText(path) == css) return false;
        File.WriteAllText(path, css);
        return true;
    }
}

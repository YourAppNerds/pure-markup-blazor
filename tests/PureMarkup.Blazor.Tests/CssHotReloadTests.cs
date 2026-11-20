using System.Reflection;
using System.Runtime.Loader;

namespace PureMarkup.Blazor.Tests;

public class CssHotReloadTests
{
    [Fact]
    public void Rewrite_writes_the_emitted_stylesheet_and_skips_an_unchanged_file()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pm-hotreload-{Guid.NewGuid():N}.css");
        try
        {
            File.WriteAllText(path, "/* stale */");
            var assembly = typeof(TestCss).Assembly;
            Assert.True(CssHotReload.Rewrite(assembly, path));
            Assert.Equal(CssEmitter.Emit(assembly), File.ReadAllText(path));
            var written = File.GetLastWriteTimeUtc(path);
            Assert.False(CssHotReload.Rewrite(assembly, path));
            Assert.Equal(written, File.GetLastWriteTimeUtc(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void An_assembly_without_a_recorded_path_is_skipped() => Assert.Null(CssHotReload.CssFileOf(typeof(TestCss).Assembly));

    /// <summary>The build targets record the generated file's path in Debug builds only (this test project builds the sample first).</summary>
    [Fact]
    public void Sample_records_the_generated_stylesheet_path_in_debug_builds_only()
    {
        var bin = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var configuration = bin.Parent!.Name;
        var repo = bin.Parent.Parent!.Parent!.Parent!.Parent!.FullName;
        var dll = Path.Combine(repo, "samples", "WebApp", "WebApp", "bin", configuration, "net10.0", "WebApp.dll");

        var alc = new AssemblyLoadContext("hot-reload-test", isCollectible: true);
        try
        {
            var recorded = alc.LoadFromAssemblyPath(dll).GetCustomAttributesData()
                .Where(a => a.AttributeType.FullName == typeof(AssemblyMetadataAttribute).FullName
                    && (string?)a.ConstructorArguments[0].Value == CssHotReload.MetadataKey)
                .Select(a => (string?)a.ConstructorArguments[1].Value)
                .SingleOrDefault();
            if (configuration == "Debug")
                Assert.Equal(Path.Combine(repo, "samples", "WebApp", "WebApp", "obj", "Debug", "net10.0", "pmcss", "WebApp.pm.css"), recorded);
            else
                Assert.Null(recorded);
        }
        finally
        {
            alc.Unload();
        }
    }
}

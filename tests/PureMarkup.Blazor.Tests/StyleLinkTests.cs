using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;

namespace PureMarkup.Blazor.Tests;

public class StyleLinkTests
{
    private static ResourceAsset Asset(string url, string label) => new(url, [new ResourceAssetProperty("label", label)]);

    private static readonly ResourceAssetCollection WithBundle = new([
        Asset("App.abc123.styles.css", "App.styles.css"),
        Asset("Lib.def456.pm.css", "Lib.pm.css"),
        Asset("App.ghi789.pm.css", "App.pm.css"),
    ]);

    [Fact]
    public void Links_the_scoped_bundle_first_then_the_generated_stylesheets_in_order() =>
        Assert.Equal(["App.abc123.styles.css", "Lib.def456.pm.css", "App.ghi789.pm.css"],
            StyleSheetLinks.Hrefs(WithBundle, ["Lib", "App"], "App"));

    [Fact]
    public void Leaves_out_the_scoped_bundle_when_the_app_has_none_or_opts_out()
    {
        Assert.Equal(["Lib.def456.pm.css"], StyleSheetLinks.Hrefs(WithBundle, ["Lib"], "Other"));
        Assert.Equal(["Lib.def456.pm.css"], StyleSheetLinks.Hrefs(WithBundle, ["Lib"], null));
        Assert.Equal(["Lib.pm.css"], StyleSheetLinks.Hrefs(ResourceAssetCollection.Empty, ["Lib"], "App"));
    }

    /// <summary>
    /// The sample references QuickGrid, whose scoped CSS makes the SDK write WebApp.styles.css; the generated stylesheet must sit
    /// beside it under its own name (this test project builds the sample first, see its csproj).
    /// </summary>
    [Fact]
    public void Sample_build_serves_the_sdk_scoped_bundle_and_the_generated_stylesheets_side_by_side()
    {
        // tests/PureMarkup.Blazor.Tests/bin/{Configuration}/net10.0/
        var bin = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var configuration = bin.Parent!.Name;
        var repo = bin.Parent.Parent!.Parent!.Parent!.Parent!.FullName;
        var output = Path.Combine(repo, "samples", "WebApp", "WebApp", "bin", configuration, "net10.0");

        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "WebApp.staticwebassets.endpoints.json")));
        var routes = doc.RootElement.GetProperty("Endpoints").EnumerateArray().Select(e => e.GetProperty("Route").GetString()!).ToHashSet();

        Assert.Contains("WebApp.styles.css", routes);
        Assert.Contains("WebApp.pm.css", routes);
        Assert.Contains("WebApp.Client.pm.css", routes);
        Assert.Contains(routes, r => Regex.IsMatch(r, @"^WebApp\.[a-z0-9]+\.styles\.css$"));
        Assert.Contains(routes, r => Regex.IsMatch(r, @"^WebApp\.[a-z0-9]+\.pm\.css$"));
        Assert.Contains(routes, r => Regex.IsMatch(r, @"^WebApp\.Client\.[a-z0-9]+\.pm\.css$"));

        var generated = File.ReadAllText(Path.Combine(repo, "samples", "WebApp", "WebApp", "obj", configuration, "net10.0", "pmcss", "WebApp.pm.css"));
        Assert.Contains(CssEmitter.LayerOrder, generated);
    }
}

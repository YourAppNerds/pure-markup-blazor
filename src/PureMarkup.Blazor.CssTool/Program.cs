// Usage: PureMarkup.Blazor.CssTool <assembly.dll> <out.css> [probe-list.txt]
// Loads the assembly in its own load context (dependencies from the probe list: one runtime .dll path per line, then
// the assembly's folder), calls PureMarkup.Blazor.Styling.CssEmitter.Emit(Assembly) from the app's own copy of the
// library, and writes the result if it changed. Exit code 1 on any error, so the build fails.
using System.Reflection;
using System.Runtime.Loader;

if (args.Length < 2)
{
    Console.Error.WriteLine("usage: PureMarkup.Blazor.CssTool <assembly.dll> <out.css> [probe-list.txt]");
    return 2;
}
var assemblyPath = Path.GetFullPath(args[0]);
var outPath = Path.GetFullPath(args[1]);
var probe = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
if (args.Length > 2)
    foreach (var line in File.ReadAllLines(args[2]))
        if (line.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) && File.Exists(line)) probe.TryAdd(Path.GetFileNameWithoutExtension(line), line);

try
{
    var alc = new AppLoadContext(Path.GetDirectoryName(assemblyPath)!, probe);
    var app = alc.LoadFromAssemblyPath(assemblyPath);
    var lib = alc.LoadFromAssemblyName(new AssemblyName("PureMarkup.Blazor"));
    var emit = lib.GetType("PureMarkup.Blazor.Styling.CssEmitter", throwOnError: true)!.GetMethod("Emit", [typeof(Assembly)])
        ?? throw new InvalidOperationException("PureMarkup.Blazor has no CssEmitter.Emit(Assembly); tool and library versions differ.");
    var css = (string)emit.Invoke(null, [app])!;
    Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
    if (!File.Exists(outPath) || File.ReadAllText(outPath) != css) File.WriteAllText(outPath, css);
    return 0;
}
catch (Exception e)
{
    var inner = e is TargetInvocationException { InnerException: { } ie } ? ie : e;
    Console.Error.WriteLine($"error PMCSS002: writing {Path.GetFileName(outPath)} failed: {inner.GetType().Name}: {inner.Message}");
    return 1;
}

sealed class AppLoadContext(string folder, Dictionary<string, string> probe) : AssemblyLoadContext("PureMarkup.Blazor.CssTool", isCollectible: true)
{
    protected override Assembly? Load(AssemblyName name)
    {
        if (name.Name is null) return null;
        if (probe.TryGetValue(name.Name, out var path)) return LoadFromAssemblyPath(path);
        var local = Path.Combine(folder, name.Name + ".dll");
        return File.Exists(local) ? LoadFromAssemblyPath(local) : null; // null: the shared framework
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace PureMarkup.Blazor.Generators.Analyzers;

/// <summary>
/// Stylesheet purity. The build runs <c>[Stylesheet]</c> types (static initializers and rule bodies) and the static
/// <c>Preset&lt;T&gt;</c> members of the assembly in a separate process, with no DI container, no JS and no app environment,
/// so code there must be pure.
/// <list type="bullet">
/// <item>PMCSS010 (error): DI, JS or network: <c>[Inject]</c>, <c>IServiceProvider</c>, <c>Microsoft.JSInterop</c>,
/// <c>NavigationManager</c>, <c>HttpClient</c>, <c>Microsoft.Extensions.{DependencyInjection,Configuration,Logging,Options}</c>, ...</item>
/// <item>PMCSS011 (error): the build machine: <c>File</c>, <c>Directory</c>, <c>Environment</c> (not <c>NewLine</c>), <c>Process</c>, ...</item>
/// <item>PMCSS012 (warning): a different value per build: <c>DateTime.Now</c>, <c>Guid.NewGuid</c>, <c>Random</c>, <c>Stopwatch</c>, ...</item>
/// <item>PMCSS013 (warning, full builds only): a stylesheet or preset reaches such code through methods, properties or static
/// initializers of other types in the same project.</item>
/// </list>
/// Not seen: code in other assemblies (only their API is matched by name), virtual or interface dispatch, delegates passed in
/// from elsewhere, and reflection.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StylesheetPurityAnalyzer : DiagnosticAnalyzer
{
    private const string Category = "PureMarkup.Styling";
    private const string Why = "the build runs stylesheets and presets in a separate process, with no services, JS or app environment";

    internal static readonly DiagnosticDescriptor Services = new(
        "PMCSS010", "Stylesheet uses services or JS",
        "'{0}' uses '{1}', which needs DI, JS or the network; " + Why,
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor Machine = new(
        "PMCSS011", "Stylesheet reads the build machine",
        "'{0}' uses '{1}', which reads files, the environment or processes; " + Why,
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor Nondeterministic = new(
        "PMCSS012", "Stylesheet value changes per build",
        "'{0}' uses '{1}', which gives a different value on every build; the CSS is written once, at build time",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor Indirect = new(
        "PMCSS013", "Stylesheet reaches impure code",
        "'{0}' reaches '{1}' through '{2}'; " + Why,
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, customTags: WellKnownDiagnosticTags.CompilationEnd);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Services, Machine, Nondeterministic, Indirect);

    internal enum Kind { None, Services, Machine, Nondeterministic }

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var stylesheet = start.Compilation.GetTypeByMetadataName("PureMarkup.Blazor.Styling.StylesheetAttribute");
            if (stylesheet is null) return;
            var state = new State(start.Compilation, stylesheet,
                start.Compilation.GetTypeByMetadataName("PureMarkup.Blazor.Preset`1"),
                start.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.InjectAttribute"));
            start.RegisterOperationAction(state.OnOperation, OperationKind.Invocation, OperationKind.ObjectCreation,
                OperationKind.PropertyReference, OperationKind.FieldReference, OperationKind.MethodReference, OperationKind.EventReference);
            start.RegisterSymbolAction(state.OnMember, SymbolKind.Field, SymbolKind.Property);
            start.RegisterCompilationEndAction(state.OnEnd);
        });
    }

    /// <summary>What a referenced API is. Matched by name, so it works without the assemblies being referenced.</summary>
    internal static Kind Classify(ISymbol symbol)
    {
        var type = (symbol as ITypeSymbol ?? symbol.ContainingType)?.OriginalDefinition;
        if (type is null) return Kind.None;
        var ns = type.ContainingNamespace?.ToDisplayString() ?? "";
        var full = ns.Length == 0 ? type.MetadataName : ns + "." + type.MetadataName;
        var member = symbol is ITypeSymbol ? "" : symbol.Name;
        switch (full)
        {
            case "System.DateTime" or "System.DateTimeOffset":
                return member is "Now" or "UtcNow" or "Today" ? Kind.Nondeterministic : Kind.None;
            case "System.Guid":
                return member is "NewGuid" or "CreateVersion7" ? Kind.Nondeterministic : Kind.None;
            case "System.Random" or "System.TimeProvider" or "System.Diagnostics.Stopwatch" or "System.Security.Cryptography.RandomNumberGenerator":
                return Kind.Nondeterministic;
            case "System.Environment":
                return member == "NewLine" ? Kind.None : Kind.Machine;
            case "System.IO.File" or "System.IO.Directory" or "System.IO.FileInfo" or "System.IO.DirectoryInfo" or "System.IO.FileSystemInfo"
                or "System.IO.FileStream" or "System.IO.DriveInfo" or "System.IO.FileSystemWatcher" or "System.Diagnostics.Process"
                or "Microsoft.Win32.Registry" or "Microsoft.Win32.RegistryKey":
                return Kind.Machine;
            case "System.IServiceProvider" or "System.Net.WebClient" or "System.Net.WebRequest" or "System.Net.HttpWebRequest"
                or "Microsoft.AspNetCore.Components.NavigationManager" or "Microsoft.AspNetCore.Components.PersistentComponentState"
                or "Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider":
                return Kind.Services;
        }
        foreach (var prefix in ServiceNamespaces)
            if (ns == prefix || ns.StartsWith(prefix + ".", StringComparison.Ordinal)) return Kind.Services;
        return Kind.None;
    }

    private static readonly string[] ServiceNamespaces =
    [
        "Microsoft.JSInterop", "System.Net.Http", "Microsoft.AspNetCore.Http", "Microsoft.AspNetCore.Components.Server",
        "Microsoft.Extensions.DependencyInjection", "Microsoft.Extensions.Configuration", "Microsoft.Extensions.Logging",
        "Microsoft.Extensions.Options", "Microsoft.Extensions.Hosting",
    ];

    private static DiagnosticDescriptor Descriptor(Kind kind) => kind switch
    {
        Kind.Services => Services,
        Kind.Machine => Machine,
        _ => Nondeterministic,
    };

    private static string ApiName(ISymbol s)
    {
        var type = s as ITypeSymbol ?? s.ContainingType;
        if (s is ITypeSymbol || s is IMethodSymbol { MethodKind: MethodKind.Constructor }) return type!.Name;
        return type is null ? s.Name : type.Name + "." + s.Name;
    }

    private static string Display(ISymbol s) => s is ITypeSymbol || s.ContainingType is null ? s.Name : s.ContainingType.Name + "." + s.Name;

    private sealed class State(Compilation compilation, INamedTypeSymbol stylesheet, INamedTypeSymbol? preset, INamedTypeSymbol? inject)
    {
        private readonly record struct Edge(ISymbol Target, Location Location);
        private readonly record struct Violation(Kind Kind, string Api);

        // Nodes are members (properties and events stand for their accessors) and types (their static initialization).
        private readonly ConcurrentDictionary<ISymbol, ConcurrentQueue<Edge>> edges = new(SymbolEqualityComparer.Default);
        private readonly ConcurrentDictionary<ISymbol, Violation> violations = new(SymbolEqualityComparer.Default);
        private readonly ConcurrentQueue<(ISymbol Root, Edge Edge)> rootEdges = new();

        private bool IsStylesheet(INamedTypeSymbol? t)
        {
            for (; t is not null; t = t.ContainingType)
                if (t.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, stylesheet))) return true;
            return false;
        }

        private bool IsPresetMember(ISymbol s)
        {
            if (preset is null || !s.IsStatic) return false;
            var type = (s as IPropertySymbol)?.Type ?? (s as IFieldSymbol)?.Type;
            return type is INamedTypeSymbol t && SymbolEqualityComparer.Default.Equals(t.OriginalDefinition, preset);
        }

        private bool IsRoot(ISymbol owner) => IsStylesheet(owner.ContainingType) || IsPresetMember(owner);

        private static ISymbol Node(ISymbol s) => s switch
        {
            IMethodSymbol { AssociatedSymbol: { } a } => a.OriginalDefinition,
            IMethodSymbol { ReducedFrom: { } r } => r.OriginalDefinition,
            _ => s.OriginalDefinition,
        };

        private bool InSource(ISymbol s) => SymbolEqualityComparer.Default.Equals(s.ContainingAssembly, compilation.Assembly);

        /// <summary>True when the operation is part of the type's static initialization (static field or property initializer, static constructor).</summary>
        private static bool InStaticInit(ISymbol owner, IOperation op)
        {
            if (!owner.IsStatic) return false;
            if (owner is IMethodSymbol { MethodKind: MethodKind.StaticConstructor }) return true;
            if (owner is not (IFieldSymbol or IPropertySymbol)) return false;
            for (var o = op; o is not null; o = o.Parent)
                if (o is IFieldInitializerOperation or IPropertyInitializerOperation) return true;
            return false;
        }

        public void OnOperation(OperationAnalysisContext ctx)
        {
            var target = Target(ctx.Operation);
            if (target is null) return;
            var owner = Node(ctx.ContainingSymbol);
            var root = IsRoot(owner);
            var location = ctx.Operation.Syntax.GetLocation();
            var staticInit = InStaticInit(owner, ctx.Operation);

            var kind = Classify(target);
            if (kind != Kind.None)
            {
                // One diagnostic per chain: `new Random().Next()` reports `new Random()`, and a member whose declared type
                // is already reported (`static HttpClient Http = new()`) does not report its initializer again.
                if (Receiver(ctx.Operation) is { } receiver && Target(receiver) is { } r && Classify(r) != Kind.None) return;
                if (root && MemberType(owner) is { } declared && Classify(declared) is Kind.Services or Kind.Machine) return;
                if (root) ctx.ReportDiagnostic(Diagnostic.Create(Descriptor(kind), location, Display(owner), ApiName(target)));
                else
                {
                    var v = new Violation(kind, ApiName(target));
                    violations.TryAdd(owner, v);
                    if (staticInit) violations.TryAdd(owner.ContainingType!.OriginalDefinition, v);
                }
                return;
            }

            if (!InSource(target)) return;
            var targets = new List<ISymbol> { Node(target) };
            // Touching a static member (or creating an instance) of another type runs that type's static initialization.
            var type = target.ContainingType?.OriginalDefinition;
            if (type is not null && (target.IsStatic || target is IMethodSymbol { MethodKind: MethodKind.Constructor })
                && !SymbolEqualityComparer.Default.Equals(type, owner.ContainingType?.OriginalDefinition))
                targets.Add(type);
            foreach (var t in targets)
            {
                // Stylesheet types report their own code directly.
                if (IsStylesheet(t as INamedTypeSymbol ?? t.ContainingType)) continue;
                var edge = new Edge(t, location);
                if (root) rootEdges.Enqueue((owner, edge));
                else
                {
                    edges.GetOrAdd(owner, _ => new()).Enqueue(edge);
                    if (staticInit) edges.GetOrAdd(owner.ContainingType!.OriginalDefinition, _ => new()).Enqueue(edge);
                }
            }
        }

        private static ISymbol? Target(IOperation op) => op switch
        {
            IInvocationOperation i => i.TargetMethod,
            IObjectCreationOperation o => (ISymbol?)o.Constructor ?? o.Type,
            IPropertyReferenceOperation p => p.Property,
            IFieldReferenceOperation f => f.Field,
            IMethodReferenceOperation m => m.Method,
            IEventReferenceOperation e => e.Event,
            _ => null,
        };

        private static IOperation? Receiver(IOperation op)
        {
            var instance = op switch
            {
                IInvocationOperation i => i.Instance,
                IMemberReferenceOperation m => m.Instance,
                _ => null,
            };
            while (instance is IConversionOperation c) instance = c.Operand;
            return instance;
        }

        private static ITypeSymbol? MemberType(ISymbol s) => (s as IPropertySymbol)?.Type ?? (s as IFieldSymbol)?.Type;

        public void OnMember(SymbolAnalysisContext ctx)
        {
            var s = ctx.Symbol;
            if (!IsRoot(s) || s.IsImplicitlyDeclared) return;
            var location = s.Locations.FirstOrDefault();
            if (inject is not null && s.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, inject)))
            {
                ctx.ReportDiagnostic(Diagnostic.Create(Services, location, Display(s), "[Inject]"));
                return;
            }
            var memberType = (s as IPropertySymbol)?.Type ?? (s as IFieldSymbol)?.Type;
            if (memberType is not null && Classify(memberType) is Kind.Services or Kind.Machine)
                ctx.ReportDiagnostic(Diagnostic.Create(Descriptor(Classify(memberType)), location, Display(s), memberType.Name));

            // A preset in an ordinary class: evaluating it runs that class's static initialization too.
            if (!IsStylesheet(s.ContainingType) && location is not null && !s.ContainingType.IsGenericType)
                rootEdges.Enqueue((s, new Edge(s.ContainingType.OriginalDefinition, location)));
        }

        public void OnEnd(CompilationAnalysisContext ctx)
        {
            var memo = new Dictionary<ISymbol, Violation?>(SymbolEqualityComparer.Default);
            var reported = new HashSet<(Location, string)>();
            foreach (var (root, edge) in rootEdges)
            {
                if (Find(edge.Target, memo) is not { } v) continue;
                if (!reported.Add((edge.Location, Display(edge.Target)))) continue;
                ctx.ReportDiagnostic(Diagnostic.Create(Indirect, edge.Location, Display(root), v.Api, Display(edge.Target)));
            }
        }

        private Violation? Find(ISymbol start, Dictionary<ISymbol, Violation?> memo)
        {
            if (memo.TryGetValue(start, out var known)) return known;
            var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default) { start };
            var queue = new Queue<ISymbol>();
            queue.Enqueue(start);
            Violation? found = null;
            while (queue.Count > 0 && found is null)
            {
                var n = queue.Dequeue();
                if (violations.TryGetValue(n, out var v)) { found = v; break; }
                if (memo.TryGetValue(n, out var m) && m is not null) { found = m; break; }
                if (!edges.TryGetValue(n, out var next)) continue;
                foreach (var e in next)
                    if (seen.Add(e.Target)) queue.Enqueue(e.Target);
            }
            memo[start] = found;
            return found;
        }
    }
}

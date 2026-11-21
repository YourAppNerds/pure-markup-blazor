# Diagnostics

Back to the [README](../README.md).

PureMarkup's analyzers and generators report these IDs. Change a severity in `.editorconfig`, for example
`dotnet_diagnostic.PMCSS021.severity = suggestion`.

## Components (`GenerateMarkup`)
These come from `[assembly: GenerateMarkup(typeof(...))]`, which generates typed parameter methods for a component from
another assembly. All three are warnings, and no methods are generated for that type.

| ID | Severity | Meaning | Fix |
|---|---|---|---|
| PMCOMP001 | Warning | The type is not a concrete component (a non-abstract class that implements `IComponent`). | Point the attribute at a component class. |
| PMCOMP002 | Warning | The type is not accessible from this project. It must be public, or internal to this project. | Use a public component, or declare it in this project (it then gets methods without the attribute). |
| PMCOMP003 | Warning | PureMarkup.Blazor already has typed methods for this type. | Remove the attribute. |

## Stylesheets
| ID | Severity | Meaning | Fix |
|---|---|---|---|
| PMCSS001 | Warning | A `Css.Rule` or `Css.Var` is not the initializer of a static member of a `[Stylesheet]` class, so it gets no class name. Using it throws. | Move it into a `[Stylesheet]` class as `public static Rule X { get; } = Css.Rule(...)`. |

### Purity (PMCSS010 to PMCSS013)
The build runs `[Stylesheet]` types and static `Preset<T>` members in a separate process. That process has no services,
no JS and no app environment, and the CSS is written once. So code in those types and members, rule bodies included,
must not depend on them.

| ID | Severity | Meaning | Examples |
|---|---|---|---|
| PMCSS010 | Error | Uses DI, JS or the network, also as a member's type. | `[Inject]`, `IServiceProvider`, `Microsoft.JSInterop`, `NavigationManager`, `HttpClient`, `Microsoft.AspNetCore.Http`, `Microsoft.Extensions.{DependencyInjection,Configuration,Logging,Options,Hosting}` |
| PMCSS011 | Error | Reads the build machine: files, the environment or processes. | `File`, `Directory`, `FileStream`, `Environment` (except `NewLine`), `Process`, the registry |
| PMCSS012 | Warning | Gives a different value on every build. | `DateTime.Now`/`UtcNow`/`Today`, `Guid.NewGuid`, `Random`, `Stopwatch`, `TimeProvider` |
| PMCSS013 | Warning | Reaches such code through methods, properties or static initializers of other types in the project. Full builds only, not live in the IDE. | A rule body that calls a helper that reads a file |

Fix: compute the value as a constant, or move the code out of the stylesheet or preset. For a theme switch at runtime,
use tokens (`Css.Token`) and toggle `pm-dark` / `pm-light`.

Limits: APIs are matched by name. The analyzers do not see code inside other assemblies, virtual or interface calls,
delegates passed in, or reflection. A `Preset<T>` in an ordinary class also counts that class's static initializers.

### Property names (PMCSS020, PMCSS021)
These check the name passed to `.Prop(name, value)` when the name is a constant.

| ID | Severity | Meaning | Fix |
|---|---|---|---|
| PMCSS020 | Warning | Not a CSS property (w3c/webref, legacy aliases such as `word-wrap`, `@font-face` descriptors inside `Css.FontFace`), not a custom property (`--name`) and not vendor-prefixed (`-webkit-...`). Suggests a near miss (`"colr"`, `"backgroundColor"`). | Fix the spelling, or use the typed method. |
| PMCSS021 | Hidden | The property has a typed method: use `.{Method}(...)`. Hidden by default, so it stays out of the error list. | Use the typed method, or raise it to `suggestion` to see it. |

Non-constant names are not checked.

## Trimming
PureMarkup's own code reports no trim or AOT warnings. `Comp<Router>()` warns IL2110/IL2111 in a trimmed WebAssembly
project, exactly as `<Router>` in a `.razor` file does; the cause is the framework's annotation on `Router.NotFoundPage`.
See [trimming-aot.md](trimming-aot.md).

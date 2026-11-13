# Spike: `.Bind(() => m.Name)` without `Expression.Compile()`

**Result: works. An interceptor emits typed get/set code, and the reflection fallback is almost as fast, so both ship: interceptor when the generator can name the types, reflection walk otherwise.**
net10.0, SDK 10.0.403, Roslyn 4.14 generator, versioned `InterceptsLocation` via `GetInterceptableLocation`. `dotnet test Tests -c Release`: 6/6 pass.

## How it works
- An interceptor only receives the `Expression`; it cannot see lambda captures. But the expression itself holds the root:
  `this` is a `ConstantExpression`, a captured local is a field on the closure object (`Member(model, Constant(closure))`).
- Generated body: `var root = (Model)BindRuntime.Root(expr, depth)` peels the members the generator handles itself and evaluates the rest
  (free for `this`, one `FieldInfo.GetValue` for a closure), then typed code: `BindCore(node, expr, root.Name, v => root.Name = v)`.
  The original `expr` goes through untouched as `ValueExpression`.
- Private members: `[UnsafeAccessor]` (e.g. `get_Draft` for `() => Draft!.Title` on a private property). AOT-safe, no reflection.
- Not intercepted (generator returns nothing, the real `Bind` body runs): owner or root type not nameable (private nested), generic owners,
  struct intermediates, init-only or readonly targets, inaccessible static members, anything that is not a plain member chain.
- Fallback = reflection walk over the expression (`FieldInfo`/`PropertyInfo` `GetValue`/`SetValue`). The members come from the expression tree,
  which roots them for the trimmer; `IsAotCompatible` analyzers report nothing. Struct parents throw (the write would hit a copy).

## Tests (`Tests/BindTests.cs`)
| Test | Result |
|---|---|
| Captured local in `EditForm` child content, `Comp<ProbeInput>().Bind(() => model.Name)`: intercepted, typing updates model, `FieldIdentifier` = (model, `Name`), re-render shows new value | pass |
| Private nullable property `() => Draft!.Title`: intercepted via `UnsafeAccessor`, setter works, field `Title` | pass |
| Private nested root type: not intercepted, reflection fallback runs, setter works | pass |
| Static SSR via `HtmlRenderer` + `EditForm`: `name="model.Name"` and `name="Draft.Title"` from the expression text | pass |

## Cost per `Bind` call (Stopwatch, 200k calls, rough)
| Path | ns |
|---|--:|
| Expression tree construction alone (compiler-emitted, also paid by Razor `@bind-Value`) | 482 |
| Hand-written `BindCore(node, expr, m.Name, v => m.Name = v)` | 544 |
| Intercepted | 633 |
| Reflection fallback | 712 |

The expression tree dominates; the interceptor saves ~80 ns per bind over reflection on CoreCLR. On WebAssembly (interpreted reflection) the gap
should be larger: not measured. The design note's fallback `.Bind(() => m.Name, x => m.Name = x)` still needs the current value, which again
means walking the expression, so it buys nothing over the reflection fallback and is not needed.

## Notes for the library
- Consumers need `<InterceptorsNamespaces>$(InterceptorsNamespaces);PureMarkup.Generated</InterceptorsNamespaces>` (ship it in the package's `.props`).
- Generated file uses `#nullable disable`: the interceptor signature must match the call site's inferred types, nullability included.
- `TValue` is inferred from the lambda's null-state: with a not-null flow state you get `Bind<InputText, string>` and a CS8631 warning against
  `InputBase<string?>`. Consider a `TValue?` parameter or per-input overloads.
- `ComponentNode<T>` needs `[DynamicallyAccessedMembers(All)]` on `T` (IL2091 otherwise, from `OpenComponent<T>`).
- Pending: NativeAOT / WASM AOT publish to confirm the reflection fallback and `UnsafeAccessor` at runtime.

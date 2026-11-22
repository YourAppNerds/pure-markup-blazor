# PureMarkup.Blazor.Templates

`dotnet new` templates for [PureMarkup.Blazor](https://www.nuget.org/packages/PureMarkup.Blazor): Blazor Web Apps written
in C# markup, with no `.razor` or `.html` files and CSS written in C#.
An independent project for Blazor; not affiliated with Microsoft.

```
dotnet new install PureMarkup.Blazor.Templates@0.1.0-preview.1
dotnet new puremarkup-blazor -o MyApp --interactivity Auto --sample-content
dotnet new puremarkup-blazor-page -n Orders -o Components/Pages        # inside a restored project
```

## Templates
- `puremarkup-blazor`: a Blazor Web App like `dotnet new blazor`: `App.cs`, `Routes.cs`, `Program.cs`, layout, pages and a
  theme (light and dark tokens, the stock look as C# rules). Options:
  - `--interactivity None|Server|WebAssembly|Auto` (default `Server`). `WebAssembly` and `Auto` add a `.Client` project.
    Interactivity is per page (`[InteractiveServer]`, `[InteractiveWebAssembly]`, `[InteractiveAuto]`).
  - `--all-interactive` (`-ai`): one render mode for the whole app.
  - `--sample-content`: a Counter page (interactive modes only) and a Weather page (stream rendering).
  - `--framework net10.0`, `--no-restore`.
- `puremarkup-blazor-page`: one routable `MarkupComponent` page. Options: `--route` (default `/<name in lower case>`),
  `--namespace` (default: the root namespace of the project in the output folder), `--interactivity`.

## Docs
- [Getting started](https://github.com/YourAppNerds/pure-markup-blazor/blob/main/docs/getting-started.md): templates, options, setup without the template
- [Render modes](https://github.com/YourAppNerds/pure-markup-blazor/blob/main/docs/render-modes.md)
- [Styling: CSS in C#](https://github.com/YourAppNerds/pure-markup-blazor/blob/main/docs/styling.md)
- [Repository README](https://github.com/YourAppNerds/pure-markup-blazor/blob/main/README.md)

MIT license.

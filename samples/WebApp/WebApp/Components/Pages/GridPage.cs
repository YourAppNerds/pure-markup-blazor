using Microsoft.AspNetCore.Components.QuickGrid;

namespace WebApp.Components.Pages;

public sealed record Planet(string Name, int Moons);

/// <summary>QuickGrid through consumer-time typed methods (see GenerateMarkup.cs).</summary>
[Route("/grid")]
[InteractiveServer]
public sealed class GridPage : MarkupComponent
{
    private static readonly IQueryable<Planet> Planets =
        new Planet[] { new("Mars", 2), new("Earth", 1), new("Jupiter", 95) }.AsQueryable();

    protected override Node Render() => Fragment(
        Comp<PageTitle>().Children("Grid"),
        H1().Text("QuickGrid"),
        P().Id("renderer").Text($"Renderer: {RendererInfo.Name}; interactive: {RendererInfo.IsInteractive}"),
        Comp<QuickGrid<Planet>>().Items(Planets).Class("planets").Children(() => Fragment(
            Comp<PropertyColumn<Planet, string>>().Property(p => p.Name).Sortable(true),
            Comp<PropertyColumn<Planet, int>>().Property(p => p.Moons).Sortable(true),
            Comp<TemplateColumn<Planet>>().Title("Many moons").ChildContent(p => Span().Text(p.Moons > 10 ? "yes" : "no")))));
}

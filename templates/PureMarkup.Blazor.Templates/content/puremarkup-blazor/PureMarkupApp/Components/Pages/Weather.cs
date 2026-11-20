using Microsoft.AspNetCore.Components.Web;

#if (GlobalClient)
namespace PureMarkupApp.Client.Pages;
#else
namespace PureMarkupApp.Components.Pages;
#endif

[Route("/weather")]
[StreamRendering]
public sealed class Weather : MarkupComponent
{
    private WeatherForecast[]? forecasts;

    protected override async Task OnInitializedAsync()
    {
        // Simulate asynchronous loading to demonstrate streaming rendering
        await Task.Delay(500);

        var startDate = DateOnly.FromDateTime(DateTime.Now);
        var summaries = new[] { "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching" };
        forecasts = Enumerable.Range(1, 5).Select(index => new WeatherForecast
        {
            Date = startDate.AddDays(index),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = summaries[Random.Shared.Next(summaries.Length)]
        }).ToArray();
    }

    protected override Node Render() => Fragment(
        Comp<PageTitle>().Children("Weather"),
        H1().Text("Weather"),
        P().Text("This component demonstrates showing data."),
        forecasts is null
            ? P().Children(Em().Text("Loading..."))
            : Table().Children(
                Thead().Children(Tr().Children(
                    Th().Text("Date"),
                    Th().AriaLabel("Temperature in Celsius").Text("Temp. (C)"),
                    Th().AriaLabel("Temperature in Fahrenheit").Text("Temp. (F)"),
                    Th().Text("Summary"))),
                Tbody().Children(
                    ForEach(forecasts, f => f.Date, f => Tr().Children(
                        Td().Text(f.Date.ToShortDateString()),
                        Td().Text(f.TemperatureC),
                        Td().Text(f.TemperatureF),
                        Td().Text(f.Summary))))));

    private sealed class WeatherForecast
    {
        public DateOnly Date { get; set; }
        public int TemperatureC { get; set; }
        public string? Summary { get; set; }
        public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
    }
}
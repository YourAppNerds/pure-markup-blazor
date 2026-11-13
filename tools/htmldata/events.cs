#:sdk Microsoft.NET.Sdk.Web
// Lists Blazor's [EventHandler] registrations (Web/EventHandlers.cs and friends) in the html.data format.
// Run: dotnet run events.cs 2>/dev/null > events.txt, then node convert.mjs (see there).
using System.Reflection;
using Microsoft.AspNetCore.Components;

var asms = new[] { typeof(ComponentBase).Assembly, typeof(Microsoft.AspNetCore.Components.Web.MouseEventArgs).Assembly };
var rows = asms.SelectMany(a => a.GetTypes())
    .SelectMany(t => t.GetCustomAttributes<EventHandlerAttribute>())
    .Select(a => (a.AttributeName, a.EventArgsType))
    .Distinct()
    .OrderBy(r => r.AttributeName, StringComparer.Ordinal);
foreach (var (name, type) in rows) Console.WriteLine($"event {name} {type.Name}");

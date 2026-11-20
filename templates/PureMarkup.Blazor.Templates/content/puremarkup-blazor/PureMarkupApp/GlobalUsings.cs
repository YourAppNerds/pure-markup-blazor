// Razor's _Imports.razor, as global usings.
global using Microsoft.AspNetCore.Components;
global using Microsoft.AspNetCore.Components.Web;
global using PureMarkup.Blazor;
global using PureMarkup.Blazor.Styling;
global using PureMarkupApp.Theme;
global using static PureMarkup.Blazor.H;
global using static PureMarkup.Blazor.Styling.CssUnits;
// Ambiguous with Microsoft.AspNetCore.Routing.RouteData in a Web SDK project; Razor imports hide this.
global using RouteData = Microsoft.AspNetCore.Components.RouteData;
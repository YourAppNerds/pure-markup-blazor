using Microsoft.AspNetCore.Components.QuickGrid;

// Components outside PureMarkup.Blazor's own coverage get typed parameter methods in this project.
[assembly: GenerateMarkup(typeof(QuickGrid<>), WholeAssembly = true)]      // Microsoft.AspNetCore.Components.QuickGrid
[assembly: GenerateMarkup(typeof(ImportMap), WholeAssembly = true)]        // Microsoft.AspNetCore.Components.Endpoints

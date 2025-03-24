using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.SignalR.Client;
using MissionCriticalDemo.Shared.Resilience;
using MissionCriticalDemo.Shared.Services;
using MudBlazor;
using MudBlazor.Services;


namespace MissionCriticalDemo.Frontend.Client;

class Program
{
    static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);

        builder.Services.AddMudServices(config =>
        {
            config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.TopCenter;
            config.SnackbarConfiguration.PreventDuplicates = true;
            config.SnackbarConfiguration.NewestOnTop = true;
            config.SnackbarConfiguration.ShowCloseIcon = false;
            config.SnackbarConfiguration.VisibleStateDuration = 2000;
            config.SnackbarConfiguration.HideTransitionDuration = 500;
            config.SnackbarConfiguration.ShowTransitionDuration = 500;
            config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
            config.SnackbarConfiguration.RequireInteraction = false;
            config.SnackbarConfiguration.MaxDisplayedSnackbars = 3;
        });

        // dispatch api support
        builder.Services
            .AddSingleton<IDispatchService, DispatchService>();
        //inject signalr connection builder
        builder.Services.AddTransient<IHubConnectionBuilder, HubConnectionBuilder>();

        builder.Services
            .AddHttpClient<IDispatchService, DispatchService>(client =>
                client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
            .AddPolicyHandler(HttpClientPolicies.GetTimeoutPolicy())
            .AddPolicyHandler((sp, req) => HttpClientPolicies.GetRetryPolicy<IDispatchService>(sp));

        builder.Services.AddAuthorizationCore();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddAuthenticationStateDeserialization();

        await builder.Build().RunAsync();
    }
}

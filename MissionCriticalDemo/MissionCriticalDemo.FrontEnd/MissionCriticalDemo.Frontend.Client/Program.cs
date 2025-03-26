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
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;


namespace MissionCriticalDemo.Frontend.Client;

class Program
{
    static async Task Main(string[] args)
    {
        await Task.Delay(2000);
        
        var builder = WebAssemblyHostBuilder.CreateDefault(args);

        
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
        
        //this approach is not working in WASM!
        // builder.Services.AddOpenTelemetry()
        //     .WithTracing(opt =>
        //     {
        //         opt.SetSampler(new AlwaysOnSampler());
        //         opt.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("blazor-wasm"))
        //             .AddSource(activitySource.Name);
        //
        //         opt.AddHttpClientInstrumentation();
        //         opt.AddZipkinExporter(bld =>
        //         {
        //             bld.Endpoint = new Uri("http://localhost:5045/zipkin");
        //             bld.ExportProcessorType = ExportProcessorType.Simple;
        //         });
        //         opt.AddConsoleExporter();
        //     });
        
        var openTelemetry = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("blazor-otel"))
            .AddSource("WasmFrontend")
            .AddZipkinExporter(o =>
            {
                o.Endpoint = new Uri("http://localhost:5054/zipkin");
                o.ExportProcessorType = ExportProcessorType.Simple;
            })
            .Build();
        
        // Add OpenTelemetry Tracing with Zipkin Exporter
        var activitySource = new ActivitySource("WasmFrontend");
        builder.Services.AddSingleton(activitySource);
        builder.Services.AddSingleton(openTelemetry);
        
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

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

        
        //builder.Services.AddScoped<CustomAuthorizationMessageHandler>();
        builder.Services
            .AddHttpClient<IDispatchService, DispatchService>(client =>
                client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
            .AddPolicyHandler(HttpClientPolicies.GetTimeoutPolicy())
            .AddPolicyHandler((sp, req) => HttpClientPolicies.GetRetryPolicy<IDispatchService>(sp));
            //.AddHttpMessageHandler<CustomAuthorizationMessageHandler>();
        
        //authentication support
        // builder.Services.AddMsalAuthentication(options =>
        // {
        //     builder.Configuration.Bind("AzureAdB2C", options.ProviderOptions.Authentication);
        //     options.ProviderOptions.DefaultAccessTokenScopes.Add("offline_access");
        //     options.ProviderOptions.DefaultAccessTokenScopes.Add("openid");
        //     options.ProviderOptions.DefaultAccessTokenScopes.Add("profile");
        //     options.ProviderOptions.DefaultAccessTokenScopes.Add("https://loekdb2c.onmicrosoft.com/b818d130-9845-4c80-b99c-f5b4b073a912/API.Access");
        //     options.ProviderOptions.LoginMode = "redirect";
        // });
        //
        // builder.Services.AddAuthorizationCore();
        
        builder.Services.AddAuthorizationCore();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddAuthenticationStateDeserialization();

        await builder.Build().RunAsync();
    }
    
    // public class CustomAuthorizationMessageHandler : AuthorizationMessageHandler
    // {
    //     public CustomAuthorizationMessageHandler(IAccessTokenProvider provider,
    //         NavigationManager navigationManager, IConfiguration configuration)
    //         : base(provider, navigationManager)
    //     {
    //         string apiUrl = navigationManager.BaseUri;
    //         ConfigureHandler(authorizedUrls: [apiUrl]);
    //
    //     }
    // }
}
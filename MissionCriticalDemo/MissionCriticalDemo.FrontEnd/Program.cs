using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.SignalR.Client;
using MissionCriticalDemo.FrontEnd;
using MissionCriticalDemo.FrontEnd.Services;
using MudBlazor.Services;


var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddHttpClient<DispatchService>("MissionCriticalDemo.ServerAPI", client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
    .AddPolicyHandler((sp, msg) => Polly.Policy.WrapAsync(
        PolicyBuilder.GetFallbackPolicy<DispatchService>(sp, DispatchService.FallbackGetCustomerGasInStore),
        PolicyBuilder.GetRetryPolicy<DispatchService>(sp)))
    .AddHttpMessageHandler<BaseAddressAuthorizationMessageHandler>();

// Supply HttpClient instances that include access tokens when making requests to the server project
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("MissionCriticalDemo.ServerAPI"));

//inject signalr connection builder
builder.Services.AddTransient<IHubConnectionBuilder, HubConnectionBuilder>();

//mudblazor
builder.Services.AddMudServices();

//custom dependency injections
builder.Services.AddScoped<IDispatchService, DispatchService>();

builder.Services.AddMsalAuthentication(options =>
{
    builder.Configuration.Bind("AzureAdB2C", options.ProviderOptions.Authentication);
    options.ProviderOptions.DefaultAccessTokenScopes.Add("https://loekdb2c.onmicrosoft.com/b818d130-9845-4c80-b99c-f5b4b073a912/API.Access");
    options.ProviderOptions.LoginMode = "redirect";
});

await builder.Build().RunAsync();

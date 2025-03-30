using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using MissionCriticalDemo.Frontend.Client.Pages;
using MissionCriticalDemo.Frontend.Components;
using MissionCriticalDemo.Shared.Resilience;
using MissionCriticalDemo.Shared.Services;
using MudBlazor.Services;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Yarp.ReverseProxy.Transforms;

namespace MissionCriticalDemo.Frontend;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        //support running behind a reverse proxy:
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.All;
            options.AllowedHosts.Add("*");
        });

        builder.AddServiceDefaults(); //adds service discovery, resilience, health checks, and OpenTelemetry

        //Add health endpoint support
        builder.Services.AddHealthChecks();

        //Configure yarp with forwarders
        builder.Services.AddHttpForwarderWithServiceDiscovery();

        //auth
        builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApp(options =>
            {
                builder.Configuration.Bind("AzureAdB2C", options);

                //options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;

                options.ResponseType = "code";
                options.UsePkce = true;
                options.SaveTokens = true;

                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("offline_access");
                options.Scope.Add("https://loekdb2c.onmicrosoft.com/b818d130-9845-4c80-b99c-f5b4b073a912/API.Access");

                options.TokenValidationParameters.NameClaimType = "name";
                //options.CorrelationCookie.SameSite = SameSiteMode.Lax;
                
                // Explicitly set the RedirectUri
                options.Events.OnRedirectToIdentityProvider = context =>
                {
                    var host = context.Request.Headers["X-Forwarded-Host"].FirstOrDefault() ?? 
                            context.Request.Host.Value;
                    
                    var scheme = context.Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? 
                                context.Request.Scheme;
                    if(!string.IsNullOrWhiteSpace(host))
                    {
                        var currentUri = $"{scheme}://{host}";
                        context.ProtocolMessage.RedirectUri = $"{currentUri}/signin-oidc"; 
                        Console.WriteLine($"Set redirect URI to: {context.ProtocolMessage.RedirectUri}");
                    }
                    return Task.CompletedTask;
                };
                
                options.Events.OnAuthenticationFailed += ctx => Task.CompletedTask;
                options.Events.OnTokenValidated += ctx => Task.CompletedTask;
                options.Events.OnAuthenticationFailed += ctx => Task.CompletedTask;
                options.Events.OnMessageReceived += ctx => Task.CompletedTask;
            });
        
        //builder.Services.ConfigureCookieOidcRefresh(CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme);
        builder.Services.AddAuthorization();

        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddHttpContextAccessor();

        // Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddInteractiveWebAssemblyComponents()
            .AddAuthenticationStateSerialization();

        builder.Services.AddMudServices();
        
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.All;
            options.AllowedHosts.Add("*");
        });
        
        //services
        builder.Services.AddSingleton<IDispatchService, DispatchService>();
        //inject signalr connection builder
        builder.Services.AddTransient<IHubConnectionBuilder, HubConnectionBuilder>();

        var app = builder.Build();
        app.UseForwardedHeaders();
        
        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseWebAssemblyDebugging();
        }
        else
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        //app.UseHttpsRedirection();
        // app.UseBlazorFrameworkFiles();
        // app.UseStaticFiles();
        // app.UseRouting(); 
        app.UseAntiforgery();
        app.MapHealthChecks("/api/healthz");

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapStaticAssets();
        app.MapDefaultEndpoints();

        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(typeof(Client._Imports).Assembly);

        app.MapForwarder("/api/{**catch-all}", "http://dispatchapi", transformBuilder => {
                //transformBuilder.AddPathSet("/api/{**catch-all}");
            transformBuilder.AddRequestTransform(async transformContext =>
            {
                var accessToken = await transformContext.HttpContext.GetTokenAsync("access_token");
                Console.WriteLine($"Using access token: {accessToken?[..8] ?? "empty"}");
                transformContext.ProxyRequest.Headers.Authorization = new("Bearer", accessToken);
            });
        });
            //.RequireAuthorization();
        app.MapForwarder("/dispatchhub/{**catch-all}", "http://dispatchapi", "/dispatchhub/{**catch-all}");
       
        string? otlpConfig = builder.Configuration["services:Jaeger:otlpEndpoint:0"];
        if (otlpConfig is not null)
        {
            app.MapForwarder("/v1/traces/{**catch-all}", otlpConfig, "/v1/traces/{**catch-all}");
        }
        
        string? zipkinConfig = builder.Configuration["ZIPKIN"] ?? builder.Configuration["services:Jaeger:zipkinEndpoint:0"];
        if (zipkinConfig is not null)
        {
            app.MapForwarder("/zipkin/{**catch-all}", zipkinConfig, "/api/v2/spans/{**catch-all}");
        }
        
        
        app.MapGroup("/authentication").MapLoginAndLogout();

        app.Run();
    }

    
}

internal static class Extensions
{
    // internal static WebApplicationBuilder ConfigureTelemetry(this WebApplicationBuilder builder)
    // {
    //     // builder.Services.ConfigureOpenTelemetryTracerProvider(tracing => 
    //     //     tracing.AddAspNetCoreInstrumentation()
    //     // );
    //     // builder.Services.ConfigureOpenTelemetryMeterProvider(metrics => 
    //     //     metrics.AddAspNetCoreInstrumentation()
    //     // );
    //
    //     builder.Services.AddOpenTelemetry()
    //         .UseOtlpExporter(OtlpExportProtocol.HttpProtobuf, new Uri("http://jaeger"));
    //         // .WithMetrics(metrics => 
    //         //     metrics.AddAspNetCoreInstrumentation()
    //         // )
    //         // .WithTracing(tracing => 
    //         //     tracing.AddAspNetCoreInstrumentation()
    //         // );
    //     
    //     return builder;
    // }
    
    internal static IEndpointConventionBuilder MapLoginAndLogout(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("");

        group.MapGet("/login", (string? returnUrl) => TypedResults.Challenge(GetAuthProperties(returnUrl)))
            .AllowAnonymous();

        // Sign out of the Cookie and OIDC handlers. If you do not sign out with the OIDC handler,
        // the user will automatically be signed back in the next time they visit a page that requires authentication
        // without being able to choose another account.
        group.MapPost("/logout", ([FromForm] string? returnUrl) => TypedResults.SignOut(GetAuthProperties(returnUrl),
            [CookieAuthenticationDefaults.AuthenticationScheme, "MicrosoftOidc"]));

        return group;
    }

    private static AuthenticationProperties GetAuthProperties(string? returnUrl)
    {
        // TODO: Use HttpContext.Request.PathBase instead.
        const string pathBase = "/";

        // Prevent open redirects.
        if (string.IsNullOrEmpty(returnUrl))
        {
            returnUrl = pathBase;
        }
        else if (!Uri.IsWellFormedUriString(returnUrl, UriKind.Relative))
        {
            returnUrl = new Uri(returnUrl, UriKind.Absolute).PathAndQuery;
        }
        else if (returnUrl[0] != '/')
        {
            returnUrl = $"{pathBase}{returnUrl}";
        }

        return new AuthenticationProperties { RedirectUri = returnUrl };
    }
}

internal sealed class CookieOidcRefresher(IOptionsMonitor<OpenIdConnectOptions> oidcOptionsMonitor)
{
    private readonly OpenIdConnectProtocolValidator oidcTokenValidator = new()
    {
        // We no longer have the original nonce cookie which is deleted at the end of the authorization code flow having served its purpose.
        // Even if we had the nonce, it's likely expired. It's not intended for refresh requests. Otherwise, we'd use oidcOptions.ProtocolValidator.
        RequireNonce = false,
    };

    public async Task ValidateOrRefreshCookieAsync(CookieValidatePrincipalContext validateContext, string oidcScheme)
    {
        var accessTokenExpirationText = validateContext.Properties.GetTokenValue("expires_at");
        if (!DateTimeOffset.TryParse(accessTokenExpirationText, out var accessTokenExpiration))
        {
            return;
        }

        var oidcOptions = oidcOptionsMonitor.Get(oidcScheme);
        var now = oidcOptions.TimeProvider!.GetUtcNow();
        if (now + TimeSpan.FromMinutes(5) < accessTokenExpiration)
        {
            return;
        }

        var oidcConfiguration =
            await oidcOptions.ConfigurationManager!.GetConfigurationAsync(validateContext.HttpContext.RequestAborted);
        var tokenEndpoint = oidcConfiguration.TokenEndpoint ??
                            throw new InvalidOperationException("Cannot refresh cookie. TokenEndpoint missing!");

        using var refreshResponse = await oidcOptions.Backchannel.PostAsync(tokenEndpoint,
            new FormUrlEncodedContent(new Dictionary<string, string?>()
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = oidcOptions.ClientId,
                ["client_secret"] = oidcOptions.ClientSecret,
                ["scope"] = string.Join(" ", oidcOptions.Scope),
                ["refresh_token"] = validateContext.Properties.GetTokenValue("refresh_token"),
            }));

        if (!refreshResponse.IsSuccessStatusCode)
        {
            validateContext.RejectPrincipal();
            return;
        }

        var refreshJson = await refreshResponse.Content.ReadAsStringAsync();
        var message = new OpenIdConnectMessage(refreshJson);

        var validationParameters = oidcOptions.TokenValidationParameters.Clone();
        if (oidcOptions.ConfigurationManager is BaseConfigurationManager baseConfigurationManager)
        {
            validationParameters.ConfigurationManager = baseConfigurationManager;
        }
        else
        {
            validationParameters.ValidIssuer = oidcConfiguration.Issuer;
            validationParameters.IssuerSigningKeys = oidcConfiguration.SigningKeys;
        }

        var validationResult = await oidcOptions.TokenHandler.ValidateTokenAsync(message.IdToken, validationParameters);

        if (!validationResult.IsValid)
        {
            validateContext.RejectPrincipal();
            return;
        }

        var validatedIdToken = JwtSecurityTokenConverter.Convert(validationResult.SecurityToken as JsonWebToken);
        validatedIdToken.Payload["nonce"] = null;
        oidcTokenValidator.ValidateTokenResponse(new()
        {
            ProtocolMessage = message,
            ClientId = oidcOptions.ClientId,
            ValidatedIdToken = validatedIdToken,
        });

        validateContext.ShouldRenew = true;
        validateContext.ReplacePrincipal(new ClaimsPrincipal(validationResult.ClaimsIdentity));

        var expiresIn = int.Parse(message.ExpiresIn, NumberStyles.Integer, CultureInfo.InvariantCulture);
        var expiresAt = now + TimeSpan.FromSeconds(expiresIn);
        validateContext.Properties.StoreTokens([
            new() { Name = "access_token", Value = message.AccessToken },
            new() { Name = "id_token", Value = message.IdToken },
            new() { Name = "refresh_token", Value = message.RefreshToken },
            new() { Name = "token_type", Value = message.TokenType },
            new() { Name = "expires_at", Value = expiresAt.ToString("o", CultureInfo.InvariantCulture) },
        ]);
    }
}

internal static class CookieOidcServiceCollectionExtensions
{
    public static IServiceCollection ConfigureCookieOidcRefresh(this IServiceCollection services, string cookieScheme,
        string oidcScheme)
    {
        services.AddSingleton<CookieOidcRefresher>();
        services.AddOptions<CookieAuthenticationOptions>(cookieScheme).Configure<CookieOidcRefresher>(
            (cookieOptions, refresher) =>
            {
                cookieOptions.Events.OnValidatePrincipal =
                    context => refresher.ValidateOrRefreshCookieAsync(context, oidcScheme);
            });
        services.AddOptions<OpenIdConnectOptions>(oidcScheme).Configure(oidcOptions =>
        {
            // Request a refresh_token.
            oidcOptions.Scope.Add(OpenIdConnectScope.OfflineAccess);
            // Store the refresh_token.
            oidcOptions.SaveTokens = true;
        });
        return services;
    }
}
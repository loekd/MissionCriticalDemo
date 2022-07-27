using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Microsoft.AspNetCore.ResponseCompression;
using MissionCriticalDemo.Messages;
using MissionCriticalDemo.DispatchApi.InputValidation;
using MissionCriticalDemo.DispatchApi.Hubs;
using MissionCriticalDemo.DispatchApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAdB2C"));

builder.Services.AddSignalR();
builder.Services.AddControllersWithViews()
    .AddDapr()
    .AddFluentValidation();
builder.Services.AddRazorPages();
builder.Services.AddResponseCompression(opts =>
{
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/octet-stream" });
});

//custom services
builder.Services.AddTransient<IValidator<MissionCriticalDemo.Shared.Contracts.Request>, RequestValidator>();
builder.Services.AddSingleton<IMappers, Mappers>();
builder.Services.AddSingleton<IGasStorage, GasStorage>();

//configure auth callbacks
builder.Services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.TokenValidationParameters.ClockSkew = TimeSpan.FromMinutes(30);
    var existingOnTokenValidatedHandler = options.Events.OnAuthenticationFailed;
    options.Events.OnAuthenticationFailed = async context =>
    {
        await existingOnTokenValidatedHandler(context);
    };
});

//outbox processing
builder.Services.AddHostedService<OutboxProcessor>();

var app = builder.Build();
app.UseResponseCompression();

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

//important to leave this out, as Dapr will call the non-https endpoint
//app.UseHttpsRedirection();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseCloudEvents();

app.MapRazorPages();

app.MapSubscribeHandler();
app.MapControllers();

app.MapHub<DispatchHub>("/dispatchhub");
app.MapFallbackToFile("index.html");

app.Run();

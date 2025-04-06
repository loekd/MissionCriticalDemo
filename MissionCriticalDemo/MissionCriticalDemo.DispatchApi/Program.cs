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

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAdB2C"));

builder.Services.AddSignalR();
builder.Services.AddControllersWithViews()
    .AddDapr();
builder.Services.AddFluentValidationAutoValidation();
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
builder.Services.AddDistributedMemoryCache();

//configure auth callbacks
builder.Services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.TokenValidationParameters.ClockSkew = TimeSpan.FromMinutes(30);
    var existingOnTokenValidatedHandler = options.Events.OnAuthenticationFailed;
    options.Events.OnAuthenticationFailed = async ctx =>
    {
        Console.WriteLine(ctx.Exception.Message);
        await existingOnTokenValidatedHandler(ctx);
    };

    options.Events.OnTokenValidated += ctx =>
    {
        Console.WriteLine("Token validated");
        return Task.CompletedTask;
    };
});

//outbox processing
builder.Services.AddHostedService<OutboxProcessor>();

//inbox processing
builder.Services.AddHostedService<InboxProcessor>();

//CORS
// builder.Services.AddCors(options =>
// {
//     options.AddDefaultPolicy(builder =>
//     {
//         //allow the frontend with tokens
//         builder.AllowAnyHeader()
//                       .AllowAnyMethod()
//                       .SetIsOriginAllowed((host) => true)
//                       .AllowCredentials();
//     });
// });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

//CORS
app.UseCors();

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

app.UseSwagger();
app.UseSwaggerUI();

app.Run();

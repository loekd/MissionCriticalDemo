using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc.Formatters;
using MissionCriticalDemo.Messages;
using MissionCriticalDemo.PlantApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
// Add services to the container.

builder.Services.AddControllers(options => options.InputFormatters.Add(new DaprRawPayloadInputFormatter()))
    .AddDapr();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//custom services
builder.Services.AddSingleton<IMappers, Mappers>();
builder.Services.AddScoped<IGasStorage, GasStorage>();


var app = builder.Build();

// Configure the HTTP request pipeline.

    app.UseSwagger();
    app.UseSwaggerUI();


//important to leave this out, as Dapr will call the non-https endpoint
//app.UseHttpsRedirection();

app.UseAuthorization();

app.UseCloudEvents();
app.MapSubscribeHandler();
app.MapControllers();


app.Run();


public class DaprRawPayloadInputFormatter : InputFormatter
{
    public DaprRawPayloadInputFormatter()
    {
        SupportedMediaTypes.Add("application/octet-stream");
        SupportedMediaTypes.Add("text/plain");
    }

    public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context)
    {
        using (MemoryStream str = new MemoryStream())
        {
            try
            {
                await context.HttpContext.Request.Body.CopyToAsync(str);

                var jsonString = System.Text.Encoding.UTF8.GetString(str.ToArray());

                var deserializedMessage = System.Text.Json.JsonSerializer.Deserialize<RawMessage>(jsonString);
                if (deserializedMessage is not null)
                {
                    var deserializedModel = System.Text.Json.JsonSerializer.Deserialize(deserializedMessage.data, context.ModelType);
                    return InputFormatterResult.Success(deserializedModel);
                }
                return InputFormatterResult.Failure();
            }
            catch
            {
                return InputFormatterResult.Failure();
            }
        }
    }
}


[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
public class RawMessage
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public string data { get; set; }
    public string datacontenttype { get; set; }
    public string id { get; set; }
    public string pubsubname { get; set; }
    public string source { get; set; }
    public string specversion { get; set; }
    public DateTime time { get; set; }
    public string topic { get; set; }
    public string traceid { get; set; }
    public string traceparent { get; set; }
    public string tracestate { get; set; }
    public string type { get; set; }
}

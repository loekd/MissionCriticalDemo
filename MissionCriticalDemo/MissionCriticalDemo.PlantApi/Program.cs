using MissionCriticalDemo.Messages;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers().AddDapr();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//custom services
builder.Services.AddSingleton<IMappers, Mappers>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//important to leave this out, as Dapr will call the non-https endpoint
//app.UseHttpsRedirection();

app.UseAuthorization();

app.UseCloudEvents();
app.MapSubscribeHandler();
app.MapControllers();


app.Run();
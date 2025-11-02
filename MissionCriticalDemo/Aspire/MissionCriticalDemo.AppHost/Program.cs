using System.Collections.Immutable;
using CommunityToolkit.Aspire.Hosting.Dapr;
using MissionCriticalDemo.AppHost.OpenTelemetry;
using MissionCriticalDemo.Shared;
// ReSharper disable UnusedVariable

var builder = DistributedApplication.CreateBuilder(args);

string daprComponentsPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!, "..", "..", "..", "..", "..", "components"));

//A containerized pub/sub message broker & state store:
var redisPassword =
    builder.AddParameter("Redis-Password", secret: true, valueGetter: () => "S3cr3tPassw0rd!");
var redis = builder
    .AddRedis("redis", password: redisPassword)
    .WithHostPort(6380)     
    .WithLifetime(ContainerLifetime.Session)
    .WithEndpointProxySupport(false)
    .WithRedisInsight();

//Open telemetry collector
var jaeger = builder.AddJaeger(zipkinPort: 9411);

builder.AddDapr();

var dispatchInboxStateStore = builder.AddDaprStateStore("inboxstate", new DaprComponentOptions
{
    LocalPath = $"{daprComponentsPath}/statestore-inbox.yaml"
}).WaitFor(redis);

var dispatchOutboxStateStore = builder.AddDaprStateStore("outboxstate", new DaprComponentOptions
{
    LocalPath = $"{daprComponentsPath}/statestore.yaml"
}).WaitFor(redis);

var dispatchGisStateStore = builder.AddDaprStateStore("gasinstorestate", new DaprComponentOptions
{
    LocalPath = $"{daprComponentsPath}/statestore-gis.yaml"
}).WaitFor(redis);

var dispatchPubSub = builder.AddDaprPubSub("dispatchpubsub", new DaprComponentOptions
{
    LocalPath = $"{daprComponentsPath}/pubsub.yaml"
}).WaitFor(redis);

var plantStateStore = builder.AddDaprStateStore("plantstate", new DaprComponentOptions
{
    LocalPath = $"{daprComponentsPath}/statestore-plant.yaml"
}).WaitFor(redis);

var dispatchApi = builder
    .AddProject<Projects.MissionCriticalDemo_DispatchApi>("DispatchApi")
    .WithDaprSidecar(opt =>
        {
            opt.WithOptions(new DaprSidecarOptions
            {
                AppId = "dispatchapi",
                AppPort = 7079,
                AppProtocol = "https",
                ResourcesPaths = ImmutableHashSet.Create(daprComponentsPath),
                SchedulerHostAddress = "", // Disable Dapr scheduler
                PlacementHostAddress = "", // Disable Dapr placement
            });
            opt.WithReference(dispatchInboxStateStore);
            opt.WithReference(dispatchOutboxStateStore);
            opt.WithReference(dispatchGisStateStore);
            opt.WithReference(dispatchPubSub);
        })
        .WithEnvironment(Constants.OtlpEndpoint, jaeger.Resource.OtlpEndpoint)
    .WithExternalHttpEndpoints()
    .WaitFor(jaeger)
    .WaitFor(redis);

var plantApi = builder
    .AddProject<Projects.MissionCriticalDemo_PlantApi>("PlantApi")
    .WithDaprSidecar(opt =>
        {
            opt.WithOptions(new DaprSidecarOptions
            {
                AppId = "plantapi",
                AppPort = 7071,
                AppProtocol = "https",
                ResourcesPaths = ImmutableHashSet.Create(daprComponentsPath),
                SchedulerHostAddress = "", // Disable Dapr scheduler
                PlacementHostAddress = "", // Disable Dapr placement
            });
            
            opt.WithReference(plantStateStore);
            opt.WithReference(dispatchPubSub);
        })
    .WithEnvironment(Constants.OtlpEndpoint, jaeger.Resource.OtlpEndpoint)
    .WaitFor(jaeger)
    .WaitFor(redis);

var frontend = builder
    .AddProject<Projects.MissionCriticalDemo_Frontend>("Frontend")
    .WithExternalHttpEndpoints()
    .WithReference(dispatchApi)
    .WithEnvironment(Constants.OtlpEndpoint, jaeger.Resource.OtlpEndpoint)
    .WithEnvironment(Constants.ZipkinEndpoint, jaeger.Resource.ZipkinEndpoint)
    .WaitFor(jaeger)
    .WaitFor(dispatchApi)
    .WaitFor(plantApi);


builder.Build().Run();


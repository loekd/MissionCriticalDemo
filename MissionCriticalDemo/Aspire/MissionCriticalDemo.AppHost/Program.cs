using System.Collections.Immutable;
using CommunityToolkit.Aspire.Hosting.Dapr;
using MissionCriticalDemo.AppHost.OpenTelemetry;
using MissionCriticalDemo.Shared;
// ReSharper disable UnusedVariable

var builder = DistributedApplication.CreateBuilder(args);

const string daprComponentsPath = "/Users/loekd/projects/MissionCriticalDemo/MissionCriticalDemo/components";

//A containerized pub/sub message broker & state store:
var redis = builder
    .AddRedis("redis")
    .WithEndpoint(name:"redis-master", port: 6380, targetPort: 6379)
    .WithLifetime(ContainerLifetime.Persistent)
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
                AppPort = 80,
                AppProtocol = "http",
                AppHealthCheckPath = "/healthz",
                AppHealthProbeInterval = 10,
                AppHealthProbeTimeout = 5,
                AppHealthThreshold = 3,
                ResourcesPaths = ImmutableHashSet.Create(daprComponentsPath)
            });
            opt.WithReference(dispatchInboxStateStore);
            opt.WithReference(dispatchOutboxStateStore);
            opt.WithReference(dispatchGisStateStore);
            opt.WithReference(dispatchPubSub);
        })
        .WithEnvironment(Constants.OtlpEndpoint, jaeger.Resource.OtlpEndpoint)
    .WithExternalHttpEndpoints()
    .WaitFor(jaeger);

var plantApi = builder
    .AddProject<Projects.MissionCriticalDemo_PlantApi>("PlantApi")
    .WithDaprSidecar(opt =>
        {
            opt.WithReference(plantStateStore);
            opt.WithReference(dispatchPubSub);
        })
    .WithEnvironment(Constants.OtlpEndpoint, jaeger.Resource.OtlpEndpoint)
    .WaitFor(jaeger);

var frontend = builder
    .AddProject<Projects.MissionCriticalDemo_Frontend>("Frontend")
    .WithExternalHttpEndpoints()
    .WithReference(dispatchApi)
    .WithEnvironment(Constants.OtlpEndpoint, jaeger.Resource.OtlpEndpoint)
    .WithEnvironment(Constants.ZipkinEndpoint, jaeger.Resource.ZipkinEndpoint)
    .WaitFor(jaeger)
    .WaitFor(dispatchApi);

builder.Build().Run();


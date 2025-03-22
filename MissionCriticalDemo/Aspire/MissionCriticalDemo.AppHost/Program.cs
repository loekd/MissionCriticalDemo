using System.Diagnostics;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MissionCriticalDemo.AppHost.OpenTelemetry;

var builder = DistributedApplication.CreateBuilder(args);

const string daprComponentsPath = "/Users/loekd/projects/MissionCriticalDemo/MissionCriticalDemo/components";
//the actual data store:
var usernameParameter = builder.AddParameter("username", "sa");
var passwordParameter = builder.AddParameter("password", "SomePassword", secret: true);
var postgresDb = builder
    .AddPostgres("postgresdb-dispatch", userName: usernameParameter, password: passwordParameter)
    .WithEndpoint(name: "main", port: 5432, targetPort: 5432)
    .WithEnvironment("POSTGRES_HOST_AUTH_METHOD", "trust")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithPgAdmin();

//the actual pub/sub message broker:
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
});

var dispatchOutboxStateStore = builder.AddDaprStateStore("outboxstate", new DaprComponentOptions
{
    LocalPath = $"{daprComponentsPath}/statestore.yaml"
});

var dispatchGisStateStore = builder.AddDaprStateStore("gasinstorestate", new DaprComponentOptions
{
    LocalPath = $"{daprComponentsPath}/statestore-gis.yaml"
});

var dispatchPubSub = builder.AddDaprPubSub("dispatchpubsub", new DaprComponentOptions
{
    LocalPath = $"{daprComponentsPath}/pubsub.yaml"
})
    .WaitFor(redis);

var plantStateStore = builder.AddDaprStateStore("plantstate", new DaprComponentOptions
{
    LocalPath = $"{daprComponentsPath}/statestore-plant.yaml"
});

var dispatchApi = builder
    .AddProject<Projects.MissionCriticalDemo_DispatchApi>("DispatchApi")
    .WithDaprSidecar()
    .WithReference(dispatchInboxStateStore)
    .WithReference(dispatchOutboxStateStore)
    .WithReference(dispatchGisStateStore)
    //.WithReference(postgresDb)
    .WithReference(dispatchPubSub)
    //.WithReference(redis)
    .WithExternalHttpEndpoints()
    .WaitFor(jaeger);

var plantApi = builder
    .AddProject<Projects.MissionCriticalDemo_PlantApi>("PlantApi")
    .WithDaprSidecar()
    .WithReference(plantStateStore)
    //.WithReference(postgresDb)
    .WithReference(dispatchPubSub)
    //.WithReference(redis);
    .WaitFor(jaeger);

var frontend = builder
    .AddProject<Projects.MissionCriticalDemo_Frontend>("Frontend")
    .WithExternalHttpEndpoints()
    .WithReference(dispatchApi)
    .WithReference(jaeger.Resource.OtlpEndpoint)
    .WaitFor(jaeger);


builder.Build().Run();


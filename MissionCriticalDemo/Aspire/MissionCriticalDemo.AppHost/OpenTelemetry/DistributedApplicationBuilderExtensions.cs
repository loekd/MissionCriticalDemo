using Aspire.Hosting.Lifecycle;

namespace MissionCriticalDemo.AppHost.OpenTelemetry;


public static class DistributedApplicationBuilderExtensions
{
    private const string JaegerImageName = "jaegertracing/jaeger";
    private const string JaegerImageTag = "2.4.0";
    private const string HealthEndpointName = "healthEndpoint";
    private const string HealthEndpointPath = "/health/status";
    public const string UserInterfaceEndpointName = "uiEndpoint";
    public const string OtlpEndpointName = "otlpEndpoint";
    public const string ZipkinEndpointName = "zipkinEndpoint";

    private const string ConfigFileVolume = "./OpenTelemetry/config.yaml";
    private const string ConfigFilePath = "/jaeger/config.yaml";
    
    /// <summary>
    /// Adds an Jaeger all in one container to the application model.
    /// </summary>
    public static IResourceBuilder<JaegerResource> AddJaeger(this IDistributedApplicationBuilder builder,
      string name = "Jaeger", int zipkinPort = 9411, int otlpPort = 4318)
    {
        builder.Services.TryAddLifecycleHook<JaegerResourceLifecycleHook>();
        var jaeger = new JaegerResource(name);
        IResourceBuilder<JaegerResource> resourceBuilder = builder.AddResource(jaeger)
                  .WithAnnotation(new ContainerImageAnnotation
                  {
                      Image = JaegerImageName,
                      Tag = JaegerImageTag
                  })
                  .WithHttpEndpoint(port: zipkinPort, targetPort:9411, name: ZipkinEndpointName)
                  .WithHttpEndpoint(port: 13133, targetPort:13133, name: HealthEndpointName)
                  .WithHttpEndpoint(port: 16686, targetPort: 16686, name: UserInterfaceEndpointName)
                  .WithHttpEndpoint(port: otlpPort, targetPort: 4318, name: OtlpEndpointName)
                  .WithBindMount(ConfigFileVolume, ConfigFilePath, true)
                  .WithLifetime(ContainerLifetime.Persistent)
                  .ExcludeFromManifest()
                  .PublishAsContainer()
                  .WithHttpHealthCheck(endpointName: HealthEndpointName, path: HealthEndpointPath)
                  .WithArgs("--config", ConfigFilePath);

        return resourceBuilder;
    }
}

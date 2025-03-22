namespace MissionCriticalDemo.AppHost.OpenTelemetry;

/// <summary>
/// An Jaeger container.
/// </summary>
/// <param name="name">Name for resource</param>
public class JaegerResource(string name) : ContainerResource(name), IResourceWithEndpoints
{
    private EndpointReference? _zipkinEndpointReference;    
    private EndpointReference? _otlpEndpointReference;


    /// <summary>
    /// Returns the Zipkin endpoint for Jaeger
    /// </summary>
    public EndpointReference ZipkinEndpoint
    {
        get
        {
            return _zipkinEndpointReference ??= new EndpointReference(this, DistributedApplicationBuilderExtensions.ZipkinEndpointName);
        }
    }
    
    /// <summary>
    /// Returns the OTLP endpoint for Jaeger
    /// </summary>
    public EndpointReference OtlpEndpoint
    {
        get
        {
            return _otlpEndpointReference ??= new EndpointReference(this, DistributedApplicationBuilderExtensions.OtlpEndpointName);
        }
    }
}

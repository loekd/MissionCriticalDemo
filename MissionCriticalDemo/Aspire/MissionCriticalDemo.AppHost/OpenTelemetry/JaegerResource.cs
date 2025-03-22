namespace MissionCriticalDemo.AppHost.OpenTelemetry;

/// <summary>
/// An Jaeger container.
/// </summary>
/// <param name="name">Name for resource</param>
public class JaegerResource(string name) : ContainerResource(name), IResourceWithEndpoints
{
    public const string ZipkinEndpointName = "http";

    private EndpointReference? _endpointReference;

    /// <summary>
    /// Returns the endpoint for Jaeger
    /// </summary>
    public EndpointReference Endpoint
    {
        get
        {
            return _endpointReference ??= new EndpointReference(this, ZipkinEndpointName);
        }
    }
}

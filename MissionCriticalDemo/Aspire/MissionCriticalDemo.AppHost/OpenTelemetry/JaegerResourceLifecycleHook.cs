using Aspire.Hosting.Lifecycle;

namespace MissionCriticalDemo.AppHost.OpenTelemetry;

internal class JaegerResourceLifecycleHook(ResourceNotificationService notificationService) : IDistributedApplicationLifecycleHook, IAsyncDisposable
{
    private readonly CancellationTokenSource _tokenSource = new();

    /// <summary>
    /// Starts downloads of the models for each Jaeger resource in the background.
    /// </summary>
    /// <param name="appModel"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task AfterResourcesCreatedAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken = default)
    {
        foreach (var resource in appModel.Resources.OfType<JaegerResource>())
        {
            //check the health endpoint
            // "/health/status"
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Cleans up (un)managed resources.
    /// </summary>
    /// <returns></returns>
    public ValueTask DisposeAsync()
    {
        _tokenSource.Cancel();
        return default;
    }

   
}

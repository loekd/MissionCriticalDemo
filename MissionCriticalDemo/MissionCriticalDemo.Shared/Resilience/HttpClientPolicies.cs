using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Extensions.Http;

namespace MissionCriticalDemo.Shared.Resilience;

public static class HttpClientPolicies
{
    public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy<TService>(IServiceProvider serviceProvider, int retryCount = 3)
    {
        var delay = Backoff.DecorrelatedJitterBackoffV2(medianFirstRetryDelay: TimeSpan.FromSeconds(1), retryCount);

        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(delay,
                onRetry: (result, span, index, ctx) =>
                {
                    var logger = serviceProvider.GetRequiredService<ILogger<TService>>();
                    logger.LogWarning("Retry #{Index}, Status: {StatusCode}", index, result.Result.StatusCode);
                }
            );
    }

    /// <summary>
    ///  can take a long time, we need to set a timeout policy to match.
    /// </summary>
    /// <param name="timeoutInSeconds"></param>
    /// <returns></returns>
    public static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy(int timeoutInSeconds = 90)
    {
        return Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(timeoutInSeconds));
    }
}

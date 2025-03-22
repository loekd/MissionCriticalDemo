using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MissionCriticalDemo.Shared.Services;
using Polly;
using Polly.Extensions.Http;

namespace MissionCriticalDemo.Shared.Resilience
{
    public static class PolicyBuilder
    {
        public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy<TService>(IServiceProvider serviceProvider, 
            int retryCount = 1)
            where TService : class, IService
        {
            var logger = serviceProvider.GetRequiredService<ILogger<TService>>();
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(retryCount,
                    retryAttempt => TimeSpan.FromSeconds(retryAttempt + Random.Shared.Next(0, 100) / 100D),
                    onRetry: (result, span, index, ctx) =>
                    {
                        logger.LogWarning("Retry attempt: {index} | Status: {statusCode}", index, result.Result.StatusCode);
                    });
        }

        public static IAsyncPolicy<HttpResponseMessage> GetFallbackPolicy<TService>(IServiceProvider serviceProvider, 
            Func<Context, CancellationToken, Task<HttpResponseMessage>> valueFactory)
            where TService : class, IService
        {
            var logger = serviceProvider.GetRequiredService<ILogger<TService>>();
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .FallbackAsync(valueFactory, (res, ctx) =>
                    {
                        logger.LogWarning($"returning fallback value...");
                        return Task.CompletedTask;
                    });
        }
    }
}

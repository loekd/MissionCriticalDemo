using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Logging;
using MissionCriticalDemo.Shared.Contracts;

namespace MissionCriticalDemo.Shared.Services;

/// <summary>
/// Processes dispatching interaction.
/// </summary>
public interface IDispatchService : IService
{
    Task SubmitRequest(Request request);

    Task<int> GetCustomerGasInStore();

    Task<int> GetOverallGasInStore();
}

/// <summary>
/// Processes dispatching interaction.
/// </summary>
public class DispatchService(HttpClient httpClient, ILogger<DispatchService> logger) : IDispatchService
{
    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    private const string DispatchEndpoint = "api/dispatch";
    private const string CustomerGisEndpoint = "api/gasinstore";
    private const string OverallGisEndpoint = "api/gasinstore/overall";

    public async Task SubmitRequest(Request request)
    {
        logger.LogDebug("Submitting request {Amount} {Direction}", request.AmountInGWh, request.Direction);
        var response = await _httpClient.PostAsJsonAsync(DispatchEndpoint, request);
        response.EnsureSuccessStatusCode();
    }

    public Task<int> GetCustomerGasInStore()
    {
        logger.LogDebug("Fetching customer gas in store");
        return _httpClient.GetFromJsonAsync<int>(CustomerGisEndpoint);
    }

    public Task<int> GetOverallGasInStore()
    {
        logger.LogDebug("Fetching overall gas in store");
        return _httpClient.GetFromJsonAsync<int>(OverallGisEndpoint);
    }

    public static Task<HttpResponseMessage> FallbackGetCustomerGasInStore(Polly.Context context, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("-1", Encoding.UTF8, "application/json")
        };

        return Task.FromResult(response);
    }
}

public interface IService
{ }
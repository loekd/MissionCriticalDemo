using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Logging;
using MissionCriticalDemo.Shared.Contracts;

namespace MissionCriticalDemo.Shared.Services;

/// <summary>
/// Processes dispatching interaction.
/// </summary>
public interface IDispatchService 
{
    Task SubmitRequest(Request request);

    Task<int> GetCustomerGasInStore();

    Task<int> GetOverallGasInStore();
}

/// <summary>
/// Processes dispatching interaction.
/// </summary>
public class DispatchService(HttpClient httpClient, ActivitySource? source, ILogger<DispatchService> logger) : IDispatchService
{
    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    private const string DispatchEndpoint = "api/dispatch";
    private const string CustomerGisEndpoint = "api/gasinstore";
    private const string OverallGisEndpoint = "api/gasinstore/overall";

    public async Task SubmitRequest(Request request)
    {
        using var activity = source?.StartActivity(nameof(SubmitRequest), ActivityKind.Client);
        activity?.AddEvent(new ActivityEvent("Submitting request SubmitRequest", DateTimeOffset.UtcNow));
        
        try
        {
            var response = await _httpClient.PostAsJsonAsync(DispatchEndpoint, request);
            activity?.SetTag("StatusCode", response.StatusCode);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            throw;
        }
    }

    public Task<int> GetCustomerGasInStore()
    {
        using var activity = source?.StartActivity(nameof(GetCustomerGasInStore), ActivityKind.Client);
        activity?.AddEvent(new ActivityEvent("Submitting request GetCustomerGasInStore", DateTimeOffset.UtcNow));

        try
        {
            return _httpClient.GetFromJsonAsync<int>(CustomerGisEndpoint);
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            throw;
        }
    }

    public Task<int> GetOverallGasInStore()
    {
        using var activity = source?.StartActivity(nameof(GetOverallGasInStore), ActivityKind.Client);
        activity?.AddEvent(new ActivityEvent("Submitting request GetOverallGasInStore", DateTimeOffset.UtcNow));
        try
        {
            return _httpClient.GetFromJsonAsync<int>(OverallGisEndpoint);
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            throw;
        }
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
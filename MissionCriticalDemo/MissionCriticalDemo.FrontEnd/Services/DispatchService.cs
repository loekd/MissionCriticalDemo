using MissionCriticalDemo.Shared.Contracts;
using System.Net.Http.Json;

namespace MissionCriticalDemo.FrontEnd.Services
{
    /// <summary>
    /// Processes dispatching interaction.
    /// </summary>
    public interface IDispatchService
    {
        Task SubmitRequest(Request request);

        Task<int> GetCustomerGasInStore();
    }

    /// <summary>
    /// Processes dispatching interaction.
    /// </summary>
    public class DispatchService : IDispatchService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<DispatchService> _logger;
        private const string _dispatchEndpoint = "api/dispatch";
        private const string _gisEndpoint = "api/gasinstore";

        public DispatchService(HttpClient httpClient, ILogger<DispatchService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger;
        }

        public async Task SubmitRequest(Request request)
        {
            _logger.LogDebug("Submitting request {Amount} {Direction}", request.AmountInGWh, request.Direction);
            var response = await _httpClient.PostAsJsonAsync(_dispatchEndpoint, request);
            response.EnsureSuccessStatusCode();
        }

        public Task<int> GetCustomerGasInStore()
        {
            _logger.LogDebug("Fetching gas in store");
            return _httpClient.GetFromJsonAsync<int>(_gisEndpoint);
        }
    }
}

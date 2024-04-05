using MissionCriticalDemo.Shared.Contracts;
using System.Net.Http.Json;
using System.Text;

namespace MissionCriticalDemo.FrontEnd.Services
{
    public interface IService
    { }

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
    public class DispatchService : IDispatchService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<DispatchService> _logger;
        private const string _dispatchEndpoint = "api/dispatch";
        private const string _customerGisEndpoint = "api/gasinstore";
        private const string _overallGisEndpoint = "api/gasinstore/overall";

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
            _logger.LogDebug("Fetching customer gas in store");
            return _httpClient.GetFromJsonAsync<int>(_customerGisEndpoint);
        }

        public Task<int> GetOverallGasInStore()
        {
            _logger.LogDebug("Fetching overall gas in store");
            return _httpClient.GetFromJsonAsync<int>(_overallGisEndpoint);
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
}

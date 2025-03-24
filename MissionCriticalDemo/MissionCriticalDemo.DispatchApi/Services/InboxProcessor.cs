using System.Text.Json;
using Dapr.Client;
using Microsoft.AspNetCore.SignalR;
using MissionCriticalDemo.DispatchApi.Hubs;
using MissionCriticalDemo.Messages;
using MissionCriticalDemo.Shared;
using MissionCriticalDemo.Shared.Contracts;

namespace MissionCriticalDemo.DispatchApi.Services
{
    public class InboxProcessor : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<InboxProcessor> _logger;
        private const string _stateStoreName = "inboxstate";
        private const string query = "{\"sort\": [{\"key\": \"value.Timestamp\",\"order\": \"DESC\"}]}";
        private readonly CancellationTokenSource _stopTokenSource = new();

        /// <summary>
        /// Monitors the Inbox and processes messages if they are there.
        /// </summary>
        /// <param name="serviceProvider"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public InboxProcessor(IServiceScopeFactory serviceScopeFactory, ILogger<InboxProcessor> logger)
        {
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            //return;
            await Task.Delay(10_000, cancellationToken);

            _logger.LogTrace("Running Inbox processor");

            using var scope = _serviceScopeFactory.CreateScope();
            var daprClient = scope.ServiceProvider.GetRequiredService<DaprClient>();
            var stopToken = _stopTokenSource.Token;

            while (!stopToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessInboxItems(daprClient, stopToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process Inbox items.");
                    await Task.Delay(10_000, stopToken);
                }
                finally
                {
                    await Task.Delay(1000, stopToken);
                }
            }

            _logger.LogTrace("Stopped Inbox processor");
        }

        private async Task ProcessInboxItems(DaprClient daprClient, CancellationToken stopToken)
        {
            var response = await FetchInboxItems(daprClient, stopToken);

            if (response != null)
            {
                foreach (var result in response.Results)
                {
                    var decoded = Convert.FromBase64String(result.Data);
                    string json = System.Text.Encoding.UTF8.GetString(decoded, 0, decoded.Length);
                    var customerRequest = JsonSerializer.Deserialize<CustomerRequest>(json)!;
                    
                    _logger.LogTrace("Processing customer change {RequestId} from customer {CustomerId}!", customerRequest.RequestId, customerRequest.CustomerId);
                    await ProcessCustomerRequest(customerRequest, stopToken);
                    await DeleteInboxMessage(daprClient, customerRequest, stopToken);
                }
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _stopTokenSource.Cancel(true);
            return Task.CompletedTask;
        }

        private static Task<StateQueryResponse<string>> FetchInboxItems(DaprClient daprClient, CancellationToken cancellationToken)
        {
            return daprClient.QueryStateAsync<string>(_stateStoreName, query, cancellationToken: cancellationToken);
        }

        private static async Task DeleteInboxMessage(DaprClient daprClient, CustomerRequest customerRequest, CancellationToken cancellationToken)
        {
            //Should not delete the inbox message, but mark it as processed, so we can detect duplicate messages.
            //This is a simple example, so we delete it.
            await daprClient.DeleteStateAsync(_stateStoreName, customerRequest.RequestId.ToGuidString(), cancellationToken: cancellationToken);
        }

        private async Task ProcessCustomerRequest(CustomerRequest? customerRequest, CancellationToken cancellationToken)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var gasStorage = scope.ServiceProvider.GetRequiredService<IGasStorage>();
            var mappers = scope.ServiceProvider.GetRequiredService<IMappers>();
            var dispatchHub = scope.ServiceProvider.GetRequiredService<IHubContext<DispatchHub>>();

            //process response
            if (customerRequest != null)
            {
                int delta = customerRequest.Direction == Shared.Enums.FlowDirection.Inject ? customerRequest.AmountInGWh : 0 - customerRequest.AmountInGWh;
                int currentAmount = await gasStorage.GetGasInStore(customerRequest.CustomerId);
                int newAmount = currentAmount + delta;

                await gasStorage.SetGasInStore(customerRequest.CustomerId, newAmount);
                var contract = mappers.ToContract(customerRequest, newAmount);

                //cache new storage levels
                await gasStorage.CacheFillLevel(customerRequest.CurrentFillLevel);
                await gasStorage.CacheMaxFillLevel(customerRequest.MaxFillLevel);

                //notify front-end
                await dispatchHub.Clients.All.SendAsync("ReceiveMessage", contract.ToJson(), cancellationToken);

                //log warning so it shows up:
                _logger.LogWarning("Processed OK - CustomerRequest id {customerRequestId} for customer {CustomerId}. Customer GIS: {GIS}", customerRequest.RequestId, customerRequest.CustomerId, newAmount);
            }
            else
            {
                int currentAmount = await gasStorage.GetGasInStore(customerRequest.CustomerId);
                var contract = mappers.ToContract(customerRequest, currentAmount, false);

                //notify front-end
                await dispatchHub.Clients.All.SendAsync("ReceiveMessage", contract.ToJson(), cancellationToken);

                //log warning so it shows up:
                _logger.LogWarning("Processing Failed - CustomerRequest id {customerRequestId} for customer {CustomerId}. Customer GIS: {GIS}", customerRequest.RequestId, customerRequest.CustomerId, currentAmount);
            }
        }
    }
}

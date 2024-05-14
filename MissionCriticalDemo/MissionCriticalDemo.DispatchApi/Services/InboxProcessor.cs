using Dapr.Client;
using Microsoft.AspNetCore.SignalR;
using MissionCriticalDemo.DispatchApi.Hubs;
using MissionCriticalDemo.Messages;
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
                foreach (var customerRequest in response.Results)
                {
                    _logger.LogTrace("Processing customer change {RequestId} from customer {CustomerId}!", customerRequest.Data.RequestId, customerRequest.Data.CustomerId);
                    await ProcessCustomerRequest(customerRequest, stopToken);
                    await MarkInboxMessageAsProcessed(daprClient, customerRequest, stopToken);
                }
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _stopTokenSource.Cancel(true);
            return Task.CompletedTask;
        }

        private static Task<StateQueryResponse<CustomerRequest>> FetchInboxItems(DaprClient daprClient, CancellationToken cancellationToken)
        {
            return daprClient.QueryStateAsync<CustomerRequest>(_stateStoreName, query, cancellationToken: cancellationToken);
        }

        private static async Task MarkInboxMessageAsProcessed(DaprClient daprClient, StateQueryItem<CustomerRequest> customerRequest, CancellationToken cancellationToken)
        {
            //Should not delete the inbox message, but mark it as processed, so we can detect duplicate messages.
            //This is a simple example, so we delete it.
            await daprClient.DeleteStateAsync(_stateStoreName, customerRequest.Key, cancellationToken: cancellationToken);
        }

        private async Task ProcessCustomerRequest(StateQueryItem<CustomerRequest> customerRequest, CancellationToken cancellationToken)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var gasStorage = scope.ServiceProvider.GetRequiredService<IGasStorage>();
            var mappers = scope.ServiceProvider.GetRequiredService<IMappers>();
            var dispatchHub = scope.ServiceProvider.GetRequiredService<IHubContext<DispatchHub>>();

            //process response
            if (customerRequest.Data.Success)
            {
                int delta = customerRequest.Data.Direction == Shared.Enums.FlowDirection.Inject ? customerRequest.Data.AmountInGWh : 0 - customerRequest.Data.AmountInGWh;
                int currentAmount = await gasStorage.GetGasInStore(customerRequest.Data.CustomerId);
                int newAmount = currentAmount + delta;

                await gasStorage.SetGasInStore(customerRequest.Data.CustomerId, newAmount);
                var contract = mappers.ToContract(customerRequest.Data, newAmount);

                //cache new storage levels
                await gasStorage.CacheFillLevel(customerRequest.Data.CurrentFillLevel);
                await gasStorage.CacheMaxFillLevel(customerRequest.Data.MaxFillLevel);

                //notify front-end
                await dispatchHub.Clients.All.SendAsync("ReceiveMessage", contract.ToJson(), cancellationToken);

                //log warning so it shows up:
                _logger.LogWarning("Processed OK - CustomerRequest id {customerRequestId} for customer {CustomerId}. Customer GIS: {GIS}", customerRequest.Data.RequestId, customerRequest.Data.CustomerId, newAmount);
            }
            else
            {
                int currentAmount = await gasStorage.GetGasInStore(customerRequest.Data.CustomerId);
                var contract = mappers.ToContract(customerRequest.Data, currentAmount, false);

                //notify front-end
                await dispatchHub.Clients.All.SendAsync("ReceiveMessage", contract.ToJson(), cancellationToken);

                //log warning so it shows up:
                _logger.LogWarning("Processing Failed - CustomerRequest id {customerRequestId} for customer {CustomerId}. Customer GIS: {GIS}", customerRequest.Data.RequestId, customerRequest.Data.CustomerId, currentAmount);
            }
        }
    }
}

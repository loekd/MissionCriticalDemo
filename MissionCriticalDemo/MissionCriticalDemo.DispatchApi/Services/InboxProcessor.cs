using Dapr.Client;
using Microsoft.AspNetCore.SignalR;
using MissionCriticalDemo.DispatchApi.Hubs;
using MissionCriticalDemo.Messages;
using MissionCriticalDemo.Shared.Contracts;
using System.Collections.Generic;
using System.Text.Json;
using MissionCriticalDemo.Shared;

namespace MissionCriticalDemo.DispatchApi.Services
{
    public class InboxProcessor : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<InboxProcessor> _logger;
        private const string _stateStoreName = "inboxstate";
        private const string _keyTrackerKey = "inbox_key_tracker";
        private readonly CancellationTokenSource _stopTokenSource = new();

        public InboxProcessor(IServiceScopeFactory serviceScopeFactory, ILogger<InboxProcessor> logger)
        {
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
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
            var results = await FetchInboxItems(daprClient, stopToken);

            if (results.Count > 0)
            {
                foreach (var customerRequest in results)
                {
                    _logger.LogTrace("Processing customer change {RequestId} from customer {CustomerId}!", 
                        customerRequest.RequestId, customerRequest.CustomerId);
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

        private async Task<List<CustomerRequest>> FetchInboxItems(DaprClient daprClient, CancellationToken cancellationToken)
        {
            // Get the key tracker
            var keyTracker = await GetKeyTracker(daprClient, cancellationToken);
            
            if (keyTracker.MessageKeys.Count == 0)
                return [];

            var results = new List<CustomerRequest>();
            
            // Fetch each message by key
            foreach (string key in keyTracker.MessageKeys)
            {
                try
                {
                    var customerRequest = await daprClient.GetStateAsync<CustomerRequest>(
                        _stateStoreName, key, cancellationToken: cancellationToken);
                    
                    if (customerRequest != null)
                    {
                        results.Add(customerRequest);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error retrieving message with key {Key}", key);
                }
            }
            
            // Sort results by timestamp descending if needed
            return results.OrderByDescending(r => r.Timestamp).ToList();
        }

        private async Task<InboxKeyTracker> GetKeyTracker(DaprClient daprClient, CancellationToken cancellationToken)
        {
            try
            {
                var tracker = await daprClient.GetStateAsync<InboxKeyTracker>(
                    _stateStoreName, _keyTrackerKey, cancellationToken: cancellationToken);
                
                return tracker ?? new InboxKeyTracker();
            }
            catch 
            {
                return new InboxKeyTracker();
            }
        }

        private async Task DeleteInboxMessage(DaprClient daprClient, CustomerRequest customerRequest, 
            CancellationToken cancellationToken)
        {
            // First remove key from tracker
            var keyTracker = await GetKeyTracker(daprClient, cancellationToken);
            keyTracker.MessageKeys.Remove(customerRequest.RequestId.ToGuidString());
            
            // Update tracker and delete message in a transaction
            var requests = new List<StateTransactionRequest>
            {
                new(
                    _keyTrackerKey, 
                    JsonSerializer.SerializeToUtf8Bytes(keyTracker), 
                    StateOperationType.Upsert
                ),
                new(
                    customerRequest.RequestId.ToGuidString(),
                    null,
                    StateOperationType.Delete
                )
            };
            
            await daprClient.ExecuteStateTransactionAsync(_stateStoreName, requests, cancellationToken: cancellationToken);
        }

        private async Task ProcessCustomerRequest(CustomerRequest customerRequest, CancellationToken cancellationToken)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var gasStorage = scope.ServiceProvider.GetRequiredService<IGasStorage>();
            var mappers = scope.ServiceProvider.GetRequiredService<IMappers>();
            var dispatchHub = scope.ServiceProvider.GetRequiredService<IHubContext<DispatchHub>>();

            //process response
            if (customerRequest.Success)
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

    // Class to track inbox message keys
    public class InboxKeyTracker
    {
        public List<string> MessageKeys { get; set; } = [];
    }
}
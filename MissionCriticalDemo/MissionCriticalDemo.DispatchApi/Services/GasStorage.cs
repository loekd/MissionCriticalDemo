using Dapr.Client;
using MissionCriticalDemo.Shared;
using System.Text.Json;
using MissionCriticalDemo.Messages;
using Microsoft.Extensions.Caching.Distributed;
using static MissionCriticalDemo.Shared.Constants;
using MissionCriticalDemo.Shared.Contracts;


namespace MissionCriticalDemo.DispatchApi.Services
{
    /// <summary>
    /// Gas storage as seen from the customer perspective
    /// </summary>
    public interface IGasStorage
    {
        Task SetGasInStore(Guid customerId, int amount);
        Task<int> GetGasInStore(Guid customerId);
        Task ProcessRequest(Guid customerId, Shared.Contracts.Request request);
        Task StoreIncomingMessage(Messages.Response response);
        Task CacheFillLevel(int amount);
        Task CacheMaxFillLevel(int amount);
        Task<int?> GetCachedFillLevel();
        Task<int?> GetCachedMaxFillLevel();
    }

    public class GasStorage(DaprClient daprClient, IMappers mappers, IDistributedCache cache, ILogger<GasStorage> logger) : IGasStorage
    {
        private const string _gasInStoreStateStoreName = "gasinstorestate";
        private const string _outboxStateStoreName = "outboxstate";
        private const string _inboxStateStoreName = "inboxstate";
        private const string _outboxKeyTrackerKey = "outbox_key_tracker";
        private const string _inboxKeyTrackerKey = "inbox_key_tracker";

        


        public async Task<int> GetGasInStore(Guid customerId)
        {
            //return 100;
            int amount = await daprClient.GetStateAsync<int>(_gasInStoreStateStoreName, customerId.ToGuidString());
            return amount;
        }

        public async Task SetGasInStore(Guid customerId, int amount)
        {
            await daprClient.SaveStateAsync(_gasInStoreStateStoreName, customerId.ToGuidString(), amount);
        }

        public async Task CacheFillLevel(int amount)
        {
            await cache.SetStringAsync(CacheKeyFillLevel, amount.ToString());
        }

        public async Task CacheMaxFillLevel(int amount)
        {
            await cache.SetStringAsync(CacheKeyMaximumFillLevel, amount.ToString());
        }

        public async Task<int?> GetCachedFillLevel()
        {
            int currentTotal;

            if (!int.TryParse(await cache.GetStringAsync(CacheKeyFillLevel), out currentTotal))
            {
                logger.LogWarning("Failed to get gas in store from cache.");
            }
            return currentTotal;
        }

        public async Task<int?> GetCachedMaxFillLevel()
        {
            int maxFillLevel;
            if (!int.TryParse(await cache.GetStringAsync(CacheKeyFillLevel), out maxFillLevel))
            {
                logger.LogWarning("Failed to get maximum fill level from cache.");
            }
            return maxFillLevel;
        }

        public async Task ProcessRequest(Guid customerId, Shared.Contracts.Request request)
        {
            //we don't directly call the Plant API, but instead put a message in the outbox
            //this way we can retry if the Plant API is down
            
            // Get current key tracker
            var keyTracker = await daprClient.GetStateAsync<OutboxKeyTracker>(
                _outboxStateStoreName, _outboxKeyTrackerKey) ?? new OutboxKeyTracker();
            string messageKey = request.RequestId.ToGuidString();

            // Add the new key if it doesn't exist
            if (!keyTracker.MessageKeys.Contains(messageKey))
            {
                keyTracker.MessageKeys.Add(messageKey);
            }
            
            var message = mappers.ToMessage(request, customerId);
            var requests = new List<StateTransactionRequest>()
            {
                new(request.RequestId.ToGuidString(), JsonSerializer.SerializeToUtf8Bytes(message), StateOperationType.Upsert),
                new(_outboxKeyTrackerKey, JsonSerializer.SerializeToUtf8Bytes(keyTracker), StateOperationType.Upsert)

                //TODO: add additional state changes in same transaction if needed
            };

            //save changes in transaction
            await daprClient.ExecuteStateTransactionAsync(_outboxStateStoreName, requests, cancellationToken: CancellationToken.None);
        }

        public async Task StoreIncomingMessage(Messages.Response response)
        {
            var contract = mappers.ToCustomerContract(response);
            string messageKey = contract.RequestId.ToGuidString();
    
            // Get current key tracker
            var keyTracker = await daprClient.GetStateAsync<InboxKeyTracker>(
                _inboxStateStoreName, _inboxKeyTrackerKey) ?? new InboxKeyTracker();
    
            // Add the new key if it doesn't exist
            if (!keyTracker.MessageKeys.Contains(messageKey))
            {
                keyTracker.MessageKeys.Add(messageKey);
            }
    
            // Save both in a transaction
            var requests = new List<StateTransactionRequest>()
            {
                new(messageKey, JsonSerializer.SerializeToUtf8Bytes(contract), StateOperationType.Upsert),
                new(_inboxKeyTrackerKey, JsonSerializer.SerializeToUtf8Bytes(keyTracker), StateOperationType.Upsert)
            };

            await daprClient.ExecuteStateTransactionAsync(_inboxStateStoreName, requests, cancellationToken: CancellationToken.None);
        }
    }
}
using Dapr.Client;
using MissionCriticalDemo.Shared;
using System.Text.Json;
using MissionCriticalDemo.Messages;
using Microsoft.Extensions.Caching.Distributed;
using static MissionCriticalDemo.Shared.Constants;
using MissionCriticalDemo.Shared.Contracts;
using System.Diagnostics;

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

    public class GasStorage : IGasStorage
    {
        private readonly DaprClient _daprClient;
        private readonly IMappers _mappers;
        private readonly IDistributedCache _cache;
        private readonly ILogger<GasStorage> _logger;
        private readonly ActivitySource _activitySource;

        private const string _gasInStoreStateStoreName = "gasinstorestate";
        private const string _outboxStateStoreName = "outboxstate";
        private const string _inboxStateStoreName = "inboxstate";
        private const string _outboxKeyTrackerKey = "outbox_key_tracker";
        private const string _inboxKeyTrackerKey = "inbox_key_tracker";

        public GasStorage(DaprClient daprClient, IMappers mappers, IDistributedCache cache, ActivitySource activitySource, ILogger<GasStorage> logger)
        {
            _daprClient = daprClient;
            _mappers = mappers;
            _cache = cache;
            _logger = logger;
            _activitySource = activitySource;
        }

        public async Task<int> GetGasInStore(Guid customerId)
        {
            using var activity = _activitySource.StartActivity("GetGasInStore", ActivityKind.Internal);
            activity?.SetTag("customerId", customerId);
            
            int amount = await _daprClient.GetStateAsync<int>(_gasInStoreStateStoreName, customerId.ToGuidString());
            activity?.SetTag("amount", amount);
            return amount;
        }

        public async Task SetGasInStore(Guid customerId, int amount)
        {
            using var activity = _activitySource.StartActivity("SetGasInStore", ActivityKind.Internal);
            activity?.SetTag("customerId", customerId);
            activity?.SetTag("amount", amount);

            await _daprClient.SaveStateAsync(_gasInStoreStateStoreName, customerId.ToGuidString(), amount);
        }

        public async Task CacheFillLevel(int amount)
        {
            using var activity = _activitySource.StartActivity("CacheFillLevel", ActivityKind.Internal);
            activity?.SetTag("amount", amount);

            await _cache.SetStringAsync(CacheKeyFillLevel, amount.ToString());
        }

        public async Task CacheMaxFillLevel(int amount)
        {
            using var activity = _activitySource.StartActivity("CacheMaxFillLevel", ActivityKind.Internal);
            activity?.SetTag("amount", amount);

            await _cache.SetStringAsync(CacheKeyMaximumFillLevel, amount.ToString());
        }

        public async Task<int?> GetCachedFillLevel()
        {
            using var activity = _activitySource.StartActivity("GetCachedFillLevel", ActivityKind.Internal);
            
            int currentTotal;
            if (!int.TryParse(await _cache.GetStringAsync(CacheKeyFillLevel), out currentTotal))
            {
                _logger.LogWarning("Failed to get gas in store from cache.");
            }
            activity?.SetTag("fillLevel", currentTotal);
            return currentTotal;
        }

        public async Task<int?> GetCachedMaxFillLevel()
        {
            using var activity = _activitySource.StartActivity("GetCachedMaxFillLevel", ActivityKind.Internal);
            
            int maxFillLevel;
            if (!int.TryParse(await _cache.GetStringAsync(CacheKeyFillLevel), out maxFillLevel))
            {
                _logger.LogWarning("Failed to get maximum fill level from cache.");
            }
            activity?.SetTag("maxFillLevel", maxFillLevel);
            return maxFillLevel;
        }

        public async Task ProcessRequest(Guid customerId, Shared.Contracts.Request request)
        {
            using var activity = _activitySource.StartActivity("ProcessRequest", ActivityKind.Internal);
            activity?.SetTag("customerId", customerId);
            activity?.SetTag("requestId", request.RequestId);
            activity?.SetTag("direction", request.Direction.ToString());
            activity?.SetTag("amount", request.AmountInGWh);
            
            // Get current key tracker
            var keyTracker = await _daprClient.GetStateAsync<OutboxKeyTracker>(
                _outboxStateStoreName, _outboxKeyTrackerKey) ?? new OutboxKeyTracker();
            string messageKey = request.RequestId.ToGuidString();

            // Add the new key if it doesn't exist
            if (!keyTracker.MessageKeys.Contains(messageKey))
            {
                keyTracker.MessageKeys.Add(messageKey);
            }
            
            var message = _mappers.ToMessage(request, customerId);

            // Store the current activity context for the message
            if (Activity.Current != null)
            {
                keyTracker.TraceContexts[messageKey] = new Dictionary<string, string>
                {
                    ["traceparent"] = Activity.Current.Id ?? string.Empty,
                    ["tracestate"] = Activity.Current.TraceStateString ?? string.Empty
                };
            }
            
            var requests = new List<StateTransactionRequest>()
            {
                new(messageKey, JsonSerializer.SerializeToUtf8Bytes(message), StateOperationType.Upsert),
                new(_outboxKeyTrackerKey, JsonSerializer.SerializeToUtf8Bytes(keyTracker), StateOperationType.Upsert)
            };

            await _daprClient.ExecuteStateTransactionAsync(_outboxStateStoreName, requests, cancellationToken: CancellationToken.None);
        }

        public async Task StoreIncomingMessage(Messages.Response response)
        {
            using var activity = _activitySource.StartActivity("StoreIncomingMessage", ActivityKind.Internal);
            activity?.SetTag("responseId", response.ResponseId);
            activity?.SetTag("customerId", response.CustomerId);
            activity?.SetTag("requestId", response.RequestId);
            
            var contract = _mappers.ToCustomerContract(response);
            string messageKey = contract.RequestId.ToGuidString();
    
            // Get current key tracker
            var keyTracker = await _daprClient.GetStateAsync<InboxKeyTracker>(
                _inboxStateStoreName, _inboxKeyTrackerKey) ?? new InboxKeyTracker();
    
            // Add the new key if it doesn't exist
            if (!keyTracker.MessageKeys.Contains(messageKey))
            {
                keyTracker.MessageKeys.Add(messageKey);
            }

            // Store the current activity context
            if (Activity.Current != null)
            {
                keyTracker.TraceContexts[messageKey] = new Dictionary<string, string>
                {
                    ["traceparent"] = Activity.Current.Id ?? string.Empty,
                    ["tracestate"] = Activity.Current.TraceStateString ?? string.Empty
                };
            }
    
            // Save both in a transaction
            var requests = new List<StateTransactionRequest>()
            {
                new(messageKey, JsonSerializer.SerializeToUtf8Bytes(contract), StateOperationType.Upsert),
                new(_inboxKeyTrackerKey, JsonSerializer.SerializeToUtf8Bytes(keyTracker), StateOperationType.Upsert)
            };

            await _daprClient.ExecuteStateTransactionAsync(_inboxStateStoreName, requests, cancellationToken: CancellationToken.None);
        }
    }
}
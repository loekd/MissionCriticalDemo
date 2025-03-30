using System.Text.Json;
using Dapr.Client;
using MissionCriticalDemo.Messages;

namespace MissionCriticalDemo.DispatchApi.Services
{
    public class OutboxProcessor : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<OutboxProcessor> _logger;
        private const string _stateStoreName = "outboxstate";
        private const string _pubSubName = "dispatchpubsub";
        private const string _pubSubTopicName = "flowint";
        private const string _outboxKeyTrackerKey = "outbox_key_tracker";
        private readonly CancellationTokenSource _stopTokenSource = new CancellationTokenSource();

        public OutboxProcessor(IServiceScopeFactory serviceScopeFactory, ILogger<OutboxProcessor> logger)
        {
            _serviceScopeFactory = serviceScopeFactory ?? throw new System.ArgumentNullException(nameof(serviceScopeFactory));
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(10_000, cancellationToken);

            _logger.LogTrace("Running outbox processor");

            using var scope = _serviceScopeFactory.CreateScope();
            var daprClient = scope.ServiceProvider.GetRequiredService<DaprClient>();
            var stopToken = _stopTokenSource.Token;

            while (!stopToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessOutboxItems(daprClient, stopToken);
                }
                catch (System.Exception ex)
                {
                    _logger.LogError(ex, "Failed to process outbox items.");
                    await Task.Delay(10_000, stopToken);
                }
                finally
                {
                    await Task.Delay(1000, stopToken);
                }
            }

            _logger.LogTrace("Stopped outbox processor");
        }

        private async Task ProcessOutboxItems(DaprClient daprClient, CancellationToken stopToken)
        {
            var keyTracker = await GetOutboxKeyTracker(daprClient, stopToken);
            if (keyTracker.MessageKeys.Count == 0)
                return;

            // Use a copy to allow safe removal during iteration
            foreach (string key in new List<string>(keyTracker.MessageKeys))
            {
                try
                {
                    var message = await daprClient.GetStateAsync<Request>(
                        _stateStoreName, key, cancellationToken: stopToken);
                    if (message != null)
                    {
                        _logger.LogTrace("Publishing message id {RequestId} from outbox", message.RequestId);
                        await PublishRequestMessage(daprClient, message, stopToken);
                        await DeleteOutboxMessage(daprClient, key, stopToken);
                    }
                    else
                    {
                        // If message is null, simply remove the key from the tracker.
                        await DeleteOutboxMessage(daprClient, key, stopToken);
                    }
                }
                catch (System.Exception ex)
                {
                    _logger.LogError(ex, "Error processing message with key {key}", key);
                }
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _stopTokenSource.Cancel(true);
            return Task.CompletedTask;
        }

        private async Task<OutboxKeyTracker> GetOutboxKeyTracker(DaprClient daprClient, CancellationToken cancellationToken)
        {
            try
            {
                var tracker = await daprClient.GetStateAsync<OutboxKeyTracker>(
                    _stateStoreName, _outboxKeyTrackerKey, cancellationToken: cancellationToken);
                return tracker ?? new OutboxKeyTracker();
            }
            catch
            {
                return new OutboxKeyTracker();
            }
        }

        private async Task DeleteOutboxMessage(DaprClient daprClient, string key, CancellationToken cancellationToken)
        {
            // First remove key from tracker and then delete the message
            var keyTracker = await GetOutboxKeyTracker(daprClient, cancellationToken);
            keyTracker.MessageKeys.Remove(key);

            var requests = new List<StateTransactionRequest>
            {
                new StateTransactionRequest(_outboxKeyTrackerKey, JsonSerializer.SerializeToUtf8Bytes(keyTracker), StateOperationType.Upsert),
                new StateTransactionRequest(key, null, StateOperationType.Delete)
            };

            await daprClient.ExecuteStateTransactionAsync(_stateStoreName, requests, cancellationToken: cancellationToken);
        }

        private static async Task PublishRequestMessage(DaprClient daprClient, Request message, CancellationToken cancellationToken)
        {
            await daprClient.PublishEventAsync(_pubSubName, _pubSubTopicName, message, cancellationToken: cancellationToken);
        }
    }
    
    
        public class OutboxKeyTracker
        {
            public List<string> MessageKeys { get; set; } = [];
        }
    
}
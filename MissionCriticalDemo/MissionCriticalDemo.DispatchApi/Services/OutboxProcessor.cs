using System.Text.Json;
using Dapr.Client;
using MissionCriticalDemo.Messages;
using System.Diagnostics;

namespace MissionCriticalDemo.DispatchApi.Services
{
    public class OutboxProcessor : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<OutboxProcessor> _logger;
        private readonly ActivitySource _activitySource;
        private const string _stateStoreName = "outboxstate";
        private const string _pubSubName = "dispatchpubsub";
        private const string _pubSubTopicName = "flowint";
        private const string _outboxKeyTrackerKey = "outbox_key_tracker";
        private readonly CancellationTokenSource _stopTokenSource = new();

        public OutboxProcessor(IServiceScopeFactory serviceScopeFactory, ActivitySource activitySource, ILogger<OutboxProcessor> logger)
        {
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _logger = logger;
            _activitySource = activitySource;
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
                catch (Exception ex)
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
            using var activity = _activitySource.StartActivity("ProcessOutboxItems", ActivityKind.Internal);
            
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

                        // Get stored trace context for this message
                        Dictionary<string, string>? traceContext = null;
                        if (keyTracker.TraceContexts.TryGetValue(key, out var storedContext))
                        {
                            traceContext = storedContext;
                        }

                        await PublishRequestMessage(daprClient, message, traceContext, stopToken);
                        await DeleteOutboxMessage(daprClient, key, stopToken);
                    }
                    else
                    {
                        // If message is null, simply remove the key from the tracker.
                        await DeleteOutboxMessage(daprClient, key, stopToken);
                    }
                }
                catch (Exception ex)
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
            using var activity = _activitySource.StartActivity("DeleteOutboxMessage", ActivityKind.Internal);
            activity?.SetTag("messageKey", key);
            
            // First remove key from tracker and then delete the message
            var keyTracker = await GetOutboxKeyTracker(daprClient, cancellationToken);
            keyTracker.MessageKeys.Remove(key);
            keyTracker.TraceContexts.Remove(key);

            var requests = new List<StateTransactionRequest>
            {
                new StateTransactionRequest(_outboxKeyTrackerKey, JsonSerializer.SerializeToUtf8Bytes(keyTracker), StateOperationType.Upsert),
                new StateTransactionRequest(key, null, StateOperationType.Delete)
            };

            await daprClient.ExecuteStateTransactionAsync(_stateStoreName, requests, cancellationToken: cancellationToken);
        }

        private async Task PublishRequestMessage(DaprClient daprClient, Request message, Dictionary<string, string>? traceContext, CancellationToken cancellationToken)
        {
            using var activity = _activitySource.StartActivity("PublishRequestMessage", ActivityKind.Producer, 
                parentContext: traceContext != null ? ExtractActivityContext(traceContext) : default);

            activity?.SetTag("requestId", message.RequestId);
            activity?.SetTag("direction", message.Direction.ToString());
            activity?.SetTag("amount", message.AmountInGWh);

            // Always use the current activity context for the outgoing message
            var metadata = new Dictionary<string, string>();
            if (Activity.Current != null)
            {
                metadata["traceparent"] = Activity.Current.Id ?? string.Empty;
                if (!string.IsNullOrEmpty(Activity.Current.TraceStateString))
                {
                    metadata["tracestate"] = Activity.Current.TraceStateString;
                }
            }

            await daprClient.PublishEventAsync(_pubSubName, _pubSubTopicName, message, metadata, cancellationToken);
        }

        private static ActivityContext ExtractActivityContext(Dictionary<string, string> traceContext)
        {
            if (traceContext.TryGetValue("traceparent", out var traceparent) && 
                ActivityContext.TryParse(traceparent, traceContext.GetValueOrDefault("tracestate"), out var activityContext))
            {
                return activityContext;
            }
            return default;
        }
    }

    public class OutboxKeyTracker
    {
        public List<string> MessageKeys { get; set; } = [];
        public Dictionary<string, Dictionary<string, string>> TraceContexts { get; set; } = [];
    }
}
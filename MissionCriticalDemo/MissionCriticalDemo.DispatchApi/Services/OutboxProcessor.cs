using Dapr.Client;
using MissionCriticalDemo.Messages;

namespace MissionCriticalDemo.DispatchApi.Services
{
    public class OutboxProcessor : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<OutboxProcessor> _logger;
        private const string _stateStoreName = "dispatchstate";
        private const string _pubSubName = "dispatchpubsub";
        private const string _pubSubTopicName = "flowint";
        private const string query = "{\"sort\": [{\"key\": \"value.Timestamp\",\"order\": \"DESC\"}]}";
        private readonly CancellationTokenSource _stopTokenSource = new CancellationTokenSource();

        /// <summary>
        /// Monitors the outbox and processes messages if they are there.
        /// </summary>
        /// <param name="serviceProvider"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public OutboxProcessor(IServiceScopeFactory serviceScopeFactory, ILogger<OutboxProcessor> logger)
        {
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            //return;
            await Task.Delay(10_000);

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
            var response = await FetchOutboxItems(daprClient, stopToken);

            if (response != null)
            {
                foreach (var flowRequest in response.Results)
                {
                    _logger.LogTrace("Publishing message id {RequestId} from customer {CustomerId}!", flowRequest.Data.RequestId, flowRequest.Data.CustomerId);
                    await PublishRequestMessage(daprClient, flowRequest, stopToken);
                    await DeleteOutboxMessage(daprClient, flowRequest, stopToken);
                }
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _stopTokenSource.Cancel(true);
            return Task.CompletedTask;
        }

        private static Task<StateQueryResponse<Request>> FetchOutboxItems(DaprClient daprClient, CancellationToken stopToken)
        {
            return daprClient.QueryStateAsync<Request>(_stateStoreName, query, cancellationToken: stopToken);
        }

        private static async Task DeleteOutboxMessage(DaprClient daprClient, StateQueryItem<Request> flowRequest, CancellationToken cancellationToken)
        {
            await daprClient.DeleteStateAsync(_stateStoreName, flowRequest.Key, cancellationToken: cancellationToken);
        }

        private static async Task PublishRequestMessage(DaprClient daprClient, StateQueryItem<Request> flowRequest, CancellationToken cancellationToken)
        {
            await daprClient.PublishEventAsync(_pubSubName, _pubSubTopicName, flowRequest.Data, cancellationToken: cancellationToken);
        }
    }
}

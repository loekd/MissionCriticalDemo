using System.Text.Json;
using Dapr.Client;
using MissionCriticalDemo.DispatchApi.Controllers;
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
                foreach (var result in response.Results)
                {
                    var decoded = Convert.FromBase64String(result.Data);
                    string json = System.Text.Encoding.UTF8.GetString(decoded, 0, decoded.Length);
                    var flowRequest = JsonSerializer.Deserialize<MissionCriticalDemo.Messages.Request>(json)!;
                    _logger.LogTrace("Publishing message id {RequestId} from customer {CustomerId}!",
                        flowRequest.RequestId, flowRequest.CustomerId);
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

        private async Task<StateQueryResponse<string>> FetchOutboxItems(DaprClient daprClient,
            CancellationToken stopToken)
        {
            try
            {
                var result = 
                    await daprClient.QueryStateAsync<string>(_stateStoreName, query, cancellationToken: stopToken);
                return result;
            }
            catch (Exception ex)
            {
                string debug =
                    await daprClient.GetStateAsync<string>(_stateStoreName, Guid.Parse("da95cb85-e946-46b7-b4ec-5952134d2d6b").ToGuidString(), cancellationToken: stopToken);
                _logger.LogError(ex, "Failed to fetch outbox items. {Record}", debug);
                throw;
            }
        }

        private static async Task DeleteOutboxMessage(DaprClient daprClient, Request flowRequest,
            CancellationToken cancellationToken)
        {
            await daprClient.DeleteStateAsync(_stateStoreName, flowRequest.RequestId.ToGuidString(), cancellationToken: cancellationToken);
        }

        private static async Task PublishRequestMessage(DaprClient daprClient, Request flowRequest,
            CancellationToken cancellationToken)
        {
            await daprClient.PublishEventAsync(_pubSubName, _pubSubTopicName, flowRequest,
                cancellationToken: cancellationToken);
        }
    }
}
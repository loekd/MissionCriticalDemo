using Dapr.Client;
using Microsoft.AspNetCore.Mvc;
using MissionCriticalDemo.Messages;
using MissionCriticalDemo.PlantApi.Services;
using MissionCriticalDemo.Shared;
using MissionCriticalDemo.Shared.Enums;

namespace MissionCriticalDemo.PlantApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FlowInstructionController(IGasStorage gasStorage, IMappers mappers, ILogger<FlowInstructionController> logger) : ControllerBase
    {
        private readonly IMappers _mappers = mappers ?? throw new ArgumentNullException(nameof(mappers));
        private const string _pubSubName = "dispatchpubsub";
        private const string _pubSubSubscriptionName = "flowint";
        private const string _pubSubTopicName = "flowres";

        //invoked by Dapr when receiving a message from the dispatch service
        [Dapr.Topic(_pubSubName, _pubSubSubscriptionName)]
        [HttpPost(_pubSubSubscriptionName)]
        public async Task<IActionResult> Post([FromServices] DaprClient daprClient, Request request)
        {
            bool success;
            try
            {
                //process
                switch (request.Direction)
                {
                    case FlowDirection.Inject:
                        await gasStorage.InjectGas(request.AmountInGWh);
                        break;
                    case FlowDirection.Withdraw:
                        await gasStorage.WithdrawGas(request.AmountInGWh);
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown flow direction {request.Direction}");
                }
                success = true;
            }
            catch (Exception ex)
            {
                success = false;
                logger.LogError(ex, "Failed to process message {RequestId} for customer {CustomerId}", request.RequestId.ToGuidString(), request.CustomerId);
            }

            var currentFillLevel = await gasStorage.GetGasInStore();
            var maxFillLevel = await gasStorage.GetMaximumFillLevel();
            Response? response = _mappers.ToResponse(request, Guid.NewGuid(), success, DateTimeOffset.UtcNow, currentFillLevel, maxFillLevel);

            await PublishFlowResponseMessage(daprClient, response);
            logger.LogInformation("Published response id {ResponseId} for customer {CustomerId}!", response.ResponseId, request.CustomerId);

            return Ok();
        }

        //this is invoked when using the built-in outbox. But it doesn't work yet. 
        [Dapr.Topic(_pubSubName, "flowint_outbox")]
        [HttpPost("flowint_outbox")]
        public async Task<IActionResult> PostFromDaprOutbox([FromServices] DaprClient daprClient, [FromBody] Request request)
        {
            int a = 23;
            return Ok();
        }

        private async Task PublishFlowResponseMessage(DaprClient daprClient, Response response)
        {
            //Fake buggy messaging service that sometimes retries sending the message multiple times
            if (Random.Shared.Next(0, 11) <= 5)
            {
                await daprClient.PublishEventAsync(_pubSubName, _pubSubTopicName, response);
                logger.LogWarning("Published the same message multiple times! Id:{ResponseId}", response.ResponseId);
            }
            await daprClient.PublishEventAsync(_pubSubName, _pubSubTopicName, response);
        }
    }
}

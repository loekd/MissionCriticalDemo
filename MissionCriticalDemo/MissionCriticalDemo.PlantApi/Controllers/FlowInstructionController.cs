using Dapr.Client;
using Microsoft.AspNetCore.Mvc;
using MissionCriticalDemo.Messages;
using MissionCriticalDemo.Shared;

namespace MissionCriticalDemo.PlantApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FlowInstructionController : ControllerBase
    {
        private readonly IMappers _mappers;
        private readonly ILogger<FlowInstructionController> _logger;
        private const string _pubSubName = "dispatch_pubsub";
        private const string _pubSubSubscriptionName = "flowint";
        private const string _pubSubTopicName = "flowres";

        public FlowInstructionController(IMappers mappers, ILogger<FlowInstructionController> logger)
        {
            _mappers = mappers ?? throw new ArgumentNullException(nameof(mappers));
            _logger = logger;
        }

        [Dapr.Topic(_pubSubName, _pubSubSubscriptionName)]
        [HttpPost(_pubSubSubscriptionName)]
        public async Task<IActionResult> Post([FromServices] DaprClient daprClient, Request request)
        {
            //TODO: 
            // enqueue command
            // process in background
            // send result through queue

            try
            {
                Response? response = _mappers.ToResponse(request, Guid.NewGuid(), true, DateTimeOffset.UtcNow);
                await Task.Delay(500);
                await PublishFlowResponseMessage(daprClient, response);
                _logger.LogInformation("Published response id {ResponseId} for customer {CustomerId}!", response.ResponseId, request.CustomerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process message {RequestId} for customer {CustomerId}", request.RequestId.ToGuidString(), request.CustomerId);
                return BadRequest(ex);
            }

            return Ok();
        }

        private static async Task PublishFlowResponseMessage(DaprClient daprClient, Response response)
        {
            await daprClient.PublishEventAsync(_pubSubName, _pubSubTopicName, response);
        }
    }
}

using Dapr;
using Dapr.Client;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Identity.Web.Resource;
using MissionCriticalDemo.DispatchApi.Hubs;
using MissionCriticalDemo.DispatchApi.Services;
using MissionCriticalDemo.Messages;
using System.Text.Json;

namespace MissionCriticalDemo.DispatchApi.Controllers
{
    [RequiredScope(RequiredScopesConfigurationKey = "AzureAdB2C:Api.AccessScope")]
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class DispatchController : ControllerBase
    {
        private readonly IGasStorage _gasStorage;

        private readonly DaprClient _daprClient;
        private readonly IHubContext<DispatchHub> _dispatchHub;
        private readonly IMappers _mappers;
        private readonly ILogger<DispatchController> _logger;
        private readonly Guid? _userId;

        private const string _stateStoreName = "dispatch_state";
        private const string _pubSubName = "dispatch_pubsub";
        private const string _pubSubSubscriptionName = "flowres";

        public DispatchController(DaprClient daprClient, IGasStorage gasStorage, IHubContext<DispatchHub> dispatchHubContext, IHttpContextAccessor contextAccessor, IMappers mappers, ILogger<DispatchController> logger)
        {
            _daprClient = daprClient ?? throw new ArgumentNullException(nameof(daprClient));
            _gasStorage = gasStorage ?? throw new ArgumentNullException(nameof(gasStorage));
            _dispatchHub = dispatchHubContext ?? throw new ArgumentNullException(nameof(dispatchHubContext));
            _mappers = mappers ?? throw new ArgumentNullException(nameof(mappers));
            _logger = logger;

            var context = contextAccessor.HttpContext ?? throw new ArgumentNullException(nameof(contextAccessor));
            if (context.User?.Identity?.IsAuthenticated ?? false)
            {
                _userId = Guid.Parse(context.User.Claims.Single(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier).Value);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Post(Shared.Contracts.Request request)
        {
            _logger.LogTrace("Storing validated message with id {RequestId} from customer {CustomerId} in outbox", request.RequestId, _userId);
            try
            {
                //validate
                int delta = request.Direction == Shared.Enums.FlowDirection.Inject ? request.AmountInGWh : 0 - request.AmountInGWh;
                var currentTotal = await _gasStorage.GetGasInStore(_userId.GetValueOrDefault());
                if (currentTotal + delta < 0)
                {
                    throw new InvalidOperationException($"Unable to withdraw more gas ({delta}) than customer {_userId} currently has in store ({currentTotal}).");
                }

                var message = _mappers.ToMessage(request, _userId.GetValueOrDefault());
                var requests = new List<StateTransactionRequest>()
                {
                    new StateTransactionRequest(request.RequestId.ToGuidString(), JsonSerializer.SerializeToUtf8Bytes(message), StateOperationType.Upsert),
                    //TODO: add additional state changes in same transaction
                };
                
                //save data in transaction
                await _daprClient.ExecuteStateTransactionAsync(_stateStoreName, requests, cancellationToken : CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save state");
                return BadRequest();
            }
            return Accepted();
        }

        //Invoked by dapr
        [AllowAnonymous]
        [Topic(_pubSubName, _pubSubSubscriptionName)]
        [HttpPost(_pubSubSubscriptionName)]
        public async Task<IActionResult> Post(Response response)
        {
            //process response
            if (response.Success)
            {
                int delta = response.Direction == Shared.Enums.FlowDirection.Inject ? response.AmountInGWh : 0 - response.AmountInGWh;
                var newTotal = await _gasStorage.AddGasInStore(response.CustomerId, delta);
                var contract = _mappers.ToContract(response, newTotal);

                await _dispatchHub.Clients.All.SendAsync("ReceiveMessage", contract.ToJson());

                _logger.LogWarning("Received flow response id {ResponseId} for customer {CustomerId}!", response.ResponseId, response.CustomerId);
            }

            return Ok();
        }
    }
}

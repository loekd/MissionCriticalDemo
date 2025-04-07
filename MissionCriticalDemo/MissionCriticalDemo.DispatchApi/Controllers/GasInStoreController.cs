using Dapr;
using Dapr.Client;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Identity.Web.Resource;
using MissionCriticalDemo.DispatchApi.Hubs;
using MissionCriticalDemo.DispatchApi.Services;
using MissionCriticalDemo.Messages;
using System.Diagnostics;
using System.Text.Json;
using static MissionCriticalDemo.Shared.Constants;

namespace MissionCriticalDemo.DispatchApi.Controllers
{
    //[RequiredScope(RequiredScopesConfigurationKey = "AzureAdB2C:Api.AccessScope")]
    //[Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class GasInStoreController : ControllerBase
    {
        private readonly IGasStorage _gasStorage;
        private readonly IMappers _mappers;
        private readonly IHubContext<DispatchHub> _dispatchHub;
        private readonly IDistributedCache _cache;
        private readonly ILogger<DispatchController> _logger;
        private readonly ActivitySource _activitySource;
        private readonly Guid? _userId;

        private const string _pubSubName = "dispatchpubsub";
        private const string _pubSubSubscriptionName = "flowres";

        public GasInStoreController(IGasStorage gasStorage, IHttpContextAccessor contextAccessor, IMappers mappers, 
            IHubContext<DispatchHub> dispatchHubContext, IDistributedCache cache, ActivitySource activitySource, ILogger<DispatchController> logger)
        {
            _gasStorage = gasStorage ?? throw new ArgumentNullException(nameof(gasStorage));
            _mappers = mappers ?? throw new ArgumentNullException(nameof(mappers));
            _dispatchHub = dispatchHubContext ?? throw new ArgumentNullException(nameof(dispatchHubContext));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _activitySource = activitySource ?? throw new ArgumentNullException(nameof(activitySource));

            // var context = contextAccessor.HttpContext ?? throw new ArgumentNullException(nameof(contextAccessor));
            // if (context.User?.Identity?.IsAuthenticated ?? false)
            // {
            //     _userId = Guid.Parse(context.User.Claims.Single(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier).Value);
            // }
            _userId = new Guid("00000000-0000-0000-0000-000000000000");
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> GetForCustomerId()
        {
            using var activity = _activitySource.StartActivity("GetForCustomerId", ActivityKind.Server);
            activity?.SetTag("userId", _userId);

            //fake buggy service
            if (Random.Shared.Next(0, 11) <= 5)
                return StatusCode(StatusCodes.Status503ServiceUnavailable);

            int currentTotal;
            _logger.LogTrace("Fetching gas in store for customer {CustomerId}", _userId);
            try
            {
                currentTotal = await _gasStorage.GetGasInStore(_userId.GetValueOrDefault());
                activity?.SetTag("amount", currentTotal);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                _logger.LogError(ex, "Failed to get gas in store for customer {CustomerId}", _userId);
                return NotFound();
            }
            return Ok(currentTotal);
        }

        [AllowAnonymous]
        [HttpGet("overall")]
        public async Task<IActionResult> GetGasInStore()
        {
            using var activity = _activitySource.StartActivity("GetGasInStore", ActivityKind.Server);
            int currentTotal = await _gasStorage.GetCachedFillLevel() ?? 0;
            activity?.SetTag("amount", currentTotal);
            return Ok(currentTotal);
        }

        [AllowAnonymous]
        [HttpGet("maxfilllevel")]
        public async Task<IActionResult> GetMaxFillLevel()
        {
            using var activity = _activitySource.StartActivity("GetMaxFillLevel", ActivityKind.Server);
            int maxFillLevel = await _gasStorage.GetCachedMaxFillLevel() ?? 0;
            activity?.SetTag("maxFillLevel", maxFillLevel);
            return Ok(maxFillLevel);
        }

        //Invoked by dapr
        [AllowAnonymous]
        [Topic(_pubSubName, _pubSubSubscriptionName)]
        [HttpPost(_pubSubSubscriptionName)]
        public async Task<IActionResult> ProcessIncomingPlantResponseMessage([FromBody] Messages.Response response)
        {
            // Extract trace context from Dapr CloudEvent headers
            var traceParent = Request.Headers.TryGetValue("traceparent", out var tp) ? tp.ToString() : null;
            var traceState = Request.Headers.TryGetValue("tracestate", out var ts) ? ts.ToString() : null;

            ActivityContext parentContext = default;
            if (!string.IsNullOrEmpty(traceParent))
            {
                ActivityContext.TryParse(traceParent, traceState, out parentContext);
            }

            using var activity = _activitySource.StartActivity(
                "ProcessIncomingPlantResponseMessage", 
                ActivityKind.Consumer,
                parentContext);

            activity?.SetTag("responseId", response.ResponseId);
            activity?.SetTag("customerId", response.CustomerId);
            activity?.SetTag("requestId", response.RequestId);
            activity?.SetTag("amount", response.AmountInGWh);
            activity?.SetTag("fillLevel", response.CurrentFillLevel);
            
            //put the incoming message in an inbox, using the dapr state store
            await _gasStorage.StoreIncomingMessage(response);

            _logger.LogWarning("Received flow response id {ResponseId} for customer {CustomerId}, amount: {Amount}, fill level: {FillLevel}.", 
                response.ResponseId, response.CustomerId, response.AmountInGWh, response.CurrentFillLevel);
            return Ok();
        }
    }
}

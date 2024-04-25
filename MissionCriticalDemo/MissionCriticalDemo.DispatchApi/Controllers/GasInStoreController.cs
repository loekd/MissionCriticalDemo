using Dapr;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Identity.Web.Resource;
using MissionCriticalDemo.DispatchApi.Hubs;
using MissionCriticalDemo.DispatchApi.Services;
using MissionCriticalDemo.Messages;
using static MissionCriticalDemo.Shared.Constants;

namespace MissionCriticalDemo.DispatchApi.Controllers
{
    [RequiredScope(RequiredScopesConfigurationKey = "AzureAdB2C:Api.AccessScope")]
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class GasInStoreController : ControllerBase
    {
        private readonly IGasStorage _gasStorage;
        private readonly IMappers _mappers;
        private readonly IHubContext<DispatchHub> _dispatchHub;
        private readonly IDistributedCache _cache;
        private readonly ILogger<DispatchController> _logger;
        private readonly Guid? _userId;

        private const string _pubSubName = "dispatchpubsub";
        private const string _pubSubSubscriptionName = "flowres";

        public GasInStoreController(IGasStorage gasStorage, IHttpContextAccessor contextAccessor, IMappers mappers, IHubContext<DispatchHub> dispatchHubContext, IDistributedCache cache, ILogger<DispatchController> logger)
        {
            _gasStorage = gasStorage ?? throw new ArgumentNullException(nameof(gasStorage));
            _mappers = mappers ?? throw new ArgumentNullException(nameof(mappers));
            _dispatchHub = dispatchHubContext ?? throw new ArgumentNullException(nameof(dispatchHubContext));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var context = contextAccessor.HttpContext ?? throw new ArgumentNullException(nameof(contextAccessor));
            if (context.User?.Identity?.IsAuthenticated ?? false)
            {
                _userId = Guid.Parse(context.User.Claims.Single(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier).Value);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetForCustomerId()
        {
            //fake buggy service
            if (Random.Shared.Next(0, 11) <= 5)
                return StatusCode(StatusCodes.Status503ServiceUnavailable);

            int currentTotal;
            _logger.LogTrace("Fetching gas in store for customer {CustomerId}", _userId);
            try
            {
                currentTotal = await _gasStorage.GetGasInStore(_userId.GetValueOrDefault());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get gas in store for customer {CustomerId}", _userId);
                return NotFound();
            }
            return Ok(currentTotal);
        }

        [HttpGet("overall")]
        public async Task<IActionResult> GetGasInStore()
        {
            int currentTotal;

            if (!int.TryParse(await _cache.GetStringAsync(CacheKeyFillLevel), out currentTotal))
            {
                _logger.LogWarning("Failed to get gas in store from cache.");
                //TODO: could call plant API to fetch latest value here
            }
            return Ok(currentTotal);
        }

        [HttpGet("maxfilllevel")]
        public async Task<IActionResult> GetMaxFillLevel()
        {
            int maxFillLevel;
            if (!int.TryParse(await _cache.GetStringAsync(CacheKeyFillLevel), out maxFillLevel))
            {
                _logger.LogWarning("Failed to get maximum fill level from cache.");
                //TODO: could call plant API to fetch latest value here
            }
            return Ok(maxFillLevel);
        }

        //Invoked by dapr
        [AllowAnonymous]
        [Topic(_pubSubName, _pubSubSubscriptionName)]
        [HttpPost(_pubSubSubscriptionName)]
        public async Task<IActionResult> ProcessIncomingPlantResponseMessage(Response response)
        {
            //process response
            if (response.Success)
            {
                int delta = response.Direction == Shared.Enums.FlowDirection.Inject ? response.AmountInGWh : 0 - response.AmountInGWh;
                int currentAmount = await _gasStorage.GetGasInStore(response.CustomerId);
                int newAmount = currentAmount + delta;
                await _gasStorage.SetGasInStore(response.CustomerId, newAmount);
                var contract = _mappers.ToContract(response, newAmount, response.CurrentFillLevel);

                //cache new storage levels
                await _cache.SetStringAsync(CacheKeyFillLevel, response.CurrentFillLevel.ToString());
                await _cache.SetStringAsync(CacheKeyMaximumFillLevel, response.MaxFillLevel.ToString());

                //notify front-end
                await _dispatchHub.Clients.All.SendAsync("ReceiveMessage", contract.ToJson());

                _logger.LogWarning("Received flow response id {ResponseId} for customer {CustomerId}!", response.ResponseId, response.CustomerId);
            }

            return Ok();
        }
    }
}

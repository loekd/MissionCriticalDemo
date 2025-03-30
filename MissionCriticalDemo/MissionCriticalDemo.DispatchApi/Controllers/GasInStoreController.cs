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

        [AllowAnonymous]
        [HttpGet("overall")]
        public async Task<IActionResult> GetGasInStore()
        {
            int currentTotal = await _gasStorage.GetCachedFillLevel() ?? 0;
            return Ok(currentTotal);
        }

        [AllowAnonymous]
        [HttpGet("maxfilllevel")]
        public async Task<IActionResult> GetMaxFillLevel()
        {
            int maxFillLevel = await _gasStorage.GetCachedMaxFillLevel() ?? 0;
            return Ok(maxFillLevel);
        }

        //Invoked by dapr
        [AllowAnonymous]
        [Topic(_pubSubName, _pubSubSubscriptionName)]
        [HttpPost(_pubSubSubscriptionName)]
        public async Task<IActionResult> ProcessIncomingPlantResponseMessage(Messages.Response response)
        {
            //put the incoming message in an inbox, using the dapr state store
            await _gasStorage.StoreIncomingMessage(response);

            _logger.LogWarning("Received flow response id {ResponseId} for customer {CustomerId}, amount: {Amount}, fill level: {FillLevel}.", response.ResponseId, response.CustomerId, response.AmountInGWh, response.CurrentFillLevel);
            return Ok();
        }
    }
}

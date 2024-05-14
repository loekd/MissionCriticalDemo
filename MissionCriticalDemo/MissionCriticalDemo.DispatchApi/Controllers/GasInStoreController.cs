using Dapr;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web.Resource;
using MissionCriticalDemo.DispatchApi.Services;

namespace MissionCriticalDemo.DispatchApi.Controllers
{
    [RequiredScope(RequiredScopesConfigurationKey = "AzureAdB2C:Api.AccessScope")]
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class GasInStoreController : ControllerBase
    {
        private readonly IGasStorage _gasStorage;
        private readonly ILogger<DispatchController> _logger;
        private readonly Guid? _userId;
        private const string _pubSubName = "dispatchpubsub";
        private const string _pubSubSubscriptionName = "flowres";

        public GasInStoreController(IGasStorage gasStorage, IHttpContextAccessor contextAccessor, ILogger<DispatchController> logger)
        {
            _gasStorage = gasStorage ?? throw new ArgumentNullException(nameof(gasStorage));
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
            int currentTotal = await _gasStorage.GetCachedFillLevel() ?? 0;
            return Ok(currentTotal);
        }

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

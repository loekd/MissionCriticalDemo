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
    public class DispatchController : ControllerBase
    {
        private readonly IGasStorage _gasStorage;
        private readonly ILogger<DispatchController> _logger;
        private readonly Guid? _userId;

        public DispatchController(IGasStorage gasStorage, IHttpContextAccessor contextAccessor, ILogger<DispatchController> logger)
        {
            _gasStorage = gasStorage ?? throw new ArgumentNullException(nameof(gasStorage));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var context = contextAccessor.HttpContext ?? throw new ArgumentNullException(nameof(contextAccessor));
            if (context.User?.Identity?.IsAuthenticated ?? false)
            {
                _userId = Guid.Parse(context.User.Claims.Single(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier).Value);
            }
        }

        [HttpPost]
        public async Task<IActionResult> ProcessRequest(Shared.Contracts.Request request)
        {
            _logger.LogTrace("Storing validated message with id {RequestId} from customer {CustomerId} in outbox", request.RequestId, _userId);
            try
            {
                //validate customer amount
                int delta = request.Direction == Shared.Enums.FlowDirection.Inject ? request.AmountInGWh : 0 - request.AmountInGWh;
                var currentTotal = await _gasStorage.GetGasInStore(_userId.GetValueOrDefault());
                if (currentTotal + delta < 0)
                {
                    throw new InvalidOperationException($"Unable to withdraw more gas ({delta}) than customer {_userId} currently has in store ({currentTotal}).");
                }

                //validate max fill level using cached data
                var maxFillLevel = await _gasStorage.GetCachedMaxFillLevel();
                if (maxFillLevel.HasValue && currentTotal + delta > maxFillLevel.Value)
                {
                    throw new InvalidOperationException($"Unable to inject more gas ({delta}) than the maximum fill level ({maxFillLevel.Value}).");
                }

                //process 
                await _gasStorage.ProcessRequest(_userId.GetValueOrDefault(), request);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to process request");
                return BadRequest();
            }
            return Accepted();
        }
    }
}

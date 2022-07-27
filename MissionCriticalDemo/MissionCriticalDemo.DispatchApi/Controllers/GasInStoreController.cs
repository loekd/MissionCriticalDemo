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

        public GasInStoreController(IGasStorage gasStorage, IHttpContextAccessor contextAccessor, ILogger<DispatchController> logger)
        {
            _gasStorage = gasStorage ?? throw new ArgumentNullException(nameof(gasStorage));
            _logger = logger;

            var context = contextAccessor.HttpContext ?? throw new ArgumentNullException(nameof(contextAccessor));
            if (context.User?.Identity?.IsAuthenticated ?? false)
            {
                _userId = Guid.Parse(context.User.Claims.Single(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier).Value);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetForCustomerId()
        {
            int currentTotal = 0;
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
    }
}

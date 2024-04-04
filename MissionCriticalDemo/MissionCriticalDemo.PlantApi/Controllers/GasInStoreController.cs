using Microsoft.AspNetCore.Mvc;
using MissionCriticalDemo.PlantApi.Services;

namespace MissionCriticalDemo.DispatchApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GasInStoreController(IGasStorage gasStorage, ILogger<GasInStoreController> logger) : ControllerBase
    {
        [HttpGet("gasinstore")]
        public async Task<IActionResult> GetGasInStore()
        {
            int currentTotal;
            try
            {
                currentTotal = await gasStorage.GetGasInStore();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get gas in store");
                throw;
            }
            return Ok(currentTotal);
        }

        [HttpGet("maxfilllevel")]
        public async Task<IActionResult> GetMaxFillLevel()
        {
            int maxFillLevel;
            try
            {
                maxFillLevel = await gasStorage.GetMaximumFillLevel();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get maximum fill level");
                throw;
            }
            return Ok(maxFillLevel);
        }
    }
}

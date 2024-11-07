using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Controllers.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class LoadBalancingController : ControllerBase
    {
        private readonly ILogger<LoadBalancingController> _logger;
        public LoadBalancingController(ILogger<LoadBalancingController> logger)
        {
            this._logger = logger;
        }

        [HttpGet]
        [ActionName("leader")]
        public async Task<ActionResult<string>> GetLeave()
        {
            _logger.LogInformation("I am the leader");
            DBUtils.IsLeader = true;
            _logger.LogInformation($"[GetAlive] Leader ? {DBUtils.IsLeader}");
            return Ok("1");
        }

        [HttpGet]
        [ActionName("alive")]
        public async Task<ActionResult<string>> GetAlive()
        {
            _logger.LogInformation("Service is alive");

            return Ok("IsAlive");
        }
    }
}

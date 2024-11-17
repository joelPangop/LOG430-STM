using Controllers.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Controllers.Rest
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
        public async Task<ActionResult<string>> GetLeader()
        {
            _logger.LogInformation("I am the leader");
            DBUtils.IsLeader = true;
            _logger.LogInformation($"[GetAlive] Leader ? {DBUtils.IsLeader}");
            return Ok("1");
        }

        [HttpGet]
        [ActionName("notleader")]
        public async Task<ActionResult<string>> GetNotLeave()
        {

            _logger.LogInformation("I am still not the leader");
            DBUtils.IsLeader = false;
            _logger.LogInformation($"[GetAlive] Leader ? {DBUtils.IsLeader}");
            return Ok("0");
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

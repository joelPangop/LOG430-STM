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
        public async Task<ActionResult<string>> GetAlive()
        {
            _logger.LogInformation("I am the leader");

            return Ok("1");
        }
    }
}

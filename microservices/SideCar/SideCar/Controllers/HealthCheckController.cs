using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace SideCar.Controllers
{
    [EnableCors("AllowOrigin")]
    [ApiController]
    [Route("sidecar/[action]")]
    public class HealthCheckController : ControllerBase
    {
        private readonly ServiceMonitor _serviceMonitor;
        private readonly ILogger<HealthCheckController> _logger;

        public HealthCheckController(ILogger<HealthCheckController> logger, ServiceMonitor serviceMonitor)
        {
            _serviceMonitor = serviceMonitor;
            _logger = logger;
        }

        [HttpGet]
        [ActionName("check")]
        public async Task<ActionResult<string>> CheckServices()
        {
            await _serviceMonitor.CheckAllServices();
            _logger.LogInformation("Health checks completed.");
            return Ok("Health checks completed.");
        }
    }
}

using Application.DTO;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Reflection.Metadata;

namespace Controllers.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class LoadBalancingController : ControllerBase
    {
        private readonly ILogger<LoadBalancingController> _logger;
        private readonly IBusInfoProvider _iBusInfoProvider;

        public LoadBalancingController(ILogger<LoadBalancingController> logger, IBusInfoProvider _iBusInfoProvider)
        {
            this._logger = logger;
            this._iBusInfoProvider = _iBusInfoProvider;
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

        [HttpGet]
        [ActionName("optimalBuses")]
        public async Task<ActionResult<string>> OptimalBuses(string fromLatitudeLongitude, string toLatitudeLongitude)
        {
            _logger.LogInformation($"Reexecution de optimalBuses avec les parametres fromLatitudeLongitude: {fromLatitudeLongitude} et toLatitudeLongitude: {toLatitudeLongitude}");

            await _iBusInfoProvider.GetBestBus(fromLatitudeLongitude, toLatitudeLongitude);

            return Ok("OptimalBuses reexecute");
        }

        [HttpGet]
        [ActionName("beginTracking")]
        public async Task<ActionResult<string>> BeginTracking(string parametres)
        {
            _logger.LogInformation($"Reexecution de beginTracking avec les parametres: {parametres}");

            RideDto? stmBus = JsonConvert.DeserializeObject<RideDto>(parametres);

            await _iBusInfoProvider.BeginTracking(stmBus);

            return Ok("BeginTracking reexecute");
        }

        [HttpGet]
        [ActionName("getTrackingUpdate")]
        public async Task<ActionResult<string>> GetTrackingUpdate()
        {
            _logger.LogInformation($"Reexecution de getTrackingUpdate");

            await _iBusInfoProvider.GetTrackingUpdate();

            return Ok("GetTrackingUpdate reexecute");
        }
    }
}

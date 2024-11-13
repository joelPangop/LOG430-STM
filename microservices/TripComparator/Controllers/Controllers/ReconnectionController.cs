using Application.DTO;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Controllers.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class ReconnectionController : ControllerBase
    {
        private readonly ILogger<LoadBalancingController> _logger;
        private readonly IBusInfoProvider _iBusInfoProvider;

        public ReconnectionController(ILogger<LoadBalancingController> logger, IBusInfoProvider _iBusInfoProvider)
        {
            this._logger = logger;
            this._iBusInfoProvider = _iBusInfoProvider;
        }

        [HttpGet]
        [ActionName("optimalBuses")]
        public async Task<ActionResult<string>> OptimalBuses(string fromLatitudeLongitude, string toLatitudeLongitude)
        {
            await _iBusInfoProvider.GetBestBus(fromLatitudeLongitude, toLatitudeLongitude);

            return Ok("OptimalBuses reexecute");
        }

        [HttpGet]
        [ActionName("beginTracking")]
        public async Task<ActionResult<string>> BeginTracking(string parametres)
        {
            RideDto? stmBus = JsonConvert.DeserializeObject<RideDto>(parametres);

            await _iBusInfoProvider.BeginTracking(stmBus);

            return Ok("BeginTracking reexecute");
        }

        [HttpGet]
        [ActionName("getTrackingUpdate")]
        public async Task<ActionResult<string>> GetTrackingUpdate()
        {
            await _iBusInfoProvider.GetTrackingUpdate();

            return Ok("GetTrackingUpdate reexecute");
        }

    }
}

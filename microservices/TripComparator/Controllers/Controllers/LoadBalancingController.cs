using Application.DTO;
using Application.Interfaces;
using Application.Interfaces.Policies;
using Application.Usecases;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Reflection.Metadata;
using System.Threading.Channels;

namespace Controllers.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class LoadBalancingController : ControllerBase
    {
        private readonly ILogger<LoadBalancingController> _logger;
        private readonly IBusInfoProvider _iBusInfoProvider;
        private readonly IRouteTimeProvider _routeTimeProvider;
        private readonly TripComparatorMqController _tripComparatorMq;

        private readonly IInfiniteRetryPolicy<TripComparatorMqController> _infiniteRetryPolicy;
        private readonly IBackOffRetryPolicy<TripComparatorMqController> _backOffRetryPolicy;
        private readonly CompareTimes _compareTimes;

        public LoadBalancingController(ILogger<LoadBalancingController> logger, IBusInfoProvider _iBusInfoProvider, TripComparatorMqController tripComparatorMq, IRouteTimeProvider routeTimeProvider,
            CompareTimes compareTimes,
        IInfiniteRetryPolicy<TripComparatorMqController> infiniteRetryPolicy,
        IBackOffRetryPolicy<TripComparatorMqController> backOffRetryPolicy)
        {
            this._logger = logger;
            this._iBusInfoProvider = _iBusInfoProvider;
            this._routeTimeProvider = routeTimeProvider;
            this._tripComparatorMq = tripComparatorMq;
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
            //_logger.LogInformation("Service is alive");

            return Ok("IsAlive");
        }

        [HttpGet]
        [ActionName("optimalBuses")]
        public async Task<ActionResult<string>> OptimalBuses(string fromLatitudeLongitude, string toLatitudeLongitude, string host, string port)
        {
            _logger.LogInformation($"Reexecution de optimalBuses avec les parametres fromLatitudeLongitude: {fromLatitudeLongitude} et toLatitudeLongitude: {toLatitudeLongitude} au port {port} et au host {host}");

            DBUtils.Db.StringSet("restart", "1");
            await _iBusInfoProvider.GetBestBus(fromLatitudeLongitude, toLatitudeLongitude);
            await _tripComparatorMq.NewConsume();

            return Ok("OptimalBuses reexecute");
        }

        [HttpGet]
        [ActionName("beginTracking")]
        public async Task<ActionResult<string>> BeginTracking(string parametres)
        {
            _logger.LogInformation($"Reexecution de beginTracking avec les parametres: {parametres}");

            DBUtils.Db.StringSet("restart", "1");

            RideDto? stmBus = JsonConvert.DeserializeObject<RideDto>(parametres);

            await _iBusInfoProvider.BeginTracking(stmBus);
            await _tripComparatorMq.NewConsume();

            return Ok("BeginTracking reexecute");
        }

        [HttpGet]
        [ActionName("getTrackingUpdate")]
        public async Task<ActionResult<string>> GetTrackingUpdate()
        {
            _logger.LogInformation($"Reexecution de getTrackingUpdate");
            DBUtils.Db.StringSet("restart", "1");

            if (DBUtils.Db.KeyExists("jsonWriter"))
            {
                var value = DBUtils.Db.StringGet("jsonWriter");
                // Vérifier que la valeur n'est pas vide ou null
                if (!string.IsNullOrEmpty(value))
                {
                    ChannelWriter<IBusPositionUpdated>? test = JsonConvert.DeserializeObject<ChannelWriter<IBusPositionUpdated>>(value!);
                    _ = _infiniteRetryPolicy.ExecuteAsync(async () => await _compareTimes.PollTrackingUpdate(test!));
                }
            }

            if (DBUtils.Db.KeyExists("jsonReader"))
            {
                var value = DBUtils.Db.StringGet("jsonReader");
                // Vérifier que la valeur n'est pas vide ou null
                if (!string.IsNullOrEmpty(value))
                {
                    ChannelReader<IBusPositionUpdated>? test = JsonConvert.DeserializeObject<ChannelReader<IBusPositionUpdated>>(value!);
                    _ = _backOffRetryPolicy.ExecuteAsync(async () => await _compareTimes.WriteToStream(test!));
                }
            }

            //await _iBusInfoProvider.GetTrackingUpdate();
            //await _tripComparatorMq.NewConsume();
            return Ok("GetTrackingUpdate reexecute");
        }

        [HttpGet]
        [ActionName("routingget")]
        public async Task<ActionResult<string>> RoutingGet(string fromLatitudeLongitude, string toLatitudeLongitude, string host, string port)
        {
            _logger.LogInformation($"Reexecution de optimalBuses avec les parametres fromLatitudeLongitude: {fromLatitudeLongitude} et toLatitudeLongitude: {toLatitudeLongitude} au port {port} et au host {host}");

            DBUtils.Db.StringSet("restart", "1");
            await _routeTimeProvider.GetTravelTimeInSeconds(fromLatitudeLongitude, toLatitudeLongitude);
            await _tripComparatorMq.NewConsume();

            return Ok("OptimalBuses reexecute");
        }
    }
}

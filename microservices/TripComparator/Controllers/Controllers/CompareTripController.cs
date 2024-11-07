using Application.Interfaces.Policies;
using Application.Usecases;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Annotations;
using StackExchange.Redis;

namespace Controllers.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class CompareTripController : ControllerBase
    {
        private readonly ILogger<CompareTripController> _logger;
        private readonly CompareTimes _compareTimes;
        private readonly Ping __ping;
        private readonly IInfiniteRetryPolicy<TripComparatorMqController> _infiniteRetryPolicy;
        private readonly IBackOffRetryPolicy<TripComparatorMqController> _backOffRetryPolicy;

        public CompareTripController(
            ILogger<CompareTripController> logger,
            CompareTimes compareTimes,
            IInfiniteRetryPolicy<TripComparatorMqController> infiniteRetryPolicy,
            IBackOffRetryPolicy<TripComparatorMqController> backOffRetryPolicy)
        {
            _logger = logger;
            _compareTimes = compareTimes;
            _infiniteRetryPolicy = infiniteRetryPolicy;
            _backOffRetryPolicy = backOffRetryPolicy;
        }

        [HttpPost]
        [SwaggerOperation("This endpoint is for you to manually test your system (without UI)")]
        public async Task<IActionResult> Post(string startingCoordinates, string destinationCoordinates)
        {
            _logger.LogInformation($"Comparing trip duration from {startingCoordinates} to {destinationCoordinates}");

            var producer = await _infiniteRetryPolicy.ExecuteAsync(async () => await _compareTimes.BeginComparingBusAndCarTime(
                RemoveWhiteSpaces(startingCoordinates),
            RemoveWhiteSpaces(destinationCoordinates)));

            _ = _infiniteRetryPolicy.ExecuteAsync(async () => await _compareTimes.PollTrackingUpdate(producer!.Writer));

            _ = _backOffRetryPolicy.ExecuteAsync(async () => await _compareTimes.WriteToStream(producer.Reader));

            var redis = ConnectionMultiplexer.Connect("redis:6379");
            var db = redis.GetDatabase();

            string key = "coordonnees";

            // Écrire des données dans Redis
            db.StringSet(key, $"Starting Coordinates: {startingCoordinates}, Destination Coordinates: {destinationCoordinates}");

            string value = db.StringGet(key);

            _logger.LogInformation($"Les coordonnees: {value}");

            return Ok();

            string RemoveWhiteSpaces(string s) => s.Replace(" ", "");
        }

        [HttpGet]
        [SwaggerOperation("This endpoint is for you to manually test your system (without UI)")]
        public async Task<IActionResult> Get()
        {
            _logger.LogInformation($"Ping/echo");

            var producer = await _infiniteRetryPolicy.ExecuteAsync(async () => await __ping.ping());

            return Ok(producer);
        }

    }
}
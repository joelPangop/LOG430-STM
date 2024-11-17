using Application.Usecases;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StackExchange.Redis;

namespace RouteTimeProvider.Controllers
{
    [EnableCors("AllowOrigin")]
    [ApiController]
    [Route("[controller]/[action]")]
    public class RouteTimeController : ControllerBase
    {
        private readonly CarTravel _carTravel;
        private readonly ILogger<RouteTimeController> _logger;

        public RouteTimeController(CarTravel carTravel, ILogger<RouteTimeController> logger)
        {
            _carTravel = carTravel;
            _logger = logger;
        }

        [HttpGet]
        [ActionName(nameof(Get))]
        [EnableRateLimiting("fixed")]
        public async Task<ActionResult<int>> Get(string startingCoordinates, string destinationCoordinates)
        {
            _logger.LogInformation($"Fetching car travel time from {startingCoordinates} to {destinationCoordinates}");

            var travelTime = await _carTravel.GetTravelTimeInSeconds(startingCoordinates, destinationCoordinates);
            var redis = ConnectionMultiplexer.Connect("redis:6379"); // Remplace "localhost" par l'adresse de ton serveur Redis
            var db = redis.GetDatabase();

            string key = "Coordonnees voiture";

            // �crire des donn�es dans Redis
            db?.StringSet(key, $"Starting Coordinates: {startingCoordinates}, Destination Coordinates: {destinationCoordinates}");

            string value = db.StringGet(key);

            _logger.LogInformation($"Les coordonnees de la voiture: {value}");

            return Ok(travelTime);
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
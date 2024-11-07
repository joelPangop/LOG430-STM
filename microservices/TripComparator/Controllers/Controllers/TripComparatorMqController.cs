using Application.Interfaces.Policies;
using Application.Usecases;
using MassTransit;
using Microsoft.Extensions.Logging;
using MqContracts;

namespace Controllers.Controllers;

public class TripComparatorMqController : IConsumer<CoordinateMessage>
{
    private readonly CompareTimes _compareTimes;
    private readonly IInfiniteRetryPolicy<TripComparatorMqController> _infiniteRetryPolicy;
    private readonly IBackOffRetryPolicy<TripComparatorMqController> _backOffRetryPolicy;

    private readonly ILogger<TripComparatorMqController> _logger;

    public TripComparatorMqController(
        ILogger<TripComparatorMqController> logger,
        CompareTimes compareTimes,
        IInfiniteRetryPolicy<TripComparatorMqController> infiniteRetryPolicy,
        IBackOffRetryPolicy<TripComparatorMqController> backOffRetryPolicy)
    {
        _logger = logger;
        _compareTimes = compareTimes;
        _infiniteRetryPolicy = infiniteRetryPolicy;
        _backOffRetryPolicy = backOffRetryPolicy;
    }

    public async Task Consume(ConsumeContext<CoordinateMessage> context)
    {
        _logger.LogInformation("Execution de la fonction Consume");
        _logger.LogInformation($"Je suis le leader ? { DBUtils.IsLeader}");
        if (DBUtils.IsLeader)
        {
            string startingCoordinates = context.Message.StartingCoordinates, destinationCoordinates = context.Message.DestinationCoordinates;

            _logger.LogInformation($"Comparing trip duration from {startingCoordinates} to {destinationCoordinates}");

            var producer = await _infiniteRetryPolicy.ExecuteAsync(async () => await _compareTimes.BeginComparingBusAndCarTime(
                RemoveWhiteSpaces(startingCoordinates),
                RemoveWhiteSpaces(destinationCoordinates)));

            _ = _infiniteRetryPolicy.ExecuteAsync(async () => await _compareTimes.PollTrackingUpdate(producer!.Writer));

            _ = _backOffRetryPolicy.ExecuteAsync(async () => await _compareTimes.WriteToStream(producer.Reader));

            string key = "coordonnees";
            string key1 = "Request";

            // Écrire des données dans Redis
            DBUtils.Db?.StringSet(key, $"Starting Coordinates: {startingCoordinates}, Destination Coordinates: {destinationCoordinates}");
            DBUtils.Db?.StringSet(key1, $"Consume");

            string value = DBUtils.Db.StringGet(key);
            string value1 = DBUtils.Db.StringGet(key1);

            _logger.LogInformation($"Les coordonnees: {value}");
            _logger.LogInformation($"Request: {value1}");

            string RemoveWhiteSpaces(string s) => s.Replace(" ", "");
        }
    }
}
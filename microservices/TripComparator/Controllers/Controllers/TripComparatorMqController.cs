using Application.Interfaces.Policies;
using Application.Usecases;
using MassTransit;
using Microsoft.Extensions.Logging;
using MqContracts;
using Newtonsoft.Json;

namespace Controllers.Controllers;

public class TripComparatorMqController : IConsumer<CoordinateMessage>
{
    private readonly CompareTimes _compareTimes;
    private readonly IInfiniteRetryPolicy<TripComparatorMqController> _infiniteRetryPolicy;
    private readonly IBackOffRetryPolicy<TripComparatorMqController> _backOffRetryPolicy;
    public string _startingCoordinates { get; set; } = string.Empty;
    public string _destinationCoordinates { get; set; } = string.Empty;

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
            string startingCoordinates = string.IsNullOrEmpty(_startingCoordinates) ? context.Message.StartingCoordinates: _startingCoordinates, destinationCoordinates = string.IsNullOrEmpty(_destinationCoordinates) ? context.Message.DestinationCoordinates : _destinationCoordinates;

            string key1 = "Request";
            string keyCoordonnees = "Coordonnees";

            var data = new Dictionary<string, string>
            {
                { "StartingCoordinates", startingCoordinates },
                { "DestinationCoordinates", destinationCoordinates }
            };

            // Sérialiser en JSON
            string json = JsonConvert.SerializeObject(data);

            Console.WriteLine($"JSON créé : {json}");

            // Écrire des données dans Redis
            DBUtils.Db?.StringSetAsync(keyCoordonnees, json);
            DBUtils.Db?.StringSet(key1, $"Consume");

            _logger.LogInformation($"Comparing trip duration from {startingCoordinates} to {destinationCoordinates}");
       
            var producer = await _infiniteRetryPolicy.ExecuteAsync(async () => await _compareTimes.BeginComparingBusAndCarTime(
                RemoveWhiteSpaces(startingCoordinates),
                RemoveWhiteSpaces(destinationCoordinates)));

            _ = _infiniteRetryPolicy.ExecuteAsync(async () => await _compareTimes.PollTrackingUpdate(producer!.Writer));
     
            _ = _backOffRetryPolicy.ExecuteAsync(async () => await _compareTimes.WriteToStream(producer.Reader));

            string RemoveWhiteSpaces(string s) => s.Replace(" ", "");
        }
    }
}
using Application.Interfaces;
using Application.Interfaces.Policies;
using Controllers.Controllers;
using MassTransit.Internals.GraphValidation;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RestSharp;
using ServiceMeshHelper;
using ServiceMeshHelper.BusinessObjects;
using ServiceMeshHelper.BusinessObjects.InterServiceRequests;
using ServiceMeshHelper.Controllers;
using System.Threading.Channels;
using static MassTransit.ValidationResultExtensions;

namespace Infrastructure.Clients;

public class RouteTimeProviderClient : IRouteTimeProvider
{
    private readonly IInfiniteRetryPolicy<RouteTimeProviderClient> _infiniteRetry;
    private static readonly ILogger _logger;

    public RouteTimeProviderClient(IInfiniteRetryPolicy<RouteTimeProviderClient> infiniteRetry)
    {
        _infiniteRetry = infiniteRetry;
    }

    public Task<int> GetTravelTimeInSeconds(string startingCoordinates, string destinationCoordinates)
    {
        string keyRequest = "Request";
        string keyService = "Service";
        string keyCoordonnees = "Coordonnees";

        var data = new Dictionary<string, string>
        {
            { "StartingCoordinates", startingCoordinates },
            { "DestinationCoordinates", destinationCoordinates }
        };

        //Sérialiser en JSON
        string json = JsonConvert.SerializeObject(data);

        Console.WriteLine($"JSON créé : {json}");
        DBUtils.Db.StringSet(keyRequest, "RouteTime/Get");
        DBUtils.Db.StringSet(keyService, "TripComparator");
        //DBUtils.Db.StringSetAsync(keyCoordonnees, json);

        string endPoint = $"{DBUtils.Db.StringGet("STM Leader")}/RouteTime/Get";
        if (string.IsNullOrEmpty(endPoint))
        {
            endPoint = "RouteTime/Get";
        }
        Console.WriteLine($"Nouveau endpoint: {endPoint}");
        return _infiniteRetry.ExecuteAsync(async () =>
        {
            var res = await RestController.Get(new GetRoutingRequest()
            {
                TargetService = "RouteTimeProvider",
                Endpoint = $"RouteTime/Get",
                Params = new List<NameValue>()
                {
                    new()
                    {
                        Name = "startingCoordinates",
                        Value = startingCoordinates
                    },
                    new()
                    {
                        Name = "destinationCoordinates",
                        Value = destinationCoordinates
                    },
                },
                Mode = LoadBalancingMode.Broadcast
            });

            var times = new List<int>();

            await foreach (var result in res!.ReadAllAsync())
            {
                times.Add(JsonConvert.DeserializeObject<int>(result.Content));
            }

            return (int)times.Average();
        });
    }

    public Task<string?> getIsAlive()
    {
        return _infiniteRetry.ExecuteAsync(static async () =>
        {
            var restClient = new RestClient("http://RouteTimeProvider");
            var restRequest = new RestRequest("RouteTime/alive");
            var test = (await restClient.ExecuteGetAsync<string?>(restRequest)).Data;
            _logger.LogInformation(test);

            return (await restClient.ExecuteGetAsync<string?>(restRequest)).Data;
        });
    }
    
}

//Exemple of how to use Restsharp for a simple request to a service (without the pros (and cons) of using the NodeController)

//var restClient = new RestClient("http://RouteTimeProvider");
//var restRequest = new RestRequest("RouteTime/Get");

//restRequest.AddQueryParameter("startingCoordinates", startingCoordinates);
//restRequest.AddQueryParameter("destinationCoordinates", destinationCoordinates);

//return (await restClient.ExecuteGetAsync<int>(restRequest)).Data;
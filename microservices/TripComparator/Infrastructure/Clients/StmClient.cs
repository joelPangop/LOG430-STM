﻿using System.Net;
using Application.BusinessObjects;
using Application.DTO;
using Application.Interfaces;
using Application.Interfaces.Policies;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RestSharp;
using ServiceMeshHelper;
using ServiceMeshHelper.BusinessObjects;
using ServiceMeshHelper.BusinessObjects.InterServiceRequests;
using ServiceMeshHelper.Controllers;
using Controllers.Controllers;

namespace Infrastructure.Clients;

public class StmClient : IBusInfoProvider
{
    readonly ILogger _logger;
    private readonly IBackOffRetryPolicy<StmClient> _backOffRetry;
    private readonly IInfiniteRetryPolicy<StmClient> _infiniteRetry;

    public StmClient(ILogger<StmClient> logger, IBackOffRetryPolicy<StmClient> backOffRetry, IInfiniteRetryPolicy<StmClient> infiniteRetry)
    {
        _logger = logger;
        _backOffRetry = backOffRetry;
        _infiniteRetry = infiniteRetry;
    }

    public Task<RideDto> GetBestBus(string startingCoordinates, string destinationCoordinates)
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
        DBUtils.Db.StringSet(keyRequest, "Finder/OptimalBuses");
        DBUtils.Db.StringSet(keyService, "TripComparator");
        //DBUtils.Db.StringSetAsync(keyCoordonnees, json);

        return _infiniteRetry.ExecuteAsync(async () =>
        {
            var channel = await RestController.Get(new GetRoutingRequest()
            {
                TargetService = "STM",
                Endpoint = $"Finder/OptimalBuses",
                Params = new List<NameValue>()
                {
                    new()
                    {
                        Name = "fromLatitudeLongitude",
                        Value = startingCoordinates
                    },
                    new()
                    {
                        Name = "toLatitudeLongitude",
                        Value = destinationCoordinates
                    },
                },
                Mode = LoadBalancingMode.RoundRobin
            });

            RideDto? busDto = null;

            await foreach (var res in channel.ReadAllAsync())
            {
                if (res.Content is null) throw new Exception("Bus request content was null");

                busDto = JsonConvert.DeserializeObject<RideDto>(res.Content);

                break;
            }

            if (busDto is null) throw new Exception("Bus Dto was null");

            return busDto;
        });
    }

    public Task BeginTracking(RideDto stmBus)
    {
        string keyRequest = "Request";
        string keyService = "Service";
        string keyRide = "stmBus";

        string json = JsonConvert.SerializeObject(stmBus);

        DBUtils.Db.StringSet(keyRequest, "Track/BeginTracking");
        DBUtils.Db.StringSet(keyService, "TripComparator");
        DBUtils.Db.StringSetAsync(keyRide, json);

        return _infiniteRetry.ExecuteAsync(async () =>
        {
            _ = await RestController.Post(new PostRoutingRequest<RideDto>()
            {
                TargetService = "STM",
                Endpoint = $"Track/BeginTracking",
                Payload = stmBus,
                Mode = LoadBalancingMode.RoundRobin
            });
        });
    }

    public Task<IBusTracking?> GetTrackingUpdate()
    {
        string keyRequest = "Request";
        string keyService = "Service";

        DBUtils.Db.StringSet(keyRequest, "Track/GetTrackingUpdate");
        DBUtils.Db.StringSet(keyService, "TripComparator");

        return _infiniteRetry.ExecuteAsync<IBusTracking?>(async () =>
        {
            var channel = await RestController.Get(new GetRoutingRequest()
            {
                TargetService = "STM",
                Endpoint = $"Track/GetTrackingUpdate",
                Params = new List<NameValue>(),
                Mode = LoadBalancingMode.RoundRobin
            });

            RestResponse? data = null;

            await foreach (var res in channel.ReadAllAsync())
            {
                data = res;

                break;
            }

            if (data is null || !data.IsSuccessStatusCode || data.StatusCode.Equals(HttpStatusCode.NoContent)) return null;

            var busTracking = JsonConvert.DeserializeObject<BusTracking>(data.Content!);

            return busTracking;

        });
    }
}
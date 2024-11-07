using Docker.DotNet;
using Docker.DotNet.Models;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Newtonsoft.Json;
using ServiceMeshHelper;
using ServiceMeshHelper.BusinessObjects;
using ServiceMeshHelper.BusinessObjects.InterServiceRequests;
using ServiceMeshHelper.Controllers;
using System;

class Program
{
    static async Task Main(string[] args)
    {
        Thread.Sleep(5000);
        var builder = WebApplication.CreateBuilder(args as string[]);
        await GetContainerPorts("TripComparator");
        //await GetContainerPorts("STM");

        var app = builder.Build();

        await app.RunAsync();
    }

    private static async Task<string?> GetContainerPorts(string containerName)
    {
        var routingData = RestController.GetAddress(containerName, LoadBalancingMode.RoundRobin).Result.First();
        var endpoint = $"http://{routingData.Host}:{routingData.Port}/LoadBalancing/leader";

        var res = await RestController.Get(new GetRoutingRequest()
        {
            TargetService = containerName,
            Endpoint = endpoint,
            Mode = LoadBalancingMode.RoundRobin
        });

        var restResponse = await res!.ReadAsync();
        if (restResponse?.Content == null)
        {
            return null; 
        }
        Console.WriteLine($"{containerName} Leader ? : {routingData.Host}:{routingData.Port} : {JsonConvert.DeserializeObject<string?>(restResponse.Content)}");
       
        //End point to ping the service if it is stiil alive
        string pingEndPoint = $"http://{routingData.Host}:{routingData.Port}/LoadBalancing/alive";
        Thread thread = new Thread(() => PingLoop(containerName, pingEndPoint));
        thread.Start();
        return JsonConvert.DeserializeObject<string?>(restResponse.Content);
    }

    static string GetRandomKey(IDictionary<string, IList<PortBinding>> dictionary, Random random)
    {
        var keys = new List<string>(dictionary.Keys);
        int randomIndex = random.Next(keys.Count);
        return keys[randomIndex];
    }

    private static async Task<string?> PingEcho(string containerName, string endpoint)
    {
        var res = await RestController.Get(new GetRoutingRequest()
        {
            TargetService = containerName,
            Endpoint = endpoint,
            Mode = LoadBalancingMode.RoundRobin
        });

        var restResponse = await res!.ReadAsync();
        if (restResponse?.Content == null)
        {
            return null;
        }
        Console.WriteLine($"Is alive? : {JsonConvert.DeserializeObject<string?>(restResponse.Content)}");
        return JsonConvert.DeserializeObject<string?>(restResponse.Content);
    }

    private static async void PingLoop(string containerName, string endpoint)
    {
        while (await PingEcho(containerName, endpoint) == "IsAlive")
        {
            await PingEcho(containerName, endpoint);
        }
    }
}

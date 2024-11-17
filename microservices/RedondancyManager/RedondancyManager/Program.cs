using Docker.DotNet;
using Docker.DotNet.Models;
using MassTransit;
using MassTransit.Transports;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Newtonsoft.Json;
using RedondancyManager;
using RestSharp;
using ServiceMeshHelper;
using ServiceMeshHelper.BusinessObjects;
using ServiceMeshHelper.BusinessObjects.InterServiceRequests;
using ServiceMeshHelper.Controllers;
using StackExchange.Redis;
using System;
using System.Text;
using System.Threading.Channels;
using RoutingData = RedondancyManager.RoutingData;

class Program
{
    private static readonly HttpClient client = new HttpClient();
    private static RoutingData routeData = new RoutingData();
    private static string stmEndpoint = "";
    private static string tcEndpoint = "";

    static async Task Main(string[] args)
    {
        //Thread.Sleep(5000);
        var builder = WebApplication.CreateBuilder(args as string[]);
        var dockerClient = new DockerClientConfiguration(new Uri("unix:///var/run/docker.sock")).CreateClient();

        await GetContainerPorts(dockerClient, "STM");

        Thread.Sleep(2000);

        await GetContainerPorts(dockerClient, "TripComparator");

        var app = builder.Build();

        await app.RunAsync();
    }

    private static async Task<string?> GetContainerPorts(DockerClient client, string containerName)
    {
        int retryCount = 10; // Nombre de tentatives de réessai
        int delay = 2000;   // Délai entre chaque tentative (en millisecondes)

        var routingData = RestController.GetAddress(containerName, LoadBalancingMode.RoundRobin).Result.First();
        var allResults = RestController.GetAddress(containerName, LoadBalancingMode.Broadcast).Result;
        var allResultsList = allResults.ToList();

        Console.WriteLine($"Liste des conteneurs {containerName}");
        // Parcours ou utilisation des résultats
        foreach (var result in allResults)
        {
            if(result.Host.Equals(routingData.Host) && !result.Port.Equals(routingData.Port)) {
                preventOtherContainers(containerName, result.Host, result.Port);
            }
            Console.WriteLine($"{containerName} Host: {result.Host}, Port: {result.Port} ");
        }
        routeData.Host = routingData.Host;
        routeData.Port = routingData.Port;
        string response = "";

        for (int i = 0; i < retryCount; i++)
        {
            try
            {

                var endpoint = $"http://{routeData.Host}:{routeData.Port}/LoadBalancing/leader";

                Console.WriteLine($"endpoint: {endpoint}");

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
                Console.WriteLine($"{containerName} Leader ? : {routeData.Host}:{routeData.Port} : {JsonConvert.DeserializeObject<string?>(restResponse.Content)}");
                var redis = ConnectionMultiplexer.Connect("redis:6379"); // Remplace "localhost" par l'adresse de ton serveur Redis
                var db = redis.GetDatabase();

                // Lancer le PingLoop pour vérifier la disponibilité continue
                string pingEndPoint = $"http://{routingData.Host}:{routingData.Port}/LoadBalancing/alive";

                if (containerName == "STM")
                {
                    string? key = "STM Leader";
                    stmEndpoint = pingEndPoint;
                    pingThread(client, containerName, stmEndpoint);
                    db.StringSet(key, $"http://{routeData.Host}:{routeData.Port}");
                    Console.WriteLine($"STM Leader endpoint: http://{routeData.Host}:{routeData.Port}");
                } else
                {
                    tcEndpoint = pingEndPoint;
                    pingThread(client, containerName, tcEndpoint);
                }

                //Thread thread = new Thread(async () => await PingLoop(client, containerName, pingEndPoint));
                //thread.Start();

                return JsonConvert.DeserializeObject<string?>(restResponse.Content);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Tentative {i + 1} échouée : {ex.Message}");
                if (i == retryCount - 1)
                {
                    // Arrête les tentatives après avoir atteint le nombre maximal de réessais
                    Console.WriteLine("Impossible de se connecter au service après plusieurs tentatives.");
                    return null;
                }
                await Task.Delay(delay); // Attendre avant la prochaine tentative
            }
        }

        return null;
    }

    private static async Task<string?> preventOtherContainers(string containerName, string host, string port)
    {
        int retryCount = 10; // Nombre de tentatives de réessai
        int delay = 2000;   // Délai entre chaque tentative (en millisecondes)

        for (int i = 0; i < retryCount; i++)
        {
            try
            {

                var endpoint = $"http://{host}:{port}/LoadBalancing/notleader";

                var jsonContent = new StringContent(
                    JsonConvert.SerializeObject("0"),
                    Encoding.UTF8,
                    "application/json"
                );

                Console.WriteLine($"Not leader endpoint: {endpoint}");

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

                return JsonConvert.DeserializeObject<string?>(restResponse.Content);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Tentative {i + 1} échouée : {ex.Message}");
                if (i == retryCount - 1)
                {
                    // Arrête les tentatives après avoir atteint le nombre maximal de réessais
                    Console.WriteLine("Impossible de se connecter au service après plusieurs tentatives.");
                    return null;
                }
                await Task.Delay(delay); // Attendre avant la prochaine tentative
            }
        }

        return null;
    }
    private static void pingThread(DockerClient client, string containerName, string pingEndPoint)
    {
        Thread thread = new Thread(async () => await PingLoop(client, containerName, pingEndPoint));
        thread.Start();
    }

    static string GetRandomKey(IDictionary<string, IList<PortBinding>> dictionary, Random random)
    {
        var keys = new List<string>(dictionary.Keys);
        int randomIndex = random.Next(keys.Count);
        return keys[randomIndex];
    }

    private static async Task PingLoop(DockerClient client, string containerName, string endpoint)
    {
        try
        {
            while (await PingEcho(client, containerName, endpoint) == "IsAlive")
            {
                await Task.Delay(100); // Optionnel : Ajouter un délai pour éviter les appels excessifs
            }
            Console.WriteLine($"{containerName}: {endpoint} est mort");
            Console.WriteLine("Reexecuter les requetes en cours");

            await ExecuteProcessesAsync(client, containerName);
            Console.WriteLine("Requetes en cours reeexecutees");
        }
        catch (ChannelClosedException ex)
        {
            Console.WriteLine($"Channel closed exception: {ex.Message}");
            // Gérer la fermeture du canal, si nécessaire, ou effectuer une autre action
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error in PingLoop: {ex.Message}");
        }
    }

    private static async Task<string?> PingEcho(DockerClient client, string containerName, string endpoint)
    {
        try
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
                Console.WriteLine("Is not alive.");
                Console.WriteLine("Search new leader.");
                await GetContainerPorts(client, containerName);
                return null;
            }

            Console.WriteLine($"{containerName} is alive?: {JsonConvert.DeserializeObject<string?>(restResponse.Content)}");
            return JsonConvert.DeserializeObject<string?>(restResponse.Content);
        }
        catch (ChannelClosedException ex)
        {
            Console.WriteLine($"Channel closed: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error in PingEcho: {ex.Message}");
            return null;
        }
    }

    public static async Task RestartProcess(string containerName)
    {
        var redis = ConnectionMultiplexer.Connect("redis:6379"); // Remplace "localhost" par l'adresse de ton serveur Redis
        var db = redis.GetDatabase();

        string? keyCoordonnees = "Coordonnees";
        string? keyRequest = "Request";
        string? keyService = "Service";
        string? keyRide = "stmBus";

        // Lire les données de Redis
        string? valueCoordonnees = db.StringGet(keyCoordonnees);
        string? valueRequest = db.StringGet(keyRequest);
        string? valueService = db.StringGet(keyService);
        string? valueRide = db.StringGet(keyRide);

        if (valueCoordonnees != null)
        {
            Console.WriteLine($"Les coordonnees: {valueCoordonnees}");
        }
        else
        {
            Console.WriteLine("Les coordonnees sont nulles ou inexistantes dans Redis.");
        }

        if (valueRequest != null)
        {
            Console.WriteLine($"Le service: {valueService}");
            Console.WriteLine($"La Requete: {valueRequest}");
            //if(valueService.Equals("Consume") && valueCoordonnees != null)
            //{
            //   await Consume(valueCoordonnees, containerName);
            //} else 
            if (valueRequest.Equals("Finder/OptimalBuses"))
            {
                await OptimalBuses(valueCoordonnees, containerName);
            }
            else if (valueRequest.Equals("Track/BeginTracking"))
            {
                await BeginTracking(valueRide, containerName);
            }
            else if (valueRequest.Equals("RouteTime/Get"))
            {
                await RoutingGet(valueCoordonnees, containerName);
            }
            else if (valueRequest.Equals("Track/GetTrackingUpdate"))
            {
                await GetTrackingUpdate(containerName);
            }
        }
        else
        {
            Console.WriteLine("La Requete est nulle ou inexistante dans Redis.");
        }

    }

    public static async Task ExecuteProcessesAsync(DockerClient client, string containerName)
    {
        await GetContainerPorts(client, containerName);
        if (containerName.Equals("TripComparator"))
            await RestartProcess(containerName);
    }

    private static async Task<string?> Consume(string valueCoordonnees, string containerName)
    {
        var routingData = RestController.GetAddress(containerName, LoadBalancingMode.RoundRobin).Result.First();
        var endpoint = $"http://{routingData.Host}:{routingData.Port}/LoadBalancing/Consume";
        var busCoordinates = JsonConvert.DeserializeObject<Coordinates>(valueCoordonnees);

        var res = await RestController.Get(new GetRoutingRequest()
        {
            TargetService = containerName,
            Endpoint = endpoint,
            Params = new List<NameValue>()
                {
                    new()
                    {
                        Name = "StartingCoordinates",
                        Value = busCoordinates.StartingCoordinates
                    },
                    new()
                    {
                        Name = "DestinationCoordinates",
                        Value = busCoordinates.DestinationCoordinates
                    },
                },
            Mode = LoadBalancingMode.RoundRobin
        });

        var restResponse = await res!.ReadAsync();
        if (restResponse?.Content == null)
        {
            return null;
        }
        Console.WriteLine($"{containerName} Leader ? : {routingData.Host}:{routingData.Port} : {JsonConvert.DeserializeObject<string?>(restResponse.Content)}");
        return restResponse?.Content;
    }

    private static async Task<string?> OptimalBuses(string valueCoordonnees, string containerName)
    {
        //var routingData = RestController.GetAddress(containerName, LoadBalancingMode.RoundRobin).Result.First();
        var endpoint = $"http://{routeData.Host}:{routeData.Port}/LoadBalancing/optimalBuses";

        Console.WriteLine($"New endpoint: {endpoint}");

        var busCoordinates = JsonConvert.DeserializeObject<Coordinates>(valueCoordonnees);

        var res = await RestController.Get(new GetRoutingRequest()
        {
            TargetService = containerName,
            Endpoint = endpoint,
            Params = new List<NameValue>()
                {
                    new()
                    {
                        Name = "fromLatitudeLongitude",
                        Value = busCoordinates.StartingCoordinates
                    },
                    new()
                    {
                        Name = "toLatitudeLongitude",
                        Value = busCoordinates.DestinationCoordinates
                    },
                     new()
                    {
                        Name = "host",
                        Value = routeData.Host
                    },
                      new()
                    {
                        Name = "port",
                        Value = routeData.Port
                    }
                },
            Mode = LoadBalancingMode.RoundRobin
        });

        var restResponse = await res!.ReadAsync();
        if (restResponse?.Content == null)
        {
            return null;
        }
        Console.WriteLine($"{containerName} Leader ? : {routeData.Host}:{routeData.Port} : {JsonConvert.DeserializeObject<string?>(restResponse.Content)}");
        return restResponse?.Content;
    }

    private static async Task<string?> BeginTracking(string valueCoordonnees, string containerName)
    {
        //var routingData = RestController.GetAddress(containerName, LoadBalancingMode.RoundRobin).Result.First();
        var endpoint = $"http://{routeData.Host}:{routeData.Port}/LoadBalancing/beginTracking";

        Console.WriteLine($"New endpoint: {endpoint}");

        var res = await RestController.Get(new GetRoutingRequest()
        {
            TargetService = containerName,
            Endpoint = endpoint,
            Params = new List<NameValue>()
                {
                    new()
                    {
                        Name = "stmBus",
                        Value = valueCoordonnees
                    },
                     new()
                    {
                        Name = "host",
                        Value = routeData.Host
                    },
                      new()
                    {
                        Name = "port",
                        Value = routeData.Port
                    }
                },
            Mode = LoadBalancingMode.RoundRobin
        });

        var restResponse = await res!.ReadAsync();
        if (restResponse?.Content == null)
        {
            return null;
        }
        Console.WriteLine($"{containerName} Leader ? : {routeData.Host}:{routeData.Port} : {JsonConvert.DeserializeObject<string?>(restResponse.Content)}");
        return restResponse?.Content;
    }

    private static async Task<string?> GetTrackingUpdate(string containerName)
    {
        //var routingData = RestController.GetAddress(containerName, LoadBalancingMode.RoundRobin).Result.First();
        var endpoint = $"http://{routeData.Host}:{routeData.Port}/LoadBalancing/getTrackingUpdate";

        Console.WriteLine($"New endpoint: {endpoint}");

        var res = await RestController.Get(new GetRoutingRequest()
        {
            TargetService = containerName,
            Endpoint = endpoint,
            Params = new List<NameValue>()
                {
                     new()
                    {
                        Name = "host",
                        Value = routeData.Host
                    },
                      new()
                    {
                        Name = "port",
                        Value = routeData.Port
                    }
                },
            Mode = LoadBalancingMode.RoundRobin
        });

        var restResponse = await res!.ReadAsync();
        if (restResponse?.Content == null)
        {
            return null;
        }
        Console.WriteLine($"{containerName} Leader ? : {routeData.Host}:{routeData.Port} : {JsonConvert.DeserializeObject<string?>(restResponse.Content)}");
        return restResponse?.Content;
    }

    private static async Task<string?> RoutingGet(string valueCoordonnees, string containerName)
    {
        //var routingData = RestController.GetAddress(containerName, LoadBalancingMode.RoundRobin).Result.First();
        var endpoint = $"http://{routeData.Host}:{routeData.Port}/LoadBalancing/routingget";

        Console.WriteLine($"New endpoint: {endpoint}");

        var busCoordinates = JsonConvert.DeserializeObject<Coordinates>(valueCoordonnees);

        var res = await RestController.Get(new GetRoutingRequest()
        {
            TargetService = containerName,
            Endpoint = endpoint,
            Params = new List<NameValue>()
                {
                    new()
                    {
                        Name = "fromLatitudeLongitude",
                        Value = busCoordinates.StartingCoordinates
                    },
                    new()
                    {
                        Name = "toLatitudeLongitude",
                        Value = busCoordinates.DestinationCoordinates
                    },
                     new()
                    {
                        Name = "host",
                        Value = routeData.Host
                    },
                      new()
                    {
                        Name = "port",
                        Value = routeData.Port
                    }
                },
            Mode = LoadBalancingMode.RoundRobin
        });

        var restResponse = await res!.ReadAsync();
        if (restResponse?.Content == null)
        {
            return null;
        }
        Console.WriteLine($"{containerName} Leader ? : {routeData.Host}:{routeData.Port} : {JsonConvert.DeserializeObject<string?>(restResponse.Content)}");
        return restResponse?.Content;
    }

    //public async Task<string?> GetTravelTimeInSeconds(string valueCoordonnees, string containerName)
    //{
        ////var routingData = RestController.GetAddress(containerName, LoadBalancingMode.RoundRobin).Result.First();
        //var endpoint = $"http://{routeData.Host}:{routeData.Port}/LoadBalancing/optimalBuses";
        //var busCoordinates = JsonConvert.DeserializeObject<Coordinates>(valueCoordonnees);

        //var res = await RestController.Get(new GetRoutingRequest()
        //{
        //    TargetService = containerName,
        //    Endpoint = endpoint,
        //    Params = new List<NameValue>()
        //        {
        //            new()
        //            {
        //                Name = "fromLatitudeLongitude",
        //                Value = busCoordinates.StartingCoordinates
        //            },
        //            new()
        //            {
        //                Name = "toLatitudeLongitude",
        //                Value = busCoordinates.DestinationCoordinates
        //            },
        //        },
        //    Mode = LoadBalancingMode.RoundRobin
        //});

        //var restResponse = await res!.ReadAsync();
        //if (restResponse?.Content == null)
        //{
        //    return null;
        //}
        //Console.WriteLine($"{containerName} Leader ? : {routeData.Host}:{routeData.Port} : {JsonConvert.DeserializeObject<string?>(restResponse.Content)}");
        //return restResponse?.Content;
    //}
}

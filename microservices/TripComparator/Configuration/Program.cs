using Application.Interfaces;
using Application.Interfaces.Policies;
using Application.Usecases;
using Configuration.Policies;
using Controllers.Controllers;
using Infrastructure.Clients;
using MassTransit;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.OpenApi.Models;
using MqContracts;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RestSharp;
using ServiceMeshHelper;
using ServiceMeshHelper.BusinessObjects;
using ServiceMeshHelper.BusinessObjects.InterServiceRequests;
using ServiceMeshHelper.Controllers;
using StackExchange.Redis;
using static MassTransit.ValidationResultExtensions;

namespace Configuration
{
    public class Program
    {
        public static async Task Main(string[] args)
        {

            //Thread.Sleep(30000);
            Thread thread = new Thread(new ThreadStart(PingLoop));
            thread.Start();

            var builder = WebApplication.CreateBuilder(args as string[]);

            ConfigureServices(builder.Services);

            var app = builder.Build();

            app.UseSwagger();

            app.UseSwaggerUI();

            app.UseHttpsRedirection();

            app.UseCors(
                options =>
                {
                    options.AllowAnyOrigin();
                    options.AllowAnyHeader();
                    options.AllowAnyMethod();
                }
            );

            app.UseAuthorization();

            app.MapControllers();

            var redis = ConnectionMultiplexer.Connect("redis:6379"); // Remplace "localhost" par l'adresse de ton serveur Redis
            var db = redis.GetDatabase();

            string key = "donnees";

            // Écrire des données dans Redis
            db.StringSet(key, "Ceci est un exemple de données stockées dans Redis.");
            Console.WriteLine("Données stockées dans Redis.");

            // Lire les données de Redis
            string value = db.StringGet(key);
            Console.WriteLine($"Contenu de Redis : {value}");

            await app.RunAsync();

        }

        private static async void PingLoop()
        {
            while (await PingEcho() == "IsAlive")
            {
                await PingEcho();
            }
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            DetectLeader(services);
            //Thread.Sleep(10000);

            if (DBUtils.IsLeader)
            {
                Console.WriteLine("Je ne suis pas leader et je configure RabbitMQ"); 
            }
            else Console.WriteLine("Je ne suis pas leader donc je ne configure pas RabbitMQ");
            ConfigureMassTransit(services);

            services.AddControllers().PartManager.ApplicationParts.Add(new AssemblyPart(typeof(CompareTripController).Assembly));
            //services.AddControllers().PartManager.ApplicationParts.Add(new AssemblyPart(typeof(ReconnectionController).Assembly));

            services.AddEndpointsApiExplorer();

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "TripComparator", Version = "v1" });
                c.EnableAnnotations();
            });

            services.AddSingleton<IHostInfo, HostInfo>();

            services.AddScoped(typeof(IInfiniteRetryPolicy<>), typeof(InfiniteRetryPolicy<>));

            services.AddScoped(typeof(IBackOffRetryPolicy<>), typeof(BackOffRetryPolicy<>));

            services.AddScoped<CompareTimes>();

            //services.AddScoped<Ping>();

            services.AddScoped<IRouteTimeProvider, RouteTimeProviderClient>();

            services.AddScoped<IDataStreamWriteModel, MassTransitRabbitMqClient>();

            services.AddScoped<IBusInfoProvider, StmClient>();
        }

        private static void ConfigureMassTransit(IServiceCollection services)
        {
            var hostInfo = new HostInfo();
            
            var routingData = RestController.GetAddress(hostInfo.GetMQServiceName(), LoadBalancingMode.RoundRobin).Result.First();

            string uniqueQueueName = $"time_comparison.node_controller-to-any.query.{Guid.NewGuid()}";

            services.AddMassTransit(x =>
            {
                x.AddConsumer<TripComparatorMqController>();

                x.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host($"rabbitmq://{ routingData.Host }:{routingData.Port}", c =>
                    {
                        c.RequestedConnectionTimeout(100);
                        c.Heartbeat(TimeSpan.FromMilliseconds(50));
                        c.PublisherConfirmation = true;
                    });

                    cfg.Message<BusPositionUpdated>(topologyConfigurator => topologyConfigurator.SetEntityName("bus_position_updated"));
                    cfg.Message<CoordinateMessage>(topologyConfigurator => topologyConfigurator.SetEntityName("coordinate_message"));

                    cfg.ReceiveEndpoint(uniqueQueueName, endpoint =>
                    {
                        endpoint.ConfigureConsumeTopology = false;

                        endpoint.Bind<CoordinateMessage>(binding =>
                        {
                            binding.ExchangeType = ExchangeType.Topic;
                            binding.RoutingKey = "trip_comparison.query";
                        });
                        Console.WriteLine("Appel de la fonction ConfigureConsumer");
                        endpoint.ConfigureConsumer<TripComparatorMqController>(context);
                    });

                    if (DBUtils.IsLeader)
                    {
                        Console.WriteLine("Je suis le leader");
                    }
                    else
                    {
                        Console.WriteLine("Je ne suis pas le leader");
                    }
                        cfg.Publish<BusPositionUpdated>(p => p.ExchangeType = ExchangeType.Topic);
                });
            });
        }

        private static async Task<string?> PingEcho()
        {
            var res = await RestController.Get(new GetRoutingRequest()
            {
                TargetService = "RouteTimeProvider",
                Endpoint = $"RouteTime/alive",
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

        public static void DetectLeader(IServiceCollection services)
        {
            services.AddControllers().PartManager.ApplicationParts.Add(new AssemblyPart(typeof(LoadBalancingController).Assembly)); ;

            //callback?.Invoke();
        }
    }
}
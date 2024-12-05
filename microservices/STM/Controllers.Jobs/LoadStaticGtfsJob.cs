using Application.Commands.Cleaning;
using Application.Commands.LoadStaticGtfs;
using Application.Commands.Seedwork;
using Application.EventHandlers.Interfaces;
using Contracts;
using Controllers.Controllers;
using Controllers.Rest;
using Domain.Aggregates.Ride;
using Domain.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Controllers.Jobs;

public class LoadStaticGtfsJob : BackgroundService
{
    private readonly ILogger<LoadStaticGtfsJob> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IDatetimeProvider _datetimeProvider;

    public LoadStaticGtfsJob(
        IServiceProvider serviceProvider,
        IDatetimeProvider datetimeProvider,
        ILogger<LoadStaticGtfsJob> logger)
    {
        _serviceProvider = serviceProvider;
        _datetimeProvider = datetimeProvider;
        _logger = logger;

        LoadBalancingController.OnLeaderSet += OnLeaderSet;
    }

    // Cette méthode sera appelée dès que l'événement OnLeaderSet est déclenché
    private async void OnLeaderSet()
    {
        _logger.LogInformation("Leader Set Event triggered");
        await ExecuteAsync(CancellationToken.None); // Lance l'exécution du service
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();

            var commandDispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();

            var eventContext = scope.ServiceProvider.GetRequiredService<IEventContext>();

            var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

            var staticGtfsDataLoaded = await eventContext.TryGetAsync<StaticGtfsDataLoaded>();

            var staticGtfsWasNeverLoaded = staticGtfsDataLoaded is null;

            _logger.LogInformation($"[LoadStaticGtfsJob][ExecuteAsync] Is leader? {DBUtils.IsLeader}");
            if (DBUtils.IsLeader)
            {
                if (staticGtfsWasNeverLoaded)
                {
                    await commandDispatcher.DispatchAsync(new ClearDb(), stoppingToken);

                    _logger.LogInformation("Loading static GTFS data");

                    await commandDispatcher.DispatchAsync(new LoadStaticGtfsCommand(), stoppingToken);

                    await publisher.Publish(new StaticGtfsDataLoaded(Guid.NewGuid(), _datetimeProvider.GetCurrentTime()));

                    _logger.LogInformation("Static GTFS data loaded");
                }
                else
                {
                    _logger.LogInformation("Static GTFS data already loaded");
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error in LoadStaticGtfsService");
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        LoadBalancingController.OnLeaderSet -= OnLeaderSet;
        return base.StopAsync(cancellationToken);
    }
}
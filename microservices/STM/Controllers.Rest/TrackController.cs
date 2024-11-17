using Application.Commands.Seedwork;
using Application.Commands.TrackBus;
using Application.EventHandlers.Interfaces;
using Contracts;
using Controllers.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Controllers.Rest;

[ApiController]
[Route("[controller]/[action]")]
public class TrackController : ControllerBase
{
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly IConsumer _consumer;
    private readonly ILogger<TrackController> _logger;

    public TrackController(ILogger<TrackController> logger, ICommandDispatcher commandDispatcher, IConsumer consumer)
    {
        _logger = logger;
        _commandDispatcher = commandDispatcher;
        _consumer = consumer;
    }

    [HttpPost]
    [ActionName(nameof(BeginTracking))]
    public async Task<AcceptedResult> BeginTracking([FromBody] TrackBusCommand trackBusCommand)
    {
        _logger.LogInformation("TrackBus endpoint reached");

        await _commandDispatcher.DispatchAsync(trackBusCommand, CancellationToken.None);

        return Accepted();
    }

    /// <summary>
    /// This does not allow to discriminate which bus is being tracked, maybe it should be published as an event by message queue?...
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    [ActionName(nameof(GetTrackingUpdate))]
    public async Task<ActionResult<ApplicationRideTrackingUpdated>> GetTrackingUpdate()
    {
        const int timeoutInMs = 5000;

        try
        {
            _logger.LogInformation("De TrackController");
            _logger.LogInformation($"Je suis le leader ? {DBUtils.IsLeader}");
           
            bool isLeader = DBUtils.IsLeader;
           // if (isLeader)
          //  {
                var update = await _consumer.ConsumeNext<ApplicationRideTrackingUpdated>(new CancellationTokenSource(timeoutInMs).Token);

                _logger.LogInformation($"update du bus {update}");
                return Ok(update);
           // } else
           // {
           //    return Unauthorized("Unauthorized");
           // }
        }
        catch (OperationCanceledException)
        {
            return Problem("Timeout while waiting for tracking update");
        }
    }
}
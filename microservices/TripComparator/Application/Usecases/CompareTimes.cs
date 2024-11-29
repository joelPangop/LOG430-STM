using System.Diagnostics;
using System.Threading.Channels;
using Application.BusinessObjects;
using Application.DTO;
using Application.Interfaces;

namespace Application.Usecases
{
    public class CompareTimes
    {
        private readonly IRouteTimeProvider _routeTimeProvider;

        private readonly IBusInfoProvider _iBusInfoProvider;

        private readonly IDataStreamWriteModel _dataStreamWriteModel;

        //This is a very aggressive polling rate, is there a better way to do this?
        private readonly PeriodicTimer _periodicTimer = new(TimeSpan.FromMilliseconds(50));

        private int _averageCarTravelTime;

        private RideDto? _optimalBus;

        public CompareTimes(IRouteTimeProvider routeTimeProvider, IBusInfoProvider iBusInfoProvider, IDataStreamWriteModel dataStreamWriteModel)
        {
            _routeTimeProvider = routeTimeProvider;
            _iBusInfoProvider = iBusInfoProvider;
            _dataStreamWriteModel = dataStreamWriteModel;
        }

        public async Task<Channel<IBusPositionUpdated>> BeginComparingBusAndCarTime(string startingCoordinates, string destinationCoordinates)
        {
            await Task.WhenAll(
                _routeTimeProvider.GetTravelTimeInSeconds(startingCoordinates, destinationCoordinates)
                    .ContinueWith(task => _averageCarTravelTime = task.Result),

                _iBusInfoProvider.GetBestBus(startingCoordinates, destinationCoordinates)
                    .ContinueWith(task =>
                    {
                        _optimalBus = task.Result;

                        return _iBusInfoProvider.BeginTracking(_optimalBus);
                    })
                );

            if (_optimalBus is null || _averageCarTravelTime < 1)
            {
                throw new Exception("bus or car data was null");
            }

            var channel = Channel.CreateUnbounded<IBusPositionUpdated>();

            return channel;
        }

        //Is polling ideal?
        public async Task PollTrackingUpdate(ChannelWriter<IBusPositionUpdated> channel)
        {
            if (_optimalBus is null) throw new Exception("bus data was null");

            var trackingOnGoing = true;
            Stopwatch stopwatch = new Stopwatch();

            while (trackingOnGoing && await _periodicTimer.WaitForNextTickAsync())
            {
                stopwatch.Restart();

                var trackingResult = await _iBusInfoProvider.GetTrackingUpdate();

                stopwatch.Stop();
                int elapsedTime = (int)stopwatch.ElapsedMilliseconds;

                // Ajuster l'intervalle si le temps est inférieur à 50ms
                int adjustedDelay = 50; // L'intervalle cible est 50 ms
                if (elapsedTime < adjustedDelay)
                {
                    // Calculez l'écart et doublez-le pour l'ajustement
                    int difference = adjustedDelay - elapsedTime;
                    adjustedDelay = adjustedDelay + (2 * difference);
                }

                if (trackingResult is null) continue;

                trackingOnGoing = !trackingResult.TrackingCompleted;

                var busPosition = new BusPosition()
                {
                    Message = trackingResult.Message + $"\nCar: {_averageCarTravelTime} seconds",
                    Seconds = trackingResult.Duration,
                };

                await channel.WriteAsync(busPosition);

                await Task.Delay(adjustedDelay);
            }

            channel.Complete();
        }

        public async Task WriteToStream(ChannelReader<IBusPositionUpdated> channelReader)
        {
            //if (DBUtils.IsLeader)
            //{
                 await foreach (var busPositionUpdated in channelReader!.ReadAllAsync())
                {
                    await _dataStreamWriteModel.Produce(busPositionUpdated);
                }
            //}

        }
    }
}

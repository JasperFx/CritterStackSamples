using ImTools;
using JasperFx.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TripMessages;
using Wolverine;
using Wolverine.Runtime;

namespace TripPublisher;

/// <summary>
/// Background loop that seeds the first burst of trips and then lets the <see cref="ContinueTrip"/>
/// ping-pong drive the rest. Injects <see cref="IWolverineRuntime"/> (not the scoped
/// <see cref="IMessageBus"/>) per the Wolverine BackgroundService rule, opening a fresh bus per round.
/// </summary>
public class KickOffPublishing : IHostedService
{
    private readonly IWolverineRuntime _runtime;
    private readonly ILogger<KickOffPublishing> _logger;

    public KickOffPublishing(IWolverineRuntime runtime, ILogger<KickOffPublishing> logger)
    {
        _runtime = runtime;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = Task.Run(() => PublishLoop(cancellationToken), cancellationToken);
        return Task.CompletedTask;
    }

    private async Task PublishLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var publisher = new Publisher();
            var bus = new MessageBus(_runtime);

            foreach (var message in publisher.InitialMessages())
            {
                if (cancellationToken.IsCancellationRequested) return;

                await bus.PublishAsync(message);
                await Task.Delay(250.Milliseconds(), cancellationToken);
            }

            _logger.LogInformation("Completed publishing round, pausing before next round");
            await Task.Delay(2.Seconds(), cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// In-memory book of currently in-flight <see cref="TripStream"/>s. Primes ~50 streams and tops up to 10
/// whenever a stream drains, so steady-state traffic stays continuous.
/// </summary>
public class Publisher
{
    private ImHashMap<Guid, TripStream> _streams = ImHashMap<Guid, TripStream>.Empty;

    public Publisher()
    {
        foreach (var stream in TripStream.RandomStreams(50))
        {
            _streams = _streams.AddOrUpdate(stream.Id, stream);
        }
    }

    public IEnumerable<object> InitialMessages()
    {
        foreach (var entry in _streams.Enumerate())
        {
            if (entry.Value.TryCheckoutCommand(out var command) && command is not null)
            {
                yield return command;
            }
        }
    }

    public IEnumerable<object> NextMessages(Guid id)
    {
        if (_streams.TryFind(id, out var stream))
        {
            if (stream.TryCheckoutCommand(out var message) && message is not null)
            {
                yield return message;
            }

            if (stream.IsFinishedPublishing())
            {
                _streams = _streams.Remove(id);
            }
        }

        while (_streams.Count() < 10)
        {
            stream = new TripStream();
            _streams = _streams.AddOrUpdate(stream.Id, stream);

            if (stream.TryCheckoutCommand(out var message) && message is not null)
            {
                yield return message;
            }
        }
    }
}

/// <summary>Wolverine handler for the <see cref="ContinueTrip"/> callback — dequeues + sends the next command.</summary>
public static class ContinueTripHandler
{
    public static IEnumerable<object> Handle(ContinueTrip message, Publisher publisher) =>
        publisher.NextMessages(message.TripId);
}

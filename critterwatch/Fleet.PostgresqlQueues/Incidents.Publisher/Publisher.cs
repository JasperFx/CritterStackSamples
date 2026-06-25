using ImTools;
using Incidents.Domain;
using JasperFx.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wolverine;
using Wolverine.Runtime;

namespace Incidents.Publisher;

/// <summary>
/// Bootstraps the first burst of incidents and lets the <see cref="ContinueIncident"/> ping-pong drive the
/// rest. Mirrors <c>TripPublisher.KickOffPublishing</c>.
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

/// <summary>In-memory book of in-flight <see cref="IncidentStream"/>s — primes ~50, tops up to 10 as they drain.</summary>
public class Publisher
{
    private ImHashMap<Guid, IncidentStream> _streams = ImHashMap<Guid, IncidentStream>.Empty;

    public Publisher()
    {
        foreach (var stream in IncidentStream.RandomStreams(50))
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
            var next = new IncidentStream();
            _streams = _streams.AddOrUpdate(next.Id, next);

            if (next.TryCheckoutCommand(out var message) && message is not null)
            {
                yield return message;
            }
        }
    }
}

/// <summary>Handler for the <see cref="ContinueIncident"/> callback — dequeues + emits the next scripted command.</summary>
public static class ContinueIncidentHandler
{
    public static IEnumerable<object> Handle(ContinueIncident message, Publisher publisher) =>
        publisher.NextMessages(message.IncidentId);
}

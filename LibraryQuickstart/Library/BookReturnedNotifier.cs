using JasperFx.Events;
using JasperFx.Events.Daemon;
using JasperFx.Events.Projections;
using Marten;
using Marten.Events.Daemon;
using Marten.Subscriptions;

namespace Library;

/// <summary>
/// The Marten equivalent of a Chronicle "reactor": a subscription that runs in the
/// async daemon and does a side effect when a BookReturned event lands.
/// </summary>
public class BookReturnedNotifier : ISubscription
{
    public Task<IChangeListener> ProcessEventsAsync(
        EventRange page,
        ISubscriptionController controller,
        IDocumentOperations operations,
        CancellationToken cancellationToken)
    {
        foreach (var e in page.Events.OfType<IEvent<BookReturned>>())
        {
            Console.WriteLine($"Reactor: book {e.StreamId} was returned -- notify the next member in line.");
        }

        return Task.FromResult(NullChangeListener.Instance);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

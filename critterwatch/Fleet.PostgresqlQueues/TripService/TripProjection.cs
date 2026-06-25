using JasperFx.Events;
using Marten;
using Marten.Events.Aggregation;
using TripMessages;

namespace TripService;

/// <summary>
/// Single-stream projection that folds the Trip event stream into a <see cref="Trip"/> snapshot. Runs in
/// the async daemon (see Program.cs) so it shows up in CritterWatch's Projections view as a live shard
/// the operator can pause / restart / rebuild.
/// </summary>
public partial class TripProjection : SingleStreamProjection<Trip, Guid>
{
    // These Apply/Create methods can be public, internal, or private — public gives a small perf edge.
    public void Apply(Arrival e, Trip trip) => trip.State = e.State;

    public void Apply(Traveled e, Trip trip) => trip.Traveled += e.TotalDistance();

    public void Apply(Departure e, Trip trip)
    {
        trip.Active = true;
        trip.WaitingRepairs = false;
        trip.State = e.State;
    }

    public void Apply(TripEnded e, Trip trip)
    {
        trip.Active = false;
        trip.EndedOn = e.Day;
        trip.State = e.State;
    }

    public Trip Create(TripStarted started) =>
        new() { StartedOn = started.Day, Active = true, State = started.State };

    public void Apply(BrokeDown e, Trip trip) => trip.WaitingRepairs = true;

    public void Apply(TripResumed e, Trip trip) => trip.WaitingRepairs = false;

    // begin-snippet: trip-projection-side-effects
    /// <summary>
    /// Projection side effect — after each batch, if the trip isn't waiting on repairs, publish a
    /// <see cref="ContinueTrip"/> back to TripPublisher. This is what keeps the self-driving traffic loop
    /// turning, and it forwards the triggering event's correlation/causation ids onto the outbound
    /// command so CritterWatch's trace view can stitch the whole chain together.
    /// </summary>
    public override ValueTask RaiseSideEffects(IDocumentOperations operations, IEventSlice<Trip> slice)
    {
        if (slice.Snapshot is { WaitingRepairs: false })
        {
            var metadata = new MessageMetadata
            {
                CorrelationId = slice.Events().FirstOrDefault()?.CorrelationId,
                CausationId = slice.Events().LastOrDefault()?.Id.ToString()
            };

            slice.PublishMessage(new ContinueTrip(slice.Snapshot.Id), metadata);
        }

        return new ValueTask();
    }
    // end-snippet
}

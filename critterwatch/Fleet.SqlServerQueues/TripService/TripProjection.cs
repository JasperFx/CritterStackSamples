using Polecat.Projections;
using TripMessages;

namespace TripService;

/// <summary>
/// Single-stream Polecat projection that folds the Trip event stream into a <see cref="Trip"/> snapshot.
/// Runs in the async daemon (see Program.cs) so it shows up in CritterWatch's Projections view as a live
/// shard the operator can pause / restart / rebuild.
///
/// <para>
/// Polecat's projection base classes (<see cref="SingleStreamProjection{TDoc,TId}"/>,
/// <c>MultiStreamProjection</c>, <c>EventProjection</c>) mirror Marten's API — same
/// <c>Apply</c>/<c>Create</c>/<c>Identity</c>/<c>FanOut</c> surface — so the only store-specific change
/// versus the Marten flagship is the <c>using Polecat.Projections;</c> namespace. Declared
/// <c>partial</c> so Polecat's bundled JasperFx.Events source generator emits the apply dispatcher.
/// </para>
/// </summary>
public partial class TripProjection : SingleStreamProjection<Trip, Guid>
{
    public TripProjection() => Name = "Trip";

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
}

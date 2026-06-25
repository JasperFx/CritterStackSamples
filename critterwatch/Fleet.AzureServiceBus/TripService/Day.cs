using JasperFx.Events;
using Marten.Events.Projections;
using TripMessages;

namespace TripService;

/// <summary>Per-day rollup document built by <see cref="DayProjection"/>.</summary>
public class Day
{
    public long Version { get; set; }

    public int Id { get; set; }

    public int Started { get; set; }

    public int Ended { get; set; }

    public int Stops { get; set; }

    public double North { get; set; }
    public double East { get; set; }
    public double West { get; set; }
    public double South { get; set; }
}

/// <summary>
/// Multi-stream projection that groups events across every trip by day, fanning the child
/// <see cref="Movement"/> / <see cref="Stop"/> collections of each <see cref="Traveled"/> event out as if
/// they were individual events. A good demo of Marten's grouping + fan-out, and a non-trivial rebuild
/// target for the CritterWatch rebuild UI.
/// </summary>
public partial class DayProjection : MultiStreamProjection<Day, int>
{
    public DayProjection()
    {
        // Group events into Day documents by their Day value.
        Identity<IDayEvent>(x => x.Day);

        // Treat each Movement / Stop child of a Traveled event as its own event for this projection.
        FanOut<Traveled, Movement>(x => x.Movements);
        FanOut<Traveled, Stop>(x => x.Data.Stops);

        Name = "Day";

        // Second-level cache + a large batch size — standard Marten projection throughput tuning.
        Options.CacheLimitPerTenant = 1000;
        Options.BatchSize = 5000;
    }

    public void Apply(Day day, TripStarted e) => day.Started++;
    public void Apply(Day day, TripEnded e) => day.Ended++;

    public void Apply(Day day, Movement e)
    {
        switch (e.Direction)
        {
            case Direction.East:
                day.East += e.Distance;
                break;
            case Direction.North:
                day.North += e.Distance;
                break;
            case Direction.South:
                day.South += e.Distance;
                break;
            case Direction.West:
                day.West += e.Distance;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public void Apply(Day day, Stop e) => day.Stops++;
}

/// <summary>Per-travel-event distance document built by <see cref="DistanceProjection"/>.</summary>
public class Distance
{
    public Guid Id { get; set; }

    public double Total { get; set; }

    public int Day { get; set; }
}

/// <summary>A plain event projection that emits one <see cref="Distance"/> document per Traveled event.</summary>
public partial class DistanceProjection : EventProjection
{
    public DistanceProjection() => Name = "Distance";

    public Distance Create(Traveled travel, IEvent e) =>
        new() { Id = e.Id, Day = travel.Day, Total = travel.TotalDistance() };
}

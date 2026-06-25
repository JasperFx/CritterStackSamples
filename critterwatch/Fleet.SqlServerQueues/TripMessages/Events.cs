namespace TripMessages;

// Past-tense event records appended to the Trip stream, plus the random-data helpers the publisher
// uses to synthesize realistic traffic.

public record TripAborted;

public record BrokeDown(bool IsCritical);

public record VacationOver;

public record Arrival(int Day, string State);

public record Departure(int Day, string State);

public enum Direction
{
    North,
    South,
    East,
    West
}

public interface IDayEvent
{
    int Day { get; }
}

public record Movement(Direction Direction, double Distance);

public record Stop(TimeOnly Time, string State, int Duration);

/// <summary>
/// A day's worth of travel: a bag of <see cref="Movement"/>s and <see cref="Stop"/>s. The
/// <see cref="TripService.DayProjection"/> fans these child collections out as if each were its own event.
/// </summary>
public class Traveled : IDayEvent
{
    public static Traveled Random(int day)
    {
        var travel = new Traveled { Day = day };

        var random = System.Random.Shared;
        var numberOfMovements = random.Next(1, 20);
        for (var i = 0; i < numberOfMovements; i++)
        {
            var movement = new Movement(TripStream.RandomDirection(), random.Next(500, 3000) / 100);
            travel.Movements.Add(movement);
        }

        var numberOfStops = random.Next(1, 10);
        for (var i = 0; i < numberOfStops; i++)
        {
            travel.Stops.Add(new Stop(TripStream.RandomTime(), TripStream.RandomState(), random.Next(10, 30)));
        }

        return travel;
    }

    public int Day { get; set; }

    public IList<Movement> Movements { get; set; } = new List<Movement>();
    public List<Stop> Stops { get; set; } = new();

    public double TotalDistance() => Movements.Sum(x => x.Distance);
}

public record TripEnded(int Day, string State) : IDayEvent;

public record TripStarted(int Day, string State) : IDayEvent;

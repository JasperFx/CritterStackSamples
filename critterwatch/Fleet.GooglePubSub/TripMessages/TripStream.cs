namespace TripMessages;

/// <summary>
/// In-memory script of the commands TripPublisher walks a single trip through. Constructed once per
/// trip; the publisher dequeues one command at a time and dispatches it over Azure Service Bus. TripService
/// handles each command, its projection publishes <see cref="ContinueTrip"/> back, and the publisher
/// advances to the next message — a self-driving loop that keeps steady-state traffic flowing.
/// </summary>
public class TripStream
{
    public static List<TripStream> RandomStreams(int number)
    {
        var list = new List<TripStream>();
        for (var i = 0; i < number; i++)
        {
            list.Add(new TripStream());
        }

        return list;
    }

    public static readonly string[] States =
        ["Texas", "Arkansas", "Missouri", "Kansas", "Oklahoma", "Connecticut", "New Jersey", "New York"];

    public static string RandomState()
    {
        var index = Random.Shared.Next(0, States.Length - 1);
        return States[index];
    }

    public static Direction RandomDirection() =>
        Random.Shared.Next(0, 3) switch
        {
            0 => Direction.East,
            1 => Direction.North,
            2 => Direction.South,
            _ => Direction.West
        };

    public static TimeOnly RandomTime() => new(Random.Shared.Next(0, 24), 0, 0);

    // Version-7 GUIDs sort sequentially (time-ordered), which keeps the event/document storage index-friendly.
    public Guid Id = Guid.CreateVersion7();

    public readonly Queue<object> Messages = new();

    public TripStream()
    {
        var random = Random.Shared;
        var startDay = random.Next(1, 100);

        Messages.Enqueue(new StartTrip(Id, startDay, RandomState()));

        var state = RandomState();
        Messages.Enqueue(new Depart(Id, startDay, state));

        var duration = random.Next(1, 20);
        var randomNumber = random.NextDouble();

        for (var i = 0; i < duration; i++)
        {
            var day = startDay + i;

            Messages.Enqueue(new RecordTravel(Id, Traveled.Random(day)));

            if (i > 0 && randomNumber > .3)
            {
                Messages.Enqueue(new Depart(Id, day, state));
                state = RandomState();
                Messages.Enqueue(new Arrive(Id, i, state));
            }

            if (randomNumber < .05)
            {
                Messages.Enqueue(new RecordBreakdown(Id, true));
            }
            else if (randomNumber < .08)
            {
                Messages.Enqueue(new RecordBreakdown(Id, false));
            }
        }

        if (randomNumber > .5)
        {
            Messages.Enqueue(new EndTrip(Id, startDay + duration, state));
        }
        else if (randomNumber > .9)
        {
            Messages.Enqueue(new AbortTrip(Id));
        }
    }

    public bool IsFinishedPublishing() => Messages.Count == 0;

    public bool TryCheckoutCommand(out object? command) => Messages.TryDequeue(out command);
}

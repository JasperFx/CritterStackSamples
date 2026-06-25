namespace Incidents.Domain;

/// <summary>
/// In-memory script of the commands an IncidentPublisher walks an incident through, mirroring
/// <c>TripStream</c>. The publisher dequeues one command at a time; the service handles it, publishes
/// <see cref="ContinueIncident"/> back, and the publisher advances.
/// </summary>
public class IncidentStream
{
    public static List<IncidentStream> RandomStreams(int number)
    {
        var list = new List<IncidentStream>(number);
        for (var i = 0; i < number; i++)
        {
            list.Add(new IncidentStream());
        }
        return list;
    }

    private static readonly ContactChannel[] Channels =
        [ContactChannel.Email, ContactChannel.Phone, ContactChannel.InPerson, ContactChannel.Generative];

    private static readonly IncidentCategory[] Categories =
        [IncidentCategory.Software, IncidentCategory.Hardware, IncidentCategory.Network, IncidentCategory.Database];

    private static readonly IncidentPriority[] Priorities =
        [IncidentPriority.Critical, IncidentPriority.High, IncidentPriority.Medium, IncidentPriority.Low];

    public Guid Id { get; } = Guid.CreateVersion7();

    public readonly Queue<object> Messages = new();

    public IncidentStream()
    {
        var random = Random.Shared;

        var customerId = Guid.CreateVersion7();
        var loggedBy = Guid.CreateVersion7();
        var contact = new Contact(
            Channels[random.Next(Channels.Length)],
            FirstName: "Customer",
            LastName: $"#{random.Next(1, 9999)}",
            Email: "customer@example.invalid",
            PhoneNumber: null);

        Messages.Enqueue(new LogIncident(
            Id, customerId, contact,
            Description: $"Synthetic incident {Id:N}",
            LoggedBy: loggedBy));

        // Category first so the IncidentsByCategory projection counts climb in lockstep with stream count.
        var category = Categories[random.Next(Categories.Length)];
        Messages.Enqueue(new CategoriseIncident(Id, category, loggedBy));

        Messages.Enqueue(new PrioritiseIncident(Id, Priorities[random.Next(Priorities.Length)], loggedBy));

        var agentId = Guid.CreateVersion7();
        Messages.Enqueue(new AssignAgentToIncident(Id, agentId));

        // A short back-and-forth so CustomerResponded + AgentResponded events show on every stream.
        var turns = random.Next(1, 4);
        for (var i = 0; i < turns; i++)
        {
            Messages.Enqueue(new RecordCustomerResponseToIncident(
                Id, new CustomerResponse(customerId, $"Customer message {i + 1}")));

            Messages.Enqueue(new RecordAgentResponseToIncident(
                Id, new AgentResponse(agentId, $"Agent reply {i + 1}", VisibleToCustomer: true)));
        }

        // ~70% reach resolution; the rest stay open so the projection's Active count doesn't drain to zero.
        if (random.NextDouble() < 0.70)
        {
            Messages.Enqueue(new ResolveIncident(Id, ResolutionType.Permanent, agentId));

            if (random.NextDouble() < 0.80)
            {
                Messages.Enqueue(new AcknowledgeResolution(Id, customerId));
                Messages.Enqueue(new CloseIncident(Id, agentId));

                // ~30% of closed incidents archive — exercises the Archived event + ShouldDelete path.
                if (random.NextDouble() < 0.30)
                {
                    Messages.Enqueue(new ArchiveIncident(Id));
                }
            }
        }
    }

    public bool IsFinishedPublishing() => Messages.Count == 0;

    public bool TryCheckoutCommand(out object? command)
    {
        if (Messages.TryDequeue(out var msg))
        {
            command = msg;
            return true;
        }
        command = null;
        return false;
    }
}

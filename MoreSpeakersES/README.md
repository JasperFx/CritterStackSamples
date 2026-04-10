# MoreSpeakers (Event Sourcing) — Critter Stack Conversion

## Original Project

**Repository:** [cwoodruff/morespeakers-com](https://github.com/cwoodruff/morespeakers-com)
**License:** MIT

Same domain as `MoreSpeakers/` (document DB version), rebuilt with Marten event sourcing and Wolverine `[AggregateHandler]` workflow.

> **Compare with:** `MoreSpeakers/` — document DB version of the same domain. Side-by-side comparison shows the difference between document-store CRUD and event-sourced aggregates.

## Event Sourcing Design

### Speakers (event-sourced aggregate)
- `SpeakerRegistered` — initial registration
- `SpeakerProfileUpdated` — name, bio, expertise changes
- `MentoringAvailabilityChanged` — toggle mentoring on/off

### Mentorships (event-sourced aggregate with lifecycle)
- `MentorshipRequested` → Pending
- `MentorshipAccepted` → Active
- `MentorshipDeclined` → Declined
- `MentorshipCompleted` → Completed
- `MentorshipCancelled` → Cancelled

The mentorship lifecycle is the strongest case for event sourcing — every state transition is an immutable fact in the event stream. You can answer questions like "when was this mentorship accepted?" or "how long was it pending?" directly from the events.

### Aggregate Handler Pattern

State-changing operations use `[AggregateHandler]` — Wolverine loads the aggregate from the event stream, the handler returns a domain event, Wolverine appends it and commits:

```csharp
[WolverinePost("/api/mentorships/{mentorshipId}/accept")]
[AggregateHandler]
public static MentorshipAccepted Post(AcceptMentorship command, Mentorship mentorship)
{
    return new MentorshipAccepted(command.MentorshipId, command.ResponseMessage);
}
```

Validation against aggregate state happens in a separate `Validate` method:

```csharp
public static ProblemDetails Validate(AcceptMentorship command, Mentorship mentorship)
{
    if (mentorship.Status != MentorshipStatus.Pending)
        return new ProblemDetails { Detail = "Cannot accept", Status = 400 };
    return WolverineContinue.NoProblems;
}
```

## Document DB vs Event Sourcing Comparison

| Aspect | MoreSpeakers (Doc DB) | MoreSpeakersES (Event Sourcing) |
|--------|----------------------|-------------------------------|
| Writes | `session.Store(speaker)` | `session.Events.StartStream(id, evt)` |
| Updates | Mutate document + `session.Store()` | `[AggregateHandler]` returns event |
| Reads | `session.Query<Speaker>()` | Same (inline snapshots) |
| History | None (last-write-wins) | Full event stream per aggregate |
| Audit trail | Manual timestamps | Built-in (event stream) |
| Complexity | Simpler | More types (event per state change) |

## Running

Requires PostgreSQL.

```bash
dotnet run
```

Swagger UI at `/swagger`.

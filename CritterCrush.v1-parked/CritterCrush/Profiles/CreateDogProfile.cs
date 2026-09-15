using Wolverine.Http;
using Wolverine.Marten;

namespace CritterCrush.Profiles;

public record CreateDogProfileRequest(string Name, string Breed, int AgeInMonths, Guid OwnerId);

public static class CreateDogProfileEndpoint
{
    /// <summary>
    /// The endpoint IS the handler. One transaction: the stream starts, anything cascaded rides
    /// the same outbox commit, and the 201 is true — the profile exists when the response
    /// returns. A separate message handler would buy nothing here but a queue hop and a second
    /// transaction; split one out only when the command genuinely needs bus visibility (other
    /// callers, retry policies, scheduling) — never for testability.
    /// </summary>
    [WolverinePost("/api/profiles")]
    public static (CreationResponse, IStartStream) Post(CreateDogProfileRequest request)
    {
        var id = Guid.NewGuid();
        var start = MartenOps.StartStream<DogProfile>(id,
            new DogProfileCreated(id, request.Name, request.Breed, request.AgeInMonths, request.OwnerId));

        return (new CreationResponse($"/api/profiles/{id}"), start);
    }
}

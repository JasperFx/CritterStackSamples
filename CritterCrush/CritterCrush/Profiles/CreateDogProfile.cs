using Wolverine.Http;
using Wolverine.Marten;

namespace CritterCrush.Profiles;

/// <summary>The command. Carries its own new stream id so callers (and specs) control identity.</summary>
public record CreateDogProfile(Guid DogProfileId, string Name, string Breed, int AgeInMonths, Guid OwnerId);

/// <summary>What the HTTP surface accepts — the id is minted at the edge, not by the client.</summary>
public record CreateDogProfileRequest(string Name, string Breed, int AgeInMonths, Guid OwnerId);

public static class CreateDogProfileEndpoint
{
    /// <summary>
    /// A state-change slice's HTTP face is a pure translation: mint the id, return the receipt,
    /// and cascade the command. The cascade rides the transactional outbox — there is no
    /// bus.InvokeAsync ceremony and no work done here that a crash could tear in half.
    /// </summary>
    [WolverinePost("/api/profiles")]
    public static (CreationResponse, CreateDogProfile) Post(CreateDogProfileRequest request)
    {
        var command = new CreateDogProfile(Guid.NewGuid(), request.Name, request.Breed,
            request.AgeInMonths, request.OwnerId);

        return (new CreationResponse($"/api/profiles/{command.DogProfileId}"), command);
    }
}

public static class CreateDogProfileHandler
{
    /// <summary>
    /// The decision is a pure function returning a side effect description — Wolverine starts the
    /// stream and commits, atomically with the inbox.
    /// </summary>
    public static IStartStream Handle(CreateDogProfile command)
        => MartenOps.StartStream<DogProfile>(command.DogProfileId,
            new DogProfileCreated(command.DogProfileId, command.Name, command.Breed,
                command.AgeInMonths, command.OwnerId));
}

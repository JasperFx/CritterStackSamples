using Wolverine.Http;
using Wolverine.Marten;

namespace CritterCrush.Profiles;

public static class GetDogProfileEndpoint
{
    /// <summary>
    /// A state-view slice reading the entity's own Inline snapshot back by id — [ReadAggregate]
    /// binds the route id and hands over the latest projected state, 404ing when the stream does
    /// not exist. No session, no ceremony.
    /// </summary>
    [WolverineGet("/api/profiles/{id}")]
    public static DogProfile Get([ReadAggregate] DogProfile profile) => profile;
}

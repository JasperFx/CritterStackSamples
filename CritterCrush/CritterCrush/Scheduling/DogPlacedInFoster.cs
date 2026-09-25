namespace CritterCrush.Scheduling;

/// <summary>
/// Inbound integration contract: DogPlacedInFoster arrives from Fostering, and no
/// slice in this model emits it — so this is the boundary's own copy of its shape. Version it
/// rather than edit it: a breaking change is DogPlacedInFosterV2, never a changed field here.
/// </summary>
public record DogPlacedInFoster(Guid FosterApplicationId, Guid OwnerId, Guid ShelterId, DateTimeOffset ProposedFor);

namespace CritterCrush.Appointments;

/// <summary>
/// Inbound integration contract: FosterPlacementApproved arrives from Fostering, and no
/// slice in this model emits it — so this is the boundary's own copy of its shape. Version it
/// rather than edit it: a breaking change is FosterPlacementApprovedV2, never a changed field here.
/// </summary>
public record FosterPlacementApproved(Guid PlacementId, Guid OwnerId, Guid ShelterId, Guid DogId, Guid FosterId, DateTimeOffset ProposedFor);


namespace CritterCrush.Scheduling;

/// <summary>
/// Inbound integration contract: SurrenderRequestReviewed arrives from Surrenders, and no
/// slice in this model emits it — so this is the boundary's own copy of its shape. Version it
/// rather than edit it: a breaking change is SurrenderRequestReviewedV2, never a changed field here.
/// </summary>
public record SurrenderRequestReviewed(Guid SurrenderRequestId, Guid OwnerId, Guid ShelterId, DateTimeOffset ProposedFor);

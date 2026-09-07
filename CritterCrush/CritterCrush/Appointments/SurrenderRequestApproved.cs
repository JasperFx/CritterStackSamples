namespace CritterCrush.Appointments;

/// <summary>
/// Inbound integration contract: SurrenderRequestApproved arrives from Surrender, and no
/// slice in this model emits it — so this is the boundary's own copy of its shape. Version it
/// rather than edit it: a breaking change is SurrenderRequestApprovedV2, never a changed field here.
/// </summary>
public record SurrenderRequestApproved();


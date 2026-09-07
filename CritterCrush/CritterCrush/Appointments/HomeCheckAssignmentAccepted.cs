namespace CritterCrush.Appointments;

/// <summary>
/// Inbound integration contract: HomeCheckAssignmentAccepted arrives from Volunteering, and no
/// slice in this model emits it — so this is the boundary's own copy of its shape. Version it
/// rather than edit it: a breaking change is HomeCheckAssignmentAcceptedV2, never a changed field here.
/// </summary>
public record HomeCheckAssignmentAccepted();


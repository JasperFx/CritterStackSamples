namespace CritterCrush.Appointments;

// The three proposal automations are triggered by events raised in OTHER chapters of the board
// (volunteering, fostering, surrender). This chapter does not own those flows, so their events
// arrive here as inbound integration contracts — declared once, versioned deliberately, and
// never shared as types with the publishing module.
//
// The model already says this: each automation's trigger is a MessageHandler whose label names
// an event no slice here emits. (Worth teaching the model to say it structurally, with an
// inbound `externalSystems:` edge — filed as a follow-up.)

public record HomeCheckAssignmentAccepted(Guid AssignmentId, Guid OwnerId, Guid DogId);

public record FosterPlacementApproved(Guid PlacementId, Guid OwnerId, Guid DogId);

public record SurrenderRequestApproved(Guid SurrenderRequestId, Guid OwnerId, Guid DogId);

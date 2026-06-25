namespace TripMessages;

// Messages that cross the TripService <-> RepairShop boundary. A critical breakdown in TripService
// emits RepairRequested; RepairShop conducts repairs and replies RepairsCompleted, which TripService
// turns into a TripResumed event on the trip stream.

public record RepairRequested(Guid TripId, string State);

public record ConductRepairs(Guid TripId);

public record RepairsCompleted(Guid TripId);

public record TripResumed;

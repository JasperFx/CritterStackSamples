namespace TripMessages;

/// <summary>
/// The publisher-bound callback. TripService's command handlers publish one of these back to TripPublisher
/// after a trip advances (via the handler's <c>OutgoingMessages</c>); the publisher's handler dequeues the
/// next scripted command for that trip and sends it. This ping-pong is what keeps the fleet's event
/// traffic flowing continuously so CritterWatch always has live activity to display.
/// </summary>
public record ContinueTrip(Guid TripId);

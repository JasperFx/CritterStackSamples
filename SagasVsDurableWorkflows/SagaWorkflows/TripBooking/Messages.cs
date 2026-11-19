using Wolverine.ErrorHandling;
using Wolverine.Persistence.Sagas;

namespace SagaWorkflows.TripBooking;

// Rebuilt from Restate's trip booking saga (restatedev/examples, typescript/patterns-use-cases/src/sagas): book a
// flight, a car and a hotel in turn. A refusal (Restate's TerminalError) undoes the bookings in reverse order, each
// cancellation finished before the next starts. Any other failure is retried and never compensated.
//
// Two sagas use these messages: Trip (a document) and EventSourcedTrip (an event stream).

public record FlightRequest(string FlightId, string PassengerName);
public record CarRequest(string PickupLocation, DateOnly RentalDate);
public record HotelRequest(DateOnly Arrival, DateOnly Departure);

public record BookTrip([property: SagaIdentity] Guid TripId, string CustomerId, FlightRequest Flight, CarRequest Car,
    HotelRequest Hotel);

public record BookFlight([property: SagaIdentity] Guid TripId, string CustomerId, FlightRequest Flight);
public record BookCar([property: SagaIdentity] Guid TripId, string CustomerId, CarRequest Car);
public record BookHotel([property: SagaIdentity] Guid TripId, string CustomerId, HotelRequest Hotel);

public record FlightBooked([property: SagaIdentity] Guid TripId);
public record CarBooked([property: SagaIdentity] Guid TripId);
public record HotelBooked([property: SagaIdentity] Guid TripId);

// The undo steps, and the replies the airline and the car rental send once they have cancelled. The hotel sends no
// reply: Wolverine's own acknowledgement confirms that one
public record CancelFlight([property: SagaIdentity] Guid TripId, string CustomerId);
public record CancelCar([property: SagaIdentity] Guid TripId, string CustomerId);
public record CancelHotel([property: SagaIdentity] Guid TripId, string CustomerId);

public record FlightCancelled([property: SagaIdentity] Guid TripId);
public record CarCancelled([property: SagaIdentity] Guid TripId);

public record TripBooked([property: SagaIdentity] Guid TripId);

public enum Booking
{
    Flight,
    Car,
    Hotel
}

// A provider said no. Retrying won't change its mind, so Wolverine skips the retries (Restate's TerminalError)
[TerminalFailure]
public class BookingRefusedException(string message) : Exception(message);

// No car anywhere for the trip's dates. A kind of refusal: Compensate methods for BookingRefusedException match it too,
// in this service or across the wire, because the fault carries the exception's type hierarchy
[TerminalFailure]
public class NoCarsException(string message) : BookingRefusedException(message);

// The hotel is full, but the flight is still worth keeping
[TerminalFailure]
public class NoRoomsException(string message) : BookingRefusedException(message);

// A provider is down. Worth retrying, and never a reason to cancel the trip
public class ProviderUnavailableException(string provider) : Exception($"{provider} is unavailable");

// A provider refused to cancel a booking
[TerminalFailure]
public class CancellationRefusedException(string message) : Exception(message);

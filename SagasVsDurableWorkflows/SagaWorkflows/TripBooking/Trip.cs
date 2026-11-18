using JasperFx;
using Wolverine;

namespace SagaWorkflows.TripBooking;

// Rebuilt from Restate's trip booking saga (restatedev/examples, typescript/patterns-use-cases/src/sagas): book a
// flight, a car and a hotel in turn. A refusal (Restate's TerminalError) undoes the bookings in reverse order, each
// cancellation finished before the next starts. Any other failure is retried and never compensated.

public record FlightRequest(string FlightId, string PassengerName);
public record CarRequest(string PickupLocation, DateOnly RentalDate);
public record HotelRequest(DateOnly Arrival, DateOnly Departure);

public record BookTrip(Guid TripId, string CustomerId, FlightRequest Flight, CarRequest Car, HotelRequest Hotel);

public record BookFlight(Guid TripId, string CustomerId, FlightRequest Flight);
public record BookCar(Guid TripId, string CustomerId, CarRequest Car);
public record BookHotel(Guid TripId, string CustomerId, HotelRequest Hotel);

public record FlightBooked(Guid TripId);
public record CarBooked(Guid TripId);
public record HotelBooked(Guid TripId);

public record CancelFlight(Guid TripId, string CustomerId);
public record CancelCar(Guid TripId, string CustomerId);
public record CancelHotel(Guid TripId, string CustomerId);

public record FlightCancelled(Guid TripId);
public record CarCancelled(Guid TripId);
public record HotelCancelled(Guid TripId);

public record TripBooked(Guid TripId);
public record TripCancelled(Guid TripId, string Reason);

public enum Booking
{
    Flight,
    Car,
    Hotel
}

// Kept for a week after it finishes, so "what happened to trip X?" can still be answered
[RetainWhenCompleted(Days = 7)]
public class Trip : CompensatingSaga, IRevisioned
{
    public Guid Id { get; set; }
    public int Version { get; set; }

    public string CustomerId { get; set; } = "";
    public CarRequest Car { get; set; } = null!;
    public HotelRequest Hotel { get; set; } = null!;

    // Restate pushes a compensation *before* each booking, because a booking that failed may still have gone through
    // on the other side. This is the same list: every booking attempted, oldest first
    public List<Booking> Attempted { get; set; } = [];
    public string? CancellationReason { get; set; }

    public static (Trip, BookFlight) Start(BookTrip command)
        => (new Trip
            {
                Id = command.TripId,
                CustomerId = command.CustomerId,
                Car = command.Car,
                Hotel = command.Hotel,
                Attempted = [Booking.Flight]
            },
            new BookFlight(command.TripId, command.CustomerId, command.Flight));

    public BookCar Handle(FlightBooked _)
    {
        Attempted.Add(Booking.Car);
        return new BookCar(Id, CustomerId, Car);
    }

    public BookHotel Handle(CarBooked _)
    {
        Attempted.Add(Booking.Hotel);
        return new BookHotel(Id, CustomerId, Hotel);
    }

    public TripBooked Handle(HotelBooked _)
    {
        MarkCompleted();
        return new TripBooked(Id);
    }

    // Only a refusal compensates, wherever the provider runs: RemoteFailure<T> matches the exception's type hierarchy
    // as it came back in the Fault<T>. An outage matches nothing here, so that booking is dead lettered after its
    // retries and the trip waits until someone replays it
    public object Compensate(BookFlight failed, RemoteFailure<BookingRefusedException> failure) => startUndo(failure);
    public object Compensate(BookCar failed, RemoteFailure<BookingRefusedException> failure) => startUndo(failure);
    public object Compensate(BookHotel failed, RemoteFailure<BookingRefusedException> failure) => startUndo(failure);

    // Each cancellation is confirmed before the next one goes out, as Restate awaits each one in turn
    public object Handle(HotelCancelled _) => undoNext();
    public object Handle(CarCancelled _) => undoNext();
    public object Handle(FlightCancelled _) => undoNext();

    private object startUndo(RemoteFailure<BookingRefusedException> failure)
    {
        CancellationReason = failure.Message;
        return undoNext();
    }

    private object undoNext()
    {
        if (Attempted.Count == 0)
        {
            MarkCompleted();
            return new TripCancelled(Id, CancellationReason!);
        }

        var last = Attempted[^1];
        Attempted.RemoveAt(Attempted.Count - 1);

        return last switch
        {
            Booking.Hotel => new CancelHotel(Id, CustomerId),
            Booking.Car => new CancelCar(Id, CustomerId),
            _ => new CancelFlight(Id, CustomerId)
        };
    }
}

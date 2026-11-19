using JasperFx.Events;
using Wolverine;

namespace SagaWorkflows.TripBooking;

// The same trip as an event-sourced saga, written with one Evolve() method instead of Apply methods. Wolverine's own
// events (the undo stack, the compensation, SagaCompleted) still reach the EventSourcedSaga base type first.
//
// - Start is static and has no saga to call PushUndo() on, so it returns PushedUndo(...) among its events.
// - The steps go out in OutgoingMessages, which no signature shows, and the catch-all Compensate(object, ...) only
//   covers what the saga's signatures return. [Compensates] names the steps it covers.
// - [ArchiveWhenCompleted] archives the stream together with the SagaCompleted event.

public record TripStarted(string CustomerId, CarRequest Car, HotelRequest Hotel);
public record Reserved(Booking Booking);

[ArchiveWhenCompleted]
public class EventSourcedTrip : EventSourcedSaga
{
    public Guid Id { get; set; }
    public string CustomerId { get; set; } = "";
    public CarRequest Car { get; set; } = null!;
    public HotelRequest Hotel { get; set; } = null!;
    public List<Booking> Reservations { get; set; } = [];

    public static (TripStarted, SagaUndoPushed, OutgoingMessages) Start(BookTrip command)
        => (new TripStarted(command.CustomerId, command.Car, command.Hotel),
            PushedUndo(new CancelFlight(command.TripId, command.CustomerId), "flight", confirmedBy: typeof(FlightCancelled)),
            [new BookFlight(command.TripId, command.CustomerId, command.Flight)]);

    public (Reserved, OutgoingMessages) Handle(FlightBooked booked)
    {
        PushUndo(new CancelCar(Id, CustomerId), "car", confirmedBy: typeof(CarCancelled));
        return (new Reserved(Booking.Flight), [new BookCar(Id, CustomerId, Car)]);
    }

    public (Reserved, OutgoingMessages) Handle(CarBooked booked)
    {
        PushUndo(new CancelHotel(Id, CustomerId), "hotel");
        return (new Reserved(Booking.Car), [new BookHotel(Id, CustomerId, Hotel)]);
    }

    public (Reserved, SagaCompleted, OutgoingMessages) Handle(HotelBooked booked)
        => (new Reserved(Booking.Hotel), new SagaCompleted(), [new TripBooked(Id)]);

    [Compensates(typeof(BookFlight), typeof(BookCar), typeof(BookHotel))]
    public UndoSequence Compensate(object failed, RemoteFailure<BookingRefusedException> refused)
        => UndoSequentially(completeWhenDone: true);

    public void Evolve(IEvent e)
    {
        switch (e.Data)
        {
            case TripStarted started:
                Id = e.StreamId;
                CustomerId = started.CustomerId;
                Car = started.Car;
                Hotel = started.Hotel;
                break;
            case Reserved reserved:
                Reservations.Add(reserved.Booking);
                break;
        }
    }
}

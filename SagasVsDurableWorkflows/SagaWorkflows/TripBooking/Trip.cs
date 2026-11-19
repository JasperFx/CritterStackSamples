using JasperFx;
using Wolverine;

namespace SagaWorkflows.TripBooking;

// The trip as a document saga, on the undo stack. Restate registers each cancellation *before* its booking, because a
// booking that failed may still have gone through on the provider's side. PushUndo does the same: an undo pushed in
// the handler that sends the step is pending until the step's reply confirms it, and an unwind includes pending
// entries by default.
//
// The evaluation's version needed an Attempted list, three Handle(XCancelled) methods and an undoNext() to cancel one
// booking at a time. UndoSequentially() is that state machine, built in.

// Kept for a week after it finishes, so "what happened to trip X?" can still be answered
[RetainWhenCompleted(Days = 7)]
public class Trip : CompensatingSaga, IRevisioned
{
    public Guid Id { get; set; }
    public string CustomerId { get; set; } = "";
    public CarRequest Car { get; set; } = null!;
    public HotelRequest Hotel { get; set; } = null!;

    public static (Trip, BookFlight) Start(BookTrip command)
    {
        var trip = new Trip { Id = command.TripId, CustomerId = command.CustomerId, Car = command.Car, Hotel = command.Hotel };
        trip.PushUndo(new CancelFlight(trip.Id, trip.CustomerId), "flight", confirmedBy: typeof(FlightCancelled));

        return (trip, new BookFlight(trip.Id, trip.CustomerId, command.Flight));
    }

    public BookCar Handle(FlightBooked booked)
    {
        PushUndo(new CancelCar(Id, CustomerId), "car", confirmedBy: typeof(CarCancelled));
        return new BookCar(Id, CustomerId, Car);
    }

    public BookHotel Handle(CarBooked booked)
    {
        PushUndo(new CancelHotel(Id, CustomerId), "hotel");
        return new BookHotel(Id, CustomerId, Hotel);
    }

    public TripBooked Handle(HotelBooked booked)
    {
        MarkCompleted();
        return new TripBooked(Id);
    }

    // A refusal of any booking, wherever the provider runs: cancel everything, newest first, each cancellation
    // confirmed before the next one goes out. Then close the trip
    public UndoSequence Compensate(object failed, RemoteFailure<BookingRefusedException> refused)
        => UndoSequentially(completeWhenDone: true);

    // No rooms: give the car back too, all at once, but keep the flight. The trip stays open for another hotel
    public OutgoingMessages Compensate(BookHotel failed, RemoteFailure<NoRoomsException> noRooms)
        => UndoMessagesSince("car");
}

using SagaWorkflows.TripBooking;
using JasperFx.Core;
using Wolverine;

namespace SagaWorkflows.Specs.TripBooking;

// Restate's trip booking on the undo stack. Each booking pushes its cancellation before it goes out, so a booking whose
// outcome is unknown is cancelled too. A refusal unwinds the stack one confirmed cancellation at a time, then closes
// the trip: UndoSequentially(completeWhenDone: true).

[BobcatFeature("A refused booking cancels the trip one booking at a time")]
[Collection(nameof(DocumentSagaCollection))]
public class unwinding_a_trip(DocumentSagas app) : SagaWorkflowSpec(app)
{
    private Guid TripId { get; } = Guid.NewGuid();

    private BookTrip TheTrip(string customer) => new(TripId, customer,
        new FlightRequest("LH-430", "Ada"), new CarRequest("ORD", new DateOnly(2027, 3, 1)),
        new HotelRequest(new DateOnly(2027, 3, 1), new DateOnly(2027, 3, 4)));

    [BobcatSpec(typeof(BookTrip))]
    public async Task a_trip_that_books_everything_cancels_nothing()
    {
        await WhenReceived(TheTrip("ada"));

        ThenMessageSent(new TripBooked(TripId));
        ThenNoMessageSent<CancelFlight>();
        ThenStillBooked("ada", Booking.Hotel);
        await ThenTheSagaIsCompleted<Trip>(TripId);
    }

    // [RetainWhenCompleted(Days = 7)]: the finished trip is kept for a week, so "what happened to trip X?" has an
    // answer, and then a node agent sweeps it away
    [BobcatSpec]
    public async Task a_finished_trip_is_kept_for_a_week_then_swept()
    {
        await GivenReceived(TheTrip("ada"));

        await WhenCompletedSagasAreSwept<Trip>(6.Days());
        await ThenTheSagaIsCompleted<Trip>(TripId);

        await WhenCompletedSagasAreSwept<Trip>(8.Days());
        await ThenNoSaga<Trip>(TripId);
    }

    // The hotel was pushed before its booking, so it is cancelled too: its outcome on the provider's side is unknown.
    // The flight and the car are confirmed by the provider's reply, the hotel by Wolverine's acknowledgement
    [BobcatSpec(typeof(BookTrip))]
    public async Task a_refused_hotel_cancels_every_booking_newest_first_each_confirmed_before_the_next()
    {
        ProviderRefuses(Booking.Hotel, "ada");

        await WhenReceived(TheTrip("ada"));

        ThenAttempted<BookHotel>(1);
        ThenUndoneOneAtATime(typeof(CancelHotel), typeof(CancelCar), typeof(CancelFlight));
        ThenNoBooking("ada", Booking.Flight);
        ThenNoBooking("ada", Booking.Car);
        await ThenTheSagaCompensated<Trip, BookHotel, BookingRefusedException>(TripId);
        await ThenTheLastCompensationUndid<Trip>(TripId, "hotel", "car", "flight");
        await ThenTheSagaStatusIs<Trip>(TripId, SagaStatus.Compensated);
        await ThenTheSagaIsCompleted<Trip>(TripId);
    }

    // NoCarsException derives from BookingRefusedException, and the fault carries the exception's type hierarchy, so
    // RemoteFailure<BookingRefusedException> matches it wherever the car rental runs
    [BobcatSpec(typeof(BookTrip))]
    public async Task no_cars_is_a_refusal_too()
    {
        NoCarsFor("ada");

        await WhenReceived(TheTrip("ada"));

        await ThenTheSagaCompensated<Trip, BookCar, NoCarsException>(TripId);
        ThenUndoneOneAtATime(typeof(CancelCar), typeof(CancelFlight));
        ThenNoBooking("ada", Booking.Flight);
    }

    // A more specific Compensate wins: Compensate(BookHotel, RemoteFailure<NoRoomsException>) returns
    // UndoMessagesSince("car"), so the car goes back and the flight stays
    [BobcatSpec(typeof(BookTrip))]
    public async Task no_rooms_gives_back_the_car_but_keeps_the_flight()
    {
        HotelIsFull("ada");

        await WhenReceived(TheTrip("ada"));

        ThenMessageSent(new CancelHotel(TripId, "ada"));
        ThenMessageSent(new CancelCar(TripId, "ada"));
        ThenNoMessageSent<CancelFlight>();
        ThenStillBooked("ada", Booking.Flight);
        await ThenTheSagaCompensated<Trip, BookHotel, NoRoomsException>(TripId);
        await ThenTheUndoStackHolds<Trip>(TripId, "flight");
        await ThenTheSagaIsStillOpen<Trip>(TripId);
    }

    // The car's cancellation fails for good: the unwind stops there, and the flight's cancellation is still on the
    // stack for whoever picks the trip up
    [BobcatSpec(typeof(BookTrip))]
    public async Task a_cancellation_that_fails_leaves_the_rest_of_the_stack()
    {
        ProviderRefuses(Booking.Hotel, "ada");
        ProviderRefusesToCancel(Booking.Car, "ada");

        await WhenReceived(TheTrip("ada"));

        ThenDeadLettered<CancelCar>();
        ThenNoMessageSent<CancelFlight>();
        ThenStillBooked("ada", Booking.Flight);
        await ThenTheSagaStatusIs<Trip>(TripId, SagaStatus.CompensationFailed);
        await ThenTheUndoStackHolds<Trip>(TripId, "flight", "car");
    }

    // An outage is not a refusal: retried, then parked in the dead letter queue with the trip still waiting.
    // Replaying the dead letter carries on
    [BobcatSpec(typeof(BookTrip))]
    public async Task an_outage_is_retried_and_parked_but_never_compensated()
    {
        ProviderIsDown(Booking.Car, "ada");

        await WhenReceived(TheTrip("ada"));

        ThenAttempted<BookCar>(3);
        ThenDeadLettered<BookCar>();
        ThenNoMessageSent<CancelFlight>();
        ThenStillBooked("ada", Booking.Flight);
        await ThenTheSagaStatusIs<Trip>(TripId, SagaStatus.Active);
        await ThenTheUndoStackHolds<Trip>(TripId, "flight", "car");
    }
}

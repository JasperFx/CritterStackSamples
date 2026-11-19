using SagaWorkflows.TripBooking;
using Wolverine;

namespace SagaWorkflows.Specs.TripBooking;

// The same trip as an event-sourced saga written with Evolve(). Its undo stack lives in its stream as Wolverine's own
// events, and [ArchiveWhenCompleted] archives the stream when the trip closes.

[BobcatFeature("An event-sourced trip unwinds from its stream")]
[Collection(nameof(EventSourcedSagaCollection))]
public class an_event_sourced_trip(EventSourcedSagas app) : SagaWorkflowSpec(app)
{
    private Guid TripId { get; } = Guid.NewGuid();

    private BookTrip TheTrip(string customer) => new(TripId, customer,
        new FlightRequest("LH-430", "Ada"), new CarRequest("ORD", new DateOnly(2027, 3, 1)),
        new HotelRequest(new DateOnly(2027, 3, 1), new DateOnly(2027, 3, 4)));

    [BobcatSpec(typeof(BookTrip))]
    public async Task a_booked_trip_completes_and_its_stream_is_archived()
    {
        await WhenReceived(TheTrip("ada"));

        ThenMessageSent(new TripBooked(TripId));
        await ThenSaga<EventSourcedTrip>(TripId, Specify<EventSourcedTrip>()
            .With(x => x.Reservations, Expect.Exactly(Booking.Flight, Booking.Car, Booking.Hotel)));
        await ThenTheSagaIsCompleted<EventSourcedTrip>(TripId);
        await ThenTheSagaStreamIsArchived<EventSourcedTrip>(TripId);
    }

    [BobcatSpec(typeof(BookTrip))]
    public async Task a_refused_hotel_cancels_every_booking_newest_first_each_confirmed_before_the_next()
    {
        ProviderRefuses(Booking.Hotel, "ada");

        await WhenReceived(TheTrip("ada"));

        ThenUndoneOneAtATime(typeof(CancelHotel), typeof(CancelCar), typeof(CancelFlight));
        ThenNoBooking("ada", Booking.Flight);
        await ThenTheLastCompensationUndid<EventSourcedTrip>(TripId, "hotel", "car", "flight");
        await ThenTheSagaStatusIs<EventSourcedTrip>(TripId, SagaStatus.Compensated);
        await ThenTheSagaIsCompleted<EventSourcedTrip>(TripId);
    }

    // The steps go out in OutgoingMessages, which no signature shows, so [Compensates] names them for the catch-all
    [BobcatSpec]
    public async Task the_service_describes_the_steps_the_catch_all_Compensate_covers()
    {
        await ThenTheCompensateMethodCovers<EventSourcedTrip>(typeof(object),
            typeof(BookCar), typeof(BookFlight), typeof(BookHotel));
    }
}

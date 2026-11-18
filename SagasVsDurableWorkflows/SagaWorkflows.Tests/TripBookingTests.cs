using SagaWorkflows.TripBooking;
using Wolverine;
using Wolverine.Tracking;

namespace SagaWorkflows.Tests;

public class TripBookingTests(AppFixture fixture) : WorkflowContext(fixture)
{
    private TravelProviders Providers => Service<TravelProviders>();

    private static BookTrip trip(string customer) => new(Guid.NewGuid(), customer,
        new FlightRequest("LH-430", "Ada"), new CarRequest("ORD", new DateOnly(2027, 3, 1)),
        new HotelRequest(new DateOnly(2027, 3, 1), new DateOnly(2027, 3, 4)));

    [Fact]
    public async Task books_the_flight_the_car_and_the_hotel()
    {
        var customer = $"c-{Guid.NewGuid():N}";
        var command = trip(customer);

        var session = await Run(command);

        session.Executed.SingleMessage<TripBooked>().TripId.ShouldBe(command.TripId);
        Providers.HasReservation(Booking.Flight, customer).ShouldBeTrue();
        Providers.HasReservation(Booking.Car, customer).ShouldBeTrue();
        Providers.HasReservation(Booking.Hotel, customer).ShouldBeTrue();

        // Retained after completion
        var saga = await LoadSaga<Trip>(command.TripId);
        saga!.IsCompleted().ShouldBeTrue();
        saga.Status.ShouldBe(SagaStatus.Active);
    }

    [Fact]
    public async Task a_refused_hotel_cancels_everything_in_reverse_order_one_at_a_time()
    {
        var customer = $"c-{Guid.NewGuid():N}";
        Providers.Refusals.Add((Booking.Hotel, customer));
        var command = trip(customer);

        var session = await Run(command);

        // A terminal failure: one attempt
        session.AllRecordsInOrder(MessageEventType.ExecutionStarted).Count(x => x.Message is BookHotel).ShouldBe(1);

        // Hotel first (it may have gone through on the provider's side), then the car, then the flight, each one
        // confirmed before the next is sent
        session.AllRecordsInOrder(MessageEventType.MessageSucceeded)
            .Select(x => x.Message)
            .Where(x => x is CancelHotel or CancelCar or CancelFlight or HotelCancelled or CarCancelled or FlightCancelled)
            .Select(x => x!.GetType())
            .ShouldBe([
                typeof(CancelHotel), typeof(HotelCancelled), typeof(CancelCar), typeof(CarCancelled),
                typeof(CancelFlight), typeof(FlightCancelled)
            ]);

        session.Executed.SingleMessage<TripCancelled>().Reason.ShouldContain("No hotel available");
        Providers.HasReservation(Booking.Flight, customer).ShouldBeFalse();
        Providers.HasReservation(Booking.Car, customer).ShouldBeFalse();

        var saga = await LoadSaga<Trip>(command.TripId);
        saga!.Status.ShouldBe(SagaStatus.Compensated);
        saga.IsCompleted().ShouldBeTrue();
    }

    [Fact]
    public async Task a_refused_flight_only_cancels_the_flight()
    {
        var customer = $"c-{Guid.NewGuid():N}";
        Providers.Refusals.Add((Booking.Flight, customer));

        var session = await Run(trip(customer));

        session.Sent.MessagesOf<CancelCar>().ShouldBeEmpty();
        session.Sent.MessagesOf<CancelHotel>().ShouldBeEmpty();
        session.Executed.SingleMessage<CancelFlight>().CustomerId.ShouldBe(customer);
        session.Executed.SingleMessage<TripCancelled>();
    }

    [Fact]
    public async Task an_outage_is_retried_then_parked_and_never_compensated()
    {
        var customer = $"c-{Guid.NewGuid():N}";
        Providers.Outages.Add((Booking.Car, customer));
        var command = trip(customer);

        var session = await Run(command);

        session.AllRecordsInOrder(MessageEventType.ExecutionStarted)
            .Where(x => x.Message is BookCar).Select(x => x.AttemptNumber).Distinct().Count().ShouldBe(3);
        session.MovedToErrorQueue.SingleMessage<BookCar>();

        session.Sent.MessagesOf<CancelFlight>().ShouldBeEmpty();
        session.Sent.MessagesOf<TripCancelled>().ShouldBeEmpty();

        // The trip is still waiting on the car, with the flight booked. Replaying the dead letter carries on
        var saga = await LoadSaga<Trip>(command.TripId);
        saga!.Status.ShouldBe(SagaStatus.Active);
        saga.Attempted.ShouldBe([Booking.Flight, Booking.Car]);
        Providers.HasReservation(Booking.Flight, customer).ShouldBeTrue();
    }
}

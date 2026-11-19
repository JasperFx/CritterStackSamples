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
        // handled before the next is sent
        var records = session.AllRecordsInOrder();
        int handled(Type type) => Array.FindIndex(records, x => x.MessageEventType == MessageEventType.MessageSucceeded && type.IsInstanceOfType(x.Message));
        int sent(Type type) => Array.FindIndex(records, x => x.MessageEventType == MessageEventType.Sent && type.IsInstanceOfType(x.Message));

        handled(typeof(CancelHotel)).ShouldBeLessThan(sent(typeof(CancelCar)));
        handled(typeof(CancelCar)).ShouldBeLessThan(sent(typeof(CancelFlight)));
        handled(typeof(CancelFlight)).ShouldBeGreaterThan(0);

        Providers.HasReservation(Booking.Flight, customer).ShouldBeFalse();
        Providers.HasReservation(Booking.Car, customer).ShouldBeFalse();

        // UndoSequentially(completeWhenDone: true) closed the trip once the last cancellation was confirmed. It's
        // retained, so it can still be read
        var saga = await LoadSaga<Trip>(command.TripId);
        saga!.Status.ShouldBe(SagaStatus.Compensated);
        saga.IsCompleted().ShouldBeTrue();
        saga.CompensationLog.Last().Undone!.Select(x => x.Step).ShouldBe(["hotel", "car", "flight"]);
    }

    [Fact]
    public async Task a_refused_flight_only_cancels_the_flight()
    {
        var customer = $"c-{Guid.NewGuid():N}";
        Providers.Refusals.Add((Booking.Flight, customer));

        var command = trip(customer);

        var session = await Run(command);

        session.Sent.MessagesOf<CancelCar>().ShouldBeEmpty();
        session.Sent.MessagesOf<CancelHotel>().ShouldBeEmpty();
        session.Executed.SingleMessage<CancelFlight>().CustomerId.ShouldBe(customer);
        (await LoadSaga<Trip>(command.TripId))!.IsCompleted().ShouldBeTrue();
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

        // The trip is still waiting on the car, with the flight booked. Replaying the dead letter carries on
        var saga = await LoadSaga<Trip>(command.TripId);
        saga!.Status.ShouldBe(SagaStatus.Active);
        saga.UndoStack.Select(x => x.Tag).ShouldBe(["flight", "car"]);
        Providers.HasReservation(Booking.Flight, customer).ShouldBeTrue();
    }
}

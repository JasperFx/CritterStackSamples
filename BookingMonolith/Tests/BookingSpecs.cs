using Bobcat;
using Bobcat.Xunit;
using Booking;
using FlightRecord = global::Flight.Flight;
using PassengerRecord = global::Passenger.Passenger;

namespace Tests;

/// <summary>
/// The event-sourced Booking module's tests, rewritten as Bobcat specifications. Each one arranges the
/// passenger and flight documents a booking looks up (or the booking's own stream) directly, instead of
/// through a chain of POSTs to the other modules, sends ONE request, and asserts what it appended and
/// what the aggregate became. The Identity, Passenger and Flight tests are still in
/// <see cref="BookingMonolithTests" />, unchanged apart from the move to xUnit v3.
/// </summary>
[BobcatFeature("Booking")]
public class BookingSpecs(BookingMonolithHost app) : BookingMonolithSpec(app)
{
    [BobcatSpec(typeof(CreateBooking))]
    public async Task create_booking_with_valid_passenger_and_flight()
    {
        var passengerId = Guid.NewGuid();
        var flightId = Guid.NewGuid();
        await GivenReadModel(new PassengerRecord { Id = passengerId, Name = "Jane Doe", PassportNumber = "XY789012" });
        await GivenReadModel(new FlightRecord { Id = flightId, FlightNumber = "FL400", Price = 499.99m });

        await WhenPosted(new CreateBooking(passengerId, flightId, "Window seat please"), "/api/bookings");

        ThenTheResponseStatusIs(200);
        ThenEvents(Specify<BookingCreatedDomainEvent>()
            .With(x => x.PassengerId, passengerId)
            .With(x => x.PassengerName, "Jane Doe")
            .With(x => x.FlightId, flightId)
            .With(x => x.FlightNumber, "FL400")
            .With(x => x.Price, 499.99m)
            .With(x => x.Description, "Window seat please"));
        await ThenAggregate<BookingRecord>(TheStartedStream<BookingRecord>(), Specify<BookingRecord>()
            .With(x => x.PassengerName, "Jane Doe")
            .With(x => x.FlightNumber, "FL400")
            .With(x => x.Price, 499.99m)
            .With(x => x.Description, "Window seat please"));
    }

    [BobcatSpec(typeof(CreateBooking))]
    public async Task create_booking_returns_400_for_missing_passenger()
    {
        var flightId = Guid.NewGuid();
        await GivenReadModel(new FlightRecord { Id = flightId, FlightNumber = "FL500", Price = 200m });

        // [Entity(OnMissing = ProblemDetailsWith400)] answers 400 before the endpoint runs
        await WhenPosted(new CreateBooking(Guid.NewGuid(), flightId, null), "/api/bookings");

        ThenTheResponseStatusIs(400);
        ThenNoEvents();
    }

    [BobcatSpec(typeof(CreateBooking))]
    public async Task create_booking_returns_400_for_missing_flight()
    {
        var passengerId = Guid.NewGuid();
        await GivenReadModel(new PassengerRecord { Id = passengerId, Name = "Test User", PassportNumber = "ZZ000000" });

        await WhenPosted(new CreateBooking(passengerId, Guid.NewGuid(), null), "/api/bookings");

        ThenTheResponseStatusIs(400);
        ThenNoEvents();
    }

    [BobcatSpec(typeof(CreateBooking))]
    public async Task create_booking_validation_rejects_empty_ids()
    {
        // The FluentValidation middleware refuses it before any lookup
        await WhenPosted(new CreateBooking(Guid.Empty, Guid.Empty, null), "/api/bookings");

        ThenTheResponseStatusIs(400);
        ThenNoEvents();
    }

    [BobcatSpec(SliceName = "GET /api/bookings")]
    public async Task get_bookings_returns_list()
    {
        var bookingId = Guid.NewGuid();
        await GivenEvents<BookingRecord>(bookingId,
            new BookingCreatedDomainEvent(bookingId, Guid.NewGuid(), "List Test", Guid.NewGuid(), "FL600", 100m, null, null));

        await WhenReading<BookingRecord>("/api/bookings");

        ThenTheResponseStatusIs(200);
        TheBookingsInclude(await TheResponse<List<BookingRecord>>(), bookingId);
    }

    [BobcatSpec(SliceName = "GET /api/bookings/{id}")]
    public async Task get_booking_by_id_returns_booking()
    {
        var bookingId = Guid.NewGuid();
        await GivenEvents<BookingRecord>(bookingId,
            new BookingCreatedDomainEvent(bookingId, Guid.NewGuid(), "ById Test", Guid.NewGuid(), "FL700", 350m, "Test booking", null));

        await WhenReading<BookingRecord>("/api/bookings/{id}", bookingId);

        ThenTheResponseStatusIs(200);
        ThenMatches(await TheResponse<BookingRecord>(), Specify<BookingRecord>()
            .With(x => x.Id, bookingId)
            .With(x => x.PassengerName, "ById Test")
            .With(x => x.FlightNumber, "FL700")
            .With(x => x.Price, 350m));
    }

    [Then("the bookings include {bookingId}")]
    public void TheBookingsInclude(List<BookingRecord> bookings, Guid bookingId)
        => SpecAssert.Fact(bookings.Any(b => b.Id == bookingId),
            $"Booking {bookingId} is not among the {bookings.Count} bookings read.");
}

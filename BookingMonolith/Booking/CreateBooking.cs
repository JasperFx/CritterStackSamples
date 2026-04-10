using FluentValidation;
using Marten;
using Microsoft.AspNetCore.Mvc;
using BookingMonolith;
using Wolverine.Http;

namespace Booking;

public record CreateBooking(Guid PassengerId, Guid FlightId, string? Description)
{
    public class Validator : AbstractValidator<CreateBooking>
    {
        public Validator()
        {
            RuleFor(x => x.PassengerId).NotEmpty();
            RuleFor(x => x.FlightId).NotEmpty();
        }
    }
}

public static class CreateBookingEndpoint
{
    // Validate that both the passenger and flight exist
    public static async Task<ProblemDetails> ValidateAsync(
        CreateBooking command,
        IQuerySession session)
    {
        var passenger = await session.LoadAsync<Passenger.PassengerRecord>(command.PassengerId);
        if (passenger is null)
            return new ProblemDetails { Detail = "Passenger not found", Status = 404 };

        var flight = await session.LoadAsync<Flight.FlightRecord>(command.FlightId);
        if (flight is null)
            return new ProblemDetails { Detail = "Flight not found", Status = 404 };

        return WolverineContinue.NoProblems;
    }

    // Event-sourced: starts a new event stream in Marten.
    // Replaces: gRPC calls to Flight/Passenger + EventStoreDB append + MongoDB projection
    [WolverinePost("/api/bookings")]
    public static async Task<(BookingRecord, BookingCreated)> Post(
        CreateBooking command,
        IDocumentSession session)
    {
        var passenger = await session.LoadAsync<Passenger.PassengerRecord>(command.PassengerId);
        var flight = await session.LoadAsync<Flight.FlightRecord>(command.FlightId);

        var bookingId = Guid.NewGuid();

        var domainEvent = new BookingCreatedDomainEvent(
            bookingId,
            passenger!.Id,
            passenger.Name,
            flight!.Id,
            flight.FlightNumber,
            flight.Price,
            command.Description,
            SeatNumber: null
        );

        // Start an event stream — Marten stores the event and
        // the BookingRecord snapshot is built via Apply methods
        session.Events.StartStream<BookingRecord>(bookingId, domainEvent);

        // Build the snapshot manually for the response
        var booking = new BookingRecord();
        booking.Apply(domainEvent);

        return (booking, new BookingCreated(bookingId, passenger.Id, flight.Id));
    }
}

public static class GetBookingsEndpoint
{
    [WolverineGet("/api/bookings")]
    public static Task<IReadOnlyList<BookingRecord>> Get(IQuerySession session)
        => session.Query<BookingRecord>().ToListAsync();
}

public static class GetBookingByIdEndpoint
{
    [WolverineGet("/api/bookings/{id}")]
    public static BookingRecord? Get(Guid id, [Wolverine.Persistence.Entity] BookingRecord? booking) => booking;
}

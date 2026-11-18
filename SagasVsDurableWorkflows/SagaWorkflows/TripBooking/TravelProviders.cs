using System.Collections.Concurrent;
using Wolverine.ErrorHandling;

namespace SagaWorkflows.TripBooking;

// A provider said no. Retrying won't change its mind, so Wolverine skips the retries
[TerminalFailure]
public class BookingRefusedException(string message) : Exception(message);

// A provider is down. Worth retrying
public class ProviderUnavailableException(string provider) : Exception($"{provider} is unavailable");

/// <summary>The airline, car rental and hotel systems, in memory</summary>
public class TravelProviders
{
    public ConcurrentDictionary<(Booking, string customer), bool> Reservations { get; } = new();

    public ConcurrentBag<(Booking, string customer)> Refusals { get; } = [];
    public ConcurrentBag<(Booking, string customer)> Outages { get; } = [];

    public void Book(Booking booking, string customer)
    {
        if (Outages.Contains((booking, customer))) throw new ProviderUnavailableException(booking.ToString());
        if (Refusals.Contains((booking, customer)))
            throw new BookingRefusedException($"No {booking.ToString().ToLowerInvariant()} available for {customer}");

        Reservations[(booking, customer)] = true;
    }

    // Cancelling something that was never booked is fine: cancellations have to be idempotent, in Restate too
    public void Cancel(Booking booking, string customer) => Reservations.TryRemove((booking, customer), out _);

    public bool HasReservation(Booking booking, string customer) => Reservations.ContainsKey((booking, customer));
}

public static class TravelProviderHandler
{
    public static FlightBooked Handle(BookFlight command, TravelProviders providers)
    {
        providers.Book(Booking.Flight, command.CustomerId);
        return new FlightBooked(command.TripId);
    }

    public static CarBooked Handle(BookCar command, TravelProviders providers)
    {
        providers.Book(Booking.Car, command.CustomerId);
        return new CarBooked(command.TripId);
    }

    public static HotelBooked Handle(BookHotel command, TravelProviders providers)
    {
        providers.Book(Booking.Hotel, command.CustomerId);
        return new HotelBooked(command.TripId);
    }

    public static FlightCancelled Handle(CancelFlight command, TravelProviders providers)
    {
        providers.Cancel(Booking.Flight, command.CustomerId);
        return new FlightCancelled(command.TripId);
    }

    public static CarCancelled Handle(CancelCar command, TravelProviders providers)
    {
        providers.Cancel(Booking.Car, command.CustomerId);
        return new CarCancelled(command.TripId);
    }

    public static HotelCancelled Handle(CancelHotel command, TravelProviders providers)
    {
        providers.Cancel(Booking.Hotel, command.CustomerId);
        return new HotelCancelled(command.TripId);
    }

    public static void Handle(TripBooked _) { }
    public static void Handle(TripCancelled _) { }
}

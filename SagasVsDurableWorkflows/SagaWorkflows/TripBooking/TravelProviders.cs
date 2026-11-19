using System.Collections.Concurrent;

namespace SagaWorkflows.TripBooking;

/// <summary>The airline, car rental and hotel systems, in memory</summary>
public class TravelProviders
{
    public ConcurrentDictionary<(Booking, string customer), bool> Reservations { get; } = new();

    public ConcurrentBag<(Booking, string customer)> Refusals { get; } = [];
    public ConcurrentBag<(Booking, string customer)> Outages { get; } = [];
    public ConcurrentBag<string> NoCars { get; } = [];
    public ConcurrentBag<string> FullHotels { get; } = [];
    public ConcurrentBag<(Booking, string customer)> CancellationRefusals { get; } = [];

    public void Reset()
    {
        Reservations.Clear();
        Refusals.Clear();
        Outages.Clear();
        NoCars.Clear();
        FullHotels.Clear();
        CancellationRefusals.Clear();
    }

    public void Book(Booking booking, string customer)
    {
        if (Outages.Contains((booking, customer))) throw new ProviderUnavailableException(booking.ToString());
        if (booking == Booking.Car && NoCars.Contains(customer)) throw new NoCarsException($"No cars for {customer}");
        if (booking == Booking.Hotel && FullHotels.Contains(customer)) throw new NoRoomsException($"No rooms for {customer}");
        if (Refusals.Contains((booking, customer)))
            throw new BookingRefusedException($"No {booking.ToString().ToLowerInvariant()} available for {customer}");

        Reservations[(booking, customer)] = true;
    }

    // Cancelling something that was never booked is fine: cancellations have to be idempotent, in Restate too
    public void Cancel(Booking booking, string customer)
    {
        if (CancellationRefusals.Contains((booking, customer)))
            throw new CancellationRefusedException($"The {booking.ToString().ToLowerInvariant()} for {customer} cannot be cancelled");

        Reservations.TryRemove((booking, customer), out _);
    }

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

    // No reply of its own: Wolverine's acknowledgement confirms it to the trip
    public static void Handle(CancelHotel command, TravelProviders providers)
        => providers.Cancel(Booking.Hotel, command.CustomerId);

    public static void Handle(TripBooked _) { }
}

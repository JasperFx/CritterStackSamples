using Alba;
using Booking;
using BookingMonolith;
using Flight;
using Identity;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Passenger;

namespace Tests;

public class BookingMonolithTests : IAsyncLifetime
{
    private IAlbaHost _host = null!;

    public async Task InitializeAsync()
    {
        _host = await AlbaHost.For<Program>();
        var store = _host.Services.GetRequiredService<IDocumentStore>();
        await store.Advanced.Clean.DeleteAllDocumentsAsync();
        await store.Advanced.Clean.DeleteAllEventDataAsync();
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    #region Identity

    [Fact]
    public async Task register_user_returns_user_account()
    {
        var result = await _host.Scenario(x =>
        {
            x.Post.Json(new RegisterUser("alice@test.com", "Alice", "Smith", "Password123")).ToUrl("/api/identity/register");
            x.StatusCodeShouldBeOk();
        });

        var user = result.ReadAsJson<UserAccount>();
        Assert.NotNull(user);
        Assert.NotEqual(Guid.Empty, user!.Id);
        Assert.Equal("alice@test.com", user.Email);
        Assert.Equal("Alice", user.FirstName);
        Assert.Equal("Smith", user.LastName);
    }

    [Fact]
    public async Task register_user_validation_rejects_invalid_email()
    {
        await _host.Scenario(x =>
        {
            x.Post.Json(new RegisterUser("bad-email", "Alice", "Smith", "Password123")).ToUrl("/api/identity/register");
            x.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task register_user_validation_rejects_short_password()
    {
        await _host.Scenario(x =>
        {
            x.Post.Json(new RegisterUser("a@b.com", "Alice", "Smith", "short")).ToUrl("/api/identity/register");
            x.StatusCodeShouldBe(400);
        });
    }

    #endregion

    #region Passenger

    [Fact]
    public async Task create_passenger_returns_passenger_record()
    {
        var result = await _host.Scenario(x =>
        {
            x.Post.Json(new CreatePassenger("Bob Jones", "AB123456", PassengerType.Male, 30)).ToUrl("/api/passengers");
            x.StatusCodeShouldBeOk();
        });

        var passenger = result.ReadAsJson<PassengerRecord>();
        Assert.NotNull(passenger);
        Assert.NotEqual(Guid.Empty, passenger!.Id);
        Assert.Equal("Bob Jones", passenger.Name);
        Assert.Equal("AB123456", passenger.PassportNumber);
        Assert.Equal(PassengerType.Male, passenger.Type);
        Assert.Equal(30, passenger.Age);
    }

    [Fact]
    public async Task create_passenger_validation_rejects_empty_name()
    {
        await _host.Scenario(x =>
        {
            x.Post.Json(new CreatePassenger("", "AB123456", PassengerType.Male, 30)).ToUrl("/api/passengers");
            x.StatusCodeShouldBe(400);
        });
    }

    #endregion

    #region Flight

    [Fact]
    public async Task create_flight_returns_flight_record()
    {
        var result = await _host.Scenario(x =>
        {
            x.Post.Json(new CreateFlight(
                "FL100",
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                120,
                299.99m,
                new DateTime(2026, 6, 1, 8, 0, 0),
                new DateTime(2026, 6, 1, 10, 0, 0),
                new DateTime(2026, 6, 1)
            )).ToUrl("/api/flights");
            x.StatusCodeShouldBeOk();
        });

        var flight = result.ReadAsJson<FlightRecord>();
        Assert.NotNull(flight);
        Assert.NotEqual(Guid.Empty, flight!.Id);
        Assert.Equal("FL100", flight.FlightNumber);
        Assert.Equal(299.99m, flight.Price);
    }

    [Fact]
    public async Task create_flight_validation_rejects_zero_price()
    {
        await _host.Scenario(x =>
        {
            x.Post.Json(new CreateFlight(
                "FL100", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                120, 0m,
                new DateTime(2026, 6, 1, 8, 0, 0),
                new DateTime(2026, 6, 1, 10, 0, 0),
                new DateTime(2026, 6, 1)
            )).ToUrl("/api/flights");
            x.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task get_flights_returns_list()
    {
        // Seed a flight
        await _host.Scenario(x =>
        {
            x.Post.Json(new CreateFlight(
                "FL200", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                90, 199.00m,
                new DateTime(2026, 7, 1, 6, 0, 0),
                new DateTime(2026, 7, 1, 7, 30, 0),
                new DateTime(2026, 7, 1)
            )).ToUrl("/api/flights");
        });

        var result = await _host.Scenario(x =>
        {
            x.Get.Url("/api/flights");
            x.StatusCodeShouldBeOk();
        });

        var flights = result.ReadAsJson<List<FlightRecord>>();
        Assert.NotNull(flights);
        Assert.NotEmpty(flights!);
    }

    [Fact]
    public async Task get_flight_by_id_returns_flight()
    {
        var createResult = await _host.Scenario(x =>
        {
            x.Post.Json(new CreateFlight(
                "FL300", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                60, 150.00m,
                new DateTime(2026, 8, 1, 10, 0, 0),
                new DateTime(2026, 8, 1, 11, 0, 0),
                new DateTime(2026, 8, 1)
            )).ToUrl("/api/flights");
        });

        var created = createResult.ReadAsJson<FlightRecord>()!;

        var result = await _host.Scenario(x =>
        {
            x.Get.Url($"/api/flights/{created.Id}");
            x.StatusCodeShouldBeOk();
        });

        var flight = result.ReadAsJson<FlightRecord>();
        Assert.NotNull(flight);
        Assert.Equal(created.Id, flight!.Id);
        Assert.Equal("FL300", flight.FlightNumber);
    }

    [Fact]
    public async Task get_flight_by_id_returns_404_for_missing()
    {
        await _host.Scenario(x =>
        {
            x.Get.Url($"/api/flights/{Guid.NewGuid()}");
            x.StatusCodeShouldBe(404);
        });
    }

    #endregion

    #region Booking

    [Fact]
    public async Task create_booking_with_valid_passenger_and_flight()
    {
        // Create passenger
        var passengerResult = await _host.Scenario(x =>
        {
            x.Post.Json(new CreatePassenger("Jane Doe", "XY789012", PassengerType.Female, 28)).ToUrl("/api/passengers");
        });
        var passenger = passengerResult.ReadAsJson<PassengerRecord>()!;

        // Create flight
        var flightResult = await _host.Scenario(x =>
        {
            x.Post.Json(new CreateFlight(
                "FL400", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                180, 499.99m,
                new DateTime(2026, 9, 1, 14, 0, 0),
                new DateTime(2026, 9, 1, 17, 0, 0),
                new DateTime(2026, 9, 1)
            )).ToUrl("/api/flights");
        });
        var flight = flightResult.ReadAsJson<FlightRecord>()!;

        // Create booking
        var result = await _host.Scenario(x =>
        {
            x.Post.Json(new CreateBooking(passenger.Id, flight.Id, "Window seat please")).ToUrl("/api/bookings");
            x.StatusCodeShouldBeOk();
        });

        var booking = result.ReadAsJson<BookingRecord>();
        Assert.NotNull(booking);
        Assert.NotEqual(Guid.Empty, booking!.Id);
        Assert.Equal(passenger.Id, booking.PassengerId);
        Assert.Equal(flight.Id, booking.FlightId);
        Assert.Equal("FL400", booking.FlightNumber);
        Assert.Equal(499.99m, booking.Price);
        Assert.Equal("Window seat please", booking.Description);
    }

    [Fact]
    public async Task create_booking_returns_400_for_missing_passenger()
    {
        // Create flight only
        var flightResult = await _host.Scenario(x =>
        {
            x.Post.Json(new CreateFlight(
                "FL500", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                90, 200m,
                new DateTime(2026, 10, 1, 8, 0, 0),
                new DateTime(2026, 10, 1, 9, 30, 0),
                new DateTime(2026, 10, 1)
            )).ToUrl("/api/flights");
        });
        var flight = flightResult.ReadAsJson<FlightRecord>()!;

        await _host.Scenario(x =>
        {
            x.Post.Json(new CreateBooking(Guid.NewGuid(), flight.Id, null)).ToUrl("/api/bookings");
            x.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task create_booking_returns_400_for_missing_flight()
    {
        var passengerResult = await _host.Scenario(x =>
        {
            x.Post.Json(new CreatePassenger("Test User", "ZZ000000", PassengerType.Male, 40)).ToUrl("/api/passengers");
        });
        var passenger = passengerResult.ReadAsJson<PassengerRecord>()!;

        await _host.Scenario(x =>
        {
            x.Post.Json(new CreateBooking(passenger.Id, Guid.NewGuid(), null)).ToUrl("/api/bookings");
            x.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task get_bookings_returns_list()
    {
        // Create passenger + flight + booking
        var passengerResult = await _host.Scenario(x =>
        {
            x.Post.Json(new CreatePassenger("List Test", "LT123456", PassengerType.Male, 25)).ToUrl("/api/passengers");
        });
        var passenger = passengerResult.ReadAsJson<PassengerRecord>()!;

        var flightResult = await _host.Scenario(x =>
        {
            x.Post.Json(new CreateFlight(
                "FL600", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                60, 100m,
                new DateTime(2026, 11, 1, 12, 0, 0),
                new DateTime(2026, 11, 1, 13, 0, 0),
                new DateTime(2026, 11, 1)
            )).ToUrl("/api/flights");
        });
        var flight = flightResult.ReadAsJson<FlightRecord>()!;

        await _host.Scenario(x =>
        {
            x.Post.Json(new CreateBooking(passenger.Id, flight.Id, null)).ToUrl("/api/bookings");
        });

        var result = await _host.Scenario(x =>
        {
            x.Get.Url("/api/bookings");
            x.StatusCodeShouldBeOk();
        });

        var bookings = result.ReadAsJson<List<BookingRecord>>();
        Assert.NotNull(bookings);
        Assert.NotEmpty(bookings!);
    }

    [Fact]
    public async Task get_booking_by_id_returns_booking()
    {
        var passengerResult = await _host.Scenario(x =>
        {
            x.Post.Json(new CreatePassenger("ById Test", "BI123456", PassengerType.Female, 35)).ToUrl("/api/passengers");
        });
        var passenger = passengerResult.ReadAsJson<PassengerRecord>()!;

        var flightResult = await _host.Scenario(x =>
        {
            x.Post.Json(new CreateFlight(
                "FL700", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                120, 350m,
                new DateTime(2026, 12, 1, 9, 0, 0),
                new DateTime(2026, 12, 1, 11, 0, 0),
                new DateTime(2026, 12, 1)
            )).ToUrl("/api/flights");
        });
        var flight = flightResult.ReadAsJson<FlightRecord>()!;

        var bookingResult = await _host.Scenario(x =>
        {
            x.Post.Json(new CreateBooking(passenger.Id, flight.Id, "Test booking")).ToUrl("/api/bookings");
        });
        var created = bookingResult.ReadAsJson<BookingRecord>()!;

        var result = await _host.Scenario(x =>
        {
            x.Get.Url($"/api/bookings/{created.Id}");
            x.StatusCodeShouldBeOk();
        });

        var booking = result.ReadAsJson<BookingRecord>();
        Assert.NotNull(booking);
        Assert.Equal(created.Id, booking!.Id);
    }

    [Fact]
    public async Task create_booking_validation_rejects_empty_ids()
    {
        await _host.Scenario(x =>
        {
            x.Post.Json(new CreateBooking(Guid.Empty, Guid.Empty, null)).ToUrl("/api/bookings");
            x.StatusCodeShouldBe(400);
        });
    }

    #endregion
}

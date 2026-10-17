using Alba;
using BookingMonolith;
using Flight;
using Identity;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Passenger;
using Shouldly;

namespace Tests;

// The original tests, on xUnit v3. The event-sourced Booking module's six tests now live in BookingSpecs as
// Bobcat specifications; these cover the document-only Identity, Passenger and Flight modules.
public class BookingMonolithTests : IAsyncLifetime
{
    private IAlbaHost _host = null!;

    public async ValueTask InitializeAsync()
    {
        _host = await AlbaHost.For<Program>();
        var store = _host.Services.GetRequiredService<IDocumentStore>();
        await store.Advanced.Clean.DeleteAllDocumentsAsync();
        await store.Advanced.Clean.DeleteAllEventDataAsync();
    }

    public async ValueTask DisposeAsync() => await _host.DisposeAsync();

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
        user.ShouldNotBeNull();
        user!.Id.ShouldNotBe(Guid.Empty);
        user.Email.ShouldBe("alice@test.com");
        user.FirstName.ShouldBe("Alice");
        user.LastName.ShouldBe("Smith");
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

        var passenger = result.ReadAsJson<Passenger.Passenger>();
        passenger.ShouldNotBeNull();
        passenger!.Id.ShouldNotBe(Guid.Empty);
        passenger.Name.ShouldBe("Bob Jones");
        passenger.PassportNumber.ShouldBe("AB123456");
        passenger.Type.ShouldBe(PassengerType.Male);
        passenger.Age.ShouldBe(30);
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

        var flight = result.ReadAsJson<Flight.Flight>();
        flight.ShouldNotBeNull();
        flight!.Id.ShouldNotBe(Guid.Empty);
        flight.FlightNumber.ShouldBe("FL100");
        flight.Price.ShouldBe(299.99m);
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

        var flights = result.ReadAsJson<List<Flight.Flight>>();
        flights.ShouldNotBeNull();
        flights!.ShouldNotBeEmpty();
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

        var created = createResult.ReadAsJson<Flight.Flight>()!;

        var result = await _host.Scenario(x =>
        {
            x.Get.Url($"/api/flights/{created.Id}");
            x.StatusCodeShouldBeOk();
        });

        var flight = result.ReadAsJson<Flight.Flight>();
        flight.ShouldNotBeNull();
        flight!.Id.ShouldBe(created.Id);
        flight.FlightNumber.ShouldBe("FL300");
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
}

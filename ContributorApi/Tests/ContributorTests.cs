using Alba;
using ContributorApi;
using Marten;
using Shouldly;

namespace ContributorApi.Tests;

public class ContributorTests : IAsyncLifetime
{
    private IAlbaHost _host = null!;

    public async Task InitializeAsync()
    {
        _host = await AlbaHost.For<Program>();
        await _host.CleanAllMartenDataAsync();
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private async Task<Contributor> CreateTestContributor(string name = "Jane Doe", string? phone = null)
    {
        var result = await _host.Scenario(s =>
        {
            s.Post.Json(new CreateContributor(name, phone != null ? "1" : null, phone, null))
                .ToUrl("/api/contributors");
            s.StatusCodeShouldBeOk();
        });
        return result.ReadAsJson<Contributor>()!;
    }

    [Fact]
    public async Task Can_create_contributor()
    {
        var contributor = await CreateTestContributor("Alice Smith", "5551234567");

        contributor.ShouldNotBeNull();
        contributor.Name.ShouldBe("Alice Smith");
        contributor.PhoneNumber.ShouldNotBeNull();
        contributor.PhoneNumber!.Number.ShouldBe("5551234567");
    }

    [Fact]
    public async Task Create_rejects_empty_name()
    {
        await _host.Scenario(s =>
        {
            s.Post.Json(new CreateContributor("", null, null, null)).ToUrl("/api/contributors");
            s.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task Create_rejects_short_name()
    {
        await _host.Scenario(s =>
        {
            s.Post.Json(new CreateContributor("A", null, null, null)).ToUrl("/api/contributors");
            s.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task Create_rejects_duplicate_name()
    {
        await CreateTestContributor("Duplicate");

        await _host.Scenario(s =>
        {
            s.Post.Json(new CreateContributor("Duplicate", null, null, null)).ToUrl("/api/contributors");
            s.StatusCodeShouldBe(409);
        });
    }

    [Fact]
    public async Task Can_get_all_contributors()
    {
        await CreateTestContributor("Zara First");
        await CreateTestContributor("Amy Second");

        var result = await _host.Scenario(s =>
        {
            s.Get.Url("/api/contributors");
            s.StatusCodeShouldBeOk();
        });

        var contributors = result.ReadAsJson<Contributor[]>();
        contributors.ShouldNotBeNull();
        contributors!.Length.ShouldBeGreaterThanOrEqualTo(2);
        // Ordered by name
        contributors[0].Name.ShouldBe("Amy Second");
    }

    [Fact]
    public async Task Get_by_id_returns_contributor()
    {
        var created = await CreateTestContributor("ById Test");

        var result = await _host.Scenario(s =>
        {
            s.Get.Url($"/api/contributors/{created.Id}");
            s.StatusCodeShouldBeOk();
        });

        var fetched = result.ReadAsJson<Contributor>();
        fetched.ShouldNotBeNull();
        fetched!.Id.ShouldBe(created.Id);
        fetched.Name.ShouldBe("ById Test");
    }

    [Fact]
    public async Task Get_by_id_returns_404_for_missing()
    {
        await _host.Scenario(s =>
        {
            s.Get.Url("/api/contributors/999999");
            s.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task Can_update_contributor()
    {
        var created = await CreateTestContributor("Original Name");

        var result = await _host.Scenario(s =>
        {
            s.Put.Json(new UpdateContributor(created.Id, "Updated Name"))
                .ToUrl($"/api/contributors/{created.Id}");
            s.StatusCodeShouldBeOk();
        });

        var updated = result.ReadAsJson<Contributor>();
        updated.ShouldNotBeNull();
        updated!.Name.ShouldBe("Updated Name");
        updated.UpdatedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Update_returns_404_for_missing()
    {
        await _host.Scenario(s =>
        {
            s.Put.Json(new UpdateContributor(999999, "Nope"))
                .ToUrl("/api/contributors/999999");
            s.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task Can_delete_contributor()
    {
        var created = await CreateTestContributor("To Delete");

        await _host.Scenario(s =>
        {
            s.Delete.Url($"/api/contributors/{created.Id}");
            s.StatusCodeShouldBe(204);
        });

        // Verify deleted
        await _host.Scenario(s =>
        {
            s.Get.Url($"/api/contributors/{created.Id}");
            s.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task Delete_returns_404_for_missing()
    {
        await _host.Scenario(s =>
        {
            s.Delete.Url("/api/contributors/999999");
            s.StatusCodeShouldBe(404);
        });
    }
}

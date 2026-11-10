using Alba;
using ContributorApi;
using Marten;
using Shouldly;

namespace ContributorApi.Tests;

// The original tests, on xUnit v3. Five of them now live in ContributorSpecs as Bobcat specifications;
// the validation and 404 checks stay plain tests: five specifications are enough to show the pattern.
public class ContributorTests : IAsyncLifetime
{
    private IAlbaHost _host = null!;

    public async ValueTask InitializeAsync()
    {
        _host = await AlbaHost.For<Program>();
        await _host.CleanAllMartenDataAsync();
    }

    public async ValueTask DisposeAsync() => await _host.DisposeAsync();

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
    public async Task Delete_returns_404_for_missing()
    {
        await _host.Scenario(s =>
        {
            s.Delete.Url("/api/contributors/999999");
            s.StatusCodeShouldBe(404);
        });
    }
}

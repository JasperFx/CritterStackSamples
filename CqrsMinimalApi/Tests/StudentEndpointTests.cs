using Alba;
using CqrsMinimalApi;
using Marten;
using Shouldly;

namespace CqrsMinimalApi.Tests;

// The original tests, on xUnit v3. Four of them now live in StudentSpecs as Bobcat specifications;
// the 404 check stays a plain test: four specifications are enough to show the pattern.
public class StudentEndpointTests : IAsyncLifetime
{
    private IAlbaHost _host = null!;

    public async ValueTask InitializeAsync()
    {
        _host = await AlbaHost.For<Program>();
        await _host.CleanAllMartenDataAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _host.DisposeAsync();
    }

    [Fact]
    public async Task Get_by_id_returns_404_for_missing()
    {
        await _host.Scenario(s =>
        {
            s.Get.Url("/student/get-by-id?id=999999");
            s.StatusCodeShouldBe(404);
        });
    }
}

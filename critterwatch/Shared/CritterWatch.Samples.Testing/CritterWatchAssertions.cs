using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace CritterWatch.Samples.Testing;

/// <summary>
/// Route-smoke assertion helpers for the CritterWatch HTTP surface, used by every sample solution's
/// <c>Tests/</c> battery. They operate on a plain <see cref="HttpClient"/> — typically the one from
/// <see cref="CritterWatchAppHostFixture{TAppHost}.CreateCritterWatchClient"/>.
/// </summary>
public static class CritterWatchAssertions
{
    // The store endpoints serialize with JsonSerializerDefaults.Web (camelCase) — see
    // CritterWatch's StoreJsonResults. So ServiceSummary.Id arrives on the wire as "id".
    // Web defaults already imply case-insensitive property matching, but we set it explicitly
    // so the parse is robust regardless of any future casing tweak upstream.
    private static readonly JsonSerializerOptions ServicesJsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Asserts <c>GET /api/critterwatch/about</c> returns HTTP 200. This is the cheapest liveness check
    /// on the console: it depends only on the console's own wiring, not on any monitored service having
    /// registered yet.
    /// </summary>
    public static async Task CritterWatchAboutOk(this HttpClient client)
    {
        using var response = await client.GetAsync("/api/critterwatch/about");
        response.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            $"GET /api/critterwatch/about should return 200 but returned {(int)response.StatusCode}.");
    }

    /// <summary>
    /// Polls <c>GET /api/critterwatch/services</c> until every name in <paramref name="expectedNames"/>
    /// appears in the registered-service list, then returns the full parsed list. Sample services
    /// self-register with the console <b>asynchronously</b> (they connect over a transport control
    /// channel after the AppHost reports the resource Running), so a single read races the registration
    /// — this method polls instead.
    /// </summary>
    /// <param name="client">An <see cref="HttpClient"/> targeting the <c>critterwatch</c> resource.</param>
    /// <param name="expectedNames">
    /// The service names (matched against each <see cref="ServiceSummaryDto.Id"/>) that must all be present.
    /// </param>
    /// <param name="timeout">Overall ceiling for the poll loop. Defaults to 2 minutes.</param>
    /// <param name="pollInterval">Delay between polls. Defaults to 2 seconds.</param>
    /// <returns>The parsed service list once all expected names are present.</returns>
    /// <exception cref="TimeoutException">
    /// Thrown if the expected names have not all registered before <paramref name="timeout"/> elapses;
    /// the message lists which names were still missing and what was actually seen.
    /// </exception>
    public static async Task<IReadOnlyList<ServiceSummaryDto>> WaitForServicesAsync(
        this HttpClient client,
        IEnumerable<string> expectedNames,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null)
    {
        var expected = expectedNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var deadline = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromMinutes(2));
        var interval = pollInterval ?? TimeSpan.FromSeconds(2);

        IReadOnlyList<ServiceSummaryDto> latest = [];

        while (true)
        {
            latest = await FetchServicesAsync(client);

            var seen = latest.Select(s => s.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (expected.All(seen.Contains))
            {
                return latest;
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                var missing = string.Join(", ", expected.Where(name => !seen.Contains(name)));
                var actual = seen.Count == 0 ? "(none)" : string.Join(", ", seen);
                throw new TimeoutException(
                    $"Timed out waiting for CritterWatch services to register. Missing: [{missing}]. Registered: [{actual}].");
            }

            await Task.Delay(interval);
        }
    }

    private static async Task<IReadOnlyList<ServiceSummaryDto>> FetchServicesAsync(HttpClient client)
    {
        try
        {
            var services = await client.GetFromJsonAsync<List<ServiceSummaryDto>>(
                "/api/critterwatch/services", ServicesJsonOptions);
            return services ?? [];
        }
        catch (HttpRequestException)
        {
            // The console may briefly 5xx while it warms up its own store/projection. Treat a
            // transient failure as "no services yet" and let the poll loop retry until the deadline.
            return [];
        }
    }
}

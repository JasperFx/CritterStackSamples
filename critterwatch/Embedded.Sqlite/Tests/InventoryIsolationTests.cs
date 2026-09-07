using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Shouldly;
using Xunit;

namespace Tests;

/// <summary>
/// The claims the embedded docs make, asserted against the running sample.
/// </summary>
/// <remarks>
/// <para>
/// A sample is only worth committing if it PROVES what it demonstrates. Each test here maps to a
/// sentence on <c>docs/deployment/embedded.md</c>, so if the shape of embedded mode changes, the
/// sample fails rather than quietly becoming a lie.
/// </para>
/// </remarks>
public class InventoryIsolationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public InventoryIsolationTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private static string HostDatabase =>
        Path.Combine(Path.GetTempPath(), "critterwatch-embedded-sample", "inventory.db");

    private static string ConsoleDatabase =>
        Path.Combine(Path.GetTempPath(), "critterwatch-embedded-sample", "critterwatch.db");

    private static string[] TablesIn(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select name from sqlite_master where type = 'table' order by name";
        using var reader = command.ExecuteReader();

        var names = new List<string>();
        while (reader.Read()) names.Add(reader.GetString(0));
        return names.ToArray();
    }

    [Fact]
    public async Task the_hosts_own_endpoints_still_work()
    {
        var client = _factory.CreateClient();

        var received = await client.PostAsJsonAsync("/inventory/receive",
            new { ProductId = "widget", Name = "Widget", Quantity = 5 });
        received.EnsureSuccessStatusCode();

        var product = await client.GetFromJsonAsync<Dictionary<string, object>>("/inventory/widget");
        product.ShouldNotBeNull();
    }

    [Fact]
    public async Task the_consoles_api_answers_under_its_own_prefix()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/critterwatch/services");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// #1139 — the assertion that could not live in CritterWatch's own suite.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>In CritterWatch's test projects this would pass vacuously.</b> <c>EmbedFrontend</c>
    /// defaults to false there, so an ordinary build ships no SPA and the fallback middleware
    /// no-ops — a test asserting "an unmatched host route still 404s" would pass identically
    /// whether the console mounted at a sub-path or at the ROOT, which is the one thing it exists
    /// to detect. This sample consumes the PACKED console, which has a real embedded SPA, so the
    /// assertion means something here and only here.
    /// </remarks>
    [Fact]
    public async Task the_console_does_not_take_over_the_hosts_route_table()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/definitely-not-a-route");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound,
            "the console's SPA fallback must answer only under its own prefix. If this returns 200 " +
            "it is serving the console's index.html for every unmatched host route, which turns " +
            "every 404 in the host application into a blank console page.");
    }

    /// <summary>
    /// The positive half that keeps <see cref="the_console_does_not_take_over_the_hosts_route_table"/>
    /// honest: the console's SPA must actually be mounted and serving.
    /// </summary>
    /// <remarks>
    /// ⚠️ Without this, the 404 test passes just as happily against a package built with no embedded
    /// SPA at all — the fallback middleware would no-op and every unmatched route would 404 for the
    /// wrong reason. Together the two say "the SPA is served HERE and nowhere else"; separately
    /// neither says anything.
    /// </remarks>
    [Fact]
    public async Task the_consoles_SPA_is_actually_mounted()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/critterwatch");

        response.StatusCode.ShouldBe(HttpStatusCode.OK,
            "the console's UI must be served under its own prefix — the package is built with the " +
            "SPA embedded, so a 404 here means the mount is broken, not that the SPA is absent");

        var body = await response.Content.ReadAsStringAsync();
        body.Contains("<div id=\"app\"", StringComparison.Ordinal).ShouldBeTrue(
            "and it must be the SPA's shell, not an API response that happens to be 200");
    }

    [Fact]
    public async Task the_consoles_documents_live_in_its_OWN_database()
    {
        var client = _factory.CreateClient();

        // Seed BOTH sides from this test rather than relying on another one having run: xUnit gives
        // no ordering, and a test whose control depends on a sibling is a test that fails for the
        // wrong reason. The write creates the host's document table; the read exercises the console.
        (await client.PostAsJsonAsync("/inventory/receive",
            new { ProductId = "isolation-probe", Name = "Probe", Quantity = 1 }))
            .EnsureSuccessStatusCode();
        await client.GetAsync("/api/critterwatch/services");

        var hostTables = TablesIn(HostDatabase);
        var consoleTables = TablesIn(ConsoleDatabase);

        consoleTables.ShouldContain(t => t.StartsWith("critterwatch_"),
            "the console's documents and events are prefixed and live in its own file");

        hostTables.ShouldNotContain(t => t.StartsWith("critterwatch_"),
            "and none of them are in the host's database — this is the isolation claim");

        // The host's own document table is the control: without it, "the host has no critterwatch_
        // tables" would also pass against a host database that was empty because nothing worked.
        hostTables.ShouldContain(t => t.Contains("product", StringComparison.OrdinalIgnoreCase),
            "control — the host's own store must be in use, or the assertion above is vacuous");
    }

    /// <summary>
    /// Documents the durability arrangement rather than wishing it away — see
    /// <c>docs/deployment/embedded.md</c>, corrected 2026-09-07.
    /// </summary>
    /// <remarks>
    /// The console gets its OWN <c>wolverine_*</c> envelope tables unless the host has set
    /// <c>opts.Durability.MessageStorageSchemaName</c> (a different property from the same-named one
    /// on the store integration). This sample deliberately does not set it, because that is the
    /// ordinary shape — so the second set is expected here, and asserting it is what stops the docs
    /// drifting back to claiming otherwise.
    /// </remarks>
    [Fact]
    public void the_console_has_its_own_envelope_tables_and_that_is_documented()
    {
        var consoleTables = TablesIn(ConsoleDatabase);

        consoleTables.ShouldContain(t => t.StartsWith("wolverine_"),
            "expected: a host that has not set opts.Durability.MessageStorageSchemaName gives the " +
            "console its own envelope tables. If this ever goes away the docs page is out of date — " +
            "it currently tells operators to sweep every store, not just the main one.");
    }
}

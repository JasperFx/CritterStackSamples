namespace Fleet.Common;

/// <summary>
/// Resolves the fleet's single infrastructure connection — the Postgres database — with a localhost
/// docker-compose fallback.
///
/// <para>
/// This sample has <b>no broker</b>. The Wolverine transport IS the database: the same Postgres holds
/// every service's event store AND the Wolverine DB-backed queue tables (including the shared
/// <c>critterwatch</c> control queue). So a single connection string is all the glue any project needs.
/// </para>
///
/// <para>
/// Under Aspire, the AppHost wires each project resource with <c>.WithReference(db)</c>, which injects
/// <c>ConnectionStrings__critterstore</c> into the child process's <b>environment</b>. We read that env
/// var directly (rather than via <c>IConfiguration</c>) because Wolverine's <c>UseWolverine(...)</c>
/// callback doesn't hand us a configuration object — the env var is the lowest-common-denominator source
/// that works the same in every service's <c>Program.cs</c>.
/// </para>
///
/// <para>
/// Run a project on its own (no Aspire) and that env var is absent, so we fall back to the Postgres the
/// repo's <c>docker compose</c> stack exposes on localhost — keeping every project independently
/// F5-runnable.
/// </para>
/// </summary>
public static class SampleConnections
{
    // ASP.NET Core's "ConnectionStrings:<name>" config maps to the "ConnectionStrings__<name>" env var,
    // which is exactly what Aspire's .WithReference(...) injects. Matches the AppHost's
    // `postgres.AddDatabase("critterstore", ...)` resource name (the console PROJECT owns "critterwatch",
    // so the DB resource — and thus this env-var key — is "critterstore").
    private const string PostgresEnvVar = "ConnectionStrings__critterstore";

    // The localhost fallback matches the port the CritterWatch docker-compose stack publishes.
    private const string PostgresFallback =
        "Host=localhost;Port=5432;Database=critterwatch;Username=postgres;Password=postgres";

    /// <summary>
    /// The Postgres connection string. It backs BOTH the event store AND the Wolverine DB-backed queues.
    /// Aspire's <c>critterstore</c> DB wins; else the localhost docker-compose Postgres.
    /// </summary>
    public static string Postgres() =>
        Environment.GetEnvironmentVariable(PostgresEnvVar) ?? PostgresFallback;
}

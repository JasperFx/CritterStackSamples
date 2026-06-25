namespace Fleet.Common;

/// <summary>
/// Resolves the WebService.Http sample's two pieces of infrastructure glue:
/// <list type="bullet">
///   <item>the Postgres connection string (the console's own store AND the order service's event store), and</item>
///   <item>the CritterWatch console's base HTTP URL — the address the order service POSTs its telemetry to,
///         because here the monitoring link rides <b>Wolverine's HTTP transport</b> rather than a broker.</item>
/// </list>
/// Both come from the Aspire-injected environment with a localhost docker-compose fallback so every project
/// stays independently F5-runnable.
/// </summary>
/// <remarks>
/// <para>
/// <b>Postgres.</b> The AppHost wires each project with <c>.WithReference(db)</c>, injecting
/// <c>ConnectionStrings__critterstore</c> (the DB-resource name — see the AppHost comment on why it isn't
/// <c>critterwatch</c>). We read the env var directly rather than via <c>IConfiguration</c> because
/// Wolverine's <c>UseWolverine(...)</c> callback hands us no configuration object.
/// </para>
/// <para>
/// <b>Console base URL.</b> The AppHost wires the order service with <c>.WithReference(console)</c>, which
/// injects an Aspire <i>service-discovery</i> variable for the console's <c>http</c> endpoint —
/// <c>services__critterwatch__http__0</c>. That is the URL the HTTP transport sender targets. Standalone
/// (no Aspire) the variable is absent and we fall back to the console's localhost launch URL.
/// </para>
/// </remarks>
public static class SampleConnections
{
    // ASP.NET Core's "ConnectionStrings:<name>" maps to "ConnectionStrings__<name>", which is what Aspire's
    // .WithReference(db) injects. The DB resource is named "critterstore" (the console PROJECT owns the name
    // "critterwatch"), so the env-var key — and this constant — is "critterstore".
    private const string PostgresEnvVar = "ConnectionStrings__critterstore";

    // Aspire's service-discovery env shape for a referenced project resource named "critterwatch" with an
    // endpoint named "http". .WithReference(console) injects this into the order service's environment.
    private const string ConsoleHttpEnvVar = "services__critterwatch__http__0";

    private const string PostgresFallback =
        "Host=localhost;Port=5432;Database=critterwatch;Username=postgres;Password=postgres";

    // Matches the console's launchSettings http profile (CritterWatchConsole/Properties/launchSettings.json).
    private const string ConsoleHttpFallback = "http://localhost:5290";

    /// <summary>The Postgres connection string. Aspire's <c>critterstore</c> DB wins; else localhost.</summary>
    public static string Postgres() =>
        Environment.GetEnvironmentVariable(PostgresEnvVar) ?? PostgresFallback;

    /// <summary>
    /// The CritterWatch console's base URL (scheme + host + port, no trailing slash). Aspire's
    /// <c>services__critterwatch__http__0</c> wins; else the console's localhost launch URL.
    /// </summary>
    public static string ConsoleBaseUrl() =>
        (Environment.GetEnvironmentVariable(ConsoleHttpEnvVar) ?? ConsoleHttpFallback).TrimEnd('/');
}

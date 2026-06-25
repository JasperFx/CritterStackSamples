namespace Fleet.Common;

/// <summary>
/// Resolves the fleet's two infrastructure connections — the Azure Service Bus namespace and the Postgres
/// event store — with a localhost / emulator fallback.
///
/// <para>
/// Under Aspire, the AppHost wires each project resource with <c>.WithReference(serviceBus)</c> /
/// <c>.WithReference(db)</c>, which injects <c>ConnectionStrings__messaging</c> and
/// <c>ConnectionStrings__critterstore</c> into the child process's <b>environment</b>. We read those env
/// vars directly (rather than via <c>IConfiguration</c>) because Wolverine's <c>UseWolverine(...)</c>
/// callback doesn't hand us a configuration object — the env var is the lowest-common-denominator source
/// that works the same in every service's <c>Program.cs</c>.
/// </para>
///
/// <para>
/// Run a project on its own (no Aspire) and those env vars are absent, so we fall back to the Postgres
/// the repo's <c>docker compose</c> stack exposes on localhost, and to the well-known Azure Service Bus
/// <b>development emulator</b> connection string — keeping every project independently F5-runnable against
/// a manually-started emulator.
/// </para>
/// </summary>
public static class SampleConnections
{
    // ASP.NET Core's "ConnectionStrings:<name>" config maps to the "ConnectionStrings__<name>" env var,
    // which is exactly what Aspire's .WithReference(...) injects.
    //
    // The ASB env-var key follows the AppHost's `builder.AddAzureServiceBus("messaging")` resource name
    // ("messaging"); the Postgres key follows `postgres.AddDatabase("critterstore", ...)` (the console
    // PROJECT owns "critterwatch", so the DB resource — and thus this key — is "critterstore").
    private const string AzureServiceBusEnvVar = "ConnectionStrings__messaging";
    private const string PostgresEnvVar = "ConnectionStrings__critterstore";

    // The localhost fallback matches the port the CritterWatch docker-compose stack publishes.
    private const string PostgresFallback =
        "Host=localhost;Port=5432;Database=critterwatch;Username=postgres;Password=postgres";

    // Well-known Azure Service Bus *development emulator* connection string. UseDevelopmentEmulator=true is
    // what tells the .NET ServiceBusClient to skip the production SAS-validation handshake. The standalone
    // fallback assumes a manually-started emulator on the default localhost AMQP port (5672). Under Aspire
    // this is never used — Aspire's `.RunAsEmulator()` injects the real per-run endpoint/port via the env
    // var above.
    private const string AzureServiceBusFallback =
        "Endpoint=sb://localhost:5672;" +
        "SharedAccessKeyName=RootManageSharedAccessKey;" +
        "SharedAccessKey=SAS_KEY_VALUE;" +
        "UseDevelopmentEmulator=true;";

    /// <summary>
    /// The Azure Service Bus connection string. Aspire's <c>messaging</c> reference (the emulator) wins;
    /// else the well-known development-emulator literal.
    /// </summary>
    public static string AzureServiceBus() =>
        Environment.GetEnvironmentVariable(AzureServiceBusEnvVar) ?? AzureServiceBusFallback;

    /// <summary>The Postgres connection string for the event store. Aspire's <c>critterstore</c> DB wins; else localhost.</summary>
    public static string Postgres() =>
        Environment.GetEnvironmentVariable(PostgresEnvVar) ?? PostgresFallback;
}

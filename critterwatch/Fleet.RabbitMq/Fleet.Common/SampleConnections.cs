namespace Fleet.Common;

/// <summary>
/// Resolves the fleet's two infrastructure connections — the RabbitMQ broker and the Postgres event
/// store — with a localhost docker-compose fallback.
///
/// <para>
/// Under Aspire, the AppHost wires each project resource with <c>.WithReference(rabbit)</c> /
/// <c>.WithReference(db)</c>, which injects <c>ConnectionStrings__rabbitmq</c> and
/// <c>ConnectionStrings__critterwatch</c> into the child process's <b>environment</b>. We read those env
/// vars directly (rather than via <c>IConfiguration</c>) because Wolverine's <c>UseWolverine(...)</c>
/// callback doesn't hand us a configuration object — the env var is the lowest-common-denominator source
/// that works the same in every service's <c>Program.cs</c>.
/// </para>
///
/// <para>
/// Run a project on its own (no Aspire) and those env vars are absent, so we fall back to the broker/db
/// the repo's <c>docker compose</c> stack exposes on localhost — keeping every project independently
/// F5-runnable.
/// </para>
/// </summary>
public static class SampleConnections
{
    // ASP.NET Core's "ConnectionStrings:<name>" config maps to the "ConnectionStrings__<name>" env var,
    // which is exactly what Aspire's .WithReference(...) injects.
    private const string RabbitEnvVar = "ConnectionStrings__rabbitmq";
    // Matches the AppHost's `postgres.AddDatabase("critterstore", ...)` resource name (the console PROJECT
    // owns "critterwatch", so the DB resource — and thus this env-var key — is "critterstore").
    private const string PostgresEnvVar = "ConnectionStrings__critterstore";

    // The localhost fallbacks match the ports the CritterWatch docker-compose stack publishes.
    private const string RabbitFallback = "amqp://guest:guest@localhost:5672";
    private const string PostgresFallback =
        "Host=localhost;Port=5432;Database=critterwatch;Username=postgres;Password=postgres";

    /// <summary>The RabbitMQ broker URI. Aspire's <c>rabbitmq</c> reference wins; else the localhost broker.</summary>
    public static Uri RabbitMq() =>
        new(Environment.GetEnvironmentVariable(RabbitEnvVar) ?? RabbitFallback);

    /// <summary>The Postgres connection string for the event store. Aspire's <c>critterwatch</c> DB wins; else localhost.</summary>
    public static string Postgres() =>
        Environment.GetEnvironmentVariable(PostgresEnvVar) ?? PostgresFallback;
}

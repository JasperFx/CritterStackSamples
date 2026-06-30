namespace Fleet.Common;

/// <summary>
/// Resolves the fleet's two infrastructure connections — the Google Cloud Pub/Sub project (reached via the
/// local emulator) and the Postgres event store — with a localhost / emulator fallback.
///
/// <para>
/// Under Aspire, the AppHost runs the Pub/Sub emulator as a container and injects
/// <c>PUBSUB_EMULATOR_HOST</c> (host:port of the emulator) into every project resource's <b>environment</b>.
/// The Google Pub/Sub client library reads that variable directly when Wolverine's transport has emulator
/// detection enabled (<c>UsePubsub(projectId).UseEmulatorDetection(...)</c>), so there is no Pub/Sub
/// "connection string" to thread through — only the <see cref="ProjectId"/> (which must match the
/// <c>--project</c> the AppHost starts the emulator with). The Postgres reference is injected as
/// <c>ConnectionStrings__critterstore</c>, read directly (not via <c>IConfiguration</c>) because Wolverine's
/// <c>UseWolverine(...)</c> callback hands us no configuration object.
/// </para>
///
/// <para>
/// Run a project on its own (no Aspire) and those env vars are absent, so we fall back to the Postgres the
/// repo's <c>docker compose</c> stack exposes on localhost and to a manually-started Pub/Sub emulator on the
/// default localhost port (8085) — keeping every project independently F5-runnable. (Set
/// <c>PUBSUB_EMULATOR_HOST=localhost:8085</c> in that standalone case.)
/// </para>
/// </summary>
public static class SampleConnections
{
    // The GCP project id the whole fleet shares. The Pub/Sub emulator is project-agnostic (it accepts any
    // id), but every participant — emulator container + console + services — MUST use the SAME id so they
    // address the same topics/subscriptions. The AppHost starts the emulator with `--project=<this>`.
    public const string ProjectId = "critterwatch-sample";

    // ASP.NET Core's "ConnectionStrings:<name>" config maps to the "ConnectionStrings__<name>" env var,
    // which Aspire's .WithReference(db) injects. The DB resource is "critterstore" (the console PROJECT owns
    // the name "critterwatch"), so the key — and this constant — is "critterstore".
    private const string PostgresEnvVar = "ConnectionStrings__critterstore";

    // The localhost fallback matches the port the CritterWatch docker-compose stack publishes.
    private const string PostgresFallback =
        "Host=localhost;Port=5432;Database=critterwatch;Username=postgres;Password=postgres";

    /// <summary>
    /// The GCP project id every participant addresses Pub/Sub topics under. Reads <c>PUBSUB_PROJECT_ID</c>
    /// if the AppHost set it (kept in sync with the emulator's <c>--project</c>); else the shared default.
    /// </summary>
    public static string PubsubProjectId() =>
        Environment.GetEnvironmentVariable("PUBSUB_PROJECT_ID") ?? ProjectId;

    /// <summary>The Postgres connection string for the event store. Aspire's <c>critterstore</c> DB wins; else localhost.</summary>
    public static string Postgres() =>
        Environment.GetEnvironmentVariable(PostgresEnvVar) ?? PostgresFallback;
}

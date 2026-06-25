namespace Fleet.Common;

/// <summary>
/// Resolves the fleet's single infrastructure connection — the SQL Server that is simultaneously the
/// Polecat event store AND the Wolverine database-backed queue transport — with a localhost
/// docker-compose fallback.
///
/// <para>
/// Under Aspire, the AppHost wires each project resource with <c>.WithReference(db)</c>, which injects
/// <c>ConnectionStrings__critterstore</c> into the child process's <b>environment</b> (the resource name
/// is "critterstore", NOT "critterwatch" — the console PROJECT owns the name "critterwatch", and Aspire
/// resource names are unique case-insensitive across types). We read that env var directly (rather than
/// via <c>IConfiguration</c>) because Wolverine's <c>UseWolverine(...)</c> callback doesn't hand us a
/// configuration object — the env var is the lowest-common-denominator source that works the same in
/// every service's <c>Program.cs</c>.
/// </para>
///
/// <para>
/// Run a project on its own (no Aspire) and that env var is absent, so we fall back to the SQL Server the
/// CritterWatch <c>docker compose</c> stack exposes on host port 1443 (CritterWatch runs SQL Server on
/// 1443, not the default 1433, so it doesn't collide with other projects on the same box) — keeping every
/// project independently F5-runnable.
/// </para>
///
/// <para>
/// <b>Why SQL Server 2025?</b> Polecat stores event payloads in the native <c>json</c> column type, which
/// only exists in SQL Server 2025 (v17). Older engines (incl. Azure SQL Edge) fail Polecat schema
/// creation with <c>SqlException 2715 "Cannot find data type json"</c>. The AppHost pins the
/// <c>2025-latest</c> image for exactly this reason.
/// </para>
/// </summary>
public static class SampleConnections
{
    // ASP.NET Core's "ConnectionStrings:<name>" config maps to the "ConnectionStrings__<name>" env var,
    // which is exactly what Aspire's .WithReference(...) injects. Matches the AppHost's
    // `sqlserver.AddDatabase("critterstore", ...)` resource name.
    private const string SqlServerEnvVar = "ConnectionStrings__critterstore";

    // The localhost fallback matches the host port + SA password the CritterWatch docker-compose stack
    // publishes (SQL Server on 1443; password P@55w0rd; TrustServerCertificate for the dev cert).
    private const string SqlServerFallback =
        "Server=localhost,1443;Database=critterwatch;User Id=sa;Password=P@55w0rd;Encrypt=False;TrustServerCertificate=True";

    /// <summary>
    /// The SQL Server connection string. Aspire's <c>critterstore</c> database reference wins; else the
    /// localhost docker-compose SQL Server. Used for BOTH the Polecat event store and the Wolverine
    /// database-queue transport in every project.
    /// </summary>
    public static string SqlServer() =>
        Environment.GetEnvironmentVariable(SqlServerEnvVar) ?? SqlServerFallback;
}

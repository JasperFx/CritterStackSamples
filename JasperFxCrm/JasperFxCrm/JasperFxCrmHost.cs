using JasperFx.Events.Daemon;
using JasperFxCrm.Features.Leads;
using Marten;
using Wolverine;
using Wolverine.Http;
using Wolverine.Marten;

namespace JasperFxCrm;

public static class JasperFxCrmHost
{
    public const string ServiceName = "JasperFxCrm";

    #region sample_add_jasperfx_crm
    /// <summary>
    /// Marten, Wolverine and Wolverine.HTTP for the CRM. Every host, this sample's
    /// <c>Program</c> and the private one, calls this first and layers its own
    /// vendor registrations on top.
    /// </summary>
    public static WebApplicationBuilder AddJasperFxCrm(this WebApplicationBuilder builder)
    {
        builder.Services.AddWolverineHttp();

        var connection = builder.Configuration.GetConnectionString("Marten")
                         ?? "Host=localhost;Port=5433;Database=jasperfx_crm;Username=postgres;Password=postgres";

        builder.Services.AddMarten(opts =>
            {
                opts.Connection(connection);

                // Clone, `docker compose up -d`, run: the database is created on first start.
                opts.CreateDatabasesForTenants(c =>
                    c.MaintenanceDatabase(connection.Replace("Database=jasperfx_crm", "Database=postgres"))
                        .ForTenant()
                        .CheckAgainstPgDatabase());

                // Configurable so the specs can isolate themselves from a running app.
                opts.DatabaseSchemaName = builder.Configuration["Marten:SchemaName"] ?? "crm";

                // Marten 9 defaults throughout: System.Text.Json, the new metadata layout.
                opts.UseSystemTextJsonForSerialization();

                // The write models are read back by id right after a write, so Inline.
                opts.Projections.Snapshot<Lead>(JasperFx.Events.Projections.SnapshotLifecycle.Inline);
            })
            .IntegrateWithWolverine(m => m.UseFastEventForwarding = true)
            .AddAsyncDaemon(DaemonMode.HotCold)
            .UseLightweightSessions()
            .ApplyAllDatabaseChangesOnStartup();

        builder.Host.UseWolverine(opts =>
        {
            // Also the Event Model's name: the specs join it by this string.
            opts.ServiceName = ServiceName;
            opts.ApplicationAssembly = typeof(JasperFxCrmHost).Assembly;

            opts.Policies.AutoApplyTransactions();
            opts.Policies.UseDurableLocalQueues();

            opts.Durability.MessageIdentity = MessageIdentity.IdAndDestination;
        });

        return builder;
    }

    public static WebApplication MapJasperFxCrm(this WebApplication app)
    {
        app.MapWolverineEndpoints();
        return app;
    }
    #endregion
}

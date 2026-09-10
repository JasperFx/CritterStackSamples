using Alba;
using Bobcat;
using Bobcat.Runtime;
using JasperFx.CommandLine;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Polecat;
using ShipmentTracking.Handlers;
using Wolverine;
using Wolverine.CritterWatch;
using Wolverine.Polecat;
using Wolverine.RabbitMQ;

namespace ShipmentTracking.Specs;

/// <summary>
/// Stands the real application up for the spec suite, with the same substitutions
/// <c>Tests/IntegrationContext</c> makes and for the same reasons.
/// </summary>
public static class SuiteConfiguration
{
    private const string SpecDatabase = "ShipmentTracking_Specs";

    private static string ConnectionString =>
        $"Server=localhost,1433;Database={SpecDatabase};User Id=sa;Password=P@ssw0rd!;TrustServerCertificate=true";

    [BobcatConfiguration]
    public static void Configure(BobcatRunner runner)
    {
        // Required when the application uses JasperFx for command line processing and you want
        // Alba to host it; without it RunJasperFxCommands(args) takes over and the host never starts.
        JasperFxEnvironment.AutoStartHost = true;

        // Polecat creates its tables on demand but never the database itself.
        CreateSpecDatabase();

        // AlbaResource<TProgram>, not the factory-delegate form. This repository's solution file
        // sits in a directory named after the project, which is exactly the shape that sends
        // WebApplicationFactory's <solution dir>/<assembly name> fallback to
        // ShipmentTracking/ShipmentTracking — the typed resource resolves the content root itself.
        // Bobcat 0.18.0 explains that failure rather than printing the bare path (bobcat#274).
        runner.Suite.AddResource(new AlbaResource<Program>(
            configure: x => x.ConfigureServices(services =>
            {
                services.RunWolverineInSoloMode();
                services.DisableCritterWatch();

                // Nothing here consumes EscalateLateShipment, so a tracked session that produces
                // one waits forever unless the queue is listened to.
                services.ConfigureWolverine(opts => opts.ListenToRabbitQueue("shipment-operations"));

                // The one double, and it stands in for a third party: the real client sleeps 45
                // seconds on purpose, and a spec may not sleep.
                services.AddSingleton<ICarrierLabelClient, InstantCarrierLabelClient>();
            }),
            reset: host => host.ResetAllPolecatDataAsync(),
            extensions: ConfigurationOverride.Create(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Shipments"] = ConnectionString
            })));
    }

    private static void CreateSpecDatabase()
    {
        using var connection = new SqlConnection(
            "Server=localhost,1433;Database=master;User Id=sa;Password=P@ssw0rd!;TrustServerCertificate=true");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"if db_id('{SpecDatabase}') is null create database [{SpecDatabase}]";
        command.ExecuteNonQuery();
    }
}

/// <summary>
/// Stands in for the third-party carrier. The real client sleeps 45 seconds on purpose — that
/// duration is the whole reason label generation is a Durable listener — and a spec may not sleep.
/// </summary>
public class InstantCarrierLabelClient : ICarrierLabelClient
{
    public Task<string> CreateLabelAsync(Guid shipmentId, string carrier, CancellationToken token)
        => Task.FromResult($"{carrier.ToUpperInvariant()}-{shipmentId.ToString("N")[..10].ToUpperInvariant()}");
}

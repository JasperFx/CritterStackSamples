using Alba;
using Bobcat;
using Bobcat.Runtime;
using JasperFx.CommandLine;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Hosting;
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
/// <c>Tests/IntegrationContext</c> makes and for the same reasons — this is the xUnit fixture's
/// content, moved onto Bobcat's resource model.
/// </summary>
public static class SuiteConfiguration
{
    private const string TestingDatabase = "ShipmentTracking_Specs";

    /// <summary>The ShipmentTracking project directory, found from this assembly's location.</summary>
    private static string AppDirectory
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ShipmentTracking.csproj")))
            {
                dir = dir.Parent;
            }

            return dir?.FullName
                   ?? throw new InvalidOperationException("Could not locate the ShipmentTracking project directory.");
        }
    }

    private static string ConnectionString =>
        $"Server=localhost,1433;Database={TestingDatabase};User Id=sa;Password=P@ssw0rd!;TrustServerCertificate=true";

    [BobcatConfiguration]
    public static void Configure(BobcatRunner runner)
        => runner.Suite.AddResource(new AlbaResource(
            factory: BuildHostAsync,
            reset: host => host.ResetAllPolecatDataAsync()));

    private static async Task<IAlbaHost> BuildHostAsync()
    {
        JasperFxEnvironment.AutoStartHost = true;

        await CreateSpecDatabaseAsync();

        return await AlbaHost.For<Program>(
            x => x
                // Bobcat's resource start resolved a doubled content root
                // (…/ShipmentTracking/ShipmentTracking), so it is pinned explicitly at the
                // application project directory — two levels up from this spec assembly's bin.
                .UseContentRoot(AppDirectory)
                .ConfigureServices(services =>
            {
                services.RunWolverineInSoloMode();
                services.DisableCritterWatch();

                // Nothing in this application consumes EscalateLateShipment, so a tracked session
                // that produces one waits forever unless the queue is listened to.
                services.ConfigureWolverine(opts => opts.ListenToRabbitQueue("shipment-operations"));

                // The one double, and it stands in for a third party: the real client sleeps 45
                // seconds on purpose, and a spec may not sleep.
                services.AddSingleton<ICarrierLabelClient, InstantCarrierLabelClient>();
            }),
            ConfigurationOverride.Create(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Shipments"] = ConnectionString
            }));
    }

    private static async Task CreateSpecDatabaseAsync()
    {
        await using var connection = new SqlConnection(
            "Server=localhost,1433;Database=master;User Id=sa;Password=P@ssw0rd!;TrustServerCertificate=true");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"if db_id('{TestingDatabase}') is null create database [{TestingDatabase}]";
        await command.ExecuteNonQueryAsync();
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

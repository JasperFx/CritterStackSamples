using JasperFx;
using JasperFx.Resources;
using JasperFx.CodeGeneration.Frames;
using JasperFx.CodeGeneration.Model;
using Marten;
using Weasel.Postgresql;
using Wolverine;
using Wolverine.Http;
using Wolverine.Marten;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddMarten(opts =>
{
    // Hard coding to what the docker compose file builds here because I'm
    // being lazy this morning
    opts.Connection("Host=localhost;Port=5433;Database=postgres;Username=postgres;password=postgres");
    opts.DatabaseSchemaName = "reports";

    // ReportId is a strong-typed id wrapping an int; Marten needs it registered as a value type
    // before it can use one as a document identity.
    opts.RegisterValueType(typeof(ReportId));
    
    
    // Create a sequence to generate unique ids for documents.
    // NOTE the schema has to be spelled out here: the string overload of Sequence puts the
    // sequence in "public", NOT in DatabaseSchemaName, so an unqualified name would be created
    // as public.report_sequence while GetNextReportId() below asks for reports.report_sequence.
    var sequence = new Sequence(new PostgresqlObjectName("reports", "report_sequence"));

    opts.Storage.ExtendedSchemaObjects.Add(sequence);
}).IntegrateWithWolverine();

// ExtendedSchemaObjects are only provisioned when Marten actually runs a schema migration, and
// GetNextReportId() below hits the sequence with raw SQL without touching a document type first —
// so nothing would trigger that migration and the very first POST /report would fail with
// 42P01 "relation reports.report_sequence does not exist". This builds all configured
// storage at startup so the sample works on a clean database with no manual `db-apply`.
builder.Services.AddResourceSetupOnStartup();

builder.Host.UseWolverine(opts =>
{
    // Here's where we are adding the ReportId generation
    opts.CodeGeneration.Sources.Add(new ReportIdSource());
});

builder.Services.AddWolverineHttp();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapWolverineEndpoints();


return await app.RunJasperFxCommands(args);

public record StartReport(string Name);

public record ReportStarted(string Name, ReportId Id);

public class Report(ReportId Id)
{
    // This has to be a real property, not just the primary-constructor parameter: a primary
    // constructor param is only captured state, so without this Marten sees no Id member at all
    // and fails with "No closed-shape id strategy is registered for Report (id type , strategy )".
    public ReportId Id { get; set; } = Id;

    public string Name { get; set; }
}

public static class StartReportEndpoint
{
    [WolverinePost("/report")]
    public static (ReportStarted, IMartenOp) Handle(
        // The command
        StartReport command, 
        
        // The next report
        ReportId id)
    {
        var op = MartenOps.Store(new Report(id) { Name = command.Name });
        return (new ReportStarted(command.Name, id), op);
    }
}



// You'd probably use something like Vogen
// on this too, but I didn't need that just
// for the demo here.
// NOTE this must be a record *struct*: Marten only recognises value-type (struct) wrappers as
// document identities. As a plain `record` (a reference type) Marten finds no Id member at all
// and blows up with "No closed-shape id strategy is registered for Report (id type , strategy )".
public record struct ReportId(int Number);

// Variable source is part of JasperFx's code generation
// subsystem. This just tells the code generation how
// to resolve code for a variable of type ReportId
internal class ReportIdSource : IVariableSource
{
    public bool Matches(Type type)
    {
        return type == typeof(ReportId);
    }

    public Variable Create(Type type)
    {
        var methodCall = new MethodCall(typeof(DocumentSessionExtensions), nameof(DocumentSessionExtensions.GetNextReportId))
            {
                CommentText = "Creating a new ReportId"
            };

        // Little sleight of hand. The return variable here knows
        // that the MethodCall creates it, so that gets woven into 
        // the generated code
        return methodCall.ReturnVariable!;
    }
}

public static class DocumentSessionExtensions
{
    public static async Task<ReportId> GetNextReportId(this IDocumentSession session, CancellationToken cancellation)
    {
        // This API was added in Marten 8.31 as I tried to write this blog post
        var number = await session.NextSequenceValue("reports.report_sequence", cancellation);
        return new ReportId(number);
    }
}


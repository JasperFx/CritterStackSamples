using JasperFx;
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
    
    
    // Create a sequence to generate unique ids for documents
    var sequence = new Sequence("report_sequence");

    opts.Storage.ExtendedSchemaObjects.Add(sequence);
}).IntegrateWithWolverine();

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
// for the demo here
public record ReportId(int Number);

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


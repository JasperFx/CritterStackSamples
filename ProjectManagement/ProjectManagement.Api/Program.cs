using Marten;
using JasperFx;
using JasperFx.CommandLine;
using ProjectManagement.Api;
using Wolverine;
using Wolverine.Http;
using Wolverine.Marten;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddMarten(opts =>
{
    var connectionString = builder.Configuration.GetConnectionString("marten");
    opts.Connection(connectionString);
})
// More on this in a bit...
.IntegrateWithWolverine()

// Minor optimization
.UseLightweightSessions();

builder.Services.AddWolverineHttp();

builder.Host.UseWolverine(opts =>
{
    // more in a bit...
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapPost("/api/project/create", async (CreateProject command, IDocumentSession session) =>
{
    var projectCreated = new ProjectCreated(command.Name);
    var adminAssigned = new AdminAssigned(command.Name);
    var events = new object[] { projectCreated, adminAssigned }
        .Concat(command.TeamMembers.Select(x => new TeamMemberAssigned(x))).ToArray();

    var id = session.Events.StartStream<Project>(events).Id;

    await session.SaveChangesAsync();

    // New Stream Id for the project we just created. 201 Created (not 200) because this
    // endpoint creates a resource, and the Location header points at the new project.
    return Results.Created($"/api/project/{id}", id);
});

app.MapWolverineEndpoints();

// Replaced the standard command line runner with the 
// Critter Stack's expanded options
return await app.RunJasperFxCommands(args);

// Just to make testing a little easier
public partial class Program {}


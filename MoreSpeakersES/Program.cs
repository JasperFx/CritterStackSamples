using JasperFx.Events.Projections;
using Marten;
using Marten.Events.Projections;
using Wolverine;
using Wolverine.FluentValidation;
using Wolverine.Http;
using Wolverine.Marten;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddMarten(opts =>
{
    var connectionString = builder.Configuration.GetConnectionString("Marten")
        ?? "Host=localhost;Database=more_speakers_es;Username=postgres;Password=postgres";

    opts.Connection(connectionString);
    opts.DatabaseSchemaName = "speakers";

    // Event-sourced aggregates with inline snapshots
    opts.Projections.Snapshot<Speakers.Speaker>(SnapshotLifecycle.Inline);
    opts.Projections.Snapshot<Mentorships.Mentorship>(SnapshotLifecycle.Inline);

    opts.Schema.For<Speakers.Speaker>().Index(x => x.Email);
    opts.Schema.For<Mentorships.Mentorship>().Index(x => x.MentorId);
    opts.Schema.For<Mentorships.Mentorship>().Index(x => x.MenteeId);
})
.IntegrateWithWolverine()
.UseLightweightSessions();

builder.Host.UseWolverine(opts =>
{
    opts.Discovery.IncludeAssembly(typeof(Program).Assembly);
    opts.Policies.AutoApplyTransactions();
    opts.UseFluentValidation();
    opts.ServiceName = "MoreSpeakersES";
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapWolverineEndpoints();

await app.RunAsync();

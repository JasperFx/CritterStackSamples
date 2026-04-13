var builder = DistributedApplication.CreateBuilder(args);

// Shared infrastructure
var postgres = builder.AddPostgres("postgres");
var rabbitmq = builder.AddRabbitMQ("rabbitmq");

// Databases — one per service + CritterWatch
var critterwatchDb = postgres.AddDatabase("critterwatchdb");
var bankDb = postgres.AddDatabase("bankaccount");
var speakersDb = postgres.AddDatabase("morespeakers");
var outboxDb = postgres.AddDatabase("outboxdemo");

// CritterWatch monitoring console
var critterwatch = builder.AddProject<Projects.CritterWatchHost>("critterwatch")
    .WithReference(critterwatchDb)
    .WithReference(rabbitmq)
    .WaitFor(postgres)
    .WaitFor(rabbitmq)
    .WithExternalHttpEndpoints();

// Starter samples — each gets its own database + shared RabbitMQ
// .WithReference(db, "Marten") injects the connection string as "Marten"
// which matches what the samples read via GetConnectionString("Marten")

builder.AddProject<Projects.BankAccountES>("bank-account")
    .WithReference(bankDb, "Marten")
    .WithReference(rabbitmq)
    .WaitFor(critterwatch);

builder.AddProject<Projects.MoreSpeakers>("more-speakers")
    .WithReference(speakersDb, "Marten")
    .WithReference(rabbitmq)
    .WaitFor(critterwatch);

builder.AddProject<Projects.OutboxDemo>("outbox-demo")
    .WithReference(outboxDb, "Marten")
    .WithReference(rabbitmq)
    .WaitFor(critterwatch);

builder.Build().Run();

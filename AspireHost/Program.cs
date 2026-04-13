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
    .WithReference(critterwatchDb, "critterwatch")
    .WithReference(rabbitmq)
    .WaitFor(postgres)
    .WaitFor(rabbitmq)
    .WithExternalHttpEndpoints()
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Production")
    .WithEnvironment("JASPERFX_LICENSE", "LC-D0E6F26A04FD44BDAF37D91851E897D5-BU-20260413 eyJJZCI6IkxDLUQwRTZGMjZBMDRGRDQ0QkRBRjM3RDkxODUxRTg5N0Q1LUJVLTIwMjYwNDEzIiwiTmFtZSI6IkNyaXR0ZXJTdGFja1NhbXBsZXMiLCJUeXBlIjoxLCJIYXNoIjoiZDEwQ1ZQekV6c0gwM1pnY1VrVUs3eDNTODVcdTAwMkJXSFx1MDAyQjRBV3BRMEIvc3VSZDYzVnozMTdRVTRYTGMzMG9yNzdhcnNZemZUd29SM3RGYVFYbTRDcGtwbkR2NUpSNVx1MDAyQjBwUEpoSUFyN0wxT0JMVjhiUlNYSnNIZkN1bVoySG1aemNnYjB0U2t2bTJxVVN5VlhkL0ZFWTZ5d25yc3JnTHM0NlFPQzJZQjFERE5tVElYZkdKMzdUZ2RIL3NJVnU4WUhqL25tSzNpVURvUWdoSDNLY1x1MDAyQlR2M3RJWjBXbS9PREhQaExJRmxLeGNaaFlrXHUwMDJCWWxhXHUwMDJCZlB0Z2xrMHhxS0FLVUV5MndMbDBSaGVoNnNNTDlUcWVuUHhXQTJMbjJubVx1MDAyQmFFd3ZsdklFelBKcDE0ckdWMEd5UVQ1Um1LMXNxbXdoTGxSTWNrcnY0WFRcdTAwMkJEMHlwcjBZSTJ3Q2lXdVhmZz09IiwiRXhwaXJ5IjoiMjAyOC0wNC0xMlQwMDowMDowMFoifQ==");

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

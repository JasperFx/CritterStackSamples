using OutboxDemo.Tests;

// One host for the Bobcat specifications, booted once for the run.
[assembly: AssemblyFixture(typeof(OutboxHost))]

// Every spec empties the store before it runs, so two specs running at once would wipe each other's data.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

// Names the Event Model the specs contribute slices to. Program.cs sets no ServiceName, so it is the
// application's assembly name.
[assembly: Bobcat.EventModelName("OutboxDemo")]

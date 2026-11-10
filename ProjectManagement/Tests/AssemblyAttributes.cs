using Tests;

// One host for the Bobcat specifications, booted once for the run.
[assembly: AssemblyFixture(typeof(ProjectManagementHost))]

// A CritterStackSpec act reads back everything appended to the store while it ran, so two specs
// running at once would see each other's events.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

// Names the Event Model the specs contribute slices to. Program.cs sets no ServiceName, so Wolverine's
// default, the application assembly's name, is the model's name.
[assembly: Bobcat.EventModelName("ProjectManagement.Api")]

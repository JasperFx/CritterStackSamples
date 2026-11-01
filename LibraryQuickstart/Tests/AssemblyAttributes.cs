using Tests;

// One Postgres container and one host for the run, shared by the original tests and the specifications.
[assembly: AssemblyFixture(typeof(AppFixture))]

// A specification's act judges everything appended to the store while it ran, and every test here
// shares the one store, so nothing in this assembly may run at the same time as anything else.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

// Names the Event Model the specs contribute slices to: the service name set in Program.cs.
[assembly: Bobcat.EventModelName("LibraryQuickstart")]

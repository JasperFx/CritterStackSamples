using Tests;

// One host for the run, shared by the original tests and the Bobcat specifications.
[assembly: AssemblyFixture(typeof(AppFixture))]

// Every test and specification resets the one test database before it runs, and a specification's
// act judges everything that happened in the application while it ran, so nothing in this assembly
// may run at the same time as anything else.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

// Names the Event Model the specs contribute slices to: the service name set in Program.cs.
[assembly: Bobcat.EventModelName("ShipmentTracking")]

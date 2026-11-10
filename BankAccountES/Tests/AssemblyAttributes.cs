using BankAccountES.Tests;

// One host for the Bobcat specifications, booted once for the run.
[assembly: AssemblyFixture(typeof(BankAccountHost))]

// A CritterStackSpec act reads back everything appended to the store while it ran, and the
// original tests in BankAccountTests wipe the whole store before each test, so nothing in this
// assembly may run at the same time as anything else.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

// Names the Event Model the specs contribute slices to: the service name set in Program.cs.
[assembly: Bobcat.EventModelName("BankAccount")]

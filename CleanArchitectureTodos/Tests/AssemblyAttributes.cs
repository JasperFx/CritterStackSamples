using CleanArchitectureTodos.Tests;

// One host for the Bobcat specifications, booted once for the run.
[assembly: AssemblyFixture(typeof(TodosHost))]

// Every test here starts from an empty document store (TodoListTests wipes it, the specs reset it),
// so nothing in this assembly may run at the same time as anything else.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

// Names the Event Model the specs contribute slices to. The app sets no opts.ServiceName,
// so this is the application assembly's name.
[assembly: Bobcat.EventModelName("CleanArchitectureTodos")]

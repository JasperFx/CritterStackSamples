using CritterCrush.Specs;

// One host for every specification, booted once for the run.
[assembly: AssemblyFixture(typeof(CritterCrushHost))]

// Every spec resets the whole store before it runs, and the scenarios use fixed dog ids, so nothing
// in this assembly may run at the same time as anything else.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

// The declared model, the running host, and the specs all merge by this one name: it matches
// opts.ServiceName in Program.cs and the `model:` field of models/CritterCrush.emodel.yaml.
[assembly: Bobcat.EventModelName("CritterCrush")]

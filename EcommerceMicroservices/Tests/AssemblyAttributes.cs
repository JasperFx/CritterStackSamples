using Tests;

// One host per service the specifications drive, each booted once for the run.
[assembly: AssemblyFixture(typeof(CatalogHost))]
[assembly: AssemblyFixture(typeof(BasketHost))]
[assembly: AssemblyFixture(typeof(OrderingHost))]

// The specs and the original CatalogTests both empty a service's store before each test, so nothing
// in this assembly may run at the same time as anything else.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

// One Event Model per assembly: the Catalog service's (opts.ServiceName), which most specs are about.
// The Basket and Ordering specs contribute their slices to it as well.
[assembly: Bobcat.EventModelName("Catalog")]

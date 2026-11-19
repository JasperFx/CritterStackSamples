global using Bobcat;
global using Bobcat.Xunit;
global using Shouldly;
global using Xunit;

// Two hosts (document sagas and event-sourced sagas) share one PostgreSQL database: run one spec at a time
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

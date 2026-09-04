using Xunit;

// MPM.Tests levanta varios hosts in-process (WebApplicationFactory, uno por clase).
// En CI (runner compartido) los hosts en paralelo saturan al Postgres de services
// y aparecen timeouts de lectura intermitentes: se serializa el assembly.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

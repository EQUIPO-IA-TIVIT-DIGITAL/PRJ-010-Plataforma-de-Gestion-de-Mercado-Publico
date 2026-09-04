using Xunit;

// Los tests de este assembly corren contra el Postgres real compartido de
// docker-compose (BD viva con escritores concurrentes: workers del API, scraper,
// E2E). Varios asserts comparan conteos globales entre dos lecturas, así que las
// clases NO pueden correr en paralelo entre sí: se serializa el assembly completo.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

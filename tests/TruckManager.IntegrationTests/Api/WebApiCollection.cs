using Xunit;

namespace TruckManager.IntegrationTests.Api;

// Phase 8 / Section F.   xUnit collection marker for serializing Web host integration tests.
//
// Why this exists: Program.cs assigns `Log.Logger` (Serilog static singleton) at top-level statements before its try{} block.
// Each WebApplicationFactory<Program> instance re-runs Program.Main -> re-assigns the static — so two test classes booting hosts in parallel race on it, and one host's DI graph ends up partially-built when the other one is also half-way through.
// The observed failure mode was an InvalidOperationException out of HostApplicationBuilder.Build() during the parallel class-fixture initialization.
//
// Collection naming is purely a marker — `[CollectionDefinition]` registers the name + parallelization mode, and `[Collection("WebApi")]` on each test class pins it.
// xUnit serializes test classes within the same collection by default.
//
// Pre-existing PostgresFixture-based tests (Phase 4 / Phase 5) stay parallel — they build DI via ServiceCollection directly without going through Program.cs, so they don't trip the Serilog static-state race.
[CollectionDefinition(nameof(WebApiCollection))]
public sealed class WebApiCollection : ICollectionFixture<WebApiFixture>
{ }

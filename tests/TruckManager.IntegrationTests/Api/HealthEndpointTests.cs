using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Xunit;

namespace TruckManager.IntegrationTests.Api;

// Phase 8 / Section F.   Health endpoint smoke tests.
//
// Two endpoints exposed by Program.cs:
//   >  /health         — liveness. Trivial. Always returns 200 with { status: "healthy" }. Orchestrators use it to decide whether to restart the container.
//   >  /health/ready   — readiness. Backed by StatusBijectionHealthCheck. Returns 200 only after the bijection has validated successfully (= "the app booted and self-validated").
//
// WebApiFixture forces host startup (via `_ = Server` in InitializeAsync), which runs the hosted-service registration chain.
// By the time these tests execute, both endpoints should be green.
[Collection(nameof(WebApiCollection))]
public sealed class HealthEndpointTests(WebApiFixture fixture)
{
    private readonly WebApiFixture _fixture = fixture;

    [Fact]
    public async Task Health_returns_200_with_healthy_status_body()
    {
        // /health is unversioned — it's an infrastructure contract for orchestrators, intentionally stable across API version bumps (Program.cs comment in Phase 6 / Section A.1).
        HttpClient client = _fixture.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Body is { status = "healthy" } — anonymous-object minimal API result.
        // Deserialize loosely so a future schema-extension (adding a "version" field, etc.) doesn't break this test.
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("status").GetString().Should().Be("healthy");
    }

    [Fact]
    public async Task HealthReady_returns_200_once_bijection_check_completes()
    {
        // Readiness goes 503 -> 200 the moment StatusBijectionHealthCheck.StartAsync sets _isReady = true.
        // WebApiFixture.InitializeAsync already forced host startup before tests run, so the readiness gate is already flipped.
        // No retry loop needed here — if this returns 503, the fixture / hosted-service chain has regressed and the test SHOULD fail loudly.
        HttpClient client = _fixture.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

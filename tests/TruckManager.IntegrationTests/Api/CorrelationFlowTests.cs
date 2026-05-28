using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AwesomeAssertions;
using Xunit;

using TruckManager.Domain.Enums;
using TruckManager.Api.Trucks.Requests;
using TruckManager.Infrastructure.Persistence;

namespace TruckManager.IntegrationTests.Api;

// Phase 8 / Section F.   Correlation-flow end-to-end coverage ([ADR-0041]).
//
// The three rules of correlation propagation:
//   1.   X-Correlation-Id absent on request   → server generates a UUIDv7 + echoes it back in the response header.
//   2.   X-Correlation-Id present on request  → server uses it verbatim + echoes it back.
//   3.   A persisted TruckDomainEvent row carries the same CorrelationId as the request that produced it (no drift between middleware → handler → aggregate → DB).
//
// Per ADR-0041, the chain runs:   CorrelationMiddleware → HttpContext.Items + LogContext → ICorrelationContext (scoped) → CreateTruckHandler → Truck.Create(...) → TruckCreated event → DomainEventPersistenceInterceptor → TruckDomainEvents.CorrelationId column.
//
// What's NOT covered here: assertion that the Logs.LogEvent row carries the same CorrelationId. The Serilog PostgreSQL.Alternative sink batches writes with a 5s window — adding a poll-with-timeout would make the test flaky. The Logs ↔ TruckDomainEvents correlation is covered by the Phase 7 manual smoke test documented in database.md §12.
[Collection(nameof(WebApiCollection))]
public sealed class CorrelationFlowTests(WebApiFixture fixture)
{
    private readonly WebApiFixture _fixture = fixture;

    private const string CorrelationHeader = "X-Correlation-Id";

    private static string UniqueCode() => Guid.NewGuid().ToString("N").ToUpperInvariant()[..8];

    [Fact]
    public async Task Server_generates_correlation_id_when_request_omits_header()
    {
        // CorrelationMiddleware.ResolveCorrelationId falls back to Guid.CreateVersion7(clock.UtcNow) when the header is missing — the response should carry the generated id back.
        HttpClient client = _fixture.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        response.Headers.TryGetValues(CorrelationHeader, out IEnumerable<string>? values).Should().BeTrue($"every response must carry {CorrelationHeader} (CorrelationMiddleware.Response.OnStarting).");
        string? raw = values?.FirstOrDefault();
        Guid.TryParse(raw, out Guid parsed).Should().BeTrue($"server-generated {CorrelationHeader} must be a parseable Guid; got '{raw}'.");
        parsed.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Server_echoes_client_supplied_correlation_id_verbatim()
    {
        // When the client supplies a parseable Guid the middleware uses it as-is — cross-service correlation only works if the value flows through unchanged.
        HttpClient client = _fixture.CreateClient();
        Guid sent = Guid.NewGuid();

        HttpRequestMessage request = new(HttpMethod.Get, "/health");
        request.Headers.Add(CorrelationHeader, sent.ToString());

        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.Headers.TryGetValues(CorrelationHeader, out IEnumerable<string>? values).Should().BeTrue();
        values!.FirstOrDefault().Should().Be(sent.ToString());
    }

    [Fact]
    public async Task Correlation_id_propagates_to_persisted_TruckDomainEvent_row()
    {
        // The end-to-end propagation guarantee from ADR-0041: same UUIDv7 from the HTTP header all the way down to the TruckDomainEvents.CorrelationId column.
        // Send a known Guid, POST a truck, then query TruckDomainEvents directly via a fresh scope on the fixture's host services.
        HttpClient client = _fixture.CreateClient();
        Guid sent = Guid.NewGuid();

        CreateTruckRequest createRequest = new(Code: UniqueCode(), Name: "Correlation Smoke", Description: null, InitialStatus: ETruckStatus.OutOfService);
        HttpRequestMessage httpRequest = new(HttpMethod.Post, "/api/v1/trucks") { Content = JsonContent.Create(createRequest) };
        httpRequest.Headers.Add(CorrelationHeader, sent.ToString());

        HttpResponseMessage response = await client.SendAsync(httpRequest, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        Guid truckId = (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("id").GetGuid();

        // UnitOfWorkBehavior commits inside the request scope — by the time the response returns, the TruckDomainEvents row is already persisted. No retry / polling needed.
        using IServiceScope scope = _fixture.Services.CreateScope();
        ApplicationDbContext ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Guid? persistedCorrelationId = await ctx.Set<Infrastructure.Persistence.Entities.TruckDomainEvent>()
                                                .Where(e => e.AggregateId == truckId)
                                                .Select(e => e.CorrelationId)
                                                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        persistedCorrelationId.Should().Be(sent, $"the CorrelationId on the TruckCreated event row must equal the X-Correlation-Id header sent with the POST request ([ADR-0041] propagation chain).");
    }
}

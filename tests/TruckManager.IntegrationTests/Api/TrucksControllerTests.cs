using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Xunit;

using TruckManager.Domain.Enums;
using TruckManager.Api;
using TruckManager.Api.Trucks.Requests;
using TruckManager.Application.Trucks.DTOs;

namespace TruckManager.IntegrationTests.Api;

// Phase 8 / Section F.   End-to-end HTTP tests for TrucksController.
//
// Each test goes:   HttpClient → Kestrel-equivalent test server → controller → dispatcher → pipeline behaviors → handler → real EF Core → real Postgres (Testcontainers).
// No mocking. All six controller actions exercised on the happy + failure paths called out in next-steps.md Section F.
//
// Code-uniqueness convention:  UniqueCode() produces an 8-char alphanumeric per call — matches the pattern in CommandPipelineTests (Phase 5) so the shared-per-class container doesn't accumulate collisions.
[Collection(nameof(WebApiCollection))]
public sealed class TrucksControllerTests(WebApiFixture fixture)
{
    private readonly WebApiFixture _fixture = fixture;

    private static string UniqueCode() => Guid.NewGuid().ToString("N").ToUpperInvariant()[..8];

    // ---- happy paths ----------------------------------------------------------------

    [Fact]
    public async Task POST_create_returns_201_with_Location_and_id()
    {
        // Sanity: 201 Created carries Location pointing at GET-by-id, and a body of { id: <guid> }. ApiResultExtensions.ToCreatedResult shapes this — see Phase 6 / Section C.
        HttpClient client = _fixture.CreateClient();
        CreateTruckRequest request = new(Code: UniqueCode(), Name: "Smoke Truck", Description: null, InitialStatus: ETruckStatus.OutOfService);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/trucks", request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain("/api/v1/trucks/");

        // Body: { id: "..." }. Parse loosely — JsonElement keeps the test resilient to future fields added to the success response.
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("id").GetGuid().Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task GET_list_returns_200_with_paged_list_dto()
    {
        // Seed one truck so the list is non-empty regardless of which test ran first (xUnit ordering is implementation-defined).
        HttpClient client = _fixture.CreateClient();
        _ = await CreateTruck(client, UniqueCode());

        HttpResponseMessage response = await client.GetAsync("/api/v1/trucks?page=1&pageSize=50", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        PagedListDto<TruckSummaryDto>? page = await response.Content.ReadFromJsonAsync<PagedListDto<TruckSummaryDto>>(JsonOpts, TestContext.Current.CancellationToken);
        page.Should().NotBeNull();
        page!.Page.Should().Be(1);
        page.PageSize.Should().Be(50);
        page.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GET_by_id_returns_200_with_truck_dto()
    {
        HttpClient client = _fixture.CreateClient();
        Guid id = await CreateTruck(client, UniqueCode());

        HttpResponseMessage response = await client.GetAsync($"/api/v1/trucks/{id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        TruckDto? truck = await response.Content.ReadFromJsonAsync<TruckDto>(JsonOpts, TestContext.Current.CancellationToken);
        truck.Should().NotBeNull();
        truck!.Id.Should().Be(id);
    }

    [Fact]
    public async Task PUT_update_returns_204()
    {
        HttpClient client = _fixture.CreateClient();
        Guid id = await CreateTruck(client, UniqueCode());

        UpdateTruckRequest request = new(Name: "Renamed", Description: "Updated description");

        HttpResponseMessage response = await client.PutAsJsonAsync($"/api/v1/trucks/{id}", request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task PATCH_status_returns_204()
    {
        // OutOfService → Loading is row #1 in the seeded TruckStatusTransitions (migration 20260524224234_InitialCreate). The transition policy accepts it.
        HttpClient client = _fixture.CreateClient();
        Guid id = await CreateTruck(client, UniqueCode(), initialStatus: ETruckStatus.OutOfService);

        ChangeTruckStatusRequest request = new(NewStatus: ETruckStatus.Loading);

        HttpResponseMessage response = await client.PatchAsJsonAsync($"/api/v1/trucks/{id}/status", request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DELETE_returns_204()
    {
        HttpClient client = _fixture.CreateClient();
        Guid id = await CreateTruck(client, UniqueCode());

        HttpResponseMessage response = await client.DeleteAsync($"/api/v1/trucks/{id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ---- failure paths --------------------------------------------------------------

    [Fact]
    public async Task POST_duplicate_code_returns_409_Conflict_problem_details()
    {
        // CreateTruckHandler's pre-check (Phase 6 in-flight fix) converts the duplicate into a Result.Failure(Conflict) — ApiResultExtensions maps to 409 + ProblemDetailsTypes.Conflict.
        HttpClient client = _fixture.CreateClient();
        string code = UniqueCode();
        _ = await CreateTruck(client, code);

        // Same code, different request, same tenant.
        CreateTruckRequest duplicate = new(Code: code, Name: "Dup", Description: null, InitialStatus: ETruckStatus.OutOfService);
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/trucks", duplicate, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("type").GetString().Should().Be(ProblemDetailsTypes.Conflict);
        problem.GetProperty("status").GetInt32().Should().Be((int)HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GET_by_id_unknown_returns_404_NotFound_problem_details()
    {
        // Random Guid is virtually guaranteed not to be in the DB. GetTruckByIdHandler returns Result.Failure(NotFound) → 404 + ProblemDetailsTypes.NotFound.
        HttpClient client = _fixture.CreateClient();
        Guid unknown = Guid.NewGuid();

        HttpResponseMessage response = await client.GetAsync($"/api/v1/trucks/{unknown}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("type").GetString().Should().Be(ProblemDetailsTypes.NotFound);
    }

    [Fact]
    public async Task POST_with_empty_code_returns_400_ValidationProblemDetails_with_Code_error()
    {
        // CreateTruckValidator rejects empty Code with Error.Code = "Validation.Code". ApiResultExtensions.BuildValidationProblem groups by property → ValidationProblemDetails.Errors["Code"] non-empty.
        HttpClient client = _fixture.CreateClient();
        CreateTruckRequest request = new(Code: "", Name: "Truck", Description: null, InitialStatus: ETruckStatus.OutOfService);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/trucks", request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("type").GetString().Should().Be(ProblemDetailsTypes.ValidationError);
        problem.GetProperty("errors").TryGetProperty("Code", out JsonElement codeErrors).Should().BeTrue("ValidationProblemDetails.errors must include a 'Code' key when CreateTruckValidator rejects the Code field.");
        codeErrors.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PATCH_invalid_transition_returns_400_ValidationProblemDetails()
    {
        // TruckStatusTransitions seeds OutOfService↔ANY plus the linear workflow Loading→ToJob→AtJob→Returning→Loading. Loading→AtJob is NOT in the table.
        //
        // Worth flagging: Truck.ChangeStatus returns Error(Type = EErrorType.Validation) on a disallowed transition — see Truck.cs:198. That's an intentional ADR-0028 call: the *caller* supplied a status that's syntactically valid (in the enum) but semantically rejected by the workflow policy → "could a well-behaved caller produce this with valid inputs?" Yes → Validation failure → 400.
        // Concurrency-conflict (409) is reserved for stamp drift; not-allowed transitions don't qualify.
        HttpClient client = _fixture.CreateClient();
        Guid id = await CreateTruck(client, UniqueCode(), initialStatus: ETruckStatus.OutOfService);

        // First step OutOfService → Loading (allowed) to put the truck in Loading state.
        ChangeTruckStatusRequest setup = new(NewStatus: ETruckStatus.Loading);
        HttpResponseMessage setupResponse = await client.PatchAsJsonAsync($"/api/v1/trucks/{id}/status", setup, TestContext.Current.CancellationToken);
        setupResponse.StatusCode.Should().Be(HttpStatusCode.NoContent, "setup transition must succeed for the negative test to be meaningful.");

        // Now attempt the disallowed Loading → AtJob.
        ChangeTruckStatusRequest request = new(NewStatus: ETruckStatus.AtJob);

        HttpResponseMessage response = await client.PatchAsJsonAsync($"/api/v1/trucks/{id}/status", request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("type").GetString().Should().Be(ProblemDetailsTypes.ValidationError);
    }

    // ---- helpers --------------------------------------------------------------------

    // Used by every test that needs an existing truck. Returns the new id straight from the response body so subsequent calls can address it directly.
    // Fails the test (via .Should()) if creation didn't succeed — that surfaces fixture problems faster than a downstream 404.
    private static async Task<Guid> CreateTruck(HttpClient client, string code, ETruckStatus initialStatus = ETruckStatus.OutOfService)
    {
        CreateTruckRequest request = new(Code: code, Name: $"Truck {code}", Description: null, InitialStatus: initialStatus);
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/trucks", request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created, $"truck creation must succeed for downstream assertions to run. Body: {await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}");

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return body.GetProperty("id").GetGuid();
    }

    // System.Text.Json defaults to case-sensitive deserialization; ASP.NET Core uses camelCase property names by default for outbound JSON. Match camelCase here so TruckDto / PagedListDto come back populated.
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
}

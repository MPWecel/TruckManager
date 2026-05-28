using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Xunit;

namespace TruckManager.IntegrationTests.Api;

// Phase 8 / Section F.   Swagger / OpenAPI document tests.
//
// The Swagger UI middleware (Swashbuckle) is gated on application.Environment.IsDevelopment() in Program.cs — WebApiFixture forces UseEnvironment("Development") specifically so this surface is reachable.
//
// One test, one assertion target: the v1 OpenAPI document exists and contains every Truck endpoint we shipped in Phase 6.
// If a controller action is added, removed, or renamed without updating the [Route] / [HttpXxx] attributes, this test fails — that's the point.
[Collection(nameof(WebApiCollection))]
public sealed class SwaggerTests(WebApiFixture fixture)
{
    private readonly WebApiFixture _fixture = fixture;

    [Fact]
    public async Task Swagger_v1_document_contains_all_six_truck_endpoints()
    {
        // ConfigureSwaggerOptions registers one SwaggerDoc per API version (via IApiVersionDescriptionProvider).
        // V1 is the only version today; URL is "/swagger/v1/swagger.json".
        HttpClient client = _fixture.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonDocument document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);

        // OpenAPI 3.0 schema: paths is a top-level object keyed by route template; each route is an object keyed by lowercase HTTP method.
        // Phase 6 surface — six (route, method) tuples:
        JsonElement paths = document.RootElement.GetProperty("paths");

        AssertPathHasOperation(paths, "/api/v1/trucks", "post");
        AssertPathHasOperation(paths, "/api/v1/trucks", "get");
        AssertPathHasOperation(paths, "/api/v1/trucks/{id}", "get");
        AssertPathHasOperation(paths, "/api/v1/trucks/{id}", "put");
        AssertPathHasOperation(paths, "/api/v1/trucks/{id}/status", "patch");
        AssertPathHasOperation(paths, "/api/v1/trucks/{id}", "delete");
    }

    // Single shared assertion helper — keeps the failure messages specific to the missing (path, method) pair.
    private static void AssertPathHasOperation(JsonElement paths, string path, string method)
    {
        paths.TryGetProperty(path, out JsonElement pathItem)
             .Should()
             .BeTrue($"OpenAPI document must contain path '{path}'.");
        pathItem.TryGetProperty(method, out _)
                .Should()
                .BeTrue($"OpenAPI path '{path}' must define operation '{method.ToUpperInvariant()}'.");
    }
}

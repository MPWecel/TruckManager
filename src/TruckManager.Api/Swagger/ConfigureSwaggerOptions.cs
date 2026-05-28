using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace TruckManager.Api.Swagger;

// Phase 6 / Section F.   Version-aware Swagger document configuration.
//
// IConfigureOptions<SwaggerGenOptions> is the standard late-binding pattern: SwaggerGen is configured during service registration (before the service provider is built),
// but IApiVersionDescriptionProvider isn't available until DI has built.
// Registering this class as transient IConfigureOptions<> means its Configure method runs the FIRST TIME SwaggerGenOptions is resolved (during request handling), by which point IApiVersionDescriptionProvider can be injected normally.
//
// One SwaggerDoc per API version: each gets its own OpenAPI document at /swagger/{groupName}/swagger.json (groupName = "v1" per the format string set in Program.cs Section D).
// When v2 lands, this loop picks it up automatically with zero changes here.
public sealed class ConfigureSwaggerOptions : IConfigureOptions<SwaggerGenOptions>
{
    private readonly IApiVersionDescriptionProvider _versionProvider;

    public ConfigureSwaggerOptions(IApiVersionDescriptionProvider versionProvider)
    {
        ArgumentNullException.ThrowIfNull(versionProvider);
        _versionProvider = versionProvider;
    }

    public void Configure(SwaggerGenOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        foreach (ApiVersionDescription description in _versionProvider.ApiVersionDescriptions)
        {
            OpenApiInfo info = new()
            {
                Title       = "TruckManager API",
                Version     = description.ApiVersion.ToString(),
                Description = $"TruckManager REST API ({description.GroupName}).",
            };

            options.SwaggerDoc(description.GroupName, info);
        }
    }
}

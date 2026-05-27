using TruckManager.Application;
using TruckManager.Infrastructure;
using TruckManager.Infrastructure.Workflows;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Phase 5 / Section A.  Application composition: CQRS dispatchers, handler assembly scan, FluentValidators.
// Pipeline behaviors land in Section B alongside IUnitOfWork.
builder.Services.AddTruckManagerApplication();

// Phase 4 / Section F.  Single composition-root call for the persistence + workflow + audit pipeline
// (DbContext + interceptors, transition policy, bijection check, audit + event hosted services, IDateTimeProvider, stub ICurrentUserService).
builder.Services.AddTruckManagerInfrastructure(builder.Configuration);

// Expose the bijection check at /health/ready. The check itself is the singleton registered by AddTruckManagerInfrastructure
// (same instance the IHostedService startup path uses), so this endpoint reads its _isReady flag without re-querying the DB.
builder.Services.AddHealthChecks()
                .AddCheck<StatusBijectionHealthCheck>(
                                                         name: "workflow_bijection",
                                                         tags: ["ready"]
                                                     );

WebApplication application = builder.Build();

// Configure the HTTP request pipeline.
if (application.Environment.IsDevelopment())
{
    application.MapOpenApi();
}

// HTTPS redirection intentionally omitted: Phase 1 local stack runs HTTP-only inside Docker (port 8080).
// Add `application.UseHttpsRedirection()` back when a real TLS termination point exists.

application.UseAuthorization();

application.MapControllers();
application.MapHealthChecks("/health/ready");

application.Run();

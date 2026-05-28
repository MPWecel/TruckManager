using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using TruckManager.Application.Abstractions;
using TruckManager.Application.Abstractions.Persistence;
using TruckManager.Common.Abstractions;
using TruckManager.Domain.Policies;
using TruckManager.Infrastructure.Auth;
using TruckManager.Infrastructure.Http;
using TruckManager.Infrastructure.Persistence;
using TruckManager.Infrastructure.Persistence.Interceptors;
using TruckManager.Infrastructure.Persistence.Serialization;
using TruckManager.Infrastructure.Time;
using TruckManager.Infrastructure.Workflows;

namespace TruckManager.Infrastructure;

// Phase 4 / Section F.  The single Infrastructure composition root: registers everything the API needs to bring the persistence + workflow + audit pipeline online.
// Api/Program.cs just calls AddTruckManagerInfrastructure(builder.Configuration) and wires the health-check endpoint on top.
//
// Lifetime choices:
//   > 'IDateTimeProvider', 'IDomainEventSerializer', 'TruckStatusTransitionPolicy', 'StatusBijectionHealthCheck', 'DomainEventPersistenceInterceptor'  ->  Singleton (stateless or designed to be load-once).
//   > 'ICurrentUserService'    ->  Scoped (Phase 9 will read per-request HttpContext claims).
//   > 'CreatedAuditFillerInterceptor'  ->  Scoped (depends on scoped ICurrentUserService).
//   > 'ApplicationDbContext'   ->  Scoped (standard EF Core lifetime).
//
// Hosted-service ordering matters (executes in registration order):
//   1. 'MigrationRunner'   ->  creates / updates tables.
//   2. 'StatusBijectionHealthCheck'    ->  queries the now-existing seeded dictionary tables.
public static class DependencyInjection
{
    public static IServiceCollection AddTruckManagerInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        const string defaultConnStringName = "Default";
        const string noConnectionStringExceptionMessage = "ConnectionStrings:Default is not configured. Local dev expects appsettings.Development.json; container runs expect the ConnectionStrings__Default env var (set by docker-compose.yml).";
        string connectionString = configuration.GetConnectionString(defaultConnStringName) ?? throw new InvalidOperationException(noConnectionStringExceptionMessage);

        // ------------------------------------------------------------------------------
        // Cross-cutting primitives
        // ------------------------------------------------------------------------------

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddScoped<ICurrentUserService, StubCurrentUserService>();

        // Phase 7 / Section B   Per-request CorrelationId source.
        // AddHttpContextAccessor registers IHttpContextAccessor as a singleton (uses AsyncLocal under the hood, request-safe).
        // HttpContextCorrelationContext is scoped — one instance per request — and reads HttpContext.Items[ItemKey], which CorrelationMiddleware sets at request entry.
        // Throws if accessed outside an HTTP request scope or before the middleware ran.
        services.AddHttpContextAccessor();
        services.AddScoped<ICorrelationContext, HttpContextCorrelationContext>();

        // ------------------------------------------------------------------------------
        // Domain-event serialization (ADR-0030)
        // ------------------------------------------------------------------------------

        services.AddSingleton<IDomainEventSerializer, DomainEventSerializer>();

        // ------------------------------------------------------------------------------
        // Workflow policy + bijection health-check (ADR-0025 / ADR-0027)
        // ------------------------------------------------------------------------------

        // Register the concrete class as the singleton;
        // the interface registration is a factory proxy so Domain consumers (ITruckStatusTransitionPolicy) and the Infrastructure health-check (concrete TruckStatusTransitionPolicy)
        // share the exact same instance — single source of truth for the loaded transitions.
        services.AddSingleton<TruckStatusTransitionPolicy>()
                .AddSingleton<ITruckStatusTransitionPolicy>(sp => sp.GetRequiredService<TruckStatusTransitionPolicy>());

        // Same proxy pattern for StatusBijectionHealthCheck — one singleton serves both the IHostedService entry (below) and the IHealthCheck registration that
        // Api/Program.cs wires via AddHealthChecks().AddCheck<StatusBijectionHealthCheck>(...).
        services.AddSingleton<StatusBijectionHealthCheck>();

        // ------------------------------------------------------------------------------
        // EF Core: interceptors + DbContext
        // ------------------------------------------------------------------------------

        services.AddSingleton<DomainEventPersistenceInterceptor>();
        services.AddScoped<CreatedAuditFillerInterceptor>();

        services.AddDbContext<ApplicationDbContext>(
                                                       (sp, options) =>
                                                       {
                                                           options.UseNpgsql(connectionString);
                                                           options.AddInterceptors(
                                                                                      sp.GetRequiredService<CreatedAuditFillerInterceptor>(),
                                                                                      sp.GetRequiredService<DomainEventPersistenceInterceptor>()
                                                                                  );
                                                       }
                                                   );

        // ------------------------------------------------------------------------------
        // Application-layer persistence abstractions [ADR-0039 / ADR-0040]
        // ------------------------------------------------------------------------------

        // Same-instance proxy: handlers inject IApplicationDbContext and receive the same scoped ApplicationDbContext that EF's interceptors hold [ADR-0034 pattern].
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        // EfUnitOfWork wraps the same scoped ApplicationDbContext so its transaction covers all IApplicationDbContext writes in the request.
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        // ------------------------------------------------------------------------------
        // Hosted services — order is significant
        // ------------------------------------------------------------------------------

        // 1) Migrations first — bring the schema online before anything queries it.
        services.AddHostedService<MigrationRunner>();

        // 2) Bijection check second — proxy to the singleton so the SAME instance also serves the AddHealthChecks().AddCheck<StatusBijectionHealthCheck>(...) registration in Api/Program.cs.
        services.AddHostedService(sp => sp.GetRequiredService<StatusBijectionHealthCheck>());

        return services;
    }
}

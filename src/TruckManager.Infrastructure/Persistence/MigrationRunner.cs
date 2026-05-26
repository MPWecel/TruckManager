using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TruckManager.Infrastructure.Persistence;

// [ADR-0018 / Phase 4 decision #8]   Development auto-apply of pending EF Core
// migrations. Since this project is local-only, "Development" is effectively always for
// us — but the env guard stays so the production path is an explicit no-op rather than
// a silent migrate-on-boot if production ever enters scope.
//
// Registered as an IHostedService so it only runs when the app actually starts.
// `dotnet ef` design-time host construction stops at service-provider build (hosted
// services are never started), so this won't fire during migrations add/update.
//
// Order discipline (Section F): MUST be registered BEFORE StatusBijectionHealthCheck so
// the tables exist when the bijection check queries them.
public sealed class MigrationRunner : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostEnvironment     _environment;

    public MigrationRunner(IServiceScopeFactory scopeFactory, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(environment);
        _scopeFactory = scopeFactory;
        _environment  = environment;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
            return;

        using IServiceScope  scope   = _scopeFactory.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await context.Database.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

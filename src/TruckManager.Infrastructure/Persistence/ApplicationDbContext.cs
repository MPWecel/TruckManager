using System.Reflection;

using Microsoft.EntityFrameworkCore;

using TruckManager.Common.Abstractions;
using TruckManager.Domain.Aggregates.Trucks;
using TruckManager.Domain.ValueObjects;
using TruckManager.Infrastructure.Persistence.Conversions;
using TruckManager.Infrastructure.Persistence.Entities;

namespace TruckManager.Infrastructure.Persistence;

// [ADR-0004 / ADR-0010]   The single DbContext for the modular monolith. Lives in
// Infrastructure only; no other layer touches it.
//
// [ADR-0029]   ConfigureConventions scans the Domain assembly for every closed type
// implementing IStronglyTypedId<TValue> and registers a generic
// StronglyTypedIdValueConverter<TId, TValue> for it. New aggregate IDs (Phase 5+) pick
// this up automatically — no per-ID configuration needed.
//
// Per-aggregate mapping lives in IEntityTypeConfiguration<> classes under
// Persistence/Configurations/, applied via ApplyConfigurationsFromAssembly so adding a
// new aggregate is a single file drop.
public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Truck>                  Trucks                 => Set<Truck>();
    public DbSet<TruckStatus>            TruckStatuses          => Set<TruckStatus>();
    public DbSet<TruckStatusTransition>  TruckStatusTransitions => Set<TruckStatusTransition>();
    public DbSet<TruckDomainEvent>       TruckDomainEvents      => Set<TruckDomainEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        RegisterStronglyTypedIdConverters(configurationBuilder);
    }

    private static void RegisterStronglyTypedIdConverters(ModelConfigurationBuilder configurationBuilder)
    {
        Assembly domainAssembly = typeof(TenantId).Assembly;

        foreach (Type idType in domainAssembly.GetTypes())
        {
            if (idType.IsAbstract || idType.IsInterface)
                continue;

            Type? markerInterface = idType.GetInterfaces()
                                          .FirstOrDefault(IsStronglyTypedIdInterface);

            if (markerInterface is null)
                continue;

            Type valueType     = markerInterface.GetGenericArguments()[0];
            Type converterType = typeof(StronglyTypedIdValueConverter<,>).MakeGenericType(idType, valueType);

            configurationBuilder.Properties(idType).HaveConversion(converterType);
        }
    }

    private static bool IsStronglyTypedIdInterface(Type @interface) =>
        @interface.IsGenericType &&
        @interface.GetGenericTypeDefinition() == typeof(IStronglyTypedId<>);
}

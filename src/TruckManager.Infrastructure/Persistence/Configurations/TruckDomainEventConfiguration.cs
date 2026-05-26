using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using TruckManager.Domain.Aggregates.Trucks;
using TruckManager.Infrastructure.Persistence.Entities;

namespace TruckManager.Infrastructure.Persistence.Configurations;

// [ADR-0003 / ADR-0030]   Append-only TruckDomainEvents table. Written exclusively by
// DomainEventPersistenceInterceptor (Section C) inside the same transaction as the
// matching aggregate-state change. PayloadJson is `jsonb` (full record serialisation per
// ADR-0030, with EventType as the polymorphic discriminator).
public sealed class TruckDomainEventConfiguration : IEntityTypeConfiguration<TruckDomainEvent>
{
    public void Configure(EntityTypeBuilder<TruckDomainEvent> entity)
    {
        entity.ToTable("TruckDomainEvents");

        entity.HasKey(e => e.EventId);
        entity.Property(e => e.EventId).ValueGeneratedNever();

        entity.Property(e => e.AggregateId).IsRequired();
        entity.Property(e => e.AggregateVersion).IsRequired();
        entity.Property(e => e.EventType).HasMaxLength(128).IsRequired();
        entity.Property(e => e.OccurredAtUtc).IsRequired();
        entity.Property(e => e.PerformedByUserId);
        entity.Property(e => e.TenantId).IsRequired();
        entity.Property(e => e.CorrelationId);
        entity.Property(e => e.CausationId);
        entity.Property(e => e.PayloadJson).HasColumnType("jsonb").IsRequired();

        entity.HasIndex(e => new { e.AggregateId, e.AggregateVersion })
              .HasDatabaseName("IX_TruckDomainEvents_AggregateId_AggregateVersion");

        entity.HasIndex(e => new { e.TenantId, e.OccurredAtUtc })
              .HasDatabaseName("IX_TruckDomainEvents_TenantId_OccurredAtUtc")
              .IsDescending(false, true);

        // The schema-level FK TruckDomainEvents.AggregateId → Trucks.Id is added by the
        // Section E migration (raw AddForeignKey). It is intentionally NOT declared via
        // HasOne/HasForeignKey here because EF Core requires CLR-type alignment between
        // the FK property (raw Guid on this row entity) and the principal PK (TruckId VO
        // on the Truck aggregate). Keeping AggregateId as a raw Guid preserves the
        // Domain/Infrastructure separation; the constraint still lives in the database.
    }
}

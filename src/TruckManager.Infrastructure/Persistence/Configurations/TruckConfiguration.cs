using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using TruckManager.Domain.Aggregates.Trucks;
using TruckManager.Domain.ValueObjects;
using TruckManager.Infrastructure.Persistence.Entities;

namespace TruckManager.Infrastructure.Persistence.Configurations;

// [ADR-0025 / ADR-0026 / ADR-0029 / ADR-0033]   EF Core configuration for the Truck
// aggregate root.
//
// Notable mapping decisions:
//   - TruckId / TenantId — handled by the generic IStronglyTypedId<TValue> convention in
//     ApplicationDbContext.ConfigureConventions (ADR-0029), so no per-property work here.
//   - TruckCode / TruckName / TruckDescription — single-column conversion via
//     property-level ValueConverter (calls each VO's internal FromTrusted helper on load
//     to skip validator re-run). TruckDescription uses convertsNulls: true so Empty maps
//     to NULL in the column.
//   - ConcurrencyStamp — owned-type with Version (ulong→bigint via HasConversion<long>)
//     marked as the EF Core concurrency token.
//   - Status — enum stored as int in the StatusId column with a hard FK to
//     TruckStatuses.Id; ETruckStatus values are aligned with the dictionary PKs.
//   - Partial unique index on (TenantId, Code) WHERE IsDeleted = false (ADR-0033).
//   - Soft-delete global query filter (ADR-0009).
//   - DomainEvents is an in-memory queue drained by DomainEventPersistenceInterceptor
//     (Section C); EF ignores it.
public sealed class TruckConfiguration : IEntityTypeConfiguration<Truck>
{
    private static readonly ValueConverter<TruckCode, string> CodeConverter = new(
        domain => domain.Value,
        db     => TruckCode.FromTrusted(db)
    );

    private static readonly ValueConverter<TruckName, string> NameConverter = new(
        domain => domain.Value,
        db     => TruckName.FromTrusted(db)
    );

    // Phase 4 decision #2 in next-steps.md locks in Empty ↔ NULL for TruckDescription.
    // The only EF Core public API that lets a ValueConverter handle nulls on BOTH sides is
    // the 4-arg constructor with `convertsNulls: true`, which carries the
    // [EntityFrameworkInternal] attribute (EF1001). The feature itself is documented and
    // stable in practice; the attribute signals "subject to change" rather than "do not
    // use." Suppression is scoped to this single declaration so any future deviation is
    // visible at code-review time.
    private static readonly ValueConverter<TruckDescription, string?> DescriptionConverter =
#pragma warning disable EF1001
        new(
            convertToProviderExpression:   domain => domain.IsEmpty ? null : domain.Value,
            convertFromProviderExpression: db     => db == null ? TruckDescription.Empty : TruckDescription.FromTrusted(db),
            convertsNulls: true
        );
#pragma warning restore EF1001

    public void Configure(EntityTypeBuilder<Truck> entity)
    {
        entity.ToTable("Trucks");

        entity.HasKey(t => t.Id);
        entity.Property(t => t.Id).ValueGeneratedNever();

        entity.Property(t => t.TenantId).IsRequired();

        entity.Property(t => t.Code)
              .HasMaxLength(TruckCode.MaxLength)
              .HasConversion(CodeConverter)
              .IsRequired();

        entity.Property(t => t.Name)
              .HasMaxLength(TruckName.MaxLength)
              .HasConversion(NameConverter)
              .IsRequired();

        entity.Property(t => t.Description)
              .HasMaxLength(TruckDescription.MaxLength)
              .HasConversion(DescriptionConverter)
              .IsRequired(false);

        entity.Property(t => t.Status)
              .HasColumnName("StatusId")
              .HasConversion<int>()
              .IsRequired();

        // The schema-level FK Trucks.StatusId → TruckStatuses.Id is added by the Section E
        // migration. It is intentionally NOT declared via HasOne/HasForeignKey here
        // because EF Core requires the FK CLR type (ETruckStatus on the aggregate) to
        // match the principal PK CLR type (int on TruckStatus); the value converter only
        // aligns the column types. Declaring the FK at the migration layer keeps the
        // aggregate's strongly-typed enum intact.

        // ConcurrencyStamp is a two-column owned VO. EF Core's constructor-binding
        // convention treats owned-type properties as navigations and rejects them as ctor
        // parameters — so the aggregate hierarchy provides parameterless EF-materialization
        // ctors (BaseEntity / AuditableEntity / AggregateRoot / Truck) that EF calls,
        // followed by property-setter population. Domain code MUST NOT use those ctors.
        entity.OwnsOne(t => t.ConcurrencyStamp, stamp =>
        {
            stamp.Property(s => s.Version)
                 .HasColumnName("Version")
                 .HasConversion<long>()
                 .IsConcurrencyToken()
                 .IsRequired();

            stamp.Property(s => s.LastModifiedUtc)
                 .HasColumnName("LastModifiedUtc")
                 .IsRequired();
        });

        entity.Property(t => t.CreatedAtUtc).IsRequired();
        entity.Property(t => t.CreatedByUserId).IsRequired();
        entity.Property(t => t.UpdatedAtUtc).IsRequired();
        entity.Property(t => t.UpdatedByUserId).IsRequired();
        entity.Property(t => t.DeletedAtUtc);
        entity.Property(t => t.DeletedByUserId);
        entity.Property(t => t.IsDeleted).HasDefaultValue(false).IsRequired();

        entity.HasIndex(t => new { t.TenantId, t.Code })
              .IsUnique()
              .HasDatabaseName("UX_Trucks_TenantId_Code")
              .HasFilter("\"IsDeleted\" = false");

        entity.HasIndex(t => new { t.TenantId, t.Status })
              .HasDatabaseName("IX_Trucks_TenantId_StatusId")
              .HasFilter("\"IsDeleted\" = false");

        entity.HasQueryFilter(t => !t.IsDeleted);

        entity.Ignore(t => t.DomainEvents);
    }
}

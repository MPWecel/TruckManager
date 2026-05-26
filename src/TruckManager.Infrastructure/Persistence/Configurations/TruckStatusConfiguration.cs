using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using TruckManager.Domain.Enums;
using TruckManager.Infrastructure.Persistence.Entities;

namespace TruckManager.Infrastructure.Persistence.Configurations;

// [ADR-0025]   Dictionary table for the TruckStatuses presentation/operations metadata.
// Numeric Id is value-generated-never because it MUST stay aligned with ETruckStatus
// values (the workflow source of truth). Seed rows match every ETruckStatus member; the
// Phase 4 startup health-check (Section D) enforces this bijection at boot.
public sealed class TruckStatusConfiguration : IEntityTypeConfiguration<TruckStatus>
{
    public void Configure(EntityTypeBuilder<TruckStatus> entity)
    {
        entity.ToTable("TruckStatuses");

        entity.HasKey(s => s.Id);
        entity.Property(s => s.Id).ValueGeneratedNever();

        entity.Property(s => s.Code).HasMaxLength(32).IsRequired();
        entity.Property(s => s.Name).HasMaxLength(64).IsRequired();
        entity.Property(s => s.Sequence).IsRequired();
        entity.Property(s => s.IsSystem).IsRequired();
        entity.Property(s => s.IsActive).IsRequired();

        entity.HasIndex(s => s.Code)
              .IsUnique()
              .HasDatabaseName("UX_TruckStatuses_Code");

        entity.HasData(
            new TruckStatus(Id: (int)ETruckStatus.OutOfService, Code: "OUT_OF_SERVICE", Name: "Out of Service", Sequence: 1, IsSystem: true, IsActive: true),
            new TruckStatus(Id: (int)ETruckStatus.Loading,      Code: "LOADING",        Name: "Loading",        Sequence: 2, IsSystem: true, IsActive: true),
            new TruckStatus(Id: (int)ETruckStatus.ToJob,        Code: "TO_JOB",         Name: "To Job",         Sequence: 3, IsSystem: true, IsActive: true),
            new TruckStatus(Id: (int)ETruckStatus.AtJob,        Code: "AT_JOB",         Name: "At Job",         Sequence: 4, IsSystem: true, IsActive: true),
            new TruckStatus(Id: (int)ETruckStatus.Returning,    Code: "RETURNING",      Name: "Returning",      Sequence: 5, IsSystem: true, IsActive: true)
        );
    }
}

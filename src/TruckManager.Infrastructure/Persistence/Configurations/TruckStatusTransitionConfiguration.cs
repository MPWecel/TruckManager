using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using TruckManager.Domain.Enums;
using TruckManager.Infrastructure.Persistence.Entities;

namespace TruckManager.Infrastructure.Persistence.Configurations;

// [ADR-0007 / ADR-0027]   Dictionary of allowed status transitions, loaded once at
// startup by TruckStatusTransitionPolicy into a HashSet (Section D). V1 seed lists only
// allowed transitions; forbidden moves are modelled by absence.
//
// Seeded workflow (per ETruckStatus / architecture.md §5):
//   - OutOfService ↔ ANY  (bidirectional with every other status)
//   - Forward cycle: Loading → ToJob → AtJob → Returning → Loading
public sealed class TruckStatusTransitionConfiguration : IEntityTypeConfiguration<TruckStatusTransition>
{
    public void Configure(EntityTypeBuilder<TruckStatusTransition> entity)
    {
        entity.ToTable("TruckStatusTransitions");

        entity.HasKey(t => t.Id);
        entity.Property(t => t.Id).ValueGeneratedNever();

        entity.Property(t => t.FromStatusId).IsRequired();
        entity.Property(t => t.ToStatusId).IsRequired();
        entity.Property(t => t.IsAllowed).IsRequired();

        entity.HasIndex(t => new { t.FromStatusId, t.ToStatusId })
              .IsUnique()
              .HasDatabaseName("UX_TruckStatusTransitions_From_To");

        entity.HasOne<TruckStatus>()
              .WithMany()
              .HasForeignKey(t => t.FromStatusId)
              .HasConstraintName("FK_TruckStatusTransitions_TruckStatuses_FromStatusId")
              .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne<TruckStatus>()
              .WithMany()
              .HasForeignKey(t => t.ToStatusId)
              .HasConstraintName("FK_TruckStatusTransitions_TruckStatuses_ToStatusId")
              .OnDelete(DeleteBehavior.Restrict);

        entity.HasData(BuildSeed());
    }

    private static TruckStatusTransition[] BuildSeed()
    {
        int outOfService = (int)ETruckStatus.OutOfService;
        int[] others =
        [
            (int)ETruckStatus.Loading,
            (int)ETruckStatus.ToJob,
            (int)ETruckStatus.AtJob,
            (int)ETruckStatus.Returning,
        ];

        List<TruckStatusTransition> rows = new(capacity: others.Length * 2 + 4);
        int nextId = 1;

        // OutOfService ↔ ANY (bidirectional)
        foreach (int other in others)
        {
            rows.Add(new TruckStatusTransition(nextId++, outOfService, other,        IsAllowed: true));
            rows.Add(new TruckStatusTransition(nextId++, other,        outOfService, IsAllowed: true));
        }

        // Forward operational cycle
        rows.Add(new TruckStatusTransition(nextId++, (int)ETruckStatus.Loading,   (int)ETruckStatus.ToJob,     IsAllowed: true));
        rows.Add(new TruckStatusTransition(nextId++, (int)ETruckStatus.ToJob,     (int)ETruckStatus.AtJob,     IsAllowed: true));
        rows.Add(new TruckStatusTransition(nextId++, (int)ETruckStatus.AtJob,     (int)ETruckStatus.Returning, IsAllowed: true));
        rows.Add(new TruckStatusTransition(nextId++, (int)ETruckStatus.Returning, (int)ETruckStatus.Loading,   IsAllowed: true));

        return rows.ToArray();
    }
}

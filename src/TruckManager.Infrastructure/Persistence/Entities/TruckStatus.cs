using TruckManager.Domain.Enums;

namespace TruckManager.Infrastructure.Persistence.Entities;

// [ADR-0025]   Dictionary table row for `TruckStatuses`. Numeric Id is aligned with the
// ETruckStatus enum (Id == (int)ETruckStatus.X). This type is presentation/operations
// metadata only — the workflow source of truth is ETruckStatus in Domain. Domain code
// MUST NOT reference this type.
//
// Bijection with ETruckStatus is enforced by the Phase 4 startup health-check
// (StatusBijectionHealthCheck, Section D). Adding/removing/renumbering an ETruckStatus
// member must land in the same commit as the matching migration row.
public sealed record TruckStatus(
                                    int    Id,
                                    string Code,
                                    string Name,
                                    int    Sequence,
                                    bool   IsSystem,
                                    bool   IsActive
                                )
{
    public ETruckStatus ToEnum() => (ETruckStatus)Id;
}

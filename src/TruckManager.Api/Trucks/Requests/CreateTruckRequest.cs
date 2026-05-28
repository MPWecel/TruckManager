using TruckManager.Domain.Enums;

namespace TruckManager.Api.Trucks.Requests;

// Phase 6 / Section E    Inbound shape for POST /api/v1/trucks.
// Contains only client-supplied fields. TenantId is server-set in the controller per decision #4 (avoid exposing server-set fields on the API surface) + decision #8 (V1 = Tenants.DefaultTenantId; Phase 9 sources from authenticated context).
public sealed record CreateTruckRequest(
                                           string Code,
                                           string Name,
                                           string? Description,
                                           ETruckStatus InitialStatus
                                       );

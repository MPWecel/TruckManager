using TruckManager.Domain.Enums;

namespace TruckManager.Api.Trucks.Requests;

// Phase 6 / Section E.   Inbound shape for PATCH /api/v1/trucks/{id}/status.
// Status filter encoding per decision #7: clients send the ETruckStatus numeric value (e.g. {"newStatus": 2} for Loading).
// Model binder also accepts the enum name string ("Loading") for free — both work.
public sealed record ChangeTruckStatusRequest(ETruckStatus NewStatus);

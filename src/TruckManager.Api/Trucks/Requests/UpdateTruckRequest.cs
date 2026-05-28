namespace TruckManager.Api.Trucks.Requests;

// Phase 6 / Section E.   Inbound shape for PUT /api/v1/trucks/{id}.
// Both fields nullable — null means "don't change that field" (matches UpdateTruckCommand semantics, which downstream resolves to a no-op via Truck.Update when nothing changed).
public sealed record UpdateTruckRequest(string? Name, string? Description);

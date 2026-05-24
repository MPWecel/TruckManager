using TruckManager.Common.Abstractions;
using TruckManager.Common.Results;
using TruckManager.Domain.Aggregates.Trucks;
using TruckManager.Domain.Enums;
using TruckManager.Domain.ValueObjects;

namespace TruckManager.UnitTests.TestHelpers;

// Shared factory for "I just need a valid Truck" in mutation tests.
// Tests that need specific field values pass them as explicit args; everything else has a sensible default.
// Constructed via Truck.Create so the result is indistinguishable from a real factory call.
internal static class TruckTestFactory
{
    public static Truck NewValid(
                                    IDateTimeProvider clock,
                                    ETruckStatus initialStatus = ETruckStatus.OutOfService,
                                    string codeRaw = "TRUCK01",
                                    string nameRaw = "Test Truck",
                                    string descriptionRaw = "Test description",
                                    Guid? id = null,
                                    TenantId? tenantId = null,
                                    Guid? createdByUserId = null,
                                    Guid? correlationId = null
                                )
    {
        Result<Truck> result = Truck.Create(
                                               id: new TruckId(id ?? Guid.NewGuid()),
                                               tenantId: tenantId ?? TenantId.Default,
                                               code: TruckCode.Create(codeRaw).Value!,
                                               name: TruckName.Create(nameRaw).Value!,
                                               description: TruckDescription.Create(descriptionRaw).Value!,
                                               initialStatus: initialStatus,
                                               clock: clock,
                                               createdByUserId: createdByUserId ?? Guid.NewGuid(),
                                               correlationId: correlationId
                                           );

        return result.Value!;
    }
}

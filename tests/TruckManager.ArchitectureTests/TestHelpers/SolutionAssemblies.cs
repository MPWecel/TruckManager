using System.Reflection;

using TruckManager.Common.Constants;
using TruckManager.Domain.Aggregates.Trucks;

namespace TruckManager.ArchitectureTests.TestHelpers;

// Phase 8 / Section A test helper.   Typed Assembly handles for every production assembly.
//
// Tests use these instead of repeating `typeof(SomeType).Assembly` chains, so the reflection surface lives in one place.
// If a marker type is removed in a future refactor, REPLACE it here with the next-most-stable public type from that assembly — never delete the entry.
//
// Marker types chosen for stability (i.e. lowest probability of being removed across V1 → V2):
//   >  Common          :   Tenants                             (Phase 2 — DefaultTenantId constant)
//   >  Domain          :   Truck                               (Phase 3 — the V1 aggregate root)
//   >  Application     :   Application.DependencyInjection     (Phase 5 — composition root)
//   >  Infrastructure  :   Infrastructure.DependencyInjection  (Phase 4 — composition root)
//   >  Api             :   Api.ProblemDetailsTypes             (Phase 6 — public static URI registry)
internal static class SolutionAssemblies
{
    public static Assembly Common => typeof(Tenants).Assembly;
    public static Assembly Domain => typeof(Truck).Assembly;
    public static Assembly Application => typeof(Application.DependencyInjection).Assembly;
    public static Assembly Infrastructure => typeof(Infrastructure.DependencyInjection).Assembly;
    public static Assembly Api => typeof(Api.ProblemDetailsTypes).Assembly;
}

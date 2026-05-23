namespace TruckManager.Common.Constants;

public static class Tenants
{
    // Synthetic, known-value constant UUID consistent with v7 rules - hence 14th & 18th chars of separated string representation are set to 7 and 8 respectively
    // Used for seeding, testing and defaulting
    // V1 - constant hardcoded value. Further on to be generated and set on runtime, but still available through this constant
    // [ADR-0008]   Tenant resolution delayed until V2. Runtime-generated constant value shall be dependent on version number (keeps us safe until version 16, then we're fucked. Luckily this project won't live that long). See DesignDocument 21.3 for details
    public static readonly Guid DefaultTenantId = new("00000000-0000-7000-8000-000000000001");
}

using Microsoft.EntityFrameworkCore;

using TruckManager.Infrastructure.Persistence;

namespace TruckManager.UnitTests.TestHelpers;

// Creates an ApplicationDbContext backed by EF Core's in-memory provider.
//
// Use a shared dbName to span seeding and the system-under-test across two DbContext instances (avoids identity-resolution conflicts when loading an entity that was previously tracked):
//
//   string db = Guid.NewGuid().ToString();
//   using (var seed = TestDbContextFactory.Create(db)) { seed.Trucks.Add(t); seed.SaveChanges(); }
//   using var ctx = TestDbContextFactory.Create(db);  // same in-memory store
//
// When you need full isolation per test and don't share data, omit dbName — a fresh GUID is used.
//
// Note: DI-registered interceptors (DomainEventPersistenceInterceptor, CreatedAuditFillerInterceptor) are NOT attached here — contexts created directly bypass DI.
// This is intentional for unit tests where domain-event persistence is tested at the integration level (Section G).
internal static class TestDbContextFactory
{
    internal static ApplicationDbContext Create(string? dbName = null)
    {
        string databaseName = dbName ?? Guid.CreateVersion7().ToString();
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(databaseName)
                                                                                                            .Options;

        return new ApplicationDbContext(options);
    }
}

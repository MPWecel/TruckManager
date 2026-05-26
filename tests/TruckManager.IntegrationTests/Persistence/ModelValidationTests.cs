using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using TruckManager.Infrastructure.Persistence;

using Xunit;

namespace TruckManager.IntegrationTests.Persistence;

// First test in the IntegrationTests project — a Phase 4 / Section A smoke that forces
// EF Core to build the model from ApplicationDbContext + all IEntityTypeConfiguration<>
// classes + the IStronglyTypedId<TValue> conversion convention. Building the model is
// when EF Core surfaces configuration errors (bad FK type alignment, missing
// constructors on owned types, value-converter wiring issues, etc.).
//
// This test does NOT connect to Postgres — model building is offline.
public sealed class ModelValidationTests
{
    [Fact]
    public void ApplicationDbContext_Model_BuildsSuccessfully()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=model_validation_placeholder;Username=ignored;Password=ignored")
            .Options;

        using ApplicationDbContext context = new(options);

        // .Model triggers OnModelCreating + ConfigureConventions; any configuration
        // misalignment throws here with EF's diagnostic message.
        var model = context.Model;

        model.GetEntityTypes().Should().NotBeEmpty();
    }
}

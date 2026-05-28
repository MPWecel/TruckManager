using AwesomeAssertions;
using Xunit;

using TruckManager.Domain.ValueObjects;
using TruckManager.UnitTests.TestHelpers;

namespace TruckManager.UnitTests.Domain.Tests.ValueObjects;

public class ConcurrencyStampTests
{
    private static readonly DateTimeOffset T0 = new(2026, 5, 13, 13, 37, 0, TimeSpan.Zero);

    [Fact]
    public void Initial_starts_at_version_1_with_clocks_current_time()
    {
        //Arrange
        FakeDateTimeProvider clock = new(T0);
        ConcurrencyStamp stamp = ConcurrencyStamp.Initial(clock);

        ulong expectedVersion = 1UL;
        DateTimeOffset expectedLastModifiedDate = T0;

        //Assert
        stamp.Version.Should()
                     .Be(expectedVersion);
        stamp.LastModifiedUtc.Should()
                             .Be(expectedLastModifiedDate);
    }

    [Fact]
    public void Next_increments_version_by_exactly_one_and_updates_last_modified()
    {
        //Arrange
        FakeDateTimeProvider clock = new(T0);
        ConcurrencyStamp initial = ConcurrencyStamp.Initial(clock);

        int timeIncrement = 5;
        ulong expectedVersion = 2UL;
        DateTimeOffset expectedLastModifiedDate = T0.AddMinutes(timeIncrement);

        //Act
        clock.Advance(TimeSpan.FromMinutes(timeIncrement));
        ConcurrencyStamp next = initial.Next(clock);

        //Assert
        next.Version.Should()
                    .Be(expectedVersion);
        next.LastModifiedUtc.Should()
                            .Be(expectedLastModifiedDate);
    }

    [Fact]
    public void Repeated_Next_calls_increment_monotonically()
    {
        //Arrange
        FakeDateTimeProvider clock = new(T0);
        ConcurrencyStamp stamp = ConcurrencyStamp.Initial(clock);

        int timeStepIncrementSeconds = 1;
        int timeFinalIncrementSeconds = 5;
        ulong expectedVersion = 6UL;
        DateTimeOffset expectedLastModifiedDate = T0.AddSeconds(timeFinalIncrementSeconds);

        //Act
        for (int i = 0; i < 5; i++)
        {
            clock.Advance(TimeSpan.FromSeconds(timeStepIncrementSeconds));
            stamp = stamp.Next(clock);
        }

        //Assert
        stamp.Version.Should()
                     .Be(expectedVersion);
        stamp.LastModifiedUtc.Should()
                             .Be(expectedLastModifiedDate);
    }

    [Fact]
    public void Initial_does_not_mutate_input_clock()
    {
        //Arrange
        FakeDateTimeProvider clock = new(T0);
        DateTimeOffset captured = clock.UtcNow;

        //Act
        ConcurrencyStamp.Initial(clock);

        //Assert
        clock.UtcNow.Should()
                    .Be(captured);
    }

    [Fact]
    public void Next_returns_a_new_instance_and_does_not_mutate_the_original()
    {
        //Arrange
        FakeDateTimeProvider clock = new(T0);
        ConcurrencyStamp original = ConcurrencyStamp.Initial(clock);

        int timeStepIncrementSeconds = 1;
        ulong expectedVersion = 1UL;
        DateTimeOffset expectedLastModifiedDate = T0;

        //Act
        clock.Advance(TimeSpan.FromSeconds(timeStepIncrementSeconds));
        ConcurrencyStamp updated = original.Next(clock);

        //Assert
        original.Version.Should()
                        .Be(expectedVersion);
        original.LastModifiedUtc.Should()
                                .Be(expectedLastModifiedDate);
        updated.Should()
               .NotBeSameAs(original);
    }

    [Fact]
    public void Two_stamps_with_identical_fields_are_equal()
    {
        //Arrange
        ConcurrencyStamp a = new(3UL, T0);
        ConcurrencyStamp b = new(3UL, T0);

        //Assert
        a.Should()
         .Be(b);
        int aHash = a.GetHashCode();
        int bHash = b.GetHashCode();
        aHash.Should()
             .Be(bHash);
    }

    [Fact]
    public void Stamps_with_different_versions_are_not_equal()
    {
        //Arrange
        ConcurrencyStamp a = new(3UL, T0);
        ConcurrencyStamp b = new(4UL, T0);

        //Assert
        a.Should()
         .NotBe(b);
    }

    [Fact]
    public void Stamps_with_different_timestamps_are_not_equal()
    {
        //Arrange
        ulong versionNo = 3UL;
        ConcurrencyStamp a = new(versionNo, T0);
        ConcurrencyStamp b = new(versionNo, T0.AddSeconds(1));

        //Assert
        a.Should()
         .NotBe(b);
    }

    [Fact]
    public void Initial_throws_when_clock_is_null()
    {
        //Arrange
        Action act = () => ConcurrencyStamp.Initial(null!);

        //Assert
        act.Should()
           .Throw<ArgumentNullException>();
    }

    [Fact]
    public void Next_throws_when_clock_is_null()
    {
        //Arrange
        FakeDateTimeProvider clock = new(T0);
        ConcurrencyStamp stamp = ConcurrencyStamp.Initial(clock);

        Action act = () => stamp.Next(null!);

        //Assert
        act.Should()
           .Throw<ArgumentNullException>();
    }
}

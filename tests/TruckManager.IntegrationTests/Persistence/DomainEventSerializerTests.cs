using AwesomeAssertions;

using TruckManager.Domain.Enums;
using TruckManager.Domain.Events;
using TruckManager.Domain.Events.Trucks;
using TruckManager.Domain.ValueObjects;
using TruckManager.Infrastructure.Persistence.Serialization;

using Xunit;

namespace TruckManager.IntegrationTests.Persistence;

// Round-trip + type-discovery coverage for DomainEventSerializer (ADR-0030). These
// tests are pure-logic (no DB), but live in IntegrationTests because the serializer is
// Infrastructure code and UnitTests is intentionally scoped to Domain/Application.
public sealed class DomainEventSerializerTests
{
    private readonly DomainEventSerializer _sut = new();

    // ----------------------------------------------------------------------------------
    // Type discovery
    // ----------------------------------------------------------------------------------

    [Fact]
    public void Deserialize_unknown_event_type_throws()
    {
        Action act = () => _sut.Deserialize("NotARealEventType", "{}");
        act.Should().Throw<InvalidOperationException>()
                    .WithMessage("*Unknown domain event type 'NotARealEventType'*");
    }

    // ----------------------------------------------------------------------------------
    // Round-trip — one Fact per concrete event type
    // ----------------------------------------------------------------------------------

    [Fact]
    public void TruckCreated_roundtrips_with_non_empty_description()
    {
        TruckCreated original = SampleTruckCreated(description: TruckDescription.Create("Tipper, 6x4, EuroVI").Value!);
        RoundTrip(original).Should().Be(original);
    }

    [Fact]
    public void TruckCreated_roundtrips_with_empty_description()
    {
        TruckCreated original = SampleTruckCreated(description: TruckDescription.Empty);
        TruckCreated rehydrated = (TruckCreated)RoundTrip(original);

        rehydrated.Should().Be(original);
        rehydrated.Description.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void TruckRenamed_roundtrips()
    {
        TruckRenamed original = new(
                                       EventId:           Guid.CreateVersion7(),
                                       AggregateId:       Guid.CreateVersion7(),
                                       AggregateVersion:  3UL,
                                       OccurredAtUtc:     new DateTimeOffset(2026, 5, 24, 12, 0, 0, TimeSpan.Zero),
                                       PerformedByUserId: Guid.CreateVersion7(),
                                       TenantId:          TenantId.Default,
                                       CorrelationId:     Guid.CreateVersion7(),
                                       CausationId:       null,
                                       OldName:           TruckName.Create("Old Truck Name").Value!,
                                       NewName:           TruckName.Create("New Truck Name").Value!
                                   );

        RoundTrip(original).Should().Be(original);
    }

    [Fact]
    public void TruckDescriptionChanged_roundtrips_when_one_side_is_empty()
    {
        TruckDescriptionChanged original = new(
                                                  EventId:           Guid.CreateVersion7(),
                                                  AggregateId:       Guid.CreateVersion7(),
                                                  AggregateVersion:  4UL,
                                                  OccurredAtUtc:     new DateTimeOffset(2026, 5, 24, 12, 5, 0, TimeSpan.Zero),
                                                  PerformedByUserId: null,
                                                  TenantId:          TenantId.Default,
                                                  CorrelationId:     null,
                                                  CausationId:       null,
                                                  OldDescription:    TruckDescription.Empty,
                                                  NewDescription:    TruckDescription.Create("Now described").Value!
                                              );

        TruckDescriptionChanged rehydrated = (TruckDescriptionChanged)RoundTrip(original);

        rehydrated.Should().Be(original);
        rehydrated.OldDescription.IsEmpty.Should().BeTrue();
        rehydrated.NewDescription.Value.Should().Be("Now described");
    }

    [Fact]
    public void TruckStatusChanged_roundtrips()
    {
        TruckStatusChanged original = new(
                                             EventId:           Guid.CreateVersion7(),
                                             AggregateId:       Guid.CreateVersion7(),
                                             AggregateVersion:  5UL,
                                             OccurredAtUtc:     new DateTimeOffset(2026, 5, 24, 12, 10, 0, TimeSpan.Zero),
                                             PerformedByUserId: Guid.CreateVersion7(),
                                             TenantId:          TenantId.Default,
                                             CorrelationId:     null,
                                             CausationId:       null,
                                             FromStatus:        ETruckStatus.Loading,
                                             ToStatus:          ETruckStatus.ToJob
                                         );

        RoundTrip(original).Should().Be(original);
    }

    [Fact]
    public void TruckDeleted_roundtrips()
    {
        TruckDeleted original = new(
                                       EventId:           Guid.CreateVersion7(),
                                       AggregateId:       Guid.CreateVersion7(),
                                       AggregateVersion:  6UL,
                                       OccurredAtUtc:     new DateTimeOffset(2026, 5, 24, 12, 15, 0, TimeSpan.Zero),
                                       PerformedByUserId: Guid.CreateVersion7(),
                                       TenantId:          TenantId.Default,
                                       CorrelationId:     Guid.CreateVersion7(),
                                       CausationId:       Guid.CreateVersion7()
                                   );

        RoundTrip(original).Should().Be(original);
    }

    [Fact]
    public void TruckRestored_roundtrips()
    {
        TruckRestored original = new(
                                        EventId:           Guid.CreateVersion7(),
                                        AggregateId:       Guid.CreateVersion7(),
                                        AggregateVersion:  7UL,
                                        OccurredAtUtc:     new DateTimeOffset(2026, 5, 24, 12, 20, 0, TimeSpan.Zero),
                                        PerformedByUserId: Guid.CreateVersion7(),
                                        TenantId:          TenantId.Default,
                                        CorrelationId:     null,
                                        CausationId:       null
                                    );

        RoundTrip(original).Should().Be(original);
    }

    // ----------------------------------------------------------------------------------
    // EventType discriminator matches the concrete .NET type name
    // ----------------------------------------------------------------------------------

    [Fact]
    public void Serialize_emits_concrete_type_name_as_event_type()
    {
        TruckRestored evt = new(
                                   EventId:           Guid.CreateVersion7(),
                                   AggregateId:       Guid.CreateVersion7(),
                                   AggregateVersion:  1UL,
                                   OccurredAtUtc:     DateTimeOffset.UnixEpoch,
                                   PerformedByUserId: null,
                                   TenantId:          TenantId.Default,
                                   CorrelationId:     null,
                                   CausationId:       null
                               );

        (string eventType, _) = _sut.Serialize(evt);

        eventType.Should().Be(nameof(TruckRestored));
    }

    // ----------------------------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------------------------

    private DomainEvent RoundTrip(DomainEvent original)
    {
        (string eventType, string payloadJson) = _sut.Serialize(original);
        return _sut.Deserialize(eventType, payloadJson);
    }

    private static TruckCreated SampleTruckCreated(TruckDescription description) =>
        new(
            EventId:           Guid.CreateVersion7(),
            AggregateId:       Guid.CreateVersion7(),
            AggregateVersion:  1UL,
            OccurredAtUtc:     new DateTimeOffset(2026, 5, 24, 12, 0, 0, TimeSpan.Zero),
            PerformedByUserId: Guid.CreateVersion7(),
            TenantId:          TenantId.Default,
            CorrelationId:     Guid.CreateVersion7(),
            CausationId:       null,
            Code:              TruckCode.Create("TRK001").Value!,
            Name:              TruckName.Create("Sample Truck").Value!,
            Description:       description,
            Status:            ETruckStatus.OutOfService
        );
}

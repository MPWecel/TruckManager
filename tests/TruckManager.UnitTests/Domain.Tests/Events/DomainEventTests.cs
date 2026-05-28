using AwesomeAssertions;
using Xunit;

using TruckManager.Domain.Events;
using TruckManager.Domain.ValueObjects;

namespace TruckManager.UnitTests.Domain.Tests.Events;

public class DomainEventTests
{
    private static readonly DateTimeOffset T0 = new(2026, 5, 13, 13, 37, 0, TimeSpan.Zero);

    private static TestDomainEvent NewEvent(Guid? eventId = null, string payload = "payload-1")
        => new(
                  EventId: eventId ?? Guid.CreateVersion7(T0),
                  AggregateId: Guid.CreateVersion7(T0),
                  AggregateVersion: 1UL,
                  OccurredAtUtc: T0,
                  PerformedByUserId: null,
                  TenantId: TenantId.Default,
                  CorrelationId: null,
                  CausationId: null,
                  Payload: payload
              );

    [Fact]
    public void Concrete_event_carries_all_base_and_payload_fields_via_positional_ctor()
    {
        //Arrange
        Guid eventId = Guid.CreateVersion7(T0);
        Guid aggregateId = Guid.CreateVersion7(T0);
        ulong aggregateVersion = 7UL;
        Guid userId = Guid.CreateVersion7(T0);
        Guid correlationId = Guid.CreateVersion7(T0);
        Guid causationId = Guid.CreateVersion7(T0);
        string payload = "Good Morning, Vietnam!";

        TestDomainEvent evt = new(
                                     EventId: eventId,
                                     AggregateId: aggregateId,
                                     AggregateVersion: aggregateVersion,
                                     OccurredAtUtc: T0,
                                     PerformedByUserId: userId,
                                     TenantId: TenantId.Default,
                                     CorrelationId: correlationId,
                                     CausationId: causationId,
                                     Payload: payload
                                 );

        //Assert
        evt.EventId.Should()
                   .Be(eventId);
        evt.AggregateId.Should()
                       .Be(aggregateId);
        evt.AggregateVersion.Should()
                            .Be(aggregateVersion);
        evt.OccurredAtUtc.Should()
                         .Be(T0);
        evt.PerformedByUserId.Should()
                             .Be(userId);
        evt.TenantId.Should()
                    .Be(TenantId.Default);
        evt.CorrelationId.Should()
                         .Be(correlationId);
        evt.CausationId.Should()
                       .Be(causationId);
        evt.Payload.Should()
                   .Be(payload);
    }

    [Fact]
    public void Two_events_with_identical_fields_are_equal()
    {
        //Arrange
        Guid sharedEventId = Guid.CreateVersion7(T0);

        TestDomainEvent a = NewEvent(sharedEventId);
        TestDomainEvent b = a with { };

        //Assert
        a.Should()
         .Be(b);
        a.GetHashCode().Should()
                       .Be(b.GetHashCode());
    }

    [Fact]
    public void Two_events_with_different_EventId_are_not_equal()
    {
        //Arrange
        TestDomainEvent a = NewEvent();
        Guid newEventId = Guid.CreateVersion7(T0.AddSeconds(1));
        TestDomainEvent b = a with { EventId = newEventId };

        //Assert
        a.Should()
         .NotBe(b);
    }

    [Fact]
    public void Two_events_with_different_payload_are_not_equal()
    {
        //Arrange
        string payloadA = "Star Wars";
        string payloadB = "Star Trek";
        TestDomainEvent a = NewEvent(payload: payloadA);
        TestDomainEvent b = a with { Payload = payloadB };

        //Assert
        a.Should()
         .NotBe(b);
    }

    [Fact]
    public void Event_is_assignable_to_DomainEvent_base()
    {
        //Arrange
        TestDomainEvent evt = NewEvent();

        //Assert
        evt.Should()
           .BeAssignableTo<DomainEvent>();
    }

    #region TestEventType

    private sealed record TestDomainEvent(
                                             Guid EventId,
                                             Guid AggregateId,
                                             ulong AggregateVersion,
                                             DateTimeOffset OccurredAtUtc,
                                             Guid? PerformedByUserId,
                                             TenantId TenantId,
                                             Guid? CorrelationId,
                                             Guid? CausationId,
                                             string Payload
                                         ) : DomainEvent(
                                                            EventId, 
                                                            AggregateId, 
                                                            AggregateVersion, 
                                                            OccurredAtUtc, 
                                                            PerformedByUserId, 
                                                            TenantId, 
                                                            CorrelationId, 
                                                            CausationId
                                                        );
    #endregion
}

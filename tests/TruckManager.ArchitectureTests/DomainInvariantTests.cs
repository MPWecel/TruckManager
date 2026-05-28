using System.Reflection;
using AwesomeAssertions;
using NetArchTest.Rules;
using Xunit;

using TruckManager.Common.Abstractions;
using TruckManager.Domain.Common;
using TruckManager.Domain.Enums;
using TruckManager.Domain.Events;
using TruckManager.ArchitectureTests.TestHelpers;

// Same Xunit.TestResult / NetArchTest.Rules.TestResult collision as LayerDependencyTests.
using TestResult = NetArchTest.Rules.TestResult;

namespace TruckManager.ArchitectureTests;

// Phase 8 / Section C.   Aggregate / VO / event / enum / encapsulation invariants.
// Each test enforces one ADR-recorded design rule that's easy to break by accident in a future refactor.
// NetArchTest covers type-shape rules; the encapsulation + enum tests fall back to reflection because NetArchTest doesn't expose property-setter / enum-value predicates.
//
// Cross-referenced ADRs:
//   >  [ADR-0023]   Strongly-typed IDs via IStronglyTypedId<TValue> marker.
//   >  [ADR-0024]   DomainEvent is `sealed record` deriving from the abstract base.
//   >  [ADR-0025]   ETruckStatus members carry explicit numeric values aligned with the dictionary.
//   >  [ADR-0029]   Generic value-converter registration via the marker (relies on every ID type implementing it).
//   >  [ADR-0032]   IAggregateRoot marker — every concrete aggregate must implement it via AggregateRoot<TId>.
public class DomainInvariantTests
{
    #region Aggregates
    // See [ADR-0032]

    [Fact]
    public void Concrete_aggregate_roots_inherit_AggregateRoot_open_generic()
    {
        // NetArchTest can't see interfaces implemented via base classes (Mono.Cecil only walks declared interfaces).
        // Truck implements IAggregateRoot via AggregateRoot<TruckId>, not directly — so we use reflection to walk the inheritance chain.
        //
        // Strategy: find every concrete class that resolves IAggregateRoot at the CLR level, then assert each one derives from the AggregateRoot<TId> open generic.
        // Per the IAggregateRoot marker comment, no concrete aggregate should implement the marker directly —
        // - it must go through AggregateRoot<TId> so the audit + event-queue plumbing lands without re-implementation.
        Type[] concreteAggregateRoots = SolutionAssemblies.Domain
                                                          .GetTypes()
                                                          .Where(t => t.IsClass && !t.IsAbstract)
                                                          .Where(t => typeof(IAggregateRoot).IsAssignableFrom(t))
                                                          .ToArray();

        concreteAggregateRoots.Should()
                              .NotBeEmpty("V1 ships Truck as the single aggregate root — at minimum, that must be found.");

        string[] violations = concreteAggregateRoots.Where(t => !DerivesFromOpenGeneric(t, typeof(AggregateRoot<>)))
                                                    .Select(t => t.FullName ?? t.Name)
                                                    .ToArray();

        violations.Should()
                  .BeEmpty(
                      $"every concrete aggregate must inherit AggregateRoot<TId> rather than implement IAggregateRoot directly (ADR-0032 marker note). Offenders: {String.Join(", ", violations)}"
                  );
    }

    #endregion

    #region Events
    // See [ADR-0024]

    [Fact]
    public void Concrete_domain_events_are_sealed_and_inherit_DomainEvent()
    {
        // ResideInNamespace does prefix-match — picks up everything under Domain.Events.*.
        // AreNotAbstract excludes the abstract DomainEvent base itself.
        TestResult result = Types.InAssembly(SolutionAssemblies.Domain)
                                 .That()
                                 .ResideInNamespace("TruckManager.Domain.Events")
                                 .And()
                                 .AreClasses()
                                 .And()
                                 .AreNotAbstract()
                                 .Should()
                                 .BeSealed()
                                 .And()
                                 .Inherit(typeof(DomainEvent))
                                 .GetResult();

        result.IsSuccessful.Should()
                           .BeTrue(BuildFailureMessage("DomainEvent shape (sealed + inherits)", result));
    }

    [Fact]
    public void Concrete_domain_events_live_in_per_aggregate_sub_namespace()
    {
        // Convention: Domain/Events/<AggregateName>/<EventName>.cs — e.g. Domain/Events/Trucks/TruckCreated.cs.
        // No concrete event should live at the Domain.Events root directly. The abstract DomainEvent base IS allowed at the root.
        Type[] eventsAtRoot = SolutionAssemblies.Domain
                                                .GetTypes()
                                                .Where(t => t.IsClass
                                                         && !t.IsAbstract
                                                         && typeof(DomainEvent).IsAssignableFrom(t)
                                                         && t.Namespace == "TruckManager.Domain.Events")
                                                .ToArray();

        eventsAtRoot.Should()
                    .BeEmpty(
                        $"concrete domain events must live in per-aggregate sub-namespaces (e.g. Domain.Events.Trucks). Offenders at the root: {String.Join(", ", eventsAtRoot.Select(t => t.FullName))}"
                    );
    }

    #endregion

    #region ValueObjects
    // [ADR-0023] / [ADR-0029]

    [Fact]
    public void Guid_backed_Id_types_in_ValueObjects_implement_IStronglyTypedId_Guid()
    {
        // Convention: every type in Domain.ValueObjects whose name ends with "Id" is a strongly-typed ID and MUST implement IStronglyTypedId<Guid>.
        // The marker drives the generic EF value-converter registration in Phase 4 (ADR-0029) — a missing marker means a missing converter means a runtime EF crash when the type is first persisted.
        //
        // NetArchTest's ImplementInterface(typeof(IStronglyTypedId<Guid>)) is unreliable for closed generic interfaces
        // — Mono.Cecil exposes the open generic on the type definition and NetArchTest's closed-form matching frequently misses it.
        // Use CLR reflection's IsAssignableFrom instead, which understands constructed generic interfaces.
        Type[] idTypes = SolutionAssemblies.Domain
                                           .GetTypes()
                                           .Where(t => (t.IsClass || t.IsValueType) && !t.IsAbstract)
                                           .Where(t => t.Namespace != null
                                                    && t.Namespace.StartsWith("TruckManager.Domain.ValueObjects"))
                                           .Where(t => t.Name.EndsWith("Id"))
                                           .ToArray();

        idTypes.Should()
               .NotBeEmpty("V1 ships TruckId at minimum — at least one strongly-typed Id must be present.");

        string[] violations = idTypes.Where(t => !typeof(IStronglyTypedId<Guid>).IsAssignableFrom(t))
                                     .Select(t => t.FullName ?? t.Name)
                                     .ToArray();

        violations.Should()
                  .BeEmpty(
                      $"every *Id type in Domain.ValueObjects must implement IStronglyTypedId<Guid> (ADR-0023 + ADR-0029). Offenders: {String.Join(", ", violations)}"
                  );
    }

    #endregion

    #region Enums
    // Enum drift guard [ADR-0025] TruckStatuses dictionary alignment

    [Fact]
    public void ETruckStatus_members_have_nonzero_unique_numeric_values()
    {
        // At runtime we cannot syntactically distinguish "explicit = N" from "positional default N" — both produce the same IL.
        // We CAN, however, verify the value SET is consistent with the ADR-0025 contract:
        //   >  no member at value 0        :   C#'s default-on-default-construct trap that [ADR-0025] explicitly avoids by starting the dictionary PK at 1.
        //   >  all values unique           :   dictionary PK constraint requires it.
        //   >  values strictly ascending   :   readability convention; reorder of source = test fails.
        //
        // The runtime StatusBijectionHealthCheck (Phase 4) handles the deeper "enum <-> dictionary row bijection" check at startup; this test guards the source-code shape.
        int[] values = Enum.GetValues<ETruckStatus>()
                           .Select(v => (int)v)
                           .ToArray();

        values.Should()
              .NotContain(0, "ADR-0025 starts the dictionary PK at 1; member at 0 is a default-construct trap.");
        values.Should()
              .OnlyHaveUniqueItems("dictionary FK requires unique numeric values per enum member.");
        values.Should()
              .BeInAscendingOrder("members are defined in workflow sequence; reordering should fail this test until the test is updated deliberately.");
    }

    #endregion

    #region Encapsulation
    // [ADR-0006] / [ADR-0023]

    [Fact]
    public void Domain_types_do_not_expose_public_setters_for_Id_TenantId_or_ConcurrencyStamp()
    {
        // ADR-0006 / ADR-0023: identity + concurrency-token mutation belong inside aggregate methods (e.g. Truck.ApplyMutation).
        // Public setters would let external code (or EF misconfiguration) bypass the contract — version drift, broken concurrency check, etc.
        //
        // BindingFlags.DeclaredOnly so we don't double-count inherited properties through the BaseEntity → AuditableEntity → AggregateRoot chain.
        //
        // Init-only setters (records with positional parameters compile each parameter to a public { get; init; } property) are EXEMPT —
        // — `init` is write-once-at-construction and cannot mutate identity post-creation. The IsExternalInit modreq distinguishes init from set at the IL level.
        string[] forbiddenPropertyNames = ["Id", "TenantId", "ConcurrencyStamp"];

        string[] violations = SolutionAssemblies.Domain
                                                .GetTypes()
                                                .SelectMany(type => type.GetProperties(
                                                                                          BindingFlags.Public
                                                                                        | BindingFlags.Instance
                                                                                        | BindingFlags.DeclaredOnly
                                                                                      ))
                                                .Where(prop => forbiddenPropertyNames.Contains(prop.Name))
                                                .Where(prop => prop.SetMethod is { IsPublic: true })
                                                .Where(prop => !IsInitOnlySetter(prop.SetMethod!))
                                                .Select(prop => $"{prop.DeclaringType?.FullName}.{prop.Name}")
                                                .ToArray();

        violations.Should()
                  .BeEmpty(
                      $"these properties must not have public setters (use protected/private set or init):\n  - {String.Join("\n  - ", violations)}"
                  );
    }

    #endregion

    #region Helpers

    private static string BuildFailureMessage(string ruleName, TestResult result)
    {
        if (result.IsSuccessful)
            return String.Empty;

        IEnumerable<string> offenders = result.FailingTypes?
                                              .Select(t => t.FullName ?? t.Name) ?? [];
        string list = String.Join("\n  - ", offenders);
        return $"{ruleName} violated. Offending types:\n  - {list}";
    }

    // Walks the BaseType chain checking each level for `type.IsGenericType && GetGenericTypeDefinition() == openGeneric`.
    // Used to detect derivation from open generics like AggregateRoot<> — `IsAssignableFrom` doesn't accept open generics, and `IsSubclassOf` only works with closed generics.
    private static bool DerivesFromOpenGeneric(Type type, Type openGeneric)
    {
        for (Type? current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == openGeneric)
                return true;
        }
        return false;
    }

    // C# init-only setters compile to a regular set_X method whose return-type modreq list includes System.Runtime.CompilerServices.IsExternalInit.
    // That modreq is how the C# compiler distinguishes `init` from `set` at the IL level; PropertyInfo.SetMethod returns the same MethodInfo for either, so this check is the only reliable runtime discriminator.
    private static bool IsInitOnlySetter(MethodInfo setter) => setter.ReturnParameter.GetRequiredCustomModifiers().Any(IsExternalInit());

    private static Func<Type, bool> IsExternalInit() => m => m.FullName == "System.Runtime.CompilerServices.IsExternalInit";

    #endregion

}

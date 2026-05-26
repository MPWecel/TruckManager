using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

using TruckManager.Domain.Enums;
using TruckManager.Infrastructure.Persistence;
using TruckManager.Infrastructure.Persistence.Entities;

namespace TruckManager.Infrastructure.Workflows;

// [ADR-0025 / ADR-0027]   Single startup pass that (a) validates `ETruckStatus` ↔
// `TruckStatuses` bijection, (b) validates every `TruckStatusTransitions` row references
// known status IDs, and (c) primes `TruckStatusTransitionPolicy` with the loaded
// transitions. The two-part validation per ADR-0025 fails fast: throwing in StartAsync
// stops the app from booting, so a corrupted seed never serves traffic.
//
// Dual interfaces:
//   - IHostedService  — fail-fast at startup (the actual data check; Section F registers).
//   - IHealthCheck    — exposed at /health/ready (Section F maps the endpoint). Returns
//                       Healthy iff StartAsync completed without throwing. Trivial in V1
//                       since data is migration-seeded and never mutated, but the
//                       endpoint stays a useful "the app booted and self-validated"
//                       signal for operators.
public sealed class StatusBijectionHealthCheck : IHostedService, IHealthCheck
{
    private readonly IServiceScopeFactory        _scopeFactory;
    private readonly TruckStatusTransitionPolicy _policy;

    private volatile bool   _isReady;
    private          string _failureReason = string.Empty;

    public StatusBijectionHealthCheck(
                                         IServiceScopeFactory        scopeFactory,
                                         TruckStatusTransitionPolicy policy
                                     )
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(policy);
        _scopeFactory = scopeFactory;
        _policy       = policy;
    }

    // ----------------------------------------------------------------------------------
    // IHostedService — startup
    // ----------------------------------------------------------------------------------

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using IServiceScope     scope   = _scopeFactory.CreateScope();
        ApplicationDbContext    context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        List<TruckStatus>           statuses    = await context.TruckStatuses
                                                                .AsNoTracking()
                                                                .ToListAsync(cancellationToken);
        List<TruckStatusTransition> transitions = await context.TruckStatusTransitions
                                                                .AsNoTracking()
                                                                .ToListAsync(cancellationToken);

        try
        {
            ValidateBijection(statuses);
            ValidateTransitionsReferenceKnownStatuses(transitions, statuses);
            _policy.LoadFrom(transitions);
            _isReady = true;
        }
        catch (InvalidOperationException ex)
        {
            _failureReason = ex.Message;
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // ----------------------------------------------------------------------------------
    // IHealthCheck — readiness endpoint
    // ----------------------------------------------------------------------------------

    public Task<HealthCheckResult> CheckHealthAsync(
                                                       HealthCheckContext context,
                                                       CancellationToken  cancellationToken = default
                                                   )
    {
        HealthCheckResult result = _isReady
            ? HealthCheckResult.Healthy("ETruckStatus ↔ TruckStatuses bijection validated; transition policy initialised.")
            : HealthCheckResult.Unhealthy(_failureReason.Length > 0
                                              ? _failureReason
                                              : "Workflow dictionary has not been validated yet.");

        return Task.FromResult(result);
    }

    // ----------------------------------------------------------------------------------
    // Validation primitives
    // ----------------------------------------------------------------------------------

    private static void ValidateBijection(IReadOnlyList<TruckStatus> statuses)
    {
        HashSet<int> enumValues = Enum.GetValues<ETruckStatus>()
                                      .Select(v => (int)v)
                                      .ToHashSet();
        HashSet<int> rowIds = statuses.Select(s => s.Id).ToHashSet();

        List<int> missingRows = enumValues.Except(rowIds).OrderBy(id => id).ToList();
        List<int> orphanRows  = rowIds.Except(enumValues).OrderBy(id => id).ToList();

        if (missingRows.Count == 0 && orphanRows.Count == 0)
            return;

        throw new InvalidOperationException(
            $"ETruckStatus ↔ TruckStatuses bijection violated. " +
            $"Enum values without a matching dictionary row: [{string.Join(", ", missingRows)}]. " +
            $"Dictionary rows without a matching enum value: [{string.Join(", ", orphanRows)}]. " +
            $"Add the missing migration row or the missing enum member (per ADR-0025 these must stay aligned)."
        );
    }

    private static void ValidateTransitionsReferenceKnownStatuses(
                                                                     IReadOnlyList<TruckStatusTransition> transitions,
                                                                     IReadOnlyList<TruckStatus>           statuses
                                                                 )
    {
        HashSet<int> knownStatusIds = statuses.Select(s => s.Id).ToHashSet();

        List<TruckStatusTransition> badRows = transitions
            .Where(t => !knownStatusIds.Contains(t.FromStatusId) || !knownStatusIds.Contains(t.ToStatusId))
            .ToList();

        if (badRows.Count == 0)
            return;

        string listing = string.Join("; ",
            badRows.Select(t => $"#{t.Id}({t.FromStatusId} → {t.ToStatusId})"));

        throw new InvalidOperationException(
            $"TruckStatusTransitions has rows pointing at unknown TruckStatuses.Id values: {listing}."
        );
    }
}

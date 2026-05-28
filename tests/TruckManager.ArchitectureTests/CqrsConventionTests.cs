using System.Reflection;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

using TruckManager.Application.Abstractions.Cqrs;
using TruckManager.Application.Abstractions.Persistence;
using TruckManager.ArchitectureTests.TestHelpers;

namespace TruckManager.ArchitectureTests;

// Phase 8 / Section D.   CQRS conventions ([ADR-0038] / [ADR-0039] / [ADR-0040]).
//
// Mix of reflection (type-shape rules) and text-grep over `src/**/*.cs` (call-site rules).
// Per decision #5 in next-steps.md, the text-grep mechanism is preferred over a Roslyn-AST analyzer for V1; if false positives surface we'll escalate to a Microsoft.CodeAnalysis.CSharp-based check and record an ADR.
//
// Cross-referenced ADRs:
//   >  [ADR-0038]   Handler / pipeline-behavior abstractions; dispatcher composes handlers + behaviors.
//   >  [ADR-0039]   UnitOfWorkBehavior is the SOLE caller of SaveChangesAsync — handlers must not commit.
//   >  [ADR-0040]   Handlers inject IApplicationDbContext (the interface), never the concrete ApplicationDbContext.
public class CqrsConventionTests
{
    #region HandlerInterfaceConformance
    // ([ADR-0038]) every *Handler in a slice folder implements one of the three handler shapes.

    [Fact]
    public void Every_Command_handler_implements_ICommandHandler()
    {
        // Slice convention: file lives in Application.Trucks.Commands.<CommandName>.<CommandName>Handler.
        // The matching interface is either ICommandHandler<TCommand> (resultless) or ICommandHandler<TCommand,TResult> (payload-returning).
        // We don't care WHICH of the two — only that one is implemented.
        Type[] commandHandlers = SolutionAssemblies.Application
                                                   .GetTypes()
                                                   .Where(t => t.IsClass && !t.IsAbstract)
                                                   .Where(t => t.Namespace is not null
                                                            && t.Namespace.StartsWith("TruckManager.Application.Trucks.Commands."))
                                                   .Where(t => t.Name.EndsWith("Handler"))
                                                   .ToArray();

        commandHandlers.Should()
                       .NotBeEmpty("V1 ships CreateTruck + UpdateTruck + ChangeTruckStatus + DeleteTruck command handlers — at minimum four must be present.");

        string[] violations = commandHandlers.Where(t => !ImplementsOpenGeneric(t, typeof(ICommandHandler<>))
                                                      && !ImplementsOpenGeneric(t, typeof(ICommandHandler<,>)))
                                             .Select(t => t.FullName ?? t.Name)
                                             .ToArray();

        violations.Should()
                  .BeEmpty(
                      $"every *Handler under Application.Trucks.Commands.** must implement ICommandHandler<T> or ICommandHandler<T,TResult> ([ADR-0038]). Offenders: {String.Join(", ", violations)}"
                  );
    }

    [Fact]
    public void Every_Query_handler_implements_IQueryHandler()
    {
        // Symmetric to the command-handler rule. There is no resultless query shape ([ADR-0038]) — all queries return a typed Result.
        Type[] queryHandlers = SolutionAssemblies.Application
                                                 .GetTypes()
                                                 .Where(t => t.IsClass && !t.IsAbstract)
                                                 .Where(t => t.Namespace is not null
                                                          && t.Namespace.StartsWith("TruckManager.Application.Trucks.Queries."))
                                                 .Where(t => t.Name.EndsWith("Handler"))
                                                 .ToArray();

        queryHandlers.Should()
                     .NotBeEmpty("V1 ships GetTruckById + ListTrucks query handlers — at minimum two must be present.");

        string[] violations = queryHandlers.Where(t => !ImplementsOpenGeneric(t, typeof(IQueryHandler<,>)))
                                           .Select(t => t.FullName ?? t.Name)
                                           .ToArray();

        violations.Should()
                  .BeEmpty(
                      $"every *Handler under Application.Trucks.Queries.** must implement IQueryHandler<TQuery,TResult> ([ADR-0038]). Offenders: {String.Join(", ", violations)}"
                  );
    }

    #endregion

    #region DbContextInjection
    // ([ADR-0040]) — handlers inject the IApplicationDbContext interface, never the concrete ApplicationDbContext.

    [Fact]
    public void No_handler_constructor_takes_concrete_ApplicationDbContext_or_a_DbContext_other_than_IApplicationDbContext()
    {
        // Transitively this is enforced by the LayerDependencyTests ban on `Application → Infrastructure` (the concrete ApplicationDbContext lives in Infrastructure, so Application code has no way to NAME the type).
        // We keep the dedicated test for two reasons:
        //   1.   If someone ever decides to move ApplicationDbContext (or a sibling DbContext) into Application, the dependency ban silently stops protecting this rule — but THIS test still does.
        //   2.   It expresses the intent of [ADR-0040] directly in failure-message form, so a future contributor sees the ADR name when the rule trips, not just a generic "Application references Infrastructure" error.
        //
        // Rule: any constructor parameter on a *Handler whose type name contains "DbContext" MUST be exactly IApplicationDbContext.
        Type[] handlers = SolutionAssemblies.Application
                                            .GetTypes()
                                            .Where(t => t.IsClass && !t.IsAbstract)
                                            .Where(t => t.Namespace is not null
                                                     && t.Namespace.StartsWith("TruckManager.Application.Trucks."))
                                            .Where(t => t.Name.EndsWith("Handler"))
                                            .ToArray();

        string[] violations = handlers.SelectMany(h => h.GetConstructors()
                                                        .SelectMany(c => c.GetParameters())
                                                        .Where(p => p.ParameterType.Name.Contains("DbContext", StringComparison.Ordinal))
                                                        .Where(p => p.ParameterType != typeof(IApplicationDbContext))
                                                        .Select(p => $"{h.FullName}.ctor({p.ParameterType.FullName} {p.Name})"))
                                      .ToArray();

        violations.Should()
                  .BeEmpty(
                      $"handlers must inject IApplicationDbContext, not a concrete DbContext type ([ADR-0040]). Offenders:\n  - {String.Join("\n  - ", violations)}"
                  );
    }

    #endregion

    #region SliceFolderShape
    // Folder-shape rule (Phase 5 vertical-slice convention).   Each slice folder = exactly three .cs files.

    [Theory]
    [InlineData("Commands", "Command")]
    [InlineData("Queries", "Query")]
    public void Slice_folder_contains_exactly_three_files_named_by_convention(string sliceKind, string requestSuffix)
    {
        // Layout per Phase 5:
        //   Application/Trucks/<sliceKind>/<Name>/<Name><requestSuffix>.cs   (the command/query DTO)
        //   Application/Trucks/<sliceKind>/<Name>/<Name>Handler.cs           (the handler)
        //   Application/Trucks/<sliceKind>/<Name>/<Name>Validator.cs         (the FluentValidator)
        //
        // No siblings, no helper files. If a slice needs a private projection / extension, it goes either inside Handler.cs (private static method) or in the parent Trucks/Queries folder (e.g. TruckQueryExtensions.cs — out of slice scope by design).
        string sliceRoot = Path.Combine(SourceRoots.Application, "Trucks", sliceKind);
        Directory.Exists(sliceRoot)
                 .Should()
                 .BeTrue($"slice root '{sliceRoot}' must exist on disk; otherwise this test gives a false green.");

        List<string> errors = [];

        foreach (string sliceDir in Directory.EnumerateDirectories(sliceRoot))
        {
            string sliceName = Path.GetFileName(sliceDir);
            string[] expected =
            [
                $"{sliceName}{requestSuffix}.cs",
                $"{sliceName}Handler.cs",
                $"{sliceName}Validator.cs",
            ];
            string[] actual = Directory.EnumerateFiles(sliceDir, "*.cs", SearchOption.TopDirectoryOnly)
                                       .Select(Path.GetFileName)
                                       .OfType<string>()
                                       .OrderBy(s => s, StringComparer.Ordinal)
                                       .ToArray();

            string[] expectedSorted = expected.OrderBy(s => s, StringComparer.Ordinal).ToArray();
            if (!actual.SequenceEqual(expectedSorted, StringComparer.Ordinal))
            {
                errors.Add($"  - {sliceDir}\n      expected: [{String.Join(", ", expectedSorted)}]\n      actual:   [{String.Join(", ", actual)}]");
            }
        }

        errors.Should()
              .BeEmpty(
                  $"each Application/Trucks/{sliceKind}/<Name>/ slice must contain exactly three files: <Name>{requestSuffix}.cs + <Name>Handler.cs + <Name>Validator.cs (Phase 5 vertical-slice convention). Violations:\n{String.Join("\n", errors)}"
              );
    }

    #endregion

    #region CallSiteSaveChangesBan
    // ([ADR-0039]) — call-site rule.   No SaveChangesAsync invocation in src/TruckManager.Application/**/*.cs.

    [Fact]
    public void No_Application_source_file_calls_SaveChangesAsync()
    {
        // UnitOfWorkBehavior is the SOLE commit path ([ADR-0039]).
        // It calls _uow.SaveChangesAsync — that is on IUnitOfWork, not on the DbContext directly, and IUnitOfWork is implemented in Infrastructure.
        // The regex below targets `<anything>.SaveChangesAsync(` ; UnitOfWorkBehavior's `_uow.SaveChangesAsync(...)` would naively match, so we exclude that file explicitly.
        // Any OTHER call-site is forbidden.
        //
        // Mechanism: text-grep over .cs files (decision #5). False-positive risk on string literals containing "SaveChangesAsync(" — accepted; the cost of a Roslyn AST pass is higher than the cost of disambiguating a string literal once.
        Regex pattern = new(@"\.SaveChangesAsync\s*\(", RegexOptions.Compiled);
        string allowed = Path.GetFullPath(Path.Combine(SourceRoots.Application, "Behaviors", "UnitOfWorkBehavior.cs"));

        List<string> hits = [];

        foreach (string file in Directory.EnumerateFiles(SourceRoots.Application, "*.cs", SearchOption.AllDirectories))
        {
            // Skip generated obj/ + Release/ trees (EnumerateFiles walks them by default).
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                continue;
            
            if (Path.GetFullPath(file).Equals(allowed, StringComparison.OrdinalIgnoreCase))
                continue;

            string content = File.ReadAllText(file);
            if (pattern.IsMatch(content))
                hits.Add(file);
        }

        hits.Should()
            .BeEmpty(
                $"only UnitOfWorkBehavior may call SaveChangesAsync ([ADR-0039]); handlers must remain stateless w.r.t. transaction control. Offending files:\n  - {String.Join("\n  - ", hits)}"
            );
    }

    #endregion

    #region Helpers

    // Walks a type's declared interfaces (and its base-type chain's declared interfaces) checking each for `t.IsGenericType && GetGenericTypeDefinition() == openGeneric`.
    // Used to detect that a handler implements one of the open-generic CQRS handler interfaces — `IsAssignableFrom` doesn't accept open generics directly.
    private static bool ImplementsOpenGeneric(Type type, Type openGeneric)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
        {
            foreach (Type iface in current.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == openGeneric)
                    return true;
            }
        }
        return false;
    }

    #endregion
}

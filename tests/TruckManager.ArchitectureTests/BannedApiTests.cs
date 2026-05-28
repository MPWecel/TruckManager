using System.Reflection;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

using TruckManager.ArchitectureTests.TestHelpers;

namespace TruckManager.ArchitectureTests;

// Phase 8 / Section E.   Banned-API enforcement ([ADR-0010] / [ADR-0015] / [ADR-0041]).
//
// Three rules, two mechanisms:
//   >  Reflection over loaded assemblies: catches type-level violations (the IRepository<T> ban) without touching source files.
//   >  Text-grep over src/**/*.cs:        catches call-site violations the type system can't see — DateTime.UtcNow and string-interpolated log templates.
//
// The text-grep mechanism (decision #5 in next-steps.md) is preferred over a Roslyn-AST analyzer for V1. If false positives surface, the escalation path is a Microsoft.CodeAnalysis.CSharp-based check (recorded as a follow-up ADR).
//
// All three tests use NonCommentLines(...) to skip `//` comments — without that, the comments inside IDateTimeProvider.cs that DOCUMENT the ban ("never call DateTime.UtcNow") would themselves trip the test.
//
// Cross-referenced ADRs:
//   >  [ADR-0010]   No generic repositories — handlers use DbSet<T> via IApplicationDbContext directly.
//   >  [ADR-0015]   IDateTimeProvider is the only legal source of DateTime[Offset].UtcNow; SystemDateTimeProvider.cs is the lone exception.
//   >  [ADR-0041]   Structured logging only — log message templates are constant strings with `{Placeholder}` tokens, never interpolated `$"..."`.
public class BannedApiTests
{
    #region DateTimeUtcNowBan
    // ([ADR-0015]) — only SystemDateTimeProvider.cs may reference DateTime[Offset].UtcNow.

    [Fact]
    public void Only_SystemDateTimeProvider_references_DateTime_UtcNow()
    {
        // Pattern catches both `DateTime.UtcNow` and `DateTimeOffset.UtcNow` (the latter is what SystemDateTimeProvider returns).
        // \b at both ends prevents partial matches inside identifiers like `MyDateTimeUtcNowWrapper`.
        Regex pattern = new(@"\bDateTime(Offset)?\.UtcNow\b", RegexOptions.Compiled);

        string allowed = Path.GetFullPath(Path.Combine(SourceRoots.Infrastructure, "Time", "SystemDateTimeProvider.cs"));

        List<string> hits = new();

        foreach (string projectRoot in AllProductionSourceRoots)
        {
            foreach (string file in EnumerateProductionFiles(projectRoot))
            {
                if (Path.GetFullPath(file).Equals(allowed, StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach ((int lineNumber, string code) in NonCommentLines(file))
                {
                    if (pattern.IsMatch(code))
                        hits.Add($"{file}:{lineNumber}");
                }
            }
        }

        hits.Should()
            .BeEmpty(
                        $"only SystemDateTimeProvider may use DateTime[Offset].UtcNow ([ADR-0015]); everywhere else inject IDateTimeProvider. Offending lines:\n  - {String.Join("\n  - ", hits)}"
                    );
    }

    #endregion

    #region GenericRepositoryBan
    // ([ADR-0010]) — no `IRepository<T>` (or any IRepository, any arity) anywhere.

    [Fact]
    public void No_assembly_declares_an_IRepository_interface()
    {
        // Reflection over all 5 production assemblies. Catches:
        //   >  non-generic IRepository
        //   >  generic IRepository<T> — reflection-name `IRepository`1`
        //   >  any higher-arity IRepository<T,U,...> — reflection-name `IRepository`N`
        //
        // Per the original [ADR-0010] reasoning: generic repositories trade off the leverage of EF's DbSet<T> + LINQ surface for an abstraction that doesn't pay for itself at one-aggregate scale.
        // Handlers go through IApplicationDbContext.
        Regex genericIRepository = new(@"^IRepository`\d+$", RegexOptions.Compiled);

        Type[] hits = AllProductionAssemblies
                          .SelectMany(a => a.GetTypes())
                          .Where(t => t.IsInterface)
                          .Where(t => t.Name == "IRepository" || genericIRepository.IsMatch(t.Name))
                          .ToArray();

        hits.Should()
            .BeEmpty(
                        $"no IRepository / IRepository<T> interface may exist anywhere ([ADR-0010]); handlers access data via DbSet<T> through IApplicationDbContext. Offenders: {String.Join(", ", hits.Select(t => t.FullName ?? t.Name))}"
                    );
    }

    #endregion

    #region InterpolatedLoggingBan
    // ([ADR-0041]) — log message templates must be constant strings with structured `{Placeholder}` tokens, never interpolated `$"..."`.

    [Fact]
    public void No_source_file_uses_string_interpolated_log_templates()
    {
        // Pattern breakdown:
        //   \b\w*[Ll]ogger   — receiver named `_logger` / `logger` / `MyLogger` / `someLogger` etc.
        //   \??              — optional null-conditional `?` for `_logger?.LogXxx(...)` use.
        //   \.Log[A-Z]\w*    — `.LogInformation`, `.LogDebug`, `.LogError`, `.LogWarning`, etc.
        //   \(\s*\$"         — opening `(` then `$"` (the interpolated string), allowing whitespace between.
        //
        // Why this matters: interpolated templates render to a unique message string per call — they defeat Serilog's structured-properties pipeline, defeat sink-level grouping in PostgreSQL.Alternative + Seq, and silently bypass SensitivePropertyDestructuringPolicy ([ADR-0041]).
        Regex pattern = new(@"\b\w*[Ll]ogger\??\.Log[A-Z]\w*\(\s*\$""", RegexOptions.Compiled);

        List<string> hits = new();

        foreach (string projectRoot in AllProductionSourceRoots)
        {
            foreach (string file in EnumerateProductionFiles(projectRoot))
            {
                foreach ((int lineNumber, string code) in NonCommentLines(file))
                {
                    if (pattern.IsMatch(code))
                        hits.Add($"{file}:{lineNumber}");
                }
            }
        }

        hits.Should()
            .BeEmpty(
                        $"log messages must be constant templates with {{Placeholder}} tokens, not interpolated strings ([ADR-0041]). Offending lines:\n  - {String.Join("\n  - ", hits)}"
                    );
    }

    #endregion

    #region Helpers

    // Bundled source-root list — keeps the per-test loops short.
    private static IEnumerable<string> AllProductionSourceRoots =>
    [
        SourceRoots.Common,
        SourceRoots.Domain,
        SourceRoots.Application,
        SourceRoots.Infrastructure,
        SourceRoots.Api,
    ];

    // Bundled assembly list — same idea for the reflection test.
    private static IEnumerable<Assembly> AllProductionAssemblies =>
    [
        SolutionAssemblies.Common,
        SolutionAssemblies.Domain,
        SolutionAssemblies.Application,
        SolutionAssemblies.Infrastructure,
        SolutionAssemblies.Api,
    ];

    // Enumerates .cs files under projectRoot, skipping obj/ + bin/ generated trees.
    // Static-build outputs (e.g. *.GlobalUsings.g.cs) live in obj/ and would otherwise trigger false positives on whatever's in user code transitively.
    private static IEnumerable<string> EnumerateProductionFiles(string projectRoot)
    {
        foreach (string file in Directory.EnumerateFiles(projectRoot, "*.cs", SearchOption.AllDirectories))
        {
            string normalized = file.Replace('\\', '/');
            if (normalized.Contains("/obj/", StringComparison.Ordinal) || normalized.Contains("/bin/", StringComparison.Ordinal))
                continue;

            yield return file;
        }
    }

    // Yields (1-based-line-number, code-without-trailing-comment) for every non-blank line in the file.
    // Naive comment stripping: cuts everything from the first `//` to EOL.
    //
    // Known false negative: a `//` that appears INSIDE a string literal would also be cut — but that pattern doesn't occur in our codebase (we'd see warnings on a real example), and the alternative (writing a tokenizer) is well past the value/effort line for a banned-API check.
    private static IEnumerable<(int LineNumber, string Code)> NonCommentLines(string filePath)
    {
        string[] lines = File.ReadAllLines(filePath);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            int commentIdx = line.IndexOf("//", StringComparison.Ordinal);
            string code = commentIdx >= 0 ? line[..commentIdx] : line;
            
            if (!String.IsNullOrWhiteSpace(code))
                yield return (i + 1, code);
        }
    }

    #endregion
}

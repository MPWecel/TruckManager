namespace TruckManager.ArchitectureTests.TestHelpers;

// Phase 8 / Section D helper.   Resolves on-disk source-tree roots for call-site grep tests.
//
// The architecture tests run from `bin/Debug/net9.0/`. We need to walk UP from there to find the repo's `src/` directory, then drop down into the project of interest.
// Walking up rather than hard-coding a relative path keeps the helper resilient to changes in the test project's bin layout (e.g. a future `<OutDir>` override).
//
// Marker for "we found the repo root": the directory contains a `src` subdirectory which itself contains a folder named `<projectName>`.
// That's specific enough that no intermediate directory will accidentally match.
internal static class SourceRoots
{
    public static string Common { get; } = LocateProject("TruckManager.Common");
    public static string Domain { get; } = LocateProject("TruckManager.Domain");
    public static string Application { get; } = LocateProject("TruckManager.Application");
    public static string Infrastructure { get; } = LocateProject("TruckManager.Infrastructure");
    public static string Api { get; } = LocateProject("TruckManager.Api");

    private static string LocateProject(string projectName)
    {
        // AppContext.BaseDirectory in test runs == bin/Debug/net9.0/.
        // From there: tests/TruckManager.ArchitectureTests/bin/Debug/net9.0 -> traverse up to the repo dir that has `src/<projectName>`.
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "src", projectName);
            if (Directory.Exists(candidate))
                return Path.GetFullPath(candidate);

            dir = dir.Parent;
        }

        string invalidOperationExceptionMessage = $"""
                                                       Could not locate src/{projectName} by walking up from '{AppContext.BaseDirectory}'. 
                                                       The architecture tests assume the standard `<repo>/src/<projectName>` layout; 
                                                       if the test project moved, update SourceRoots.LocateProject.
                                                   """;

        throw new InvalidOperationException(invalidOperationExceptionMessage);
    }
}

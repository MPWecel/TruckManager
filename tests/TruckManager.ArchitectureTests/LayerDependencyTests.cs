using System.Reflection;
using AwesomeAssertions;
using NetArchTest.Rules;
using Xunit;

using TruckManager.ArchitectureTests.TestHelpers;

// xunit.v3 introduces Xunit.TestResult which collides with NetArchTest.Rules.TestResult. Alias the NetArchTest one so the rest of the file reads naturally.
using TestResult = NetArchTest.Rules.TestResult;

namespace TruckManager.ArchitectureTests;

// Phase 8 / Section B.   Layer dependency rules 
// One test per layer pair (or per banned-dependency-set) so a failure shows exactly which boundary was violated.
// Failure messages render the offending types from TestResult.FailingTypes for actionable diagnostics — NetArchTest's bare "isSuccessful: false" is much harder to triage than a list of offending FQTNs.
//
// Dependency contract:
//   Common         :   referenced by all; references nothing.
//   Domain         :   references Common only.
//   Application    :   references Common + Domain.   (Microsoft.EntityFrameworkCore allowed per [ADR-0040].)
//   Infrastructure :   references Common + Domain + Application.   (Microsoft.AspNetCore.* allowed per [ADR-0041].)
//   Api            :   references Common + Application + Infrastructure.
public class LayerDependencyTests
{
    #region DomainArchitecture
    // (innermost — references only Common) 

    [Fact]
    public void Domain_does_not_depend_on_Application_Infrastructure_or_Api()
    {
        TestResult result = Types.InAssembly(SolutionAssemblies.Domain)
                                 .Should()
                                 .NotHaveDependencyOnAny(
                                                            "TruckManager.Application",
                                                            "TruckManager.Infrastructure",
                                                            "TruckManager.Api"
                                                        )
                                 .GetResult();

        result.IsSuccessful.Should()
                           .BeTrue(BuildFailureMessage("Domain", result));
    }

    [Fact]
    public void Domain_does_not_depend_on_AspNetCore_or_EntityFrameworkCore()
    {
        // Domain is pure C# with no persistence / web concerns. The only external boundary it crosses is the IDateTimeProvider / IStronglyTypedId<T> abstractions in Common.
        TestResult result = Types.InAssembly(SolutionAssemblies.Domain)
                                 .Should()
                                 .NotHaveDependencyOnAny(
                                                            "Microsoft.AspNetCore",
                                                            "Microsoft.EntityFrameworkCore"
                                                        )
                                 .GetResult();

        result.IsSuccessful.Should()
                           .BeTrue(BuildFailureMessage("Domain", result));
    }

    #endregion

    #region ApplicationArchitecture
    // (references Common + Domain only) 

    [Fact]
    public void Application_does_not_depend_on_Infrastructure_or_Api()
    {
        TestResult result = Types.InAssembly(SolutionAssemblies.Application)
                                 .Should()
                                 .NotHaveDependencyOnAny(
                                                            "TruckManager.Infrastructure",
                                                            "TruckManager.Api"
                                                        )
                                 .GetResult();

        result.IsSuccessful.Should()
                           .BeTrue(BuildFailureMessage("Application", result));
    }

    [Fact]
    public void Application_does_not_depend_on_AspNetCore()
    {
        // Microsoft.EntityFrameworkCore IS allowed here (ADR-0040 — IApplicationDbContext exposes DbSet<T> as the handler facade).
        // Microsoft.AspNetCore is not — Application stays HTTP-agnostic; ICorrelationContext is the request-correlation abstraction.
        TestResult result = Types.InAssembly(SolutionAssemblies.Application)
                                 .Should()
                                 .NotHaveDependencyOn("Microsoft.AspNetCore")
                                 .GetResult();

        result.IsSuccessful.Should()
                           .BeTrue(BuildFailureMessage("Application", result));
    }

    #endregion

    #region InfrastructureArchitecture 
    //(references Common + Domain + Application; no Api) 

    [Fact]
    public void Infrastructure_does_not_depend_on_Api()
    {
        // Microsoft.AspNetCore.* IS allowed in Infrastructure since Phase 7 / Section B (FrameworkReference Microsoft.AspNetCore.App for IHttpContextAccessor on HttpContextCorrelationContext — see ADR-0041 for the layering decision).
        TestResult result = Types.InAssembly(SolutionAssemblies.Infrastructure).Should()
                                                                               .NotHaveDependencyOn("TruckManager.Api")
                                                                               .GetResult();

        result.IsSuccessful.Should()
                           .BeTrue(BuildFailureMessage("Infrastructure", result));
    }

    #endregion

    #region OpenApiBlock
    // Forbid Microsoft.AspNetCore.OpenApi everywhere 
    // Phase 6 / Section A removed the .NET 9 native OpenAPI package in favour of Swashbuckle.AspNetCore (decision #2).
    // One test per layer so the failing layer is identifiable at a glance if the package ever re-appears via a transitive ref.

    [Fact]
    public void Common_does_not_depend_on_Microsoft_AspNetCore_OpenApi() => AssertNoOpenApi(SolutionAssemblies.Common, "Common");

    [Fact]
    public void Domain_does_not_depend_on_Microsoft_AspNetCore_OpenApi() => AssertNoOpenApi(SolutionAssemblies.Domain, "Domain");

    [Fact]
    public void Application_does_not_depend_on_Microsoft_AspNetCore_OpenApi() => AssertNoOpenApi(SolutionAssemblies.Application, "Application");

    [Fact]
    public void Infrastructure_does_not_depend_on_Microsoft_AspNetCore_OpenApi() => AssertNoOpenApi(SolutionAssemblies.Infrastructure, "Infrastructure");

    [Fact]
    public void Api_does_not_depend_on_Microsoft_AspNetCore_OpenApi() => AssertNoOpenApi(SolutionAssemblies.Api, "Api");

    private static void AssertNoOpenApi(Assembly assembly, string layerName)
    {
        TestResult result = Types.InAssembly(assembly).Should()
                                                      .NotHaveDependencyOn("Microsoft.AspNetCore.OpenApi")
                                                      .GetResult();

        result.IsSuccessful.Should()
                           .BeTrue(BuildFailureMessage(layerName, result));
    }

    #endregion

    #region Helpers

    private static string BuildFailureMessage(string layerName, TestResult result)
    {
        if (result.IsSuccessful)
            return String.Empty;

        IEnumerable<string> offenders = result.FailingTypes?
                                              .Select(t => t.FullName ?? t.Name) ?? [];
        string list = String.Join("\n  - ", offenders);
        string failureMessage = $"layer '{layerName}' has forbidden dependencies. Offending types:\n  - {list}";
        return failureMessage;
    }

    #endregion
}

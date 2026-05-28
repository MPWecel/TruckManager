using AwesomeAssertions;
using Xunit;

using TruckManager.ArchitectureTests.TestHelpers;

namespace TruckManager.ArchitectureTests;

// Phase 8 / Section A.   Sanity test for the SolutionAssemblies helper.
//
// Every production assembly handle must resolve and carry the expected AssemblyName.Name.
// Failures here catch misconfigured ProjectReferences or removed marker types BEFORE any architecture rule does —
// - which is critical because NetArchTest's diagnostic on a missing reference is much harder to read than a plain "expected X to be Y" assertion failure.
public class SolutionAssembliesTests
{
    [Fact]
    public void Common_assembly_resolves()
    {
        SolutionAssemblies.Common.Should()
                                 .NotBeNull();
        SolutionAssemblies.Common.GetName()
                                 .Name.Should()
                                      .Be("TruckManager.Common");
    }

    [Fact]
    public void Domain_assembly_resolves()
    {
        SolutionAssemblies.Domain.Should()
                                 .NotBeNull();
        SolutionAssemblies.Domain.GetName()
                                 .Name.Should()
                                      .Be("TruckManager.Domain");
    }

    [Fact]
    public void Application_assembly_resolves()
    {
        SolutionAssemblies.Application.Should()
                                      .NotBeNull();
        SolutionAssemblies.Application.GetName()
                                      .Name.Should()
                                           .Be("TruckManager.Application");
    }

    [Fact]
    public void Infrastructure_assembly_resolves()
    {
        SolutionAssemblies.Infrastructure.Should()
                                         .NotBeNull();
        SolutionAssemblies.Infrastructure.GetName()
                                         .Name.Should()
                                              .Be("TruckManager.Infrastructure");
    }

    [Fact]
    public void Api_assembly_resolves()
    {
        SolutionAssemblies.Api.Should()
                              .NotBeNull();
        SolutionAssemblies.Api.GetName()
                              .Name.Should()
                                   .Be("TruckManager.Api");
    }
}

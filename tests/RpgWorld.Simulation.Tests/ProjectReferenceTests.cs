namespace RpgWorld.Simulation.Tests;

public sealed class ProjectReferenceTests
{
    [Fact]
    public void Test_project_references_simulation_project()
    {
        var assemblyName = typeof(AssemblyMarker).Assembly.GetName().Name;

        Assert.Equal("RpgWorld.Simulation", assemblyName);
    }

    [Fact]
    public void Simulation_project_does_not_reference_web_layer()
    {
        var references = typeof(AssemblyMarker).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.DoesNotContain("RpgWorld.Api", references);
        Assert.DoesNotContain("RpgWorld.Web", references);
        Assert.DoesNotContain("RpgWorld.Modules.Default", references);
    }
}

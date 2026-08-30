namespace RpgWorld.Simulation.Tests;

public sealed class ProjectReferenceTests
{
    [Fact]
    public void Test_project_references_simulation_project()
    {
        var assemblyName = typeof(AssemblyMarker).Assembly.GetName().Name;

        Assert.Equal("RpgWorld.Simulation", assemblyName);
    }
}

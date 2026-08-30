namespace RpgWorld.Domain.Tests;

public sealed class ProjectReferenceTests
{
    [Fact]
    public void Test_project_references_domain_project()
    {
        var assemblyName = typeof(AssemblyMarker).Assembly.GetName().Name;

        Assert.Equal("RpgWorld.Domain", assemblyName);
    }
}


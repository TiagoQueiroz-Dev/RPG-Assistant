using RpgWorld.Application.Worlds.Importing;
using RpgWorld.Infrastructure.Worlds.Importing;

namespace RpgWorld.Infrastructure.Tests.Worlds;

public sealed class ColorMapRegionClassifierTests
{
    [Theory]
    [InlineData(245, 245, 245, "snow")]
    [InlineData(20, 60, 190, "ocean")]
    [InlineData(40, 150, 170, "river")]
    [InlineData(35, 35, 35, "mountain")]
    [InlineData(210, 175, 70, "desert")]
    [InlineData(35, 150, 55, "forest")]
    [InlineData(120, 165, 80, "grassland")]
    public void Identifies_reference_colors_with_confidence(
        byte red,
        byte green,
        byte blue,
        string expectedBiome)
    {
        var result = new ColorMapRegionClassifier().Classify(new MapColorSample(red, green, blue));

        Assert.Equal(expectedBiome, result.BiomeCode);
        Assert.InRange(result.Confidence, 0.5m, 1m);
    }
}

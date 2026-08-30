using RpgWorld.Application.Worlds.Importing;

namespace RpgWorld.Infrastructure.Worlds.Importing;

public sealed class ColorMapRegionClassifier : IMapRegionClassifier
{
    public MapRegionClassification Classify(MapColorSample sample)
    {
        var red = (int)sample.Red;
        var green = (int)sample.Green;
        var blue = (int)sample.Blue;
        var maximum = Math.Max(red, Math.Max(green, blue));
        var minimum = Math.Min(red, Math.Min(green, blue));
        var saturation = maximum - minimum;
        var luminance = (red * 299 + green * 587 + blue * 114) / 1000;

        if (luminance > 220 && saturation < 30) return Result("snow", 0.92m, luminance - 220);
        if (green > red * 1.50 && blue > red * 1.50 && Math.Abs(blue - green) < 70)
            return Result("river", 0.72m, Math.Min(blue - red, green - red));
        if (blue > red * 1.18 && blue > green * 1.15)
            return Result("ocean", 0.78m, blue - Math.Max(red, green));
        if (luminance < 58) return Result("mountain", 0.74m, 58 - luminance);
        if (red > 145 && green > 115 && blue < 135) return Result("desert", 0.76m, Math.Min(red - 145, 135 - blue));
        if (green > red * 1.45 && green > blue * 1.20) return Result("forest", 0.78m, green - Math.Max(red, blue));
        if (saturation < 28) return Result("mountain", 0.68m, 28 - saturation);
        return new MapRegionClassification("grassland", 0.62m);
    }

    private static MapRegionClassification Result(string biome, decimal baseline, int margin) =>
        new(biome, Math.Min(0.99m, baseline + Math.Max(0, margin) / 500m));
}

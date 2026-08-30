using Microsoft.Extensions.Configuration;

namespace RpgWorld.Infrastructure.Caching;

public sealed record RedisOptions(
    bool Enabled,
    string? ConnectionString,
    string InstanceName)
{
    public const string SectionName = "Redis";

    public static RedisOptions FromConfiguration(IConfiguration configuration)
    {
        var enabled = bool.TryParse(
            configuration[$"{SectionName}:Enabled"],
            out var configuredEnabled) && configuredEnabled;

        var instanceName = configuration[$"{SectionName}:InstanceName"];
        if (string.IsNullOrWhiteSpace(instanceName))
        {
            instanceName = "rpg-world";
        }

        return new RedisOptions(
            enabled,
            configuration[$"{SectionName}:ConnectionString"],
            instanceName.Trim().TrimEnd(':'));
    }
}


using System.Text.Json;

namespace RpgWorld.Domain.Worlds.Content;

public enum CustomContentKind { Creature, Item, Npc, Biome, Rule, Class, Faction, Event }

public sealed class CustomContentDefinition
{
    public const int MaximumPayloadLength = 32_768;
    private CustomContentDefinition() { }

    private CustomContentDefinition(
        Guid worldId,
        CustomContentKind kind,
        string code,
        string name,
        string payload,
        DateTimeOffset createdAtUtc)
    {
        Id = Guid.CreateVersion7();
        WorldId = RequiredId(worldId, nameof(worldId));
        Kind = kind;
        Code = RequiredCode(code);
        Name = RequiredName(name);
        Payload = RequiredPayload(payload);
        Version = 1;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid WorldId { get; private set; }
    public CustomContentKind Kind { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Payload { get; private set; } = "{}";
    public int Version { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static CustomContentDefinition Create(
        Guid worldId, CustomContentKind kind, string code, string name, string payload, DateTimeOffset createdAtUtc) =>
        new(worldId, kind, code, name, payload, createdAtUtc);

    public void Update(string name, string payload, DateTimeOffset updatedAtUtc)
    {
        Name = RequiredName(name);
        Payload = RequiredPayload(payload);
        Version = checked(Version + 1);
        UpdatedAtUtc = updatedAtUtc.ToUniversalTime();
    }

    private static Guid RequiredId(Guid value, string parameterName) =>
        value == Guid.Empty ? throw new ArgumentException("Identifier is required.", parameterName) : value;

    private static string RequiredCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Content code is required.", nameof(value));
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length > 80 || normalized.Any(character =>
                !char.IsLetterOrDigit(character) && character is not '.' and not '-' and not '_'))
            throw new ArgumentException("Content code contains unsupported characters.", nameof(value));
        return normalized;
    }

    private static string RequiredName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Content name is required.", nameof(value));
        var normalized = value.Trim();
        if (normalized.Length > 200) throw new ArgumentException("Content name cannot exceed 200 characters.", nameof(value));
        return normalized;
    }

    private static string RequiredPayload(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Content payload is required.", nameof(value));
        if (value.Length > MaximumPayloadLength) throw new ArgumentException("Content payload is too large.", nameof(value));
        try
        {
            using var document = JsonDocument.Parse(value);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("Content payload must be a JSON object.", nameof(value));
            return document.RootElement.GetRawText();
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("Content payload must be valid JSON.", nameof(value), exception);
        }
    }
}

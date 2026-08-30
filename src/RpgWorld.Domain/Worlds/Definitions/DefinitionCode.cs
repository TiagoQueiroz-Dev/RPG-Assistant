using System.Text.RegularExpressions;

namespace RpgWorld.Domain.Worlds.Definitions;

internal static partial class DefinitionCode
{
    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{0,79}$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidCodePattern();

    public static string Normalize(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Definition code cannot be empty.", parameterName);
        }

        var normalized = value.Trim().ToLowerInvariant();

        if (!ValidCodePattern().IsMatch(normalized))
        {
            throw new ArgumentException(
                "Definition code must start with an alphanumeric character and contain only letters, numbers, dots, hyphens or underscores (maximum 80 characters).",
                parameterName);
        }

        return normalized;
    }

    public static string RequiredName(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Definition name cannot be empty.", parameterName);
        }

        return value.Trim();
    }

    public static IReadOnlySet<string> NormalizeTags(
        IEnumerable<string>? values,
        string parameterName) =>
        new HashSet<string>(
            (values ?? []).Select(value => Normalize(value, parameterName)),
            StringComparer.Ordinal);
}

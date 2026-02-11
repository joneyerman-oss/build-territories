using System.Text.RegularExpressions;

namespace TerritoryBuilder.Core.Utilities;

public static class AddressNormalizer
{
    private static readonly Dictionary<string, string> Tokens = new(StringComparer.OrdinalIgnoreCase)
    {
        [" STREET "] = " ST ",
        [" ROAD "] = " RD ",
        [" AVENUE "] = " AVE ",
        [" BOULEVARD "] = " BLVD ",
        [" DRIVE "] = " DR ",
        [" LANE "] = " LN "
    };

    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var value = $" {Regex.Replace(input.ToUpperInvariant().Trim(), @"\s+", " ")} ";
        foreach (var kvp in Tokens)
        {
            value = value.Replace(kvp.Key, kvp.Value, StringComparison.OrdinalIgnoreCase);
        }

        return value.Trim();
    }
}

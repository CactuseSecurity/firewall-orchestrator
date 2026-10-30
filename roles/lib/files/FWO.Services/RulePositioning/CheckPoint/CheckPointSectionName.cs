namespace FWO.Services.RulePositioning.CheckPoint;

/// <summary>Builds the canonical Check Point zone-pair Section name.</summary>
public static class CheckPointSectionName
{
    /// <summary>Builds destination-first normalized Section identity.</summary>
    public static bool TryBuild(string? destinationZoneName, string? sourceZoneName, out string value)
    {
        value = string.Empty;
        if (!TryNormalizeZone(destinationZoneName, out string destination) ||
            !TryNormalizeZone(sourceZoneName, out string source))
        {
            return false;
        }
        value = $"[{destination}] < [{source}]";
        return true;
    }

    /// <summary>Validates and invariant-normalizes one zone name.</summary>
    private static bool TryNormalizeZone(string? input, out string value)
    {
        value = input?.Trim() ?? string.Empty;
        if (value.Length == 0 || value.IndexOfAny(['[', ']', '<']) >= 0)
        {
            value = string.Empty;
            return false;
        }
        value = value.ToUpperInvariant();
        return true;
    }
}

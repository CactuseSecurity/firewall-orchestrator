namespace FWO.Data.Provisioning;

/// <summary>Naming templates for provisioned modelling objects.</summary>
public static class ProvisioningModellingNamingConventions
{
    public static readonly IReadOnlyList<string> ObjectTypes =
        new List<string> { "appRole", "appZone", "network", "host", "networkArea", "range" }.AsReadOnly();

    /// <summary>Uses managed ID strings for roles and zones and names for the other objects.</summary>
    public static Dictionary<string, string> CreateDefaults() => new(StringComparer.Ordinal)
    {
        { "appRole", "{idString}" },
        { "appZone", "{idString}" },
        { "network", "{name}" },
        { "host", "{name}" },
        { "networkArea", "{idString}" },
        { "range", "{name}" }
    };
}

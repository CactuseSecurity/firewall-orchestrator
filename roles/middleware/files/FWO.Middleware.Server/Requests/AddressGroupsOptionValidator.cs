using Microsoft.AspNetCore.Mvc;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Validates the optional 'option' container of the address group lookup.
/// </summary>
public static class AddressGroupsOptionValidator
{
    private static readonly List<RequestKeyDefinition> kAllowedKeys =
    [
        new RequestKeyDefinition(
            "separateZoneGroups",
            "false (default): one flat list of all groups. true: groups are split into 'standardGroups' and 'zoneGroups'.")
    ];

    /// <summary>
    /// Gets the keys accepted inside the 'option' container.
    /// </summary>
    public static IReadOnlyList<RequestKeyDefinition> AllowedKeys => kAllowedKeys;

    /// <summary>
    /// Checks that the option container only carries supported keys.
    /// </summary>
    /// <param name="option">The option container of the request.</param>
    /// <param name="errorResult">The bad request result if validation failed.</param>
    /// <returns>True if the option container is valid.</returns>
    public static bool TryValidate(AddressGroupsOption? option, out ActionResult? errorResult)
    {
        if (option?.AdditionalData is { Count: > 0 })
        {
            errorResult = RequestValidationMessageBuilder.BuildAllowedKeysError("option", kAllowedKeys);
            return false;
        }

        errorResult = null;
        return true;
    }
}

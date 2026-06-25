using System.Linq;
using System.Text.Json;
using FWO.Data;

namespace FWO.Middleware.Server.Controllers;

/// <summary>
/// Resolves rule response fields from database-backed and custom-field-backed data.
/// </summary>
public static class RuleFieldSourceResolver
{
    /// <summary>
    /// Default fallback text used by the rule response for fields that cannot be resolved.
    /// </summary>
    public const string NotFoundValue = "Not Found in Database";

    /// <summary>
    /// Resolves the owner information payload for a rule.
    /// </summary>
    public static OwnerInformation ResolveOwnerInformation(Rule rule, string customFieldKey)
    {
        string? normalizedCustomFieldKey = NormalizeCustomFieldKeys(customFieldKey);

        return new OwnerInformation
        {
            Id = rule.RuleOwner.FirstOrDefault()?.OwnerId,
            ExtAppId = normalizedCustomFieldKey is null
                ? null
                : CustomFieldResolver.ExtractCustomFieldValue<string>(rule, normalizedCustomFieldKey, out _)
        };
    }

    /// <summary>
    /// Resolves the additional information payload for a rule.
    /// </summary>
    public static AdditionalInformation ResolveAdditionalInformation(Rule rule, string customFieldKey)
    {
        string? normalizedCustomFieldKey = NormalizeCustomFieldKeys(customFieldKey);
        if (normalizedCustomFieldKey is null)
        {
            return new AdditionalInformation();
        }

        return new AdditionalInformation
        {
            ChangeId = CustomFieldResolver.ExtractCustomFieldValue<string>(rule, normalizedCustomFieldKey, out _)
        };
    }

    private static string? NormalizeCustomFieldKeys(string customFieldKey)
    {
        if (string.IsNullOrWhiteSpace(customFieldKey))
        {
            return null;
        }

        string trimmed = customFieldKey.Trim();

        try
        {
            string[]? keyArray = JsonSerializer.Deserialize<string[]>(trimmed);
            if (keyArray is not null)
            {
                List<string> cleanedKeys = keyArray
                    .Where(key => !string.IsNullOrWhiteSpace(key))
                    .Select(key => key.Trim())
                    .ToList();

                if (cleanedKeys.Count > 0)
                {
                    return JsonSerializer.Serialize(cleanedKeys);
                }

                return null;
            }
        }
        catch (JsonException)
        {
            // Fall back to treating the value as a single raw key.
        }

        try
        {
            string? singleKey = JsonSerializer.Deserialize<string>(trimmed);
            if (!string.IsNullOrWhiteSpace(singleKey))
            {
                return JsonSerializer.Serialize(new[] { singleKey.Trim() });
            }
        }
        catch (JsonException)
        {
            // Fall back to treating the value as a single raw key.
        }

        return JsonSerializer.Serialize(new[] { trimmed });
    }
}

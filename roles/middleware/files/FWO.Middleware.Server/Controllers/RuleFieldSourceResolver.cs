using System.Linq;
using System.Text.Json;
using FWO.Basics;
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
        string? extAppId = normalizedCustomFieldKey is null
            ? null
            : CustomFieldResolver.ExtractCustomFieldValue<string>(rule, normalizedCustomFieldKey, out _);
        OwnerMappingSourceStm? mappingSource = GetRuleOwnerMappingSource(rule);

        return mappingSource switch
        {
            OwnerMappingSourceStm.CustomField => ResolveStrictOwnerInformation(rule, extAppId),
            _ => ResolvePermissiveOwnerInformation(rule, extAppId)
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

    private static OwnerInformation ResolveStrictOwnerInformation(Rule rule, string? extAppId)
    {
        int[] ownerIds = (rule.RuleOwner ?? [])
            .OfType<RuleOwner>()
            .Where(owner => owner.Removed is null)
            .Select(owner => owner.OwnerId)
            .ToArray();

        if (ownerIds.Length == 0)
        {
            return new OwnerInformation
            {
                ExtAppId = extAppId
            };
        }

        if (ownerIds.Length > 1)
        {
            throw new InvalidOperationException(
                $"Rule {rule.Id} has {ownerIds.Length} active owners. Exclusive owner mapping requires exactly one owner.");
        }

        return new OwnerInformation
        {
            ExtAppId = extAppId,
            OwnerIds = [ownerIds[0]]
        };
    }

    private static OwnerInformation ResolvePermissiveOwnerInformation(Rule rule, string? extAppId)
    {
        return new OwnerInformation
        {
            ExtAppId = extAppId,
            OwnerIds = (rule.RuleOwner ?? [])
                .OfType<RuleOwner>()
                .Where(owner => owner.Removed is null)
                .Select(owner => owner.OwnerId)
                .ToList()
        };
    }

    private static OwnerMappingSourceStm? GetRuleOwnerMappingSource(Rule rule)
    {
        int[] mappingSourceIds = (rule.RuleOwner ?? [])
            .OfType<RuleOwner>()
            .Where(owner => owner.Removed is null)
            .Select(owner => owner.OwnerMappingSourceId)
            .Where(mappingSourceId => mappingSourceId > 0)
            .Distinct()
            .ToArray();

        if (mappingSourceIds.Length != 1 || !Enum.IsDefined(typeof(OwnerMappingSourceStm), mappingSourceIds[0]))
        {
            return null;
        }

        return (OwnerMappingSourceStm)mappingSourceIds[0];
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

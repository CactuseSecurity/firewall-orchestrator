using System.Linq;
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
    /// <param name="rule">Rule to resolve.</param>
    /// <param name="customFieldKey">Configured owner key setting.</param>
    /// <returns>The owner information payload.</returns>
    public static OwnerInformation ResolveOwnerInformation(Rule rule, string customFieldKey)
    {
        return ResolveOwnerInformation(rule, CustomFieldResolver.NormalizeCustomFieldKeys(customFieldKey));
    }

    /// <summary>
    /// Resolves the owner information payload for a rule from already normalized keys.
    /// </summary>
    /// <param name="rule">Rule to resolve.</param>
    /// <param name="ownerKeys">Normalized owner custom field keys.</param>
    /// <returns>The owner information payload.</returns>
    public static OwnerInformation ResolveOwnerInformation(Rule rule, IReadOnlyList<string> ownerKeys)
    {
        string? extAppId = CustomFieldResolver.ExtractCustomFieldValue<string>(rule, ownerKeys, out _);
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
    /// <param name="rule">Rule to resolve.</param>
    /// <param name="customFieldKey">Configured change-ID key setting.</param>
    /// <returns>The additional information payload.</returns>
    public static AdditionalInformation ResolveAdditionalInformation(Rule rule, string customFieldKey)
    {
        return ResolveAdditionalInformation(rule, CustomFieldResolver.NormalizeCustomFieldKeys(customFieldKey));
    }

    /// <summary>
    /// Resolves the additional information payload for a rule from already normalized keys.
    /// </summary>
    /// <param name="rule">Rule to resolve.</param>
    /// <param name="changeIdKeys">Normalized change-ID custom field keys.</param>
    /// <returns>The additional information payload.</returns>
    public static AdditionalInformation ResolveAdditionalInformation(Rule rule, IReadOnlyList<string> changeIdKeys)
    {
        return new AdditionalInformation
        {
            ChangeId = CustomFieldResolver.ExtractCustomFieldValue<string>(rule, changeIdKeys, out _,
                CustomFieldKeyMatching.IgnoreCase)
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

}

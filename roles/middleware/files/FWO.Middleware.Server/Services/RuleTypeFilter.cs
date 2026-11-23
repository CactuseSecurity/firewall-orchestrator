using FWO.Data;

namespace FWO.Middleware.Server.Services;

/// <summary>
/// Rule types a REST rule lookup can be restricted to, and their mapping onto the rule table.
/// </summary>
/// <remarks>
/// An access rule has <c>access_rule</c> set. A NAT rule is stored as two rows that both have <c>nat_rule</c>
/// set: the original (match) row, whose <c>xlate_rule</c> points to the translated row, and the translated row
/// itself. Only the original row counts as a NAT rule; the translated row is returned as its translation and
/// never on its own. Omitted or null rule types select access rules only, so NAT rules are only returned on
/// request. The helper is shared so that every rule- or flow-based lookup applies the same semantics.
/// </remarks>
public static class RuleTypeFilter
{
    /// <summary>
    /// Rule type of access (security) rules.
    /// </summary>
    public const string kAccess = "access";

    /// <summary>
    /// Rule type of NAT rules.
    /// </summary>
    public const string kNat = "nat";

    /// <summary>
    /// Maximum number of values in a rule type filter.
    /// </summary>
    public const int kMaxRuleTypeValues = 2;

    private static readonly List<string> kAllowedRuleTypes = [kAccess, kNat];

    /// <summary>
    /// Rule types used when the filter is omitted or null: access rules only.
    /// </summary>
    public static readonly IReadOnlyList<string> kDefaultRuleTypes = [kAccess];

    /// <summary>
    /// Validates a rule type filter.
    /// </summary>
    /// <param name="ruleTypes">The requested rule types; null selects the default.</param>
    /// <param name="path">The request path of the filter, used in the error messages.</param>
    /// <returns>All errors found; empty when the filter is valid.</returns>
    public static List<string> Validate(List<string?>? ruleTypes, string path)
    {
        List<string> errors = [];
        if (ruleTypes is null)
        {
            return errors;
        }

        if (ruleTypes.Count == 0)
        {
            errors.Add($"{path} must contain at least one value; omit it or send null to get access rules only.");
        }
        else if (ruleTypes.Count > kMaxRuleTypeValues)
        {
            errors.Add($"{path} must not contain more than {kMaxRuleTypeValues} values.");
        }

        List<string> invalidValues = ruleTypes
            .Where(ruleType => ruleType is null || !kAllowedRuleTypes.Contains(ruleType))
            .Select(ruleType => ruleType ?? "null")
            .Distinct()
            .ToList();
        if (invalidValues.Count > 0)
        {
            errors.Add($"{path} only accepts \"{kAccess}\" or \"{kNat}\" (lower case); invalid: {string.Join(", ", invalidValues)}.");
        }

        return errors;
    }

    /// <summary>
    /// Resolves a validated rule type filter to the selected rule types.
    /// </summary>
    /// <param name="ruleTypes">The validated rule types; null selects the default.</param>
    public static RuleTypeSelection Resolve(IEnumerable<string?>? ruleTypes)
    {
        List<string?> effectiveRuleTypes = [.. ruleTypes ?? kDefaultRuleTypes];
        return new RuleTypeSelection(effectiveRuleTypes.Contains(kAccess), effectiveRuleTypes.Contains(kNat));
    }

    /// <summary>
    /// Builds the Hasura boolean expression on the rule table (<c>firewall_rule_bool_exp</c>) for the selection.
    /// </summary>
    /// <param name="selection">The selected rule types; at least one type must be selected.</param>
    /// <exception cref="ArgumentException">Thrown when no rule type is selected.</exception>
    public static Dictionary<string, object> BuildRuleBoolExp(RuleTypeSelection selection)
    {
        List<Dictionary<string, object>> alternatives = [];
        if (selection.IncludeAccess)
        {
            alternatives.Add(BuildAccessRuleExpression());
        }
        if (selection.IncludeNat)
        {
            alternatives.Add(BuildNatRuleExpression());
        }

        return alternatives.Count switch
        {
            0 => throw new ArgumentException("At least one rule type must be selected.", nameof(selection)),
            1 => alternatives[0],
            _ => new Dictionary<string, object> { ["_or"] = alternatives }
        };
    }

    /// <summary>
    /// Returns the rule type of a rule: <see cref="kNat"/> for the original row of a NAT rule, otherwise
    /// <see cref="kAccess"/>.
    /// </summary>
    public static string GetRuleType(Rule rule)
    {
        return IsNatRule(rule) ? kNat : kAccess;
    }

    /// <summary>
    /// Tells whether the rule is the original (match) row of a NAT rule.
    /// </summary>
    public static bool IsNatRule(Rule rule)
    {
        return rule.NatRule && rule.XlateRule is not null;
    }

    private static Dictionary<string, object> BuildAccessRuleExpression()
    {
        return new Dictionary<string, object>
        {
            ["access_rule"] = new Dictionary<string, object> { ["_eq"] = true }
        };
    }

    private static Dictionary<string, object> BuildNatRuleExpression()
    {
        return new Dictionary<string, object>
        {
            ["nat_rule"] = new Dictionary<string, object> { ["_eq"] = true },
            ["xlate_rule"] = new Dictionary<string, object> { ["_is_null"] = false }
        };
    }
}

/// <summary>
/// Rule types selected by a rule type filter.
/// </summary>
/// <param name="IncludeAccess">True when access rules are returned.</param>
/// <param name="IncludeNat">True when NAT rules are returned.</param>
public sealed record RuleTypeSelection(bool IncludeAccess, bool IncludeNat);

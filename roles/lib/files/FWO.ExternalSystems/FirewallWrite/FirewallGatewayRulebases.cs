using FWO.Data;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// Policy package and first access layer of a gateway, taken from its imported rulebase links: the policy link points to
    /// the package (Check Point), the first ordered link from the package - or, below a global policy, the domain link - to
    /// the first local access layer. Null where the import has no such link.
    /// </summary>
    public sealed record FirewallGatewayRulebases(string? PolicyName, string? FirstLayerName)
    {
        private const int kLinkTypeOrdered = 2;
        private const int kLinkTypeDomain = 5;
        private const int kLinkTypePolicy = 7;

        /// <summary>No rulebase information, e.g. for a task without gateway.</summary>
        public static FirewallGatewayRulebases None { get; } = new(null, null);

        /// <summary>Derives package and first layer from the current, local rulebase links of one gateway.</summary>
        public static FirewallGatewayRulebases FromLinks(IEnumerable<RulebaseLink> links)
        {
            ArgumentNullException.ThrowIfNull(links);
            List<RulebaseLink> localLinks = [.. links.Where(link => !link.IsGlobal && link.ToRulebase != null)];
            RulebaseLink? policyLink = localLinks.FirstOrDefault(link => link.LinkType == kLinkTypePolicy
                && link.FromRulebaseId == null && link.FromRuleId == null);
            long? policyRulebaseId = policyLink?.ToRulebase?.Id;
            RulebaseLink? layerLink = localLinks.FirstOrDefault(link => link.LinkType == kLinkTypeOrdered
                && policyRulebaseId != null && link.FromRulebaseId == policyRulebaseId);
            layerLink ??= localLinks.FirstOrDefault(link => link.LinkType == kLinkTypeDomain);
            return new FirewallGatewayRulebases(NameOf(policyLink), NameOf(layerLink));
        }

        private static string? NameOf(RulebaseLink? link) =>
            string.IsNullOrWhiteSpace(link?.ToRulebase?.Name) ? null : link.ToRulebase.Name;
    }
}

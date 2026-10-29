using FWO.Data.Workflow;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>Where a new rule is inserted relative to the rulebase or an anchor rule.</summary>
    public enum FirewallRuleAnchor
    {
        Bottom,
        Top,
        Above,
        Below
    }

    /// <summary>
    /// Vendor-neutral position of a rule to create or move. Above and below need the UID of the anchor rule; the layer UID
    /// is optional and replaces the gateway's rulebase in <see cref="FirewallWritePlaceholders.LayerName"/>.
    /// </summary>
    public sealed record FirewallRulePlacement(FirewallRuleAnchor Anchor, string? AnchorRuleUid = null, string? LayerUid = null)
    {
        private const string kAbove = "above";
        private const string kBelow = "below";

        /// <summary>Insert at the end of the rulebase, the default without positioning information.</summary>
        public static FirewallRulePlacement EndOfRulebase { get; } = new(FirewallRuleAnchor.Bottom);

        /// <summary>
        /// Renders the position as JSON value for <see cref="FirewallWritePlaceholders.RulePosition"/>:
        /// "top", "bottom", {"above": uid} or {"below": uid}, the format of the Check Point management API.
        /// </summary>
        public string ToJson()
        {
            if (Anchor is FirewallRuleAnchor.Above or FirewallRuleAnchor.Below)
            {
                if (string.IsNullOrWhiteSpace(AnchorRuleUid))
                {
                    throw new InvalidOperationException($"Rule position '{Anchor}' needs an anchor rule UID.");
                }
                JsonObject relative = new() { [Anchor == FirewallRuleAnchor.Above ? kAbove : kBelow] = AnchorRuleUid };
                return relative.ToJsonString();
            }
            return JsonSerializer.Serialize(Anchor.ToString().ToLowerInvariant());
        }
    }

    /// <summary>
    /// Supplies the position of a rule to create or modify from an implementation task. The positioning of PR #5559
    /// (WfImplTask.RulePosition) is connected through this interface.
    /// </summary>
    public interface IFirewallRulePlacementProvider
    {
        /// <summary>Returns the position for the rule of the task, or null to insert at the end of the rulebase.</summary>
        FirewallRulePlacement? GetPlacement(WfImplTask implTask);
    }

    /// <summary>Places every rule at the end of the rulebase.</summary>
    public sealed class EndOfRulebasePlacementProvider : IFirewallRulePlacementProvider
    {
        /// <inheritdoc/>
        public FirewallRulePlacement? GetPlacement(WfImplTask implTask) => null;
    }
}

using FWO.Data.RulePositioning;
using FWO.Data.RulePositioning.CheckPoint;
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
    /// is optional and replaces the gateway's rulebase in <see cref="FirewallWritePlaceholders.LayerName"/>. Top and
    /// bottom with a section UID place the rule at the top or bottom of that section instead of the whole rulebase.
    /// </summary>
    public sealed record FirewallRulePlacement(FirewallRuleAnchor Anchor, string? AnchorRuleUid = null, string? LayerUid = null,
        string? SectionUid = null)
    {
        private const string kAbove = "above";
        private const string kBelow = "below";

        /// <summary>Insert at the end of the rulebase, the default without positioning information.</summary>
        public static FirewallRulePlacement EndOfRulebase { get; } = new(FirewallRuleAnchor.Bottom);

        /// <summary>
        /// Renders the position as JSON value for <see cref="FirewallWritePlaceholders.RulePosition"/>:
        /// "top", "bottom", {"top": section uid}, {"bottom": section uid}, {"above": uid} or {"below": uid}, the format of
        /// the Check Point management API.
        /// </summary>
        public string ToJson()
        {
            string anchorName = Anchor.ToString().ToLowerInvariant();
            if (Anchor is FirewallRuleAnchor.Above or FirewallRuleAnchor.Below)
            {
                if (string.IsNullOrWhiteSpace(AnchorRuleUid))
                {
                    throw new InvalidOperationException($"Rule position '{Anchor}' needs an anchor rule UID.");
                }
                JsonObject relative = new() { [Anchor == FirewallRuleAnchor.Above ? kAbove : kBelow] = AnchorRuleUid };
                return relative.ToJsonString();
            }
            if (!string.IsNullOrWhiteSpace(SectionUid))
            {
                JsonObject inSection = new() { [anchorName] = SectionUid };
                return inSection.ToJsonString();
            }
            return JsonSerializer.Serialize(anchorName);
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

    /// <summary>
    /// Places a rule at the position the rule positioning calculated and stored with the implementation task
    /// (<see cref="WfImplTask.RulePosition"/>). A task without position is placed at the end of the rulebase; a position
    /// type the library cannot write fails, so that no rule is written to a guessed place.
    /// </summary>
    public sealed class RulePositionPlacementProvider : IFirewallRulePlacementProvider
    {
        /// <inheritdoc/>
        /// <exception cref="FirewallWriteException">Thrown for a position type the library cannot write.</exception>
        public FirewallRulePlacement? GetPlacement(WfImplTask implTask)
        {
            ArgumentNullException.ThrowIfNull(implTask);
            return implTask.RulePosition switch
            {
                null => null,
                CheckPointRulePosition checkPointPosition => new FirewallRulePlacement(ToAnchor(checkPointPosition.AnchorKind),
                    checkPointPosition.AnchorRuleUid, checkPointPosition.LayerUid, checkPointPosition.SectionUid),
                RulePosition otherPosition => throw new FirewallWriteException(FirewallWriteErrorKind.Unsupported,
                    $"The rule position type '{otherPosition.PositionType}' of implementation task {implTask.Id} cannot be written to a firewall.")
            };
        }

        private static FirewallRuleAnchor ToAnchor(CheckPointPositionAnchorKind anchorKind) => anchorKind switch
        {
            CheckPointPositionAnchorKind.Top => FirewallRuleAnchor.Top,
            CheckPointPositionAnchorKind.Above => FirewallRuleAnchor.Above,
            CheckPointPositionAnchorKind.Below => FirewallRuleAnchor.Below,
            _ => FirewallRuleAnchor.Bottom
        };
    }
}

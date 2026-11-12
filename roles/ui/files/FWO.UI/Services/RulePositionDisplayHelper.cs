using FWO.Config.Api;
using FWO.Data.RulePositioning;
using FWO.Data.RulePositioning.CheckPoint;

namespace FWO.Ui.Services;

/// <summary>Formats persisted rule positions for human-readable workflow displays.</summary>
public static class RulePositionDisplayHelper
{
    /// <summary>Returns a localized description of a rule position or its JSON fallback.</summary>
    public static string Format(RulePosition? rulePosition, UserConfig userConfig)
    {
        if (rulePosition is not CheckPointRulePosition checkPointPosition)
        {
            return rulePosition == null
                ? ""
                : System.Text.Json.JsonSerializer.Serialize(rulePosition);
        }

        string layer = string.IsNullOrWhiteSpace(checkPointPosition.LayerUid)
            ? userConfig.GetText("unknown")
            : checkPointPosition.LayerUid;
        return checkPointPosition.AnchorKind switch
        {
            CheckPointPositionAnchorKind.Above => string.Format(
                userConfig.GetText("rule_position_before_rule"),
                checkPointPosition.AnchorRuleUid ?? userConfig.GetText("unknown")),
            CheckPointPositionAnchorKind.Below => string.Format(
                userConfig.GetText("rule_position_behind_rule"),
                checkPointPosition.AnchorRuleUid ?? userConfig.GetText("unknown")),
            CheckPointPositionAnchorKind.Top => string.Format(
                userConfig.GetText("rule_position_at_start_of_layer"),
                layer),
            CheckPointPositionAnchorKind.Bottom => string.Format(
                userConfig.GetText("rule_position_at_end_of_layer"),
                layer),
            _ => System.Text.Json.JsonSerializer.Serialize(checkPointPosition)
        };
    }
}

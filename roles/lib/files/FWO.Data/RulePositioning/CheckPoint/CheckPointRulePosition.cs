using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using FWO.Data.DeviceSpecificSettings;
using Newtonsoft.Json;

namespace FWO.Data.RulePositioning.CheckPoint;

/// <summary>
/// Semantic Check Point insertion anchor, independent of a vendor API request format.
/// </summary>
public enum CheckPointPositionAnchorKind
{
    Top,
    Bottom,
    Above,
    Below
}

/// <summary>
/// Persistable semantic position for one Check Point access layer.
/// </summary>
[RulePositionType(kDiscriminator)]
public sealed class CheckPointRulePosition : RulePosition, IJsonOnDeserialized
{
    public const string kDiscriminator = "check-point.rule-position.v1";

    [JsonProperty("layer_uid"), JsonPropertyName("layer_uid"), JsonInclude]
    public string LayerUid { get; private set; } = "";

    [JsonProperty("anchor_kind"), JsonPropertyName("anchor_kind"), JsonInclude]
    public CheckPointPositionAnchorKind AnchorKind { get; private set; }

    [JsonProperty("anchor_rule_uid"), JsonPropertyName("anchor_rule_uid"), JsonInclude]
    public string? AnchorRuleUid { get; private set; }

    /// <summary>
    /// Creates and validates a semantic Check Point position.
    /// </summary>
    public CheckPointRulePosition(
        string layerUid,
        CheckPointPositionAnchorKind anchorKind,
        string? anchorRuleUid,
        ProvisioningPositioningAlgorithm positioningAlgorithm)
    {
        LayerUid = layerUid;
        AnchorKind = anchorKind;
        AnchorRuleUid = anchorRuleUid;
        PositioningAlgorithm = positioningAlgorithm;
        Validate(layerUid, anchorKind, anchorRuleUid, positioningAlgorithm);
    }

    /// <summary>
    /// Supports serializer construction before invariant validation callbacks run.
    /// </summary>
    private CheckPointRulePosition()
    { }

    /// <summary>
    /// Validates values populated by System.Text.Json.
    /// </summary>
    void IJsonOnDeserialized.OnDeserialized() => Validate(
        LayerUid,
        AnchorKind,
        AnchorRuleUid,
        PositioningAlgorithm);

    /// <summary>
    /// Validates values populated by Newtonsoft.Json.
    /// </summary>
    [OnDeserialized]
    private void OnDeserialized(StreamingContext context) => Validate(
        LayerUid,
        AnchorKind,
        AnchorRuleUid,
        PositioningAlgorithm);

    /// <summary>
    /// Enforces anchor and algorithm invariants for construction and persistence loading.
    /// </summary>
    private static void Validate(
        string layerUid,
        CheckPointPositionAnchorKind anchorKind,
        string? anchorRuleUid,
        ProvisioningPositioningAlgorithm positioningAlgorithm)
    {
        if (string.IsNullOrWhiteSpace(layerUid))
        {
            throw new ArgumentException("A Check Point layer UID is required.", nameof(layerUid));
        }
        if (!Enum.IsDefined(anchorKind))
        {
            throw new ArgumentOutOfRangeException(nameof(anchorKind));
        }
        bool needsRuleUid = anchorKind is CheckPointPositionAnchorKind.Above or CheckPointPositionAnchorKind.Below;
        if (needsRuleUid != !string.IsNullOrWhiteSpace(anchorRuleUid))
        {
            throw new ArgumentException(
                "Above and below anchors require one rule UID; top and bottom anchors prohibit it.",
                nameof(anchorRuleUid));
        }
        if (!Enum.IsDefined(positioningAlgorithm) ||
            positioningAlgorithm == ProvisioningPositioningAlgorithm.Undefined)
        {
            throw new ArgumentOutOfRangeException(nameof(positioningAlgorithm));
        }
    }
}

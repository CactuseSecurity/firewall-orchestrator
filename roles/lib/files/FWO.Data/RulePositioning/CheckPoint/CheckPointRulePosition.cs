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
    /// <summary>Stable persisted discriminator for Check Point rule positions.</summary>
    public const string kDiscriminator = "check-point.rule-position.v1";

    [JsonProperty("layer_uid"), JsonPropertyName("layer_uid"), JsonInclude]
    /// <summary>Gets the target Check Point layer identifier.</summary>
    public string LayerUid { get; private set; } = "";

    [JsonProperty("anchor_kind"), JsonPropertyName("anchor_kind"), JsonInclude]
    /// <summary>Gets the semantic anchor kind within the target layer.</summary>
    public CheckPointPositionAnchorKind AnchorKind { get; private set; }

    [JsonProperty("anchor_rule_uid"), JsonPropertyName("anchor_rule_uid"), JsonInclude]
    /// <summary>Gets the optional anchor rule identifier.</summary>
    public string? AnchorRuleUid { get; private set; }

    [JsonProperty("section_uid"), JsonPropertyName("section_uid"), JsonInclude]
    /// <summary>Gets the optional target section identifier.</summary>
    public string? SectionUid { get; private set; }

    [JsonProperty("section_name"), JsonPropertyName("section_name"), JsonInclude]
    /// <summary>Gets the optional target section name for operator verification.</summary>
    public string? SectionName { get; private set; }

    /// <summary>
    /// Creates and validates a semantic Check Point position.
    /// </summary>
    public CheckPointRulePosition(
        string layerUid,
        CheckPointPositionAnchorKind anchorKind,
        string? anchorRuleUid,
        ProvisioningPositioningAlgorithm positioningAlgorithm,
        string? sectionUid = null,
        string? sectionName = null)
    {
        LayerUid = layerUid;
        AnchorKind = anchorKind;
        AnchorRuleUid = anchorRuleUid;
        SectionUid = sectionUid;
        SectionName = sectionName;
        PositioningAlgorithm = positioningAlgorithm;
        Validate(layerUid, anchorKind, anchorRuleUid, positioningAlgorithm, sectionUid, sectionName);
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
        PositioningAlgorithm,
        SectionUid,
        SectionName);

    /// <summary>
    /// Validates values populated by Newtonsoft.Json.
    /// </summary>
    [OnDeserialized]
    private void OnDeserialized(StreamingContext context) => Validate(
        LayerUid,
        AnchorKind,
        AnchorRuleUid,
        PositioningAlgorithm,
        SectionUid,
        SectionName);

    /// <summary>
    /// Enforces anchor and algorithm invariants for construction and persistence loading.
    /// </summary>
    private static void Validate(
        string layerUid,
        CheckPointPositionAnchorKind anchorKind,
        string? anchorRuleUid,
        ProvisioningPositioningAlgorithm positioningAlgorithm,
        string? sectionUid,
        string? sectionName)
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
        if ((sectionUid != null && string.IsNullOrWhiteSpace(sectionUid)) ||
            (sectionUid == null) != (sectionName == null))
        {
            throw new ArgumentException(
                "A section-relative anchor requires both a non-empty section UID and a section name value.",
                nameof(sectionUid));
        }
        if (sectionUid != null && needsRuleUid)
        {
            throw new ArgumentException(
                "Section-relative anchors must use top or bottom without a rule UID.",
                nameof(sectionUid));
        }
        if (!Enum.IsDefined(positioningAlgorithm) ||
            positioningAlgorithm == ProvisioningPositioningAlgorithm.Undefined)
        {
            throw new ArgumentOutOfRangeException(nameof(positioningAlgorithm));
        }
    }
}

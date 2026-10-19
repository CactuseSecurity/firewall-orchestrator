using System.Text.Json.Nodes;
using FWO.Basics.Exceptions;
using FWO.Data.Provisioning;
using FWO.Data.RulePositioning;
using FWO.Data.RulePositioning.CheckPoint;

namespace FWO.ExternalSystems.CheckPoint;

/// <summary>
/// Renders a semantic Check Point rule position into an access-rule request body.
/// </summary>
internal static class CheckPointRulePositionRenderer
{
    private const string kInvalidAlgorithmMessage = "The configured rule-positioning algorithm is invalid.";
    private const string kUnsupportedPositionMessage = "The rule position is not supported for Check Point rendering.";
    private const string kInvalidPositionMessage = "The Check Point rule position is invalid.";
    private const string kAlgorithmMismatchMessage = "The rule position does not match the configured positioning algorithm.";
    private const string kInvalidBodyMessage = "The Check Point access-rule request body must be a JSON object.";
    private const int kSupportedSchemaVersion = 1;

    /// <summary>
    /// Returns an independent request body containing the explicitly selected position.
    /// </summary>
    internal static JsonNode Render(
        JsonNode requestBody,
        RulePosition? rulePosition,
        ProvisioningPositioningAlgorithm expectedAlgorithm)
    {
        ArgumentNullException.ThrowIfNull(requestBody);

        if (rulePosition is null)
        {
            return requestBody.DeepClone();
        }

        if (!IsValidAlgorithm(expectedAlgorithm))
        {
            throw new ConfigException(kInvalidAlgorithmMessage);
        }

        if (rulePosition is not CheckPointRulePosition checkPointPosition)
        {
            throw new ConfigException(kUnsupportedPositionMessage);
        }

        if (!IsValid(checkPointPosition))
        {
            throw new ConfigException(kInvalidPositionMessage);
        }

        if (checkPointPosition.PositioningAlgorithm != expectedAlgorithm)
        {
            throw new ConfigException(kAlgorithmMismatchMessage);
        }

        if (requestBody is not JsonObject requestObject)
        {
            throw new ConfigException(kInvalidBodyMessage);
        }

        JsonObject renderedBody = (JsonObject)requestObject.DeepClone();
        renderedBody["layer"] = checkPointPosition.LayerUid;
        renderedBody["position"] = RenderPosition(checkPointPosition);
        return renderedBody;
    }

    /// <summary>
    /// Checks the persisted Check Point position defensively before rendering it.
    /// </summary>
    private static bool IsValid(CheckPointRulePosition position)
    {
        if (position.PositionType != CheckPointRulePosition.kDiscriminator ||
            position.SchemaVersion != kSupportedSchemaVersion ||
            string.IsNullOrWhiteSpace(position.LayerUid) ||
            !Enum.IsDefined(position.AnchorKind) ||
            !IsValidAlgorithm(position.PositioningAlgorithm))
        {
            return false;
        }

        bool requiresRuleUid = position.AnchorKind is CheckPointPositionAnchorKind.Above or CheckPointPositionAnchorKind.Below;
        return requiresRuleUid == !string.IsNullOrWhiteSpace(position.AnchorRuleUid);
    }

    /// <summary>
    /// Checks whether an algorithm value can participate in exact identity matching.
    /// </summary>
    private static bool IsValidAlgorithm(ProvisioningPositioningAlgorithm algorithm)
    {
        return algorithm != ProvisioningPositioningAlgorithm.Undefined && Enum.IsDefined(algorithm);
    }

    /// <summary>
    /// Maps a validated semantic anchor to the Check Point API representation.
    /// </summary>
    private static JsonNode RenderPosition(CheckPointRulePosition position)
    {
        return position.AnchorKind switch
        {
            CheckPointPositionAnchorKind.Top => JsonValue.Create("top"),
            CheckPointPositionAnchorKind.Bottom => JsonValue.Create("bottom"),
            CheckPointPositionAnchorKind.Above => new JsonObject { ["above"] = position.AnchorRuleUid },
            CheckPointPositionAnchorKind.Below => new JsonObject { ["below"] = position.AnchorRuleUid },
            _ => throw new ConfigException(kInvalidPositionMessage)
        };
    }
}

using FWO.Data.RulePositioning;
using FWO.Services.RulePositioning.Blocks;
using FWO.Services.RulePositioning.CheckPoint;

namespace FWO.Compliance.RulePositioning.CheckPoint;

/// <summary>Maps typed Check Point positioning causes to public failures.</summary>
internal static class CheckPointRulePositioningFailureMapper
{
    /// <summary>Maps a recipe-execution result to its public failure contract.</summary>
    internal static RulePositioningFailure MapExecutionFailure(
        string stage,
        PositioningBlockResult<CheckPointBlockValue> result)
    {
        if (result is not PositioningBlockFailure<CheckPointBlockValue> failure)
        {
            return new RulePositioningFailure(RulePositioningFailureCategory.CalculationFailed);
        }
        RulePositioningFailureCategory category = failure.Category == PositioningBlockFailureCategory.InvalidRecipe
            ? RulePositioningFailureCategory.ConfigurationInvalid
            : RulePositioningFailureCategory.CalculationFailed;
        return KnownFailure(
            stage,
            category,
            failure.Category,
            failure.Diagnostic,
            failure.OriginatingBlockKind);
    }

    /// <summary>Creates a failure from one typed lower-level cause.</summary>
    internal static RulePositioningFailure KnownFailure(
        string stage,
        RulePositioningFailureCategory category,
        Enum specificCategory,
        string diagnostic,
        string? originatingBlockKind = null)
    {
        string block = originatingBlockKind == null ? "" : $" / {originatingBlockKind}";
        return new RulePositioningFailure(
            category,
            $"{stage}{block} / {specificCategory}: {diagnostic}");
    }
}

/// <summary>Identifies failures originating inside a Check Point positioning strategy.</summary>
internal enum CheckPointStrategyFailureCategory
{
    UnexpectedPositionResult
}

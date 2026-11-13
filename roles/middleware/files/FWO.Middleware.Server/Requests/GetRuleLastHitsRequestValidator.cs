using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Validates a <see cref="GetRuleLastHitsRequest"/> completely and reports every detected key-level error in
/// one result.
/// </summary>
public static class GetRuleLastHitsRequestValidator
{
    /// <summary>
    /// Validates the whole request without stopping at the first error.
    /// </summary>
    /// <param name="request">Request to validate, or null when no body was supplied.</param>
    /// <param name="maxLimit">Maximum page size.</param>
    /// <returns>All detected errors; empty when the request is valid.</returns>
    public static RequestValidationErrorResponse Validate(GetRuleLastHitsRequest? request, int maxLimit)
    {
        RequestValidationErrorResponse result = new();
        if (request == null)
        {
            result.Errors.Add(AggregatedRequestKeyValidation.BuildError(GetRuleLastHitsValidationSchema.kRootPath,
                $"{GetRuleLastHitsValidationSchema.kEndpointName} requires a request body. Valid root keys: " +
                AggregatedRequestKeyValidation.DescribeKeys(GetRuleLastHitsValidationSchema.RootKeys)));
            return result;
        }

        ValidateManagementId(request, result);
        ValidatePaging(request.Options, maxLimit, result);
        if (request.Options.Filter != null)
        {
            ValidateFilter(request.Options.Filter, result);
        }
        AggregatedRequestKeyValidation.AddUnsupportedKeyErrors(request.AdditionalData, GetRuleLastHitsValidationSchema.kRootPath,
            GetRuleLastHitsValidationSchema.RootKeys, result);
        AggregatedRequestKeyValidation.AddUnsupportedKeyErrors(request.Options.AdditionalData, GetRuleLastHitsValidationSchema.kOptionsPath,
            GetRuleLastHitsValidationSchema.OptionsKeys, result);
        AggregatedRequestKeyValidation.AddUnsupportedKeyErrors(request.Options.Filter?.AdditionalData, GetRuleLastHitsValidationSchema.kFilterPath,
            GetRuleLastHitsValidationSchema.FilterKeys, result);
        return result;
    }

    private static void ValidateManagementId(GetRuleLastHitsRequest request, RequestValidationErrorResponse result)
    {
        if (request.ManagementId == null)
        {
            result.Errors.Add(AggregatedRequestKeyValidation.BuildError(GetRuleLastHitsValidationSchema.kManagementIdPath,
                $"'{GetRuleLastHitsValidationSchema.kManagementIdPath}' is required. {GetRuleLastHitsValidationSchema.RootKeys[0].Description}"));
        }
        else if (request.ManagementId <= 0)
        {
            result.Errors.Add(AggregatedRequestKeyValidation.BuildError(GetRuleLastHitsValidationSchema.kManagementIdPath,
                $"'{GetRuleLastHitsValidationSchema.kManagementIdPath}' must be greater than 0."));
        }
    }

    private static void ValidatePaging(GetRuleLastHitsOptions options, int maxLimit, RequestValidationErrorResponse result)
    {
        if (!ListPaging.IsValidLimit(options.Limit, maxLimit))
        {
            result.Errors.Add(AggregatedRequestKeyValidation.BuildError(GetRuleLastHitsValidationSchema.kLimitPath,
                ListPaging.BuildLimitError(GetRuleLastHitsValidationSchema.kLimitPath, maxLimit)));
        }
        if (!ListPaging.IsValidOffset(options.Offset))
        {
            result.Errors.Add(AggregatedRequestKeyValidation.BuildError(GetRuleLastHitsValidationSchema.kOffsetPath,
                ListPaging.BuildOffsetError(GetRuleLastHitsValidationSchema.kOffsetPath)));
        }
    }

    private static void ValidateFilter(RuleLastHitFilter filter, RequestValidationErrorResponse result)
    {
        ValidatePositiveId(filter.MgmId, GetRuleLastHitsValidationSchema.kFilterMgmIdPath, result);
        ValidatePositiveId(filter.DevId, GetRuleLastHitsValidationSchema.kFilterDevIdPath, result);
        ValidateRuleUids(filter.RuleUid, result);
        ValidateTextLength(filter.RuleName, GetRuleLastHitsValidationSchema.kFilterRuleNamePath, result);
        ValidateTextLength(filter.GatewayName, GetRuleLastHitsValidationSchema.kFilterGatewayNamePath, result);
        ValidateLastHitRange(filter, result);
    }

    private static void ValidatePositiveId(int? id, string path, RequestValidationErrorResponse result)
    {
        if (id <= 0)
        {
            result.Errors.Add(AggregatedRequestKeyValidation.BuildError(path, $"'{path}' must be greater than 0 when supplied."));
        }
    }

    private static void ValidateRuleUids(List<string>? ruleUids, RequestValidationErrorResponse result)
    {
        if (ruleUids == null)
        {
            return;
        }

        string path = GetRuleLastHitsValidationSchema.kFilterRuleUidPath;
        if (ruleUids.Count == 0 || ruleUids.Count > RuleLastHitFilter.kMaxFilterValues)
        {
            result.Errors.Add(AggregatedRequestKeyValidation.BuildError(path,
                $"'{path}' must contain between 1 and {RuleLastHitFilter.kMaxFilterValues} values when supplied. " +
                GetRuleLastHitsValidationSchema.DescribeFilterKey("ruleUid")));
            return;
        }
        for (int index = 0; index < ruleUids.Count; index++)
        {
            string itemPath = $"{path}[{index}]";
            if (string.IsNullOrEmpty(ruleUids[index]))
            {
                result.Errors.Add(AggregatedRequestKeyValidation.BuildError(itemPath, $"'{itemPath}' must not be null or empty."));
            }
            ValidateTextLength(ruleUids[index], itemPath, result);
        }
    }

    private static void ValidateTextLength(string? value, string path, RequestValidationErrorResponse result)
    {
        if (value?.Length > RuleLastHitFilter.kMaxFilterTextLength)
        {
            result.Errors.Add(AggregatedRequestKeyValidation.BuildError(path,
                $"'{path}' must not be longer than {RuleLastHitFilter.kMaxFilterTextLength} characters."));
        }
    }

    private static void ValidateLastHitRange(RuleLastHitFilter filter, RequestValidationErrorResponse result)
    {
        if (filter.LastHitAfter != null && filter.LastHitBefore != null && filter.LastHitBefore <= filter.LastHitAfter)
        {
            result.Errors.Add(AggregatedRequestKeyValidation.BuildError(GetRuleLastHitsValidationSchema.kFilterLastHitBeforePath,
                $"'{GetRuleLastHitsValidationSchema.kFilterLastHitBeforePath}' must be later than 'lastHitAfter'. " +
                GetRuleLastHitsValidationSchema.DescribeFilterKey("lastHitBefore")));
        }
        if (filter.NeverHit == true && (filter.LastHitAfter != null || filter.LastHitBefore != null))
        {
            result.Errors.Add(AggregatedRequestKeyValidation.BuildError(GetRuleLastHitsValidationSchema.kFilterNeverHitPath,
                $"'{GetRuleLastHitsValidationSchema.kFilterNeverHitPath}' must not be true together with a last hit bound. " +
                GetRuleLastHitsValidationSchema.DescribeFilterKey("neverHit")));
        }
    }
}

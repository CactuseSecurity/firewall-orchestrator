using FWO.Data.Modelling;
using FWO.Middleware.Server.Services;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Validates the requests of the modelling object endpoints and collects every detected key-level error.
/// </summary>
public static class ModellingObjectRequestValidator
{
    private const string kAppIdExternalPath = "appIdExternal";
    private const string kOptionsPath = "options";
    private const string kFilterPath = "options.filter";
    private static readonly List<string> kPermissionValues =
    [
        InterfacePermissions.Public.ToString().ToLowerInvariant(),
        InterfacePermissions.Restricted.ToString().ToLowerInvariant(),
        InterfacePermissions.Private.ToString().ToLowerInvariant()
    ];

    /// <summary>
    /// Validates a request for the interfaces available to an application.
    /// </summary>
    /// <param name="request">The request to validate.</param>
    /// <param name="maxLimit">Maximum page size.</param>
    /// <returns>The errors keyed by request path; empty when the request is valid.</returns>
    public static Dictionary<string, string[]> Validate(GetAvailableInterfacesRequest request, int maxLimit)
    {
        Dictionary<string, string[]> errors = [];
        ValidateAppIdExternal(request.AppIdExternal, errors);
        ValidateOptions(request.Options, maxLimit, errors, ValidateInterfaceFilter);
        return errors;
    }

    /// <summary>
    /// Validates a request for the app roles of an application.
    /// </summary>
    /// <param name="request">The request to validate.</param>
    /// <param name="maxLimit">Maximum page size.</param>
    /// <returns>The errors keyed by request path; empty when the request is valid.</returns>
    public static Dictionary<string, string[]> Validate(GetAppRolesPerAppRequest request, int maxLimit)
    {
        Dictionary<string, string[]> errors = [];
        ValidateAppIdExternal(request.AppIdExternal, errors);
        ValidateOptions(request.Options, maxLimit, errors, ValidateGroupFilter);
        return errors;
    }

    /// <summary>
    /// Validates a request for network areas.
    /// </summary>
    /// <param name="request">The request to validate.</param>
    /// <param name="maxLimit">Maximum page size.</param>
    /// <returns>The errors keyed by request path; empty when the request is valid.</returns>
    public static Dictionary<string, string[]> Validate(GetNetworkAreasRequest request, int maxLimit)
    {
        Dictionary<string, string[]> errors = [];
        ValidateOptions(request.Options, maxLimit, errors, ValidateGroupFilter);
        return errors;
    }

    private static void ValidateAppIdExternal(string? appIdExternal, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(appIdExternal))
        {
            AddError(errors, kAppIdExternalPath, $"{kAppIdExternalPath} is required and must not be blank.");
            return;
        }
        ValidateText(appIdExternal, kAppIdExternalPath, errors);
    }

    private static void ValidateOptions<TFilter>(ModellingListOptions<TFilter>? options, int maxLimit,
        Dictionary<string, string[]> errors, Action<TFilter, Dictionary<string, string[]>> validateFilter) where TFilter : class
    {
        if (options is null)
        {
            AddError(errors, kOptionsPath, $"{kOptionsPath} must be an object when supplied.");
            return;
        }

        if (options.Filter is not null)
        {
            validateFilter(options.Filter, errors);
        }
        foreach (KeyValuePair<string, string[]> pagingError in ListPaging.ValidatePaging(
            options.Limit, options.Offset, maxLimit, "options.limit", "options.offset"))
        {
            errors[pagingError.Key] = pagingError.Value;
        }
    }

    private static void ValidateInterfaceFilter(AvailableInterfacesFilter filter, Dictionary<string, string[]> errors)
    {
        ValidatePositiveIds(filter.ConnectionId, $"{kFilterPath}.connectionId", errors);
        ValidateTextValues(filter.InterfaceName, $"{kFilterPath}.interfaceName", errors);
        ValidateTextValues(filter.InterfaceDescription, $"{kFilterPath}.interfaceDescription", errors);
        ValidatePermissionValues(filter.Permission, $"{kFilterPath}.permission", errors);
        ValidateTextValues(filter.ProvidingAppName, $"{kFilterPath}.providingAppName", errors);
        ValidateTextValues(filter.ProvidingAppIdExternal, $"{kFilterPath}.providingAppIdExternal", errors);
    }

    private static void ValidateGroupFilter(ModellingGroupFilter filter, Dictionary<string, string[]> errors)
    {
        ValidatePositiveIds(filter.Id, $"{kFilterPath}.id", errors);
        ValidateTextValues(filter.IdString, $"{kFilterPath}.idString", errors);
        ValidateTextValues(filter.Name, $"{kFilterPath}.name", errors);
    }

    private static bool ValidateValueCount<T>(List<T>? values, string fieldName, Dictionary<string, string[]> errors)
    {
        if (values is null)
        {
            return false;
        }
        if (values.Count > ModellingObjectRequestLimits.kMaxFilterValues)
        {
            AddError(errors, fieldName, $"{fieldName} must not contain more than {ModellingObjectRequestLimits.kMaxFilterValues} values.");
        }
        return true;
    }

    private static void ValidatePositiveIds<T>(List<T>? values, string fieldName, Dictionary<string, string[]> errors)
        where T : struct, IComparable<T>
    {
        if (!ValidateValueCount(values, fieldName, errors))
        {
            return;
        }

        for (int index = 0; index < values!.Count; index++)
        {
            if (values[index].CompareTo(default) <= 0)
            {
                AddError(errors, $"{fieldName}[{index}]", $"{fieldName}[{index}] must be a positive integer when supplied.");
            }
        }
    }

    private static void ValidateTextValues(List<string>? values, string fieldName, Dictionary<string, string[]> errors)
    {
        if (!ValidateValueCount(values, fieldName, errors))
        {
            return;
        }

        for (int index = 0; index < values!.Count; index++)
        {
            string indexedFieldName = $"{fieldName}[{index}]";
            string? value = values[index];
            if (string.IsNullOrWhiteSpace(value))
            {
                AddError(errors, indexedFieldName, $"{indexedFieldName} must not be null or blank when supplied.");
                continue;
            }
            ValidateText(value, indexedFieldName, errors);
        }
    }

    private static void ValidatePermissionValues(List<string>? values, string fieldName, Dictionary<string, string[]> errors)
    {
        if (!ValidateValueCount(values, fieldName, errors))
        {
            return;
        }

        string allowedValues = string.Join(", ", kPermissionValues);
        for (int index = 0; index < values!.Count; index++)
        {
            string? value = values[index]?.Trim();
            if (value is null || !kPermissionValues.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                AddError(errors, $"{fieldName}[{index}]", $"{fieldName}[{index}] must be one of: {allowedValues}.");
            }
        }
    }

    private static void ValidateText(string value, string fieldName, Dictionary<string, string[]> errors)
    {
        List<string> fieldErrors = [];
        if (value.Length > ModellingObjectRequestLimits.kMaxTextLength)
        {
            fieldErrors.Add($"{fieldName} must not exceed {ModellingObjectRequestLimits.kMaxTextLength} characters.");
        }
        if (value.Any(char.IsControl))
        {
            fieldErrors.Add($"{fieldName} must not contain control characters.");
        }
        if (fieldErrors.Count > 0)
        {
            errors[fieldName] = fieldErrors.ToArray();
        }
    }

    private static void AddError(Dictionary<string, string[]> errors, string fieldName, string error)
    {
        string[] errorValues = new string[1];
        errorValues[0] = error;
        errors[fieldName] = errorValues;
    }
}

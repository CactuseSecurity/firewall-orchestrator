using FWO.Data.Middleware;
using NetTools;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Validates a <see cref="ReportGetParameters"/> request before its values are joined into a report filter.
/// Every error is reported under the JSON path of the offending value, and all errors are returned together.
/// </summary>
public static class ReportGetParametersValidator
{
    /// <summary>
    /// Maximum number of values in each list of the request.
    /// </summary>
    public const int kMaxFilterValues = 50;

    /// <summary>
    /// Maximum number of management or gateway ids in the device filter.
    /// </summary>
    public const int kMaxDeviceFilterIds = 1000;

    /// <summary>
    /// Maximum length of each text value of the request.
    /// </summary>
    public const int kMaxFilterTextLength = 255;

    private const int kMinPort = 0;
    private const int kMaxPort = 65535;
    private const int kMinProtocol = 0;
    private const int kMaxProtocol = 255;
    private const string kIpNotation = "a single IP address, a CIDR network such as '10.1.0.0/16' or a range such as '10.1.0.1-10.1.0.9' without blanks";

    /// <summary>
    /// Validates the request and returns all detected field errors.
    /// </summary>
    /// <param name="parameters">The request; null when the body was empty.</param>
    /// <returns>The errors per request path; empty when the request is valid.</returns>
    public static Dictionary<string, string[]> Validate(ReportGetParameters? parameters)
    {
        Dictionary<string, string[]> errors = [];
        if (parameters is null)
        {
            AddError(errors, "body", "The request body must be a report request object.");
            return errors;
        }

        if (parameters.ApiReportType is null)
        {
            AddError(errors, "report-type", "'report-type' must be a string when supplied.");
        }
        ValidateText(parameters.ApiReportType, "report-type", errors);
        ValidateTextList(parameters.ApiReportView, "report-view", errors);
        ValidateText(parameters.Action, "action", errors);
        ValidateDeviceFilter(parameters.ApiDeviceFilter, errors);
        ValidateRuleFilter(parameters.ApiRuleFilter, errors);
        return errors;
    }

    private static void ValidateDeviceFilter(ApiDeviceFilter? deviceFilter, Dictionary<string, string[]> errors)
    {
        if (deviceFilter is null)
        {
            AddError(errors, "device-filter", "'device-filter' must be an object when supplied.");
            return;
        }

        ValidateCount(deviceFilter.ManagementIds, "device-filter.management-ids", kMaxDeviceFilterIds, errors);
        ValidateCount(deviceFilter.DeviceIds, "device-filter.gateway-ids", kMaxDeviceFilterIds, errors);
    }

    private static void ValidateRuleFilter(ApiRuleFilter? ruleFilter, Dictionary<string, string[]> errors)
    {
        if (ruleFilter is null)
        {
            AddError(errors, "rule-filter", "'rule-filter' must be an object when supplied.");
            return;
        }

        ValidateIpList(ruleFilter.SourceIps, "rule-filter.source-ip", errors);
        ValidateIpList(ruleFilter.DestinationIps, "rule-filter.destination-ip", errors);
        ValidateIpList(ruleFilter.Ips, "rule-filter.ip", errors);
        ValidateServices(ruleFilter.Services, "rule-filter.service", errors);
    }

    private static void ValidateIpList(List<string>? ips, string path, Dictionary<string, string[]> errors)
    {
        if (!ValidateCount(ips, path, kMaxFilterValues, errors))
        {
            return;
        }

        for (int index = 0; index < ips!.Count; index++)
        {
            if (!IsIpAddressOrRange(ips[index]))
            {
                string elementPath = $"{path}[{index}]";
                AddError(errors, elementPath, $"'{elementPath}' must be {kIpNotation}.");
            }
        }
    }

    private static void ValidateServices(List<ApiService>? services, string path, Dictionary<string, string[]> errors)
    {
        if (!ValidateCount(services, path, kMaxFilterValues, errors))
        {
            return;
        }

        for (int index = 0; index < services!.Count; index++)
        {
            string elementPath = $"{path}[{index}]";
            ApiService? service = services[index];
            if (service is null)
            {
                AddError(errors, elementPath, $"'{elementPath}' must be an object.");
                continue;
            }

            ValidateText(service.Name, $"{elementPath}.name", errors);
            ValidateRange(service.Protocol, $"{elementPath}.protocol", kMinProtocol, kMaxProtocol, errors);
            ValidateRange(service.Port, $"{elementPath}.port", kMinPort, kMaxPort, errors);
        }
    }

    private static void ValidateTextList(List<string>? values, string path, Dictionary<string, string[]> errors)
    {
        if (!ValidateCount(values, path, kMaxFilterValues, errors))
        {
            return;
        }

        for (int index = 0; index < values!.Count; index++)
        {
            string elementPath = $"{path}[{index}]";
            if (values[index] is null)
            {
                AddError(errors, elementPath, $"'{elementPath}' must be a string.");
                continue;
            }
            ValidateText(values[index], elementPath, errors);
        }
    }

    /// <summary>
    /// Checks that a list is given and does not exceed its maximum size.
    /// </summary>
    /// <returns>True when the list is given and its elements may be checked.</returns>
    private static bool ValidateCount<T>(List<T>? values, string path, int maxCount, Dictionary<string, string[]> errors)
    {
        if (values is null)
        {
            AddError(errors, path, $"'{path}' must be a list when supplied.");
            return false;
        }

        if (values.Count > maxCount)
        {
            AddError(errors, path, $"'{path}' must not contain more than {maxCount} values.");
            return false;
        }
        return true;
    }

    private static void ValidateText(string? value, string path, Dictionary<string, string[]> errors)
    {
        if (value?.Length > kMaxFilterTextLength)
        {
            AddError(errors, path, $"'{path}' must not be longer than {kMaxFilterTextLength} characters.");
        }
    }

    private static void ValidateRange(int? value, string path, int minValue, int maxValue, Dictionary<string, string[]> errors)
    {
        if (value < minValue || value > maxValue)
        {
            AddError(errors, path, $"'{path}' must be between {minValue} and {maxValue}.");
        }
    }

    /// <summary>
    /// True for an address, network or range in the notation the report filter accepts. Only address characters
    /// are allowed, so that the value cannot carry brackets, blanks or other filter syntax into the filter.
    /// </summary>
    private static bool IsIpAddressOrRange(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > kMaxFilterTextLength || !value.All(IsIpNotationCharacter))
        {
            return false;
        }
        return IPAddressRange.TryParse(value, out _);
    }

    private static bool IsIpNotationCharacter(char character)
    {
        return char.IsAsciiHexDigit(character) || character is '.' or ':' or '/' or '-';
    }

    /// <summary>
    /// Records the error of one request path; each path is checked by one rule only.
    /// </summary>
    private static void AddError(Dictionary<string, string[]> errors, string path, string message)
    {
        string[] pathErrors = new string[1];
        pathErrors[0] = message;
        errors[path] = pathErrors;
    }
}

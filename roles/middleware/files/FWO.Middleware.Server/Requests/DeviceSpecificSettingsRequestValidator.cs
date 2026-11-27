using FWO.Data.DeviceSpecificSettings;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using FWO.Services.DeviceSpecificSettings;
using Schema = FWO.Middleware.Server.Requests.DeviceSpecificSettingsValidationSchema;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Validates the device specific settings requests completely and reports every detected key-level error in one result.
/// </summary>
public static class DeviceSpecificSettingsRequestValidator
{
    /// <summary>Validates an import request; the package itself is validated against the installation by the import.</summary>
    public static RequestValidationErrorResponse ValidateImport(ImportDeviceSpecificSettingsRequest? request)
    {
        RequestValidationErrorResponse result = new();
        if (request is null)
        {
            AddMissingBody(result, Schema.ImportRootKeys);
            return result;
        }

        if (request.Package is null)
        {
            Add(result, Schema.kPackagePath, $"'{Schema.kPackagePath}' is required. {Schema.kPackageDescription}");
        }

        if (request.Options.Mode is not null && ParseMode(request.Options.Mode) is null)
        {
            Add(result, Schema.kModePath, $"'{Schema.kModePath}' must be '{Schema.kModeMerge}' or '{Schema.kModeReplace}'.");
        }

        AggregatedRequestKeyValidation.AddUnsupportedKeyErrors(request.AdditionalData, Schema.kRootPath, Schema.ImportRootKeys, result);
        AggregatedRequestKeyValidation.AddUnsupportedKeyErrors(request.Options.AdditionalData, Schema.kOptionsPath, Schema.ImportOptionsKeys, result);
        return result;
    }

    /// <summary>Validates an add snapshot request including the structure of a given package.</summary>
    public static RequestValidationErrorResponse ValidateAddSnapshot(AddDeviceSpecificSettingsSnapshotRequest? request)
    {
        RequestValidationErrorResponse result = new();
        if (request is null)
        {
            AddMissingBody(result, Schema.AddSnapshotRootKeys);
            return result;
        }

        DeviceSpecificSettingsPackage structurePlaceholder = new();
        foreach (DeviceSpecificSettingsImportIssue issue in DeviceSpecificSettingsSnapshotService.ValidateSnapshot(
            request.Name, request.Description, request.Package ?? structurePlaceholder))
        {
            Add(result, issue.Path, issue.Message);
        }

        AggregatedRequestKeyValidation.AddUnsupportedKeyErrors(request.AdditionalData, Schema.kRootPath, Schema.AddSnapshotRootKeys, result);
        AggregatedRequestKeyValidation.AddUnsupportedKeyErrors(request.Options.AdditionalData, Schema.kOptionsPath, Schema.NoOptionsKeys, result);
        return result;
    }

    /// <summary>Validates a list snapshots request.</summary>
    public static RequestValidationErrorResponse ValidateListSnapshots(ListDeviceSpecificSettingsSnapshotsRequest? request, int maxLimit)
    {
        RequestValidationErrorResponse result = new();
        if (request is null)
        {
            AddMissingBody(result, Schema.ListSnapshotsRootKeys);
            return result;
        }

        ListDeviceSpecificSettingsSnapshotsOptions options = request.Options;
        if (!ListPaging.IsValidLimit(options.Limit, maxLimit))
        {
            Add(result, Schema.kLimitPath, ListPaging.BuildLimitError(Schema.kLimitPath, maxLimit));
        }
        if (!ListPaging.IsValidOffset(options.Offset))
        {
            Add(result, Schema.kOffsetPath, ListPaging.BuildOffsetError(Schema.kOffsetPath));
        }
        ValidateFilterText(options.Filter?.Name, Schema.kFilterNamePath, result);
        ValidateFilterText(options.Filter?.Description, Schema.kFilterDescriptionPath, result);
        ValidateFilterText(options.Filter?.FwoVersion, Schema.kFilterFwoVersionPath, result);

        AggregatedRequestKeyValidation.AddUnsupportedKeyErrors(request.AdditionalData, Schema.kRootPath, Schema.ListSnapshotsRootKeys, result);
        AggregatedRequestKeyValidation.AddUnsupportedKeyErrors(options.AdditionalData, Schema.kOptionsPath, Schema.ListSnapshotsOptionsKeys, result);
        AggregatedRequestKeyValidation.AddUnsupportedKeyErrors(options.Filter?.AdditionalData, Schema.kFilterPath, Schema.ListSnapshotsFilterKeys, result);
        return result;
    }

    /// <summary>Converts the import errors of a package to request errors below the <c>package</c> key.</summary>
    public static RequestValidationErrorResponse FromImportIssues(IEnumerable<DeviceSpecificSettingsImportIssue> issues)
    {
        RequestValidationErrorResponse result = new();
        foreach (DeviceSpecificSettingsImportIssue issue in issues)
        {
            Add(result, issue.Path.Length == 0 ? Schema.kPackagePath : $"{Schema.kPackagePath}.{issue.Path}", issue.Message);
        }
        return result;
    }

    /// <summary>The import mode of a mode text; null for an unknown text. A null text selects merge.</summary>
    public static DeviceSpecificSettingsImportMode? ParseMode(string? mode) => mode switch
    {
        null or Schema.kModeMerge => DeviceSpecificSettingsImportMode.Merge,
        Schema.kModeReplace => DeviceSpecificSettingsImportMode.Replace,
        _ => null
    };

    private static void ValidateFilterText(string? value, string path, RequestValidationErrorResponse result)
    {
        if (value?.Length > Schema.kMaxFilterTextLength)
        {
            Add(result, path, $"'{path}' must not be longer than {Schema.kMaxFilterTextLength} characters.");
        }
    }

    private static void AddMissingBody(RequestValidationErrorResponse result, IReadOnlyList<RequestKeyDefinition> rootKeys)
    {
        Add(result, Schema.kRootPath, $"A request body is required. Valid root keys: {AggregatedRequestKeyValidation.DescribeKeys(rootKeys)}");
    }

    private static void Add(RequestValidationErrorResponse result, string path, string message)
    {
        result.Errors.Add(AggregatedRequestKeyValidation.BuildError(path, message));
    }
}

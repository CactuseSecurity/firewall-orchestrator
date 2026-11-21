using FWO.Data.DeviceSpecificSettings;
using FWO.Middleware.Server.Services;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Holds the single authoritative description of every key of the device specific settings requests. Validation help
/// text is built from these definitions, so API documentation and validation messages cannot contradict one another.
/// </summary>
public static class DeviceSpecificSettingsValidationSchema
{
    /// <summary>JSON path of the root request object.</summary>
    public const string kRootPath = "";

    /// <summary>JSON path of the package key.</summary>
    public const string kPackagePath = "package";

    /// <summary>JSON path of the options object.</summary>
    public const string kOptionsPath = "options";

    /// <summary>JSON path of the import mode key.</summary>
    public const string kModePath = "options.mode";

    /// <summary>JSON path of the snapshot name key.</summary>
    public const string kNamePath = "name";

    /// <summary>JSON path of the snapshot description key.</summary>
    public const string kDescriptionPath = "description";

    /// <summary>JSON path of the page size key.</summary>
    public const string kLimitPath = "options.limit";

    /// <summary>JSON path of the paging offset key.</summary>
    public const string kOffsetPath = "options.offset";

    /// <summary>JSON path of the filter object.</summary>
    public const string kFilterPath = "options.filter";

    /// <summary>JSON path of the name filter key.</summary>
    public const string kFilterNamePath = "options.filter.name";

    /// <summary>JSON path of the description filter key.</summary>
    public const string kFilterDescriptionPath = "options.filter.description";

    /// <summary>JSON path of the FWO version filter key.</summary>
    public const string kFilterFwoVersionPath = "options.filter.fwo_version";

    /// <summary>Import mode that only sets the values of the package.</summary>
    public const string kModeMerge = "merge";

    /// <summary>Import mode that makes the package the complete state of the levels it names.</summary>
    public const string kModeReplace = "replace";

    /// <summary>Largest length of a text filter value.</summary>
    public const int kMaxFilterTextLength = 256;

    /// <summary>Description of a device specific settings package.</summary>
    public static readonly string kPackageDescription =
        $"Device specific settings package (format '{DeviceSpecificSettingsPackage.kFormat}', version {DeviceSpecificSettingsPackage.kCurrentVersion}) " +
        $"as returned by GET /api/DeviceSpecificSettings, with at most {DeviceSpecificSettingsPackage.kMaxNodes} levels.";

    private static readonly List<RequestKeyDefinition> kImportRootKeys =
    [
        new RequestKeyDefinition("package", $"Required. {kPackageDescription}"),
        new RequestKeyDefinition("options", "Optional import options. Defaults to {}.")
    ];

    private static readonly List<RequestKeyDefinition> kImportOptionsKeys =
    [
        new RequestKeyDefinition("mode", $"Optional. '{kModeMerge}' only sets the values of the package; '{kModeReplace}' makes the package the complete state of the levels it names and removes their other overrides. Levels not named in the package are never changed. Defaults to '{kModeMerge}'; null means the default."),
        new RequestKeyDefinition("dryRun", "Optional. True only validates the package and returns the changes it would make; nothing is written. Defaults to false; null means the default.")
    ];

    private static readonly List<RequestKeyDefinition> kAddSnapshotRootKeys =
    [
        new RequestKeyDefinition("name", $"Required, unique name of the snapshot, at most {DeviceSpecificSettingsSnapshotSummary.kMaxNameLength} characters."),
        new RequestKeyDefinition("description", $"Optional description of at most {DeviceSpecificSettingsSnapshotSummary.kMaxDescriptionLength} characters. Defaults to null."),
        new RequestKeyDefinition("package", $"Optional. {kPackageDescription} Only its structure is checked, so packages of other installations can be stored. Defaults to null, which stores the current settings of this installation."),
        new RequestKeyDefinition("options", "Optional options; no options are defined yet. Defaults to {}.")
    ];

    private static readonly List<RequestKeyDefinition> kNoOptionsKeys = [];

    private static readonly List<RequestKeyDefinition> kListSnapshotsRootKeys =
    [
        new RequestKeyDefinition("options", "Optional output options. Defaults to {}.")
    ];

    private static readonly List<RequestKeyDefinition> kListSnapshotsOptionsKeys =
    [
        new RequestKeyDefinition("filter", "Optional response filter. Omitted or null applies no restriction."),
        new RequestKeyDefinition("limit", $"Optional page size. {ListPaging.kLimitDescription}"),
        new RequestKeyDefinition("offset", $"Optional. {ListPaging.kOffsetDescription}")
    ];

    private static readonly List<RequestKeyDefinition> kListSnapshotsFilterKeys =
    [
        new RequestKeyDefinition("id", "Optional exact snapshot id. Null applies no restriction."),
        new RequestKeyDefinition("name", $"Optional case-insensitive part of the name, at most {kMaxFilterTextLength} characters. Null applies no restriction."),
        new RequestKeyDefinition("description", $"Optional case-insensitive part of the description, at most {kMaxFilterTextLength} characters. Null applies no restriction."),
        new RequestKeyDefinition("fwo_version", $"Optional exact FWO version, at most {kMaxFilterTextLength} characters. Null applies no restriction."),
        new RequestKeyDefinition("created", "Optional exact creation time. Null applies no restriction.")
    ];

    /// <summary>Root keys of the import request.</summary>
    public static IReadOnlyList<RequestKeyDefinition> ImportRootKeys => kImportRootKeys;

    /// <summary>Keys of the import options.</summary>
    public static IReadOnlyList<RequestKeyDefinition> ImportOptionsKeys => kImportOptionsKeys;

    /// <summary>Root keys of the add snapshot request.</summary>
    public static IReadOnlyList<RequestKeyDefinition> AddSnapshotRootKeys => kAddSnapshotRootKeys;

    /// <summary>Keys of an options object without options.</summary>
    public static IReadOnlyList<RequestKeyDefinition> NoOptionsKeys => kNoOptionsKeys;

    /// <summary>Root keys of the list snapshots request.</summary>
    public static IReadOnlyList<RequestKeyDefinition> ListSnapshotsRootKeys => kListSnapshotsRootKeys;

    /// <summary>Keys of the list snapshots options.</summary>
    public static IReadOnlyList<RequestKeyDefinition> ListSnapshotsOptionsKeys => kListSnapshotsOptionsKeys;

    /// <summary>Keys of the list snapshots filter.</summary>
    public static IReadOnlyList<RequestKeyDefinition> ListSnapshotsFilterKeys => kListSnapshotsFilterKeys;
}

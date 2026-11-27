using FWO.Data.Workflow;
using FWO.Middleware.Server.Services;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Holds the single authoritative description of every key of the workflow configuration requests. Validation help text
/// is built from these definitions, so API documentation and validation messages cannot contradict one another.
/// </summary>
public static class WorkflowConfigurationValidationSchema
{
    /// <summary>JSON path of the root request object.</summary>
    public const string kRootPath = "";

    /// <summary>JSON path of the options object.</summary>
    public const string kOptionsPath = "options";

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

    /// <summary>JSON path of the exported configuration id.</summary>
    public const string kConfigurationIdPath = "configurationId";

    /// <summary>JSON path of the phase selection of the export.</summary>
    public const string kPhasesPath = "options.phases";

    /// <summary>JSON path of the package key.</summary>
    public const string kPackagePath = "package";

    /// <summary>JSON path of the configuration name inside the package.</summary>
    public const string kPackageConfigurationNamePath = "package.configuration.name";

    /// <summary>JSON path of the name of the new configuration.</summary>
    public const string kNamePath = "options.name";

    /// <summary>JSON path of the configuration the package is merged into.</summary>
    public const string kTargetConfigurationIdPath = "options.targetConfigurationId";

    /// <summary>Largest length of a text filter value and of a task type or phase name in the phase selection.</summary>
    public const int kMaxTextLength = 256;

    /// <summary>Largest length of the name of an imported configuration.</summary>
    public const int kMaxNameLength = 256;

    /// <summary>Largest number of phases in the phase selection of an export.</summary>
    public const int kMaxPhaseSelections = 100;

    /// <summary>
    /// Largest number of entries of an exported package: phases, states, transition groups, their transitions and
    /// visibility groups together.
    /// </summary>
    public const int kMaxExportEntries = 10000;

    /// <summary>Description of a workflow configuration package.</summary>
    public static readonly string kPackageDescription =
        $"Workflow configuration package (format '{WorkflowConfigurationTransferPackage.kFormat}', version {WorkflowConfigurationTransferPackage.kCurrentVersion}) " +
        "as returned by POST /api/WorkflowConfiguration/Export or downloaded on the workflow settings page.";

    private static readonly List<RequestKeyDefinition> kListRootKeys =
    [
        new RequestKeyDefinition("options", "Optional output options. Defaults to {}.")
    ];

    private static readonly List<RequestKeyDefinition> kListOptionsKeys =
    [
        new RequestKeyDefinition("filter", "Optional response filter. Omitted or null applies no restriction."),
        new RequestKeyDefinition("limit", $"Optional page size. {ListPaging.kLimitDescription}"),
        new RequestKeyDefinition("offset", $"Optional. {ListPaging.kOffsetDescription}")
    ];

    private static readonly List<RequestKeyDefinition> kListFilterKeys =
    [
        new RequestKeyDefinition("id", "Optional exact configuration id. Null applies no restriction."),
        new RequestKeyDefinition("name", $"Optional case-insensitive part of the name, at most {kMaxTextLength} characters. Null applies no restriction."),
        new RequestKeyDefinition("description", $"Optional case-insensitive part of the description, at most {kMaxTextLength} characters. Null applies no restriction."),
        new RequestKeyDefinition("is_active", "Optional exact active flag; at most one configuration is active. Null applies no restriction.")
    ];

    private static readonly List<RequestKeyDefinition> kExportRootKeys =
    [
        new RequestKeyDefinition("configurationId", "Required id of the configuration to export, as listed by POST /api/WorkflowConfiguration/List."),
        new RequestKeyDefinition("options", "Optional export options. Defaults to {}.")
    ];

    private static readonly List<RequestKeyDefinition> kExportOptionsKeys =
    [
        new RequestKeyDefinition("includeVisibilityGroups", "Optional. True also exports the visibility groups of the exported transition groups with their member DNs. Defaults to false; null means the default."),
        new RequestKeyDefinition("phases", $"Optional list of at most {kMaxPhaseSelections} phases of the configuration to export, each given by 'task_type' and 'phase'. Only the transition groups of these phases are exported. Defaults to null, which exports all phases.")
    ];

    private static readonly List<RequestKeyDefinition> kPhaseSelectionKeys =
    [
        new RequestKeyDefinition("task_type", $"Required task type of the phase, e.g. 'access', at most {kMaxTextLength} characters."),
        new RequestKeyDefinition("phase", $"Required name of the phase, e.g. 'approval', at most {kMaxTextLength} characters.")
    ];

    private static readonly List<RequestKeyDefinition> kImportRootKeys =
    [
        new RequestKeyDefinition("package", $"Required. {kPackageDescription}"),
        new RequestKeyDefinition("options", "Optional import options. Defaults to {}.")
    ];

    private static readonly List<RequestKeyDefinition> kImportOptionsKeys =
    [
        new RequestKeyDefinition("name", $"Optional name of the new, inactive configuration, at most {kMaxNameLength} characters and not used by another configuration (case-insensitive). Defaults to null, which uses the configuration name of the package. Must not be combined with 'targetConfigurationId'."),
        new RequestKeyDefinition("targetConfigurationId", "Optional id of an existing configuration to merge the package into: missing phases are added, existing phases get the missing derived states and transition groups. Defaults to null, which creates a new configuration.")
    ];

    /// <summary>Root keys of the list request.</summary>
    public static IReadOnlyList<RequestKeyDefinition> ListRootKeys => kListRootKeys;

    /// <summary>Keys of the list options.</summary>
    public static IReadOnlyList<RequestKeyDefinition> ListOptionsKeys => kListOptionsKeys;

    /// <summary>Keys of the list filter.</summary>
    public static IReadOnlyList<RequestKeyDefinition> ListFilterKeys => kListFilterKeys;

    /// <summary>Root keys of the export request.</summary>
    public static IReadOnlyList<RequestKeyDefinition> ExportRootKeys => kExportRootKeys;

    /// <summary>Keys of the export options.</summary>
    public static IReadOnlyList<RequestKeyDefinition> ExportOptionsKeys => kExportOptionsKeys;

    /// <summary>Keys of one phase of the phase selection.</summary>
    public static IReadOnlyList<RequestKeyDefinition> PhaseSelectionKeys => kPhaseSelectionKeys;

    /// <summary>Root keys of the import request.</summary>
    public static IReadOnlyList<RequestKeyDefinition> ImportRootKeys => kImportRootKeys;

    /// <summary>Keys of the import options.</summary>
    public static IReadOnlyList<RequestKeyDefinition> ImportOptionsKeys => kImportOptionsKeys;

    /// <summary>JSON path of one phase of the phase selection.</summary>
    /// <param name="index">Position of the phase in the selection.</param>
    public static string PhasePath(int index) => $"{kPhasesPath}[{index}]";
}

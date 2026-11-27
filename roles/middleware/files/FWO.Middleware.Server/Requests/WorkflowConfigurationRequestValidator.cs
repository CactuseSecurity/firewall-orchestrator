using FWO.Data.Workflow;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using FWO.Services.Workflow;
using Schema = FWO.Middleware.Server.Requests.WorkflowConfigurationValidationSchema;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Validates the workflow configuration requests completely and reports every detected key-level error in one result.
/// </summary>
public static class WorkflowConfigurationRequestValidator
{
    /// <summary>Validates a list request.</summary>
    /// <param name="request">The request; null if the body was missing.</param>
    /// <param name="maxLimit">Maximum page size.</param>
    public static RequestValidationErrorResponse ValidateList(ListWorkflowConfigurationsRequest? request, int maxLimit)
    {
        RequestValidationErrorResponse result = new();
        if (request is null)
        {
            AddMissingBody(result, Schema.ListRootKeys);
            return result;
        }

        ListWorkflowConfigurationsOptions options = request.Options;
        if (!ListPaging.IsValidLimit(options.Limit, maxLimit))
        {
            Add(result, Schema.kLimitPath, ListPaging.BuildLimitError(Schema.kLimitPath, maxLimit));
        }
        if (!ListPaging.IsValidOffset(options.Offset))
        {
            Add(result, Schema.kOffsetPath, ListPaging.BuildOffsetError(Schema.kOffsetPath));
        }
        ValidateTextLength(options.Filter?.Name, Schema.kFilterNamePath, Schema.kMaxTextLength, result);
        ValidateTextLength(options.Filter?.Description, Schema.kFilterDescriptionPath, Schema.kMaxTextLength, result);

        AggregatedRequestKeyValidation.AddUnsupportedKeyErrors(request.AdditionalData, Schema.kRootPath, Schema.ListRootKeys, result);
        AggregatedRequestKeyValidation.AddUnsupportedKeyErrors(options.AdditionalData, Schema.kOptionsPath, Schema.ListOptionsKeys, result);
        AggregatedRequestKeyValidation.AddUnsupportedKeyErrors(options.Filter?.AdditionalData, Schema.kFilterPath, Schema.ListFilterKeys, result);
        return result;
    }

    /// <summary>Validates an export request; whether the configuration and its phases exist is checked by the export.</summary>
    /// <param name="request">The request; null if the body was missing.</param>
    public static RequestValidationErrorResponse ValidateExport(ExportWorkflowConfigurationRequest? request)
    {
        RequestValidationErrorResponse result = new();
        if (request is null)
        {
            AddMissingBody(result, Schema.ExportRootKeys);
            return result;
        }

        if (request.ConfigurationId is null or < 1)
        {
            Add(result, Schema.kConfigurationIdPath, $"'{Schema.kConfigurationIdPath}' is required and must be a positive id. {Describe(Schema.ExportRootKeys, "configurationId")}");
        }
        ValidatePhaseSelection(request.Options.Phases, result);

        AggregatedRequestKeyValidation.AddUnsupportedKeyErrors(request.AdditionalData, Schema.kRootPath, Schema.ExportRootKeys, result);
        AggregatedRequestKeyValidation.AddUnsupportedKeyErrors(request.Options.AdditionalData, Schema.kOptionsPath, Schema.ExportOptionsKeys, result);
        return result;
    }

    /// <summary>
    /// Validates an import request including the structure of the package; whether the package fits this installation
    /// is checked by the import.
    /// </summary>
    /// <param name="request">The request; null if the body was missing.</param>
    public static RequestValidationErrorResponse ValidateImport(ImportWorkflowConfigurationRequest? request)
    {
        RequestValidationErrorResponse result = new();
        if (request is null)
        {
            AddMissingBody(result, Schema.ImportRootKeys);
            return result;
        }

        ImportWorkflowConfigurationOptions options = request.Options;
        ValidatePackage(request.Package, result);
        if (options.TargetConfigurationId is not null)
        {
            if (options.TargetConfigurationId < 1)
            {
                Add(result, Schema.kTargetConfigurationIdPath, $"'{Schema.kTargetConfigurationIdPath}' must be a positive id when supplied. {Describe(Schema.ImportOptionsKeys, "targetConfigurationId")}");
            }
            if (options.Name is not null)
            {
                Add(result, Schema.kNamePath, $"'{Schema.kNamePath}' must not be combined with '{Schema.kTargetConfigurationIdPath}'. {Describe(Schema.ImportOptionsKeys, "name")}");
            }
        }
        else if (options.Name is not null || request.Package?.Configuration is not null)
        {
            ValidateNewName(options.Name, request.Package?.Configuration?.Name, result);
        }

        AggregatedRequestKeyValidation.AddUnsupportedKeyErrors(request.AdditionalData, Schema.kRootPath, Schema.ImportRootKeys, result);
        AggregatedRequestKeyValidation.AddUnsupportedKeyErrors(options.AdditionalData, Schema.kOptionsPath, Schema.ImportOptionsKeys, result);
        return result;
    }

    /// <summary>The name of the configuration an import creates: the given name or else the package's, trimmed.</summary>
    /// <param name="request">A request that passed <see cref="ValidateImport"/> without a target configuration.</param>
    public static string NewConfigurationName(ImportWorkflowConfigurationRequest request) =>
        (request.Options.Name ?? request.Package!.Configuration.Name).Trim();

    /// <summary>
    /// Finds the phases of a selection that the configuration does not have and reports them at their request path.
    /// </summary>
    /// <param name="selection">The validated phase selection.</param>
    /// <param name="available">The phases of the configuration.</param>
    public static RequestValidationErrorResponse ValidateSelectedPhasesExist(List<WorkflowPhaseSelectionRequest?> selection,
        List<WorkflowConfigurationPhase> available)
    {
        RequestValidationErrorResponse result = new();
        HashSet<string> availableKeys = available.Select(phase => PhaseKey(phase.TaskType, phase.Phase)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        string availableText = string.Join(", ", available.Select(phase => $"{phase.TaskType}/{phase.Phase}"));
        for (int index = 0; index < selection.Count; index++)
        {
            WorkflowPhaseSelectionRequest phase = selection[index]!;
            if (!availableKeys.Contains(PhaseKey(phase.TaskType!, phase.Phase!)))
            {
                Add(result, Schema.PhasePath(index), $"The configuration has no phase '{phase.TaskType}/{phase.Phase}'. Available phases: {availableText}.");
            }
        }
        return result;
    }

    /// <summary>The phase keys of a validated selection, in the form the transfer service expects.</summary>
    /// <param name="selection">The validated phase selection; null selects all phases.</param>
    public static HashSet<string>? SelectedPhaseKeys(List<WorkflowPhaseSelectionRequest?>? selection) =>
        selection?.Select(phase => PhaseKey(phase!.TaskType!, phase.Phase!)).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static void ValidatePhaseSelection(List<WorkflowPhaseSelectionRequest?>? phases, RequestValidationErrorResponse result)
    {
        if (phases is null)
        {
            return;
        }
        if (phases.Count == 0 || phases.Count > Schema.kMaxPhaseSelections)
        {
            Add(result, Schema.kPhasesPath, $"'{Schema.kPhasesPath}' must hold between 1 and {Schema.kMaxPhaseSelections} phases when supplied. {Describe(Schema.ExportOptionsKeys, "phases")}");
            return;
        }
        for (int index = 0; index < phases.Count; index++)
        {
            ValidatePhase(phases[index], Schema.PhasePath(index), result);
        }
    }

    private static void ValidatePhase(WorkflowPhaseSelectionRequest? phase, string path, RequestValidationErrorResponse result)
    {
        if (phase is null)
        {
            Add(result, path, $"'{path}' must be an object with the keys 'task_type' and 'phase'.");
            return;
        }
        ValidateRequiredText(phase.TaskType, $"{path}.task_type", "task_type", result);
        ValidateRequiredText(phase.Phase, $"{path}.phase", "phase", result);
        AggregatedRequestKeyValidation.AddUnsupportedKeyErrors(phase.AdditionalData, path, Schema.PhaseSelectionKeys, result);
    }

    private static void ValidateRequiredText(string? value, string path, string key, RequestValidationErrorResponse result)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > Schema.kMaxTextLength)
        {
            Add(result, path, $"'{path}' is required and must not be longer than {Schema.kMaxTextLength} characters. {Describe(Schema.PhaseSelectionKeys, key)}");
        }
    }

    private static void ValidatePackage(WorkflowConfigurationTransferPackage? package, RequestValidationErrorResponse result)
    {
        if (package is null)
        {
            Add(result, Schema.kPackagePath, $"'{Schema.kPackagePath}' is required. {Schema.kPackageDescription}");
            return;
        }
        try
        {
            WorkflowConfigurationTransferService.ValidateStructure(package);
        }
        catch (InvalidDataException exception)
        {
            Add(result, Schema.kPackagePath, $"{exception.Message} {Schema.kPackageDescription}");
        }
    }

    private static void ValidateNewName(string? name, string? packageName, RequestValidationErrorResponse result)
    {
        string path = name is null ? Schema.kPackageConfigurationNamePath : Schema.kNamePath;
        string? effectiveName = name ?? packageName;
        if (string.IsNullOrWhiteSpace(effectiveName) || effectiveName.Trim().Length > Schema.kMaxNameLength)
        {
            Add(result, path, $"The name of the new configuration must not be empty or longer than {Schema.kMaxNameLength} characters. {Describe(Schema.ImportOptionsKeys, "name")}");
        }
    }

    private static void ValidateTextLength(string? value, string path, int maxLength, RequestValidationErrorResponse result)
    {
        if (value?.Length > maxLength)
        {
            Add(result, path, $"'{path}' must not be longer than {maxLength} characters.");
        }
    }

    private static string PhaseKey(string taskType, string phase) => $"{taskType.Trim()}:{phase.Trim()}";

    private static string Describe(IReadOnlyList<RequestKeyDefinition> keys, string jsonName) =>
        keys.Single(key => key.JsonName == jsonName).Description;

    private static void AddMissingBody(RequestValidationErrorResponse result, IReadOnlyList<RequestKeyDefinition> rootKeys)
    {
        Add(result, Schema.kRootPath, $"A request body is required. Valid root keys: {AggregatedRequestKeyValidation.DescribeKeys(rootKeys)}");
    }

    private static void Add(RequestValidationErrorResponse result, string path, string message)
    {
        result.Errors.Add(AggregatedRequestKeyValidation.BuildError(path, message));
    }
}

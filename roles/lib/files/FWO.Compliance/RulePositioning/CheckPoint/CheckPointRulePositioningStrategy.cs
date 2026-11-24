using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.RulePositioning;
using FWO.Data.RulePositioning.CheckPoint;
using FWO.Services.RulePositioning;
using FWO.Services.RulePositioning.Blocks;
using FWO.Services.RulePositioning.CheckPoint;
using CheckPointLayerGuardReadResult = FWO.Data.RulePositioning.PositioningResult<
    FWO.Data.RulePositioning.CheckPoint.CheckPointLayerGuardSnapshot,
    FWO.Services.RulePositioning.CheckPoint.CheckPointLayerGuardReadFailureCategory>;
using RulebaseGraphReadResult = FWO.Data.RulePositioning.PositioningResult<
    FWO.Services.RulePositioning.RulebaseGraphReadValue,
    FWO.Services.RulePositioning.RulebaseGraphReadFailureCategory>;
using CreationRuleNetworkResolutionResult = FWO.Data.RulePositioning.PositioningResult<
    FWO.Data.RulePositioning.CreationRuleNetworkInput,
    FWO.Compliance.RulePositioning.CreationRuleNetworkResolutionFailureCategory>;
using CreationRuleZoneResolutionResult = FWO.Data.RulePositioning.PositioningResult<
    FWO.Compliance.RulePositioning.CreationRuleZoneInput,
    FWO.Compliance.RulePositioning.CreationRuleZoneResolutionFailureCategory>;
using DesignatedZoneMatrixReadResult = FWO.Data.RulePositioning.PositioningResult<
    FWO.Compliance.RulePositioning.DesignatedZoneMatrixReadValue,
    FWO.Compliance.RulePositioning.DesignatedZoneMatrixReadFailureCategory>;
using CheckPointRecipeSelectionResult = FWO.Data.RulePositioning.PositioningResult<
    FWO.Services.RulePositioning.Blocks.PositioningRecipe<FWO.Data.RulePositioning.CheckPoint.CheckPointPositioningContext,
        FWO.Services.RulePositioning.CheckPoint.CheckPointBlockValue>,
    FWO.Compliance.RulePositioning.CheckPoint.CheckPointRecipeSelectionFailureCategory>;

namespace FWO.Compliance.RulePositioning.CheckPoint;

/// <summary>Orchestrates the ticket-aware Check Point layer-guard positioning calculation.</summary>
public sealed class CheckPointRulePositioningStrategy : IRulePositioningStrategy
{
    private readonly IRulebaseGraphSnapshotReader graphReader;
    private readonly ICreationRuleNetworkInputResolver networkResolver;
    private readonly ICheckPointLayerGuardSnapshotReader guardReader;
    private readonly IDesignatedZoneMatrixReader zoneMatrixReader;
    private readonly ICreationRuleZoneResolver zoneResolver;
    private readonly ICheckPointPositioningRecipeCatalog recipeCatalog;
    /// <summary>Executes the selected Check Point positioning recipe.</summary>
    private readonly PositioningRecipeExecutor<CheckPointPositioningContext, CheckPointBlockValue> recipeExecutor = new();

    /// <summary>Creates the Check Point positioning orchestration strategy.</summary>
    public CheckPointRulePositioningStrategy(
        IRulebaseGraphSnapshotReader graphReader,
        ICreationRuleNetworkInputResolver networkResolver,
        ICheckPointLayerGuardSnapshotReader guardReader,
        IDesignatedZoneMatrixReader zoneMatrixReader,
        ICreationRuleZoneResolver zoneResolver,
        ICheckPointPositioningRecipeCatalog recipeCatalog)
    {
        this.graphReader = graphReader ?? throw new ArgumentNullException(nameof(graphReader));
        this.networkResolver = networkResolver ?? throw new ArgumentNullException(nameof(networkResolver));
        this.guardReader = guardReader ?? throw new ArgumentNullException(nameof(guardReader));
        this.zoneMatrixReader = zoneMatrixReader ?? throw new ArgumentNullException(nameof(zoneMatrixReader));
        this.zoneResolver = zoneResolver ?? throw new ArgumentNullException(nameof(zoneResolver));
        this.recipeCatalog = recipeCatalog ?? throw new ArgumentNullException(nameof(recipeCatalog));
    }

    /// <inheritdoc />
    public bool CanHandle(DeviceType deviceType, ProvisioningPositioningAlgorithm positioningAlgorithm)
    {
        return deviceType != null
            && deviceType.Id == DeviceTypeIds.kCheckPointR8x
            && IsUsableAlgorithm(positioningAlgorithm)
            && recipeCatalog.SupportsAlgorithm(positioningAlgorithm);
    }

    /// <inheritdoc />
    public async Task<RulePositioningResult> CalculateAsync(
        RulePositioningContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            return await CalculateCoreAsync(context, cancellationToken);
        }
        catch (OperationCanceledException exception)
            when (cancellationToken.IsCancellationRequested
                || cancellationToken.CanBeCanceled && exception.CancellationToken == cancellationToken)
        {
            throw;
        }
        catch (Exception)
        {
            return Failure(RulePositioningFailureCategory.CalculationFailed);
        }
    }

    /// <summary>Runs the validated customer orchestration without exception translation.</summary>
    private async Task<RulePositioningResult> CalculateCoreAsync(
        RulePositioningContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RulePositioningFailureCategory? validationFailure =
            RulePositioningContextValidator.ValidateTicketAwareContext(context);
        if (validationFailure.HasValue)
        {
            return Failure(validationFailure.Value);
        }

        ProvisioningPositioningAlgorithm algorithm = context.EffectiveSettings.PositioningAlgorithm;
        if (!CanHandle(context.TargetGateway.DeviceType, algorithm))
        {
            return Failure(RulePositioningFailureCategory.PositioningMethodNotImplemented);
        }

        PositioningResult<CheckPointSectionNameTemplate, CheckPointSectionNameTemplateFailureCategory> templateResult =
            CheckPointSectionNameTemplate.Parse(context.EffectiveSettings.CheckPointLayerGuardSectionNameTemplate);
        if (!templateResult.TryGetValue(out CheckPointSectionNameTemplate? sectionNameTemplate))
        {
            templateResult.TryGetFailure(out CheckPointSectionNameTemplateFailureCategory templateFailure);
            return CheckPointRulePositioningFailureMapper.KnownFailure(
                "section-name template",
                RulePositioningFailureCategory.ConfigurationInvalid,
                templateFailure,
                "The Check Point layer-guard section-name template is invalid.");
        }

        InputPreparationResult preparation = await PrepareInputsAsync(
            context,
            sectionNameTemplate.RequiredVariables.Count > 0,
            cancellationToken);
        if (preparation.PreparationFailure != null)
        {
            return preparation.PreparationFailure;
        }
        PreparedInputs inputs = preparation.Inputs!;

        PositioningResult<string, CheckPointLayerGuardSectionNameFailureCategory> sectionNameResult =
            CheckPointLayerGuardSectionNameResolver.Resolve(sectionNameTemplate, inputs.ZoneInput);
        if (!sectionNameResult.TryGetValue(out string? normalizedSectionName))
        {
            sectionNameResult.TryGetFailure(out CheckPointLayerGuardSectionNameFailureCategory sectionNameFailure);
            return CheckPointRulePositioningFailureMapper.KnownFailure(
                "section-name resolution",
                RulePositioningFailureCategory.ConfigurationInvalid,
                sectionNameFailure,
                "The Check Point layer-guard section name could not be resolved.");
        }

        CheckPointLayerGuardZonePairPositioningInput positioningInput = new(
            inputs.NetworkInput,
            inputs.GuardSnapshot);
        CheckPointRecipeSelectionResult selectionResult = recipeCatalog.SelectRecipe(
            algorithm,
            positioningInput,
            normalizedSectionName);
        if (!selectionResult.TryGetValue(out PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue>? recipe))
        {
            return MapSelectionFailure(selectionResult);
        }

        cancellationToken.ThrowIfCancellationRequested();
        CheckPointPositioningContext checkPointContext = new(
            context,
            inputs.Snapshot,
            positioningInput);
        PositioningBlockResult<CheckPointBlockValue> executionResult = await recipeExecutor.ExecuteAsync(
            recipe,
            checkPointContext,
            cancellationToken);
        if (executionResult is not PositioningBlockSuccess<CheckPointBlockValue> executionSuccess)
        {
            return CheckPointRulePositioningFailureMapper.MapExecutionFailure(
                "recipe execution",
                executionResult);
        }
        if (executionSuccess.Value is not CheckPointPositionValue positionValue
            || positionValue.Position.PositioningAlgorithm != algorithm)
        {
            return CheckPointRulePositioningFailureMapper.KnownFailure(
                "recipe execution result",
                RulePositioningFailureCategory.CalculationFailed,
                CheckPointStrategyFailureCategory.UnexpectedPositionResult,
                "The positioning recipe returned an unexpected result.");
        }

        return new RulePositioningSuccess(positionValue.Position);
    }

    /// <summary>Loads and validates the graph, effective networks, guards, matrix, and zones.</summary>
    private async Task<InputPreparationResult> PrepareInputsAsync(
        RulePositioningContext context,
        bool requiresZones,
        CancellationToken cancellationToken)
    {
        RulebaseGraphReadResult graphResult = await graphReader.ReadAsync(
            context.TargetManagement.Id,
            context.TargetGateway.Id,
            cancellationToken);
        if (!graphResult.TryGetValue(out RulebaseGraphReadValue? graphValue))
        {
            return InputPreparationResult.Failed(MapGraphFailure(graphResult));
        }

        cancellationToken.ThrowIfCancellationRequested();
        CreationRuleNetworkResolutionResult networkResult = await networkResolver.ResolveAsync(
            context.ParentRequestTask,
            context.TicketRequestTasks!,
            cancellationToken);
        if (!networkResult.TryGetValue(out CreationRuleNetworkInput? networkInput))
        {
            return InputPreparationResult.Failed(MapNetworkFailure(networkResult));
        }

        cancellationToken.ThrowIfCancellationRequested();
        CheckPointLayerGuardReadResult guardResult = await guardReader.ReadAsync(
            graphValue.Snapshot,
            graphValue.ImportId,
            cancellationToken);
        if (!guardResult.TryGetValue(out CheckPointLayerGuardSnapshot? guardSnapshot))
        {
            return InputPreparationResult.Failed(MapGuardFailure(guardResult));
        }

        CreationRuleZoneInput? zoneInput = null;
        if (requiresZones)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RulePositioningConfiguration configuration = context.Configuration!;
            DesignatedZoneMatrixReadResult matrixResult = await zoneMatrixReader.ReadAsync(
                configuration.DesignatedZoneMatrixId,
                cancellationToken);
            if (!matrixResult.TryGetValue(out DesignatedZoneMatrixReadValue? matrixValue))
            {
                return InputPreparationResult.Failed(MapMatrixFailure(matrixResult));
            }

            cancellationToken.ThrowIfCancellationRequested();
            CreationRuleZoneResolutionResult zoneResult = await zoneResolver.ResolveAsync(
                networkInput,
                matrixValue.NetworkZones,
                configuration,
                cancellationToken);
            if (!zoneResult.TryGetValue(out zoneInput))
            {
                return InputPreparationResult.Failed(MapZoneFailure(zoneResult));
            }
        }

        return InputPreparationResult.Succeeded(new PreparedInputs(
            graphValue.Snapshot,
            networkInput,
            guardSnapshot,
            zoneInput));
    }

    /// <summary>Validated data required to select and execute one positioning recipe.</summary>
    private sealed record PreparedInputs(
        RulebaseGraphSnapshot Snapshot,
        CreationRuleNetworkInput NetworkInput,
        CheckPointLayerGuardSnapshot GuardSnapshot,
        CreationRuleZoneInput? ZoneInput);

    /// <summary>Result of preparing positioning inputs without throwing domain failures.</summary>
    private sealed record InputPreparationResult(PreparedInputs? Inputs, RulePositioningFailure? PreparationFailure)
    {
        /// <summary>Creates a successful preparation result.</summary>
        internal static InputPreparationResult Succeeded(PreparedInputs inputs) => new(inputs, null);

        /// <summary>Creates a failed preparation result.</summary>
        internal static InputPreparationResult Failed(RulePositioningFailure failure) => new(null, failure);
    }

    /// <summary>Maps effective-network failures to the public positioning contract.</summary>
    private static RulePositioningFailure MapNetworkFailure(CreationRuleNetworkResolutionResult result)
    {
        CreationRuleNetworkResolutionFailureCategory failureCategory = result.FailureCategory!.Value;
        RulePositioningFailureCategory category = failureCategory switch
        {
            CreationRuleNetworkResolutionFailureCategory.UnsupportedTask =>
                RulePositioningFailureCategory.UnsupportedTask,
            CreationRuleNetworkResolutionFailureCategory.GroupQueryFailed or
                CreationRuleNetworkResolutionFailureCategory.UnexpectedFailure =>
                RulePositioningFailureCategory.CalculationFailed,
            _ => RulePositioningFailureCategory.InvalidInput
        };
        return CheckPointRulePositioningFailureMapper.KnownFailure(
            "creation-rule networks",
            category,
            failureCategory,
            failureCategory.GetDiagnostic());
    }

    /// <summary>Maps fixed-import layer-guard failures to the public positioning contract.</summary>
    private static RulePositioningFailure MapGuardFailure(CheckPointLayerGuardReadResult result)
    {
        CheckPointLayerGuardReadFailureCategory failureCategory = result.FailureCategory!.Value;
        RulePositioningFailureCategory category = failureCategory is CheckPointLayerGuardReadFailureCategory.InvalidInput
                or CheckPointLayerGuardReadFailureCategory.ScopeMismatch
                ? RulePositioningFailureCategory.InvalidInput
                : RulePositioningFailureCategory.CalculationFailed;
        return CheckPointRulePositioningFailureMapper.KnownFailure(
            "layer guards",
            category,
            failureCategory,
            failureCategory.GetDiagnostic());
    }

    /// <summary>Checks whether an algorithm is defined and non-default.</summary>
    private static bool IsUsableAlgorithm(ProvisioningPositioningAlgorithm algorithm)
    {
        return algorithm != ProvisioningPositioningAlgorithm.Undefined && Enum.IsDefined(algorithm);
    }

    /// <summary>Maps graph-reader failures to the Phase 1 contract.</summary>
    private static RulePositioningFailure MapGraphFailure(RulebaseGraphReadResult result)
    {
        RulebaseGraphReadFailureCategory failureCategory = result.FailureCategory!.Value;
        RulePositioningFailureCategory category = failureCategory is
            RulebaseGraphReadFailureCategory.InvalidInput or RulebaseGraphReadFailureCategory.ScopeMismatch
                ? RulePositioningFailureCategory.InvalidInput
                : RulePositioningFailureCategory.CalculationFailed;
        return CheckPointRulePositioningFailureMapper.KnownFailure(
            "rulebase graph",
            category,
            failureCategory,
            failureCategory.GetDiagnostic());
    }

    /// <summary>Maps designated-matrix-reader failures to the Phase 1 contract.</summary>
    private static RulePositioningFailure MapMatrixFailure(DesignatedZoneMatrixReadResult result)
    {
        DesignatedZoneMatrixReadFailureCategory failureCategory = result.FailureCategory!.Value;
        RulePositioningFailureCategory category = failureCategory is
            DesignatedZoneMatrixReadFailureCategory.ConfigurationInvalid
                or DesignatedZoneMatrixReadFailureCategory.MatrixUnavailable
                ? RulePositioningFailureCategory.ConfigurationInvalid
                : RulePositioningFailureCategory.CalculationFailed;
        return CheckPointRulePositioningFailureMapper.KnownFailure(
            "designated zone matrix",
            category,
            failureCategory,
            failureCategory.GetDiagnostic());
    }

    /// <summary>Maps zone-resolution failures to the Phase 1 contract.</summary>
    private static RulePositioningFailure MapZoneFailure(CreationRuleZoneResolutionResult result)
    {
        CreationRuleZoneResolutionFailureCategory failureCategory = result.FailureCategory!.Value;
        RulePositioningFailureCategory category = failureCategory switch
        {
            CreationRuleZoneResolutionFailureCategory.InvalidInput
                or CreationRuleZoneResolutionFailureCategory.UnsupportedObject
                or CreationRuleZoneResolutionFailureCategory.InvalidAddress => RulePositioningFailureCategory.InvalidInput,
            CreationRuleZoneResolutionFailureCategory.UnsupportedTask => RulePositioningFailureCategory.UnsupportedTask,
            CreationRuleZoneResolutionFailureCategory.InvalidZoneData => RulePositioningFailureCategory.ConfigurationInvalid,
            _ => RulePositioningFailureCategory.CalculationFailed
        };
        return CheckPointRulePositioningFailureMapper.KnownFailure(
            "creation-rule zones",
            category,
            failureCategory,
            failureCategory.GetDiagnostic());
    }

    /// <summary>Maps recipe-selection failures to the Phase 1 contract.</summary>
    private static RulePositioningFailure MapSelectionFailure(CheckPointRecipeSelectionResult result)
    {
        CheckPointRecipeSelectionFailureCategory failureCategory = result.FailureCategory!.Value;
        RulePositioningFailureCategory category = failureCategory == CheckPointRecipeSelectionFailureCategory.NoMatchingRecipe
            ? RulePositioningFailureCategory.PositioningMethodNotImplemented
            : RulePositioningFailureCategory.ConfigurationInvalid;
        return CheckPointRulePositioningFailureMapper.KnownFailure(
            "recipe selection",
            category,
            failureCategory,
            failureCategory.GetDiagnostic());
    }

    /// <summary>Creates a Phase 1 failure with its fixed safe diagnostic.</summary>
    private static RulePositioningFailure Failure(RulePositioningFailureCategory category)
    {
        return new RulePositioningFailure(category);
    }

}

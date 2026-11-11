using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.RulePositioning;
using FWO.Data.RulePositioning.CheckPoint;
using FWO.Services.RulePositioning;
using FWO.Services.RulePositioning.Blocks;
using FWO.Services.RulePositioning.CheckPoint;
using System.Diagnostics;
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
    private readonly ICheckPointLayerGuardHappyPathFinder happyPathFinder;
    /// <summary>Executes the selected Check Point positioning recipe.</summary>
    private readonly PositioningRecipeExecutor<CheckPointPositioningContext, CheckPointBlockValue> recipeExecutor = new();

    /// <summary>Creates the Check Point positioning orchestration strategy.</summary>
    public CheckPointRulePositioningStrategy(
        IRulebaseGraphSnapshotReader graphReader,
        ICreationRuleNetworkInputResolver networkResolver,
        ICheckPointLayerGuardSnapshotReader guardReader,
        IDesignatedZoneMatrixReader zoneMatrixReader,
        ICreationRuleZoneResolver zoneResolver,
        ICheckPointPositioningRecipeCatalog recipeCatalog,
        ICheckPointLayerGuardHappyPathFinder happyPathFinder)
    {
        this.graphReader = graphReader ?? throw new ArgumentNullException(nameof(graphReader));
        this.networkResolver = networkResolver ?? throw new ArgumentNullException(nameof(networkResolver));
        this.guardReader = guardReader ?? throw new ArgumentNullException(nameof(guardReader));
        this.zoneMatrixReader = zoneMatrixReader ?? throw new ArgumentNullException(nameof(zoneMatrixReader));
        this.zoneResolver = zoneResolver ?? throw new ArgumentNullException(nameof(zoneResolver));
        this.recipeCatalog = recipeCatalog ?? throw new ArgumentNullException(nameof(recipeCatalog));
        this.happyPathFinder = happyPathFinder ?? throw new ArgumentNullException(nameof(happyPathFinder));
    }

    /// <inheritdoc />
    public bool CanHandle(DeviceType deviceType, ProvisioningPositioningAlgorithm positioningAlgorithm)
    {
        bool supported = deviceType != null
            && deviceType.Id == DeviceTypeIds.kCheckPointR8x
            && IsUsableAlgorithm(positioningAlgorithm)
            && recipeCatalog.SupportsAlgorithm(positioningAlgorithm);
        RulePositioningDiagnosticLog.WriteInfo("recipe.capability", "summary",
            new Dictionary<string, object?>
            {
                ["strategy"] = nameof(CheckPointRulePositioningStrategy),
                ["device_type_id"] = deviceType?.Id,
                ["algorithm"] = positioningAlgorithm.ToString(),
                ["supported"] = supported
            });
        return supported;
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
        catch (Exception exception)
        {
            RulePositioningDiagnosticLog.WriteException("strategy.exception", exception, new Dictionary<string, object?>
            {
                ["strategy"] = nameof(CheckPointRulePositioningStrategy),
                ["public_category"] = RulePositioningFailureCategory.CalculationFailed.ToString()
            });
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
            return LoggedMapping("validation", validationFailure.Value.ToString(), validationFailure.Value, null, 0);
        }

        ProvisioningPositioningAlgorithm algorithm = context.EffectiveSettings.PositioningAlgorithm;
        if (!CanHandle(context.TargetGateway.DeviceType, algorithm))
        {
            return LoggedMapping("capability", "unsupported", RulePositioningFailureCategory.PositioningMethodNotImplemented,
                algorithm, 0);
        }

        string? templateText = context.EffectiveSettings.CheckPointLayerGuardSectionNameTemplate;
        RulePositioningDiagnosticFingerprint templateFingerprint = RulePositioningDiagnosticLog.Fingerprint(templateText);
        Stopwatch operation = Stopwatch.StartNew();
        PositioningResult<CheckPointSectionNameTemplate, CheckPointSectionNameTemplateFailureCategory> templateResult =
            CheckPointSectionNameTemplate.Parse(templateText);
        if (!templateResult.TryGetValue(out CheckPointSectionNameTemplate? sectionNameTemplate))
        {
            templateResult.TryGetFailure(out CheckPointSectionNameTemplateFailureCategory parseCategory);
            RulePositioningDiagnosticLog.WriteFailure("section-template.parse", new Dictionary<string, object?>
            {
                ["template_length"] = templateFingerprint.Length,
                ["template_fingerprint"] = templateFingerprint.Sha256Prefix,
                ["parse_category"] = parseCategory.ToString()
            });
            return LoggedMapping("template.parse", parseCategory.ToString(),
                RulePositioningFailureCategory.ConfigurationInvalid, algorithm, operation.ElapsedMilliseconds);
        }
        RulePositioningDiagnosticLog.WriteInfo("section-template.parse", "success", new Dictionary<string, object?>
        {
            ["template_length"] = templateFingerprint.Length,
            ["template_fingerprint"] = templateFingerprint.Sha256Prefix,
            ["required_variables"] = sectionNameTemplate.RequiredVariables.OrderBy(variable => variable)
                .Select(variable => variable.ToString()).ToArray()
        });

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
            sectionNameResult.TryGetFailure(out CheckPointLayerGuardSectionNameFailureCategory sectionCategory);
            return LoggedMapping("template.render", sectionCategory.ToString(),
                RulePositioningFailureCategory.ConfigurationInvalid, algorithm, 0);
        }

        CheckPointLayerGuardZonePairPositioningInput positioningInput = new(
            inputs.NetworkInput,
            inputs.GuardSnapshot);
        operation.Restart();
        CheckPointRecipeSelectionResult selectionResult = recipeCatalog.SelectRecipe(
            algorithm,
            positioningInput,
            normalizedSectionName);
        if (!selectionResult.TryGetValue(out PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue>? recipe))
        {
            return MapSelectionFailure(selectionResult, algorithm, operation.ElapsedMilliseconds);
        }

        cancellationToken.ThrowIfCancellationRequested();
        CheckPointPositioningContext checkPointContext = new(
            context,
            inputs.Snapshot,
            positioningInput);
        operation.Restart();
        PositioningBlockResult<CheckPointBlockValue> executionResult = await recipeExecutor.ExecuteAsync(
            recipe,
            checkPointContext,
            cancellationToken);
        if (executionResult is not PositioningBlockSuccess<CheckPointBlockValue> executionSuccess)
        {
            long executionDuration = operation.ElapsedMilliseconds;
            await SearchHappyPathAfterTargetFailureAsync(
                executionResult, context, inputs, sectionNameTemplate, cancellationToken);
            return MapExecutionFailure(executionResult, algorithm, executionDuration);
        }
        if (executionSuccess.Value is not CheckPointPositionValue positionValue
            || positionValue.Position.PositioningAlgorithm != algorithm)
        {
            return LoggedMapping("execution.result", executionSuccess.Value.GetType().Name,
                RulePositioningFailureCategory.CalculationFailed, algorithm, 0);
        }

        WritePositionSuccess(positionValue.Position, algorithm);
        return new RulePositioningSuccess(positionValue.Position);
    }

    /// <summary>Loads and validates the graph, effective networks, guards, matrix, and zones.</summary>
    private async Task<InputPreparationResult> PrepareInputsAsync(
        RulePositioningContext context,
        bool requiresZones,
        CancellationToken cancellationToken)
    {
        Stopwatch stage = Stopwatch.StartNew();
        RulebaseGraphReadResult graphResult = await graphReader.ReadAsync(
            context.TargetManagement.Id,
            context.TargetGateway.Id,
            cancellationToken);
        if (!graphResult.TryGetValue(out RulebaseGraphReadValue? graphValue))
        {
            return InputPreparationResult.Failed(MapGraphFailure(graphResult,
                context.EffectiveSettings.PositioningAlgorithm, stage.ElapsedMilliseconds));
        }

        cancellationToken.ThrowIfCancellationRequested();
        stage.Restart();
        CreationRuleNetworkResolutionResult networkResult = await networkResolver.ResolveAsync(
            context.ParentRequestTask,
            context.TicketRequestTasks!,
            cancellationToken);
        if (!networkResult.TryGetValue(out CreationRuleNetworkInput? networkInput))
        {
            return InputPreparationResult.Failed(MapNetworkFailure(networkResult,
                context.EffectiveSettings.PositioningAlgorithm, stage.ElapsedMilliseconds));
        }

        cancellationToken.ThrowIfCancellationRequested();
        stage.Restart();
        CheckPointLayerGuardReadResult guardResult = await guardReader.ReadAsync(
            graphValue.Snapshot,
            graphValue.ImportId,
            cancellationToken);
        if (!guardResult.TryGetValue(out CheckPointLayerGuardSnapshot? guardSnapshot))
        {
            return InputPreparationResult.Failed(MapGuardFailure(guardResult,
                context.EffectiveSettings.PositioningAlgorithm, stage.ElapsedMilliseconds));
        }

        CreationRuleZoneInput? zoneInput = null;
        IReadOnlyCollection<NetworkZone>? networkZones = null;
        if (requiresZones)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RulePositioningConfiguration configuration = context.Configuration!;
            stage.Restart();
            DesignatedZoneMatrixReadResult matrixResult = await zoneMatrixReader.ReadAsync(
                configuration.DesignatedZoneMatrixId,
                cancellationToken);
            if (!matrixResult.TryGetValue(out DesignatedZoneMatrixReadValue? matrixValue))
            {
                return InputPreparationResult.Failed(MapMatrixFailure(matrixResult,
                    context.EffectiveSettings.PositioningAlgorithm, stage.ElapsedMilliseconds));
            }
            networkZones = matrixValue.NetworkZones;

            cancellationToken.ThrowIfCancellationRequested();
            stage.Restart();
            CreationRuleZoneResolutionResult zoneResult = await zoneResolver.ResolveAsync(
                networkInput,
                matrixValue.NetworkZones,
                configuration,
                cancellationToken);
            if (!zoneResult.TryGetValue(out zoneInput))
            {
                return InputPreparationResult.Failed(MapZoneFailure(zoneResult,
                    context.EffectiveSettings.PositioningAlgorithm, stage.ElapsedMilliseconds));
            }
        }

        RulePositioningDiagnosticLog.WriteInfo("strategy.inputs", "success", new Dictionary<string, object?>
        {
            ["algorithm"] = context.EffectiveSettings.PositioningAlgorithm.ToString(),
            ["import_id"] = graphValue.ImportId,
            ["graph_rulebase_count"] = graphValue.Snapshot.Rulebases.Count,
            ["graph_link_count"] = graphValue.Snapshot.Links.Count,
            ["source_range_count"] = networkInput.SourceRanges.Count,
            ["destination_range_count"] = networkInput.DestinationRanges.Count,
            ["guard_count"] = guardSnapshot.GuardsByTargetLayerId.Count,
            ["source_zone_ids"] = zoneInput?.SourceZones.Select(zone => zone.Id).Take(16).ToArray(),
            ["destination_zone_ids"] = zoneInput?.DestinationZones.Select(zone => zone.Id).Take(16).ToArray(),
            ["matrix_id"] = zoneInput?.CriterionId
        });
        return InputPreparationResult.Succeeded(new PreparedInputs(
            graphValue.Snapshot,
            networkInput,
            guardSnapshot,
            zoneInput,
            networkZones));
    }

    /// <summary>Validated data required to select and execute one positioning recipe.</summary>
    private sealed record PreparedInputs(
        RulebaseGraphSnapshot Snapshot,
        CreationRuleNetworkInput NetworkInput,
        CheckPointLayerGuardSnapshot GuardSnapshot,
        CreationRuleZoneInput? ZoneInput,
        IReadOnlyCollection<NetworkZone>? NetworkZones);

    /// <summary>Runs bounded diagnostics only after the exact target section-scope failure.</summary>
    private async Task SearchHappyPathAfterTargetFailureAsync(
        PositioningBlockResult<CheckPointBlockValue> executionResult,
        RulePositioningContext context,
        PreparedInputs inputs,
        CheckPointSectionNameTemplate sectionNameTemplate,
        CancellationToken cancellationToken)
    {
        if (executionResult is not PositioningBlockFailure<CheckPointBlockValue>
            {
                Category: PositioningBlockFailureCategory.ScopeNotFound,
                OriginatingBlockKind: CheckPointSectionScopeBlock.kKind
            })
        {
            return;
        }
        RulePositioningDiagnosticLog.WriteInfo("happy-path.search", "start", new Dictionary<string, object?>
        {
            ["contains_customer_network_data"] = true
        });
        if (inputs.NetworkZones == null || context.Configuration == null)
        {
            RulePositioningDiagnosticLog.WriteInfo("happy-path.search", "skipped", new Dictionary<string, object?>
            {
                ["contains_customer_network_data"] = true,
                ["reason"] = "ZoneSnapshotUnavailable"
            });
            return;
        }
        try
        {
            CheckPointLayerGuardHappyPathSearchResult result = await happyPathFinder.FindAsync(
                context,
                inputs.Snapshot,
                inputs.GuardSnapshot,
                inputs.NetworkZones,
                context.Configuration,
                sectionNameTemplate,
                cancellationToken);
            WriteHappyPathResult(result);
        }
        catch (OperationCanceledException exception)
            when (cancellationToken.IsCancellationRequested ||
                cancellationToken.CanBeCanceled && exception.CancellationToken == cancellationToken)
        {
            throw;
        }
        catch (Exception exception)
        {
            RulePositioningDiagnosticLog.WriteInfo("happy-path.search", "exception", new Dictionary<string, object?>
            {
                ["contains_customer_network_data"] = true,
                ["exception_type"] = exception.GetType().FullName ?? exception.GetType().Name
            });
        }
    }

    /// <summary>Writes one bounded terminal event without dumping search inputs.</summary>
    private static void WriteHappyPathResult(CheckPointLayerGuardHappyPathSearchResult result)
    {
        Dictionary<string, object?> facts = SearchFacts(result);
        if (result.Status == CheckPointLayerGuardHappyPathSearchStatus.Found && result.Candidate != null)
        {
            AddCandidateFacts(facts, result.Candidate);
            RulePositioningDiagnosticLog.WriteInfo("happy-path.search", "success", facts);
            return;
        }
        RulePositioningDiagnosticLog.WriteInfo("happy-path.search", "summary", facts);
    }

    /// <summary>Creates bounded status and counter facts shared by every terminal result.</summary>
    private static Dictionary<string, object?> SearchFacts(CheckPointLayerGuardHappyPathSearchResult result) => new()
    {
        ["contains_customer_network_data"] = true,
        ["status"] = result.Status.ToString(),
        ["reason"] = result.Reason.ToString(),
        ["rendered_zone_pair_count"] = result.Statistics.RenderedZonePairCount,
        ["structural_match_count"] = result.Statistics.StructuralMatchCount,
        ["address_pair_validation_count"] = result.Statistics.AddressPairValidationCount,
        ["zone_rejection_count"] = result.Statistics.ZoneRejectionCount,
        ["guard_rejection_count"] = result.Statistics.GuardRejectionCount,
        ["section_rejection_count"] = result.Statistics.SectionRejectionCount,
        ["recipe_rejection_count"] = result.Statistics.RecipeRejectionCount
    };

    /// <summary>Adds only the single validated candidate and predicted semantic position.</summary>
    private static void AddCandidateFacts(
        Dictionary<string, object?> facts,
        CheckPointLayerGuardHappyPathCandidate candidate)
    {
        facts["source_ip"] = candidate.SourceAddress.ToString();
        facts["destination_ip"] = candidate.DestinationAddress.ToString();
        facts["source_zone_id"] = candidate.SourceZoneId;
        facts["source_zone_name"] = candidate.SourceZoneName;
        facts["destination_zone_id"] = candidate.DestinationZoneId;
        facts["destination_zone_name"] = candidate.DestinationZoneName;
        facts["guard_rule_id"] = candidate.GuardRuleId;
        facts["target_layer_id"] = candidate.TargetLayerId;
        facts["target_layer_name"] = candidate.TargetLayerName;
        facts["target_layer_uid"] = candidate.TargetLayerUid;
        facts["section_id"] = candidate.SectionId;
        facts["section_name"] = candidate.SectionName;
        facts["section_uid"] = candidate.SectionUid;
        facts["rendered_section_name"] = candidate.RenderedSectionName;
        facts["anchor_kind"] = candidate.PredictedPosition.AnchorKind.ToString();
        facts["layer_uid"] = candidate.PredictedPosition.LayerUid;
        facts["anchor_rule_uid"] = candidate.PredictedPosition.AnchorRuleUid;
    }

    /// <summary>Result of preparing positioning inputs without throwing domain failures.</summary>
    private sealed record InputPreparationResult(PreparedInputs? Inputs, RulePositioningFailure? PreparationFailure)
    {
        /// <summary>Creates a successful preparation result.</summary>
        internal static InputPreparationResult Succeeded(PreparedInputs inputs) => new(inputs, null);

        /// <summary>Creates a failed preparation result.</summary>
        internal static InputPreparationResult Failed(RulePositioningFailure failure) => new(null, failure);
    }

    /// <summary>Maps effective-network failures to the public positioning contract.</summary>
    private static RulePositioningFailure MapNetworkFailure(
        CreationRuleNetworkResolutionResult result,
        ProvisioningPositioningAlgorithm algorithm,
        long durationMs)
    {
        RulePositioningFailureCategory category = result.TryGetFailure(
            out CreationRuleNetworkResolutionFailureCategory failureCategory)
            ? failureCategory switch
            {
                CreationRuleNetworkResolutionFailureCategory.UnsupportedTask =>
                    RulePositioningFailureCategory.UnsupportedTask,
                CreationRuleNetworkResolutionFailureCategory.GroupQueryFailed or
                    CreationRuleNetworkResolutionFailureCategory.UnexpectedFailure =>
                    RulePositioningFailureCategory.CalculationFailed,
                _ => RulePositioningFailureCategory.InvalidInput
            }
            : RulePositioningFailureCategory.CalculationFailed;
        result.TryGetFailure(out CreationRuleNetworkResolutionFailureCategory internalCategory);
        return LoggedMapping("network", internalCategory.ToString(), category, algorithm, durationMs);
    }

    /// <summary>Maps fixed-import layer-guard failures to the public positioning contract.</summary>
    private static RulePositioningFailure MapGuardFailure(
        CheckPointLayerGuardReadResult result,
        ProvisioningPositioningAlgorithm algorithm,
        long durationMs)
    {
        RulePositioningFailureCategory category = result.TryGetFailure(out CheckPointLayerGuardReadFailureCategory failureCategory)
            && failureCategory is CheckPointLayerGuardReadFailureCategory.InvalidInput
                or CheckPointLayerGuardReadFailureCategory.ScopeMismatch
                ? RulePositioningFailureCategory.InvalidInput
                : RulePositioningFailureCategory.CalculationFailed;
        result.TryGetFailure(out CheckPointLayerGuardReadFailureCategory internalCategory);
        return LoggedMapping("guard", internalCategory.ToString(), category, algorithm, durationMs);
    }

    /// <summary>Checks whether an algorithm is defined and non-default.</summary>
    private static bool IsUsableAlgorithm(ProvisioningPositioningAlgorithm algorithm)
    {
        return algorithm != ProvisioningPositioningAlgorithm.Undefined && Enum.IsDefined(algorithm);
    }

    /// <summary>Maps graph-reader failures to the Phase 1 contract.</summary>
    private static RulePositioningFailure MapGraphFailure(
        RulebaseGraphReadResult result,
        ProvisioningPositioningAlgorithm algorithm,
        long durationMs)
    {
        RulePositioningFailureCategory category = result.TryGetFailure(out RulebaseGraphReadFailureCategory failureCategory)
            && failureCategory is RulebaseGraphReadFailureCategory.InvalidInput or RulebaseGraphReadFailureCategory.ScopeMismatch
                ? RulePositioningFailureCategory.InvalidInput
                : RulePositioningFailureCategory.CalculationFailed;
        result.TryGetFailure(out RulebaseGraphReadFailureCategory internalCategory);
        return LoggedMapping("graph", internalCategory.ToString(), category, algorithm, durationMs);
    }

    /// <summary>Maps designated-matrix-reader failures to the Phase 1 contract.</summary>
    private static RulePositioningFailure MapMatrixFailure(
        DesignatedZoneMatrixReadResult result,
        ProvisioningPositioningAlgorithm algorithm,
        long durationMs)
    {
        RulePositioningFailureCategory category = result.TryGetFailure(
            out DesignatedZoneMatrixReadFailureCategory failureCategory)
            && failureCategory is DesignatedZoneMatrixReadFailureCategory.ConfigurationInvalid
                or DesignatedZoneMatrixReadFailureCategory.MatrixUnavailable
                ? RulePositioningFailureCategory.ConfigurationInvalid
                : RulePositioningFailureCategory.CalculationFailed;
        result.TryGetFailure(out DesignatedZoneMatrixReadFailureCategory internalCategory);
        return LoggedMapping("matrix", internalCategory.ToString(), category, algorithm, durationMs);
    }

    /// <summary>Maps zone-resolution failures to the Phase 1 contract.</summary>
    private static RulePositioningFailure MapZoneFailure(
        CreationRuleZoneResolutionResult result,
        ProvisioningPositioningAlgorithm algorithm,
        long durationMs)
    {
        RulePositioningFailureCategory category = result.TryGetFailure(
            out CreationRuleZoneResolutionFailureCategory failureCategory)
            ? failureCategory switch
            {
                CreationRuleZoneResolutionFailureCategory.InvalidInput
                    or CreationRuleZoneResolutionFailureCategory.UnsupportedObject
                    or CreationRuleZoneResolutionFailureCategory.InvalidAddress => RulePositioningFailureCategory.InvalidInput,
                CreationRuleZoneResolutionFailureCategory.UnsupportedTask => RulePositioningFailureCategory.UnsupportedTask,
                CreationRuleZoneResolutionFailureCategory.InvalidZoneData => RulePositioningFailureCategory.ConfigurationInvalid,
                _ => RulePositioningFailureCategory.CalculationFailed
            }
            : RulePositioningFailureCategory.CalculationFailed;
        result.TryGetFailure(out CreationRuleZoneResolutionFailureCategory internalCategory);
        return LoggedMapping("zone", internalCategory.ToString(), category, algorithm, durationMs);
    }

    /// <summary>Maps recipe-selection failures to the Phase 1 contract.</summary>
    private static RulePositioningFailure MapSelectionFailure(
        CheckPointRecipeSelectionResult result,
        ProvisioningPositioningAlgorithm algorithm,
        long durationMs)
    {
        RulePositioningFailureCategory category = RulePositioningFailureCategory.CalculationFailed;
        if (result.TryGetFailure(out CheckPointRecipeSelectionFailureCategory failureCategory))
        {
            category = failureCategory == CheckPointRecipeSelectionFailureCategory.NoMatchingRecipe
                ? RulePositioningFailureCategory.PositioningMethodNotImplemented
                : RulePositioningFailureCategory.ConfigurationInvalid;
        }
        result.TryGetFailure(out CheckPointRecipeSelectionFailureCategory internalCategory);
        return LoggedMapping("recipe.selection", internalCategory.ToString(), category, algorithm, durationMs);
    }

    /// <summary>Maps recipe-execution failures to the Phase 1 contract.</summary>
    private static RulePositioningFailure MapExecutionFailure(
        PositioningBlockResult<CheckPointBlockValue> result,
        ProvisioningPositioningAlgorithm algorithm,
        long durationMs)
    {
        RulePositioningFailureCategory category = result is PositioningBlockFailure<CheckPointBlockValue> failure
            && failure.Category == PositioningBlockFailureCategory.InvalidRecipe
                ? RulePositioningFailureCategory.ConfigurationInvalid
                : RulePositioningFailureCategory.CalculationFailed;
        string internalCategory = result is PositioningBlockFailure<CheckPointBlockValue> blockFailure
            ? blockFailure.Category.ToString()
            : "UnexpectedResultType";
        return LoggedMapping("recipe.execution", internalCategory, category, algorithm, durationMs);
    }

    /// <summary>Creates a Phase 1 failure with its fixed safe diagnostic.</summary>
    private static RulePositioningFailure Failure(RulePositioningFailureCategory category)
    {
        return new RulePositioningFailure(category);
    }

    private static RulePositioningFailure LoggedMapping(
        string component,
        string internalCategory,
        RulePositioningFailureCategory publicCategory,
        ProvisioningPositioningAlgorithm? algorithm,
        long durationMs)
    {
        RulePositioningDiagnosticLog.WriteFailure("strategy.mapping", new Dictionary<string, object?>
        {
            ["strategy"] = nameof(CheckPointRulePositioningStrategy),
            ["component"] = component,
            ["internal_category"] = internalCategory,
            ["public_category"] = publicCategory.ToString(),
            ["algorithm"] = algorithm?.ToString(),
            ["duration_ms"] = durationMs
        });
        return Failure(publicCategory);
    }

    private static void WritePositionSuccess(
        CheckPointRulePosition position,
        ProvisioningPositioningAlgorithm algorithm)
    {
        RulePositioningDiagnosticLog.WriteInfo("strategy.position", "success", new Dictionary<string, object?>
        {
            ["strategy"] = nameof(CheckPointRulePositioningStrategy),
            ["algorithm"] = algorithm.ToString(),
            ["anchor_kind"] = position.AnchorKind.ToString(),
            ["layer_uid"] = RulePositioningDiagnosticLog.Fingerprint(position.LayerUid),
            ["anchor_uid"] = RulePositioningDiagnosticLog.Fingerprint(position.AnchorRuleUid)
        });
    }
}

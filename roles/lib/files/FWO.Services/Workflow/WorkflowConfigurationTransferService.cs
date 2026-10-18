using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Logging;

namespace FWO.Services.Workflow
{
    public class WorkflowConfigurationTransferService(ApiConnection apiConnection)
    {
        public WorkflowConfigurationImportStatistics LastImportStatistics { get; private set; } = new();
        /// <summary>
        /// Loads the phase mappings available for a workflow configuration.
        /// </summary>
        public async Task<List<WorkflowConfigurationPhase>> GetPhaseMappings(int configurationId)
        {
            return await apiConnection.SendQueryAsync<List<WorkflowConfigurationPhase>>(
                RequestQueries.getWorkflowConfigurationPhaseMappings, new { configurationId });
        }

        /// <summary>
        /// Builds an ID-independent package for the selected parts of a workflow configuration.
        /// </summary>
        public async Task<WorkflowConfigurationTransferPackage> Export(int configurationId, bool includeVisibilityGroups,
            IReadOnlySet<string>? selectedPhaseKeys = null)
        {
            List<WorkflowConfiguration> configurations = await apiConnection.SendQueryAsync<List<WorkflowConfiguration>>(
                RequestQueries.getWorkflowConfigurations);
            WorkflowConfiguration configuration = configurations.Single(item => item.Id == configurationId);
            List<WorkflowConfigurationPhase> phaseMappings = await GetPhaseMappings(configurationId);
            List<WorkflowConfigurationPhase> selectedPhaseMappings = phaseMappings.Where(mapping =>
                selectedPhaseKeys == null || selectedPhaseKeys.Contains(PhaseKey(mapping))).ToList();
            if (selectedPhaseMappings.Count == 0)
            {
                throw new InvalidDataException("At least one workflow phase must be selected for export.");
            }
            List<StateMatrixTransitionGroup> allTransitionGroups = await apiConnection.SendQueryAsync<List<StateMatrixTransitionGroup>>(
                RequestQueries.getStateMatrixTransitionGroups);
            Dictionary<int, StateMatrixTransitionGroup> groupsById = allTransitionGroups.ToDictionary(group => group.Id);
            HashSet<int> usedGroupIds = selectedPhaseMappings.SelectMany(mapping => mapping.PhaseMatrix.TransitionGroups)
                .Select(link => link.TransitionGroupId).ToHashSet();
            List<StateMatrixTransitionGroup> usedGroups = usedGroupIds.Select(id => groupsById.TryGetValue(id, out StateMatrixTransitionGroup? group)
                ? group
                : throw new InvalidOperationException($"Transition group {id} referenced by the configuration was not loaded.")).ToList();

            WorkflowConfigurationTransferPackage package = new()
            {
                Configuration = BuildConfiguration(configuration, selectedPhaseMappings, groupsById),
                TransitionGroups = usedGroups.OrderBy(group => group.Name)
                    .Select(group => BuildTransitionGroup(group, includeVisibilityGroups)).ToList()
            };
            if (includeVisibilityGroups)
            {
                package.VisibilityGroups = await ExportVisibilityGroups(usedGroups);
            }
            ValidateStructure(package);
            return package;
        }

        /// <summary>
        /// Imports a package as a new inactive configuration and returns its database ID.
        /// </summary>
        public async Task<int> Import(WorkflowConfigurationTransferPackage package, string configurationName,
            int? targetConfigurationId = null)
        {
            List<WorkflowConfiguration> configurations = await apiConnection.SendQueryAsync<List<WorkflowConfiguration>>(
                RequestQueries.getWorkflowConfigurations);
            List<StateMatrixTransitionGroup> existingGroups = await apiConnection.SendQueryAsync<List<StateMatrixTransitionGroup>>(
                RequestQueries.getStateMatrixTransitionGroups);
            List<WorkflowVisibilityGroup> existingVisibilityGroups = await apiConnection.SendQueryAsync<List<WorkflowVisibilityGroup>>(
                RequestQueries.getWorkflowVisibilityGroups);
            List<WfState> states = await apiConnection.SendQueryAsync<List<WfState>>(RequestQueries.getStates);
            WorkflowConfiguration? targetConfiguration = targetConfigurationId.HasValue
                ? configurations.SingleOrDefault(configuration => configuration.Id == targetConfigurationId.Value)
                : null;
            if (targetConfigurationId.HasValue && targetConfiguration == null)
            {
                throw new InvalidDataException("The selected target workflow configuration does not exist.");
            }
            Validate(package, targetConfiguration?.Name ?? configurationName, configurations, states, true,
                targetConfiguration != null);
            ValidateExistingDefinitions(package, existingGroups, existingVisibilityGroups);
            List<WorkflowConfigurationPhase> targetPhases = targetConfiguration == null
                ? []
                : await GetPhaseMappings(targetConfiguration.Id);
            ValidateMerge(package, targetPhases);

            List<int> createdTransitionGroupIds = [];
            List<int> createdVisibilityGroupIds = [];
            List<int> createdStateIds = [];
            try
            {
                await CreateMissingStates(package, states, createdStateIds);
                Dictionary<string, int> visibilityGroupIds = await ResolveVisibilityGroups(
                    package.VisibilityGroups ?? [], existingVisibilityGroups, createdVisibilityGroupIds);
                Dictionary<string, int> transitionGroupIds = await ResolveTransitionGroups(
                    package.TransitionGroups, existingGroups, visibilityGroupIds, createdTransitionGroupIds);
                if (targetConfiguration != null)
                {
                    WorkflowConfigurationImportStatistics statistics = new()
                    {
                        CreatedStates = createdStateIds.Count,
                        CreatedTransitionGroups = createdTransitionGroupIds.Count,
                        CreatedVisibilityGroups = createdVisibilityGroupIds.Count,
                        MergedPhases = package.Configuration.Phases.Count(phase => targetPhases.Any(target =>
                            string.Equals(PhaseKey(target), PhaseKey(phase), StringComparison.OrdinalIgnoreCase)))
                    };
                    await MergeIntoConfiguration(package, targetConfiguration.Id, targetConfiguration.Name, targetPhases,
                        transitionGroupIds, statistics);
                    LastImportStatistics = statistics;
                    Log.WriteDebug("Workflow configuration transfer",
                        $"Imported workflow configuration package into {targetConfiguration.Name}: {StatisticsText(statistics)}");
                    return targetConfiguration.Id;
                }
                LastImportStatistics = new()
                {
                    CreatedStates = createdStateIds.Count,
                    CreatedTransitionGroups = createdTransitionGroupIds.Count,
                    CreatedVisibilityGroups = createdVisibilityGroupIds.Count,
                    AddedPhases = package.Configuration.Phases.Count
                };
                ReturnId result = await apiConnection.SendQueryAsync<ReturnId>(RequestQueries.createWorkflowConfiguration, new
                {
                    name = configurationName.Trim(),
                    description = NormalizeOptional(package.Configuration.Description),
                    phaseMappings = BuildPhaseMappings(package.Configuration.Phases, configurationName.Trim(), transitionGroupIds)
                });
                Log.WriteDebug("Workflow configuration transfer",
                    $"Imported workflow configuration package as {configurationName.Trim()}: {StatisticsText(LastImportStatistics)}");
                return result.NewId;
            }
            catch (Exception importException)
            {
                try
                {
                    await RollBack(createdTransitionGroupIds, createdVisibilityGroupIds, createdStateIds);
                }
                catch (Exception rollbackException)
                {
                    throw new AggregateException("Workflow configuration import and rollback failed.", importException, rollbackException);
                }
                throw;
            }
        }

        /// <summary>
        /// Validates package structure and target state compatibility before creating records.
        /// </summary>
        public static void Validate(WorkflowConfigurationTransferPackage package, string configurationName,
            IEnumerable<WorkflowConfiguration> existingConfigurations, IEnumerable<WfState> states,
            bool allowMissingStates = false, bool allowExistingConfiguration = false)
        {
            ValidateStructure(package);
            if (string.IsNullOrWhiteSpace(configurationName) || (!allowExistingConfiguration && existingConfigurations.Any(configuration =>
                string.Equals(configuration.Name, configurationName.Trim(), StringComparison.OrdinalIgnoreCase))))
            {
                throw new InvalidDataException("The imported configuration name is empty or already exists.");
            }
            if (!allowMissingStates)
            {
                ValidateStates(package, states.Select(state => state.Id).ToHashSet());
            }
        }

        private static void ValidateMerge(WorkflowConfigurationTransferPackage package,
            List<WorkflowConfigurationPhase> targetPhases)
        {
            Dictionary<string, WorkflowConfigurationPhase> existingByKey = targetPhases.ToDictionary(PhaseKey,
                StringComparer.OrdinalIgnoreCase);
            foreach (WorkflowPhaseTransferData imported in package.Configuration.Phases)
            {
                if (!existingByKey.TryGetValue(PhaseKey(imported), out WorkflowConfigurationPhase? existing))
                {
                    continue;
                }
                Dictionary<int, int> existingDerived = existing.PhaseMatrix.DerivedStates
                    .ToDictionary(state => state.FromStateId, state => state.DerivedStateId);
                foreach (StateMatrixDerivedState importedState in imported.DerivedStates)
                {
                    if (existingDerived.TryGetValue(importedState.FromStateId, out int existingTarget)
                        && existingTarget != importedState.DerivedStateId)
                    {
                        throw new InvalidDataException(
                            $"Cannot merge phase '{imported.TaskType}/{imported.Phase}': state {importedState.FromStateId} "
                            + $"maps to both {existingTarget} and {importedState.DerivedStateId}.");
                    }
                }
            }
        }

        private async Task MergeIntoConfiguration(WorkflowConfigurationTransferPackage package, int configurationId,
            string configurationName, List<WorkflowConfigurationPhase> targetPhases,
            Dictionary<string, int> transitionGroupIds, WorkflowConfigurationImportStatistics statistics)
        {
            Dictionary<string, WorkflowConfigurationPhase> existingByKey = targetPhases.ToDictionary(PhaseKey,
                StringComparer.OrdinalIgnoreCase);
            foreach (WorkflowPhaseTransferData imported in package.Configuration.Phases)
            {
                if (!existingByKey.TryGetValue(PhaseKey(imported), out WorkflowConfigurationPhase? existing))
                {
                    await AddPhase(configurationId, configurationName, imported, transitionGroupIds);
                    statistics.AddedPhases++;
                    continue;
                }
                (int derivedStateCount, int transitionGroupLinkCount) = await AddMissingPhaseContent(existing, imported, transitionGroupIds);
                statistics.AddedDerivedStates += derivedStateCount;
                statistics.AddedTransitionGroupLinks += transitionGroupLinkCount;
            }
        }

        private async Task AddPhase(int configurationId, string configurationName, WorkflowPhaseTransferData phase,
            Dictionary<string, int> transitionGroupIds)
        {
            List<object> derivedStates = phase.DerivedStates.Where(state => state.FromStateId != state.DerivedStateId)
                .Select(state => (object)new { from_state_id = state.FromStateId, derived_state_id = state.DerivedStateId }).ToList();
            List<object> transitionGroups = phase.TransitionGroups.Select((name, sortOrder) => (object)new
            {
                transition_group_id = transitionGroupIds[name], sort_order = sortOrder
            }).ToList();
            await apiConnection.SendQueryAsync<object>(RequestQueries.addWorkflowConfigurationPhase, new
            {
                configurationId,
                taskType = phase.TaskType,
                phase = phase.Phase,
                name = $"{configurationName}::{phase.TaskType}::{phase.Phase}",
                active = phase.Active,
                phaseVisibilityMode = phase.VisibilityMode.ToString(),
                lowestInputState = phase.LowestInputState,
                lowestStartState = phase.LowestStartState,
                lowestEndState = phase.LowestEndState,
                derivedStates,
                transitionGroups
            });
        }

        private async Task<(int DerivedStateCount, int TransitionGroupLinkCount)> AddMissingPhaseContent(
            WorkflowConfigurationPhase existing, WorkflowPhaseTransferData imported,
            Dictionary<string, int> transitionGroupIds)
        {
            HashSet<int> existingDerivedStateIds = existing.PhaseMatrix.DerivedStates.Select(state => state.FromStateId).ToHashSet();
            List<object> derivedStates = imported.DerivedStates.Where(state => state.FromStateId != state.DerivedStateId
                && !existingDerivedStateIds.Contains(state.FromStateId)).Select(state => (object)new
                {
                    phase_matrix_id = existing.PhaseMatrixId,
                    from_state_id = state.FromStateId,
                    derived_state_id = state.DerivedStateId
                }).ToList();
            if (derivedStates.Count > 0)
            {
                await apiConnection.SendQueryAsync<object>(RequestQueries.replaceStateMatrixConfiguration, new
                {
                    removedDerivedStates = Array.Empty<object>(),
                    removedTransitions = Array.Empty<object>(),
                    phaseMatrices = Array.Empty<object>(),
                    derivedStates,
                    transitions = Array.Empty<object>()
                });
            }
            HashSet<int> existingGroupIds = existing.PhaseMatrix.TransitionGroups.Select(group => group.TransitionGroupId).ToHashSet();
            int nextSortOrder = existing.PhaseMatrix.TransitionGroups.Count;
            int addedTransitionGroupLinks = 0;
            foreach (string name in imported.TransitionGroups)
            {
                int groupId = transitionGroupIds[name];
                if (!existingGroupIds.Contains(groupId))
                {
                    await apiConnection.SendQueryAsync<object>(RequestQueries.linkStateMatrixTransitionGroup, new
                    {
                        phaseMatrixId = existing.PhaseMatrixId,
                        transitionGroupId = groupId,
                        sortOrder = nextSortOrder++
                    });
                    addedTransitionGroupLinks++;
                }
            }
            return (derivedStates.Count, addedTransitionGroupLinks);
        }

        private async Task CreateMissingStates(WorkflowConfigurationTransferPackage package, List<WfState> states,
            List<int> createdStateIds)
        {
            HashSet<int> existingStateIds = states.Select(state => state.Id).ToHashSet();
            foreach (int stateId in ReferencedStateIds(package).Where(id => !existingStateIds.Contains(id)).Distinct().Order())
            {
                await apiConnection.SendQueryAsync<object>(RequestQueries.createState, new
                {
                    id = stateId,
                    name = $"Imported state {stateId}",
                    automaticOnly = false
                });
                createdStateIds.Add(stateId);
            }
        }

        private static void ValidateExistingDefinitions(WorkflowConfigurationTransferPackage package,
            List<StateMatrixTransitionGroup> existingGroups, List<WorkflowVisibilityGroup> existingVisibilityGroups)
        {
            foreach (WorkflowVisibilityGroupTransferData imported in package.VisibilityGroups ?? [])
            {
                WorkflowVisibilityGroup? existing = existingVisibilityGroups.FirstOrDefault(group =>
                    string.Equals(group.Name, imported.Name, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    EnsureEquivalent(existing, imported);
                }
            }
            foreach (WorkflowTransitionGroupTransferData imported in package.TransitionGroups)
            {
                StateMatrixTransitionGroup? existing = existingGroups.FirstOrDefault(group =>
                    string.Equals(group.Name, imported.Name, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    EnsureEquivalent(existing, imported);
                }
            }
        }

        /// <summary>
        /// Validates the self-contained structure and references of a transfer package.
        /// </summary>
        public static void ValidateStructure(WorkflowConfigurationTransferPackage package)
        {
            if (package.Configuration == null || package.TransitionGroups == null || package.Configuration.Phases == null
                || package.Configuration.Phases.Any(phase => phase == null || phase.DerivedStates == null || phase.TransitionGroups == null)
                || package.TransitionGroups.Any(group => group == null || group.Transitions == null)
                || (package.VisibilityGroups?.Any(group => group == null || group.Members == null) ?? false))
            {
                throw new InvalidDataException("The workflow configuration package is incomplete.");
            }
            if (package.Format != WorkflowConfigurationTransferPackage.kFormat || package.Version != WorkflowConfigurationTransferPackage.kCurrentVersion)
            {
                throw new InvalidDataException($"Unsupported workflow configuration package format or version {package.Version}.");
            }
            ValidateNamesAndReferences(package);
            ValidatePhases(package.Configuration.Phases);
        }

        private static WorkflowConfigurationTransferData BuildConfiguration(WorkflowConfiguration configuration,
            List<WorkflowConfigurationPhase> mappings, Dictionary<int, StateMatrixTransitionGroup> groupsById) => new()
            {
                Name = configuration.Name,
                Description = configuration.Description,
                Phases = mappings.Select(mapping => new WorkflowPhaseTransferData
                {
                    TaskType = mapping.TaskType,
                    Phase = mapping.Phase,
                    Active = mapping.PhaseMatrix.Active,
                    VisibilityMode = mapping.PhaseMatrix.VisibilityMode,
                    LowestInputState = mapping.PhaseMatrix.LowestInputState,
                    LowestStartState = mapping.PhaseMatrix.LowestStartState,
                    LowestEndState = mapping.PhaseMatrix.LowestEndState,
                    DerivedStates = mapping.PhaseMatrix.DerivedStates.Select(state => new StateMatrixDerivedState
                    {
                        FromStateId = state.FromStateId,
                        DerivedStateId = state.DerivedStateId
                    }).ToList(),
                    TransitionGroups = mapping.PhaseMatrix.TransitionGroups.OrderBy(link => link.SortOrder)
                        .Select(link => groupsById[link.TransitionGroupId].Name).ToList()
                }).ToList()
            };

        private static WorkflowTransitionGroupTransferData BuildTransitionGroup(StateMatrixTransitionGroup group, bool includeVisibilityGroup) => new()
        {
            Name = group.Name,
            Description = group.Description,
            Phase = group.Phase,
            Exclusive = includeVisibilityGroup && group.Exclusive,
            VisibilityGroup = includeVisibilityGroup ? group.VisibilityGroup?.Name : null,
            Transitions = group.Transitions.OrderBy(transition => transition.FromStateId).ThenBy(transition => transition.SortOrder)
                .Select(transition => new StateMatrixTransition
                {
                    FromStateId = transition.FromStateId,
                    ToStateId = transition.ToStateId,
                    SortOrder = transition.SortOrder
                }).ToList()
        };

        private async Task<List<WorkflowVisibilityGroupTransferData>> ExportVisibilityGroups(List<StateMatrixTransitionGroup> groups)
        {
            HashSet<int> usedIds = groups.Where(group => group.VisibilityGroupId.HasValue)
                .Select(group => group.VisibilityGroupId!.Value).ToHashSet();
            if (usedIds.Count == 0)
            {
                return [];
            }
            List<WorkflowVisibilityGroup> visibilityGroups = await apiConnection.SendQueryAsync<List<WorkflowVisibilityGroup>>(
                RequestQueries.getWorkflowVisibilityGroups);
            return visibilityGroups.Where(group => usedIds.Contains(group.Id)).OrderBy(group => group.Name)
                .Select(group => new WorkflowVisibilityGroupTransferData
                {
                    Name = group.Name,
                    Description = group.Description,
                    Members = group.Members.Select(member => member.MemberDn).Order(StringComparer.OrdinalIgnoreCase).ToList()
                }).ToList();
        }

        private async Task<Dictionary<string, int>> ResolveVisibilityGroups(List<WorkflowVisibilityGroupTransferData> importedGroups,
            List<WorkflowVisibilityGroup> existingGroups, List<int> createdIds)
        {
            Dictionary<string, int> resolved = new(StringComparer.OrdinalIgnoreCase);
            foreach (WorkflowVisibilityGroupTransferData imported in importedGroups)
            {
                WorkflowVisibilityGroup? existing = existingGroups.FirstOrDefault(group =>
                    string.Equals(group.Name, imported.Name, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    EnsureEquivalent(existing, imported);
                    resolved[imported.Name] = existing.Id;
                    continue;
                }
                ReturnId result = await apiConnection.SendQueryAsync<ReturnId>(RequestQueries.createWorkflowVisibilityGroup,
                    new { name = imported.Name, description = NormalizeOptional(imported.Description) });
                createdIds.Add(result.NewId);
                resolved[imported.Name] = result.NewId;
                if (imported.Members.Count > 0)
                {
                    List<object> members = imported.Members.Select(memberDn => (object)new
                    {
                        visibility_group_id = result.NewId,
                        member_dn = memberDn
                    }).ToList();
                    await apiConnection.SendQueryAsync<object>(RequestQueries.replaceWorkflowVisibilityGroupMembers,
                        new { removedMembers = Array.Empty<object>(), members });
                }
            }
            return resolved;
        }

        private async Task<Dictionary<string, int>> ResolveTransitionGroups(List<WorkflowTransitionGroupTransferData> importedGroups,
            List<StateMatrixTransitionGroup> existingGroups, Dictionary<string, int> visibilityGroupIds, List<int> createdIds)
        {
            Dictionary<string, int> resolved = new(StringComparer.OrdinalIgnoreCase);
            foreach (WorkflowTransitionGroupTransferData imported in importedGroups)
            {
                StateMatrixTransitionGroup? existing = existingGroups.FirstOrDefault(group =>
                    string.Equals(group.Name, imported.Name, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    EnsureEquivalent(existing, imported);
                    resolved[imported.Name] = existing.Id;
                    continue;
                }
                int? visibilityGroupId = imported.VisibilityGroup == null ? null : visibilityGroupIds[imported.VisibilityGroup];
                ReturnId result = await apiConnection.SendQueryAsync<ReturnId>(RequestQueries.createStateMatrixTransitionGroup, new
                {
                    name = imported.Name,
                    description = NormalizeOptional(imported.Description),
                    phase = NormalizeOptional(imported.Phase),
                    visibilityGroupId,
                    exclusive = visibilityGroupId.HasValue && imported.Exclusive
                });
                createdIds.Add(result.NewId);
                resolved[imported.Name] = result.NewId;
                await InsertTransitions(result.NewId, imported.Transitions);
            }
            return resolved;
        }

        private async Task InsertTransitions(int groupId, List<StateMatrixTransition> importedTransitions)
        {
            if (importedTransitions.Count == 0)
            {
                return;
            }
            List<object> transitions = importedTransitions.Select(transition => (object)new
            {
                transition_group_id = groupId,
                from_state_id = transition.FromStateId,
                to_state_id = transition.ToStateId,
                sort_order = transition.SortOrder
            }).ToList();
            await apiConnection.SendQueryAsync<object>(RequestQueries.replaceStateMatrixTransitionGroupTransitions,
                new { removedTransitions = Array.Empty<object>(), transitions });
        }

        private static List<object> BuildPhaseMappings(List<WorkflowPhaseTransferData> phases, string configurationName,
            Dictionary<string, int> transitionGroupIds) => phases.Select(phase => (object)new
            {
                task_type = phase.TaskType,
                phase = phase.Phase,
                state_matrix_phase = new
                {
                    data = new
                    {
                        name = $"{configurationName}::{phase.TaskType}::{phase.Phase}",
                        phase = phase.Phase,
                        active = phase.Active,
                        phase_visibility_mode = phase.VisibilityMode.ToString(),
                        lowest_input_state = phase.LowestInputState,
                        lowest_start_state = phase.LowestStartState,
                        lowest_end_state = phase.LowestEndState,
                        state_matrix_derived_states = new
                        {
                            data = phase.DerivedStates.Where(state => state.FromStateId != state.DerivedStateId).Select(state => new
                            {
                                from_state_id = state.FromStateId,
                                derived_state_id = state.DerivedStateId
                            }).ToList()
                        },
                        state_matrix_phase_transition_groups = new
                        {
                            data = phase.TransitionGroups.Select((name, sortOrder) => new
                            {
                                transition_group_id = transitionGroupIds[name],
                                sort_order = sortOrder
                            }).ToList()
                        }
                    }
                }
            }).ToList();

        private static void ValidateNamesAndReferences(WorkflowConfigurationTransferPackage package)
        {
            EnsureUniqueNames(package.TransitionGroups.Select(group => group.Name), "transition group");
            EnsureUniqueNames((package.VisibilityGroups ?? []).Select(group => group.Name), "visibility group");
            HashSet<string> transitionGroupNames = package.TransitionGroups.Select(group => group.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            HashSet<string> referencedTransitionGroups = package.Configuration.Phases.SelectMany(phase => phase.TransitionGroups)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!transitionGroupNames.SetEquals(referencedTransitionGroups))
            {
                throw new InvalidDataException("The transition groups included in the package do not match its phase references.");
            }
            HashSet<string> visibilityGroupNames = (package.VisibilityGroups ?? []).Select(group => group.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            HashSet<string> referencedVisibilityGroups = package.TransitionGroups.Where(group => group.VisibilityGroup != null)
                .Select(group => group.VisibilityGroup!).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!visibilityGroupNames.SetEquals(referencedVisibilityGroups))
            {
                throw new InvalidDataException("The visibility groups included in the package do not match its transition-group references.");
            }
            if (package.TransitionGroups.Any(group => group.Transitions.GroupBy(transition =>
                (transition.FromStateId, transition.ToStateId)).Any(duplicates => duplicates.Count() > 1)))
            {
                throw new InvalidDataException("A transition group contains duplicate transitions.");
            }
            if (package.TransitionGroups.Any(group => group.Exclusive && group.VisibilityGroup == null
                || group.Phase != null && (group.Phase != group.Phase.Trim()
                    || !Enum.TryParse(group.Phase, true, out WorkflowPhases _))))
            {
                throw new InvalidDataException("A transition group contains an invalid phase or exclusive visibility definition.");
            }
            if ((package.VisibilityGroups ?? []).Any(group => group.Members.Any(member => string.IsNullOrWhiteSpace(member)
                || member != member.Trim()) || group.Members.GroupBy(member => member, StringComparer.OrdinalIgnoreCase).Any(members => members.Count() > 1)))
            {
                throw new InvalidDataException("A visibility group contains an empty or duplicate member DN.");
            }
        }

        private static void ValidatePhases(List<WorkflowPhaseTransferData> phases)
        {
            if (phases.Count == 0 || phases.Any(phase => string.IsNullOrWhiteSpace(phase.TaskType)
                || string.IsNullOrWhiteSpace(phase.Phase) || phase.TaskType != phase.TaskType.Trim() || phase.Phase != phase.Phase.Trim()
                || !Enum.TryParse(phase.TaskType, true, out WfTaskType _) || !Enum.TryParse(phase.Phase, true, out WorkflowPhases _)))
            {
                throw new InvalidDataException("The package contains no phases or an unknown task type or phase.");
            }
            if (phases.GroupBy(phase => (phase.TaskType.ToLowerInvariant(), phase.Phase.ToLowerInvariant())).Any(group => group.Count() > 1))
            {
                throw new InvalidDataException("The package contains duplicate task type and phase mappings.");
            }
            if (phases.Any(phase => phase.TransitionGroups.GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Any(group => group.Count() > 1) || phase.DerivedStates.GroupBy(state => state.FromStateId).Any(group => group.Count() > 1)))
            {
                throw new InvalidDataException("A phase contains duplicate transition-group links or derived-state inputs.");
            }
        }

        private static IEnumerable<int> ReferencedStateIds(WorkflowConfigurationTransferPackage package)
        {
            return package.Configuration.Phases.SelectMany(phase => new List<int>
                {
                    phase.LowestInputState,
                    phase.LowestStartState,
                    phase.LowestEndState
                }.Concat(phase.DerivedStates.SelectMany(state => new List<int> { state.FromStateId, state.DerivedStateId })))
                .Concat(package.TransitionGroups.SelectMany(group => group.Transitions)
                    .SelectMany(transition => new List<int> { transition.FromStateId, transition.ToStateId }));
        }

        private static void ValidateStates(WorkflowConfigurationTransferPackage package, HashSet<int> stateIds)
        {
            IEnumerable<int> referencedStateIds = ReferencedStateIds(package);
            List<int> missingStateIds = referencedStateIds.Distinct().Where(id => !stateIds.Contains(id)).Order().ToList();
            if (missingStateIds.Count > 0)
            {
                throw new InvalidDataException($"The target installation is missing workflow states: {string.Join(", ", missingStateIds)}.");
            }
        }

        private static void EnsureUniqueNames(IEnumerable<string> names, string entity)
        {
            if (names.Any(name => string.IsNullOrWhiteSpace(name) || name != name.Trim())
                || names.GroupBy(name => name, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            {
                throw new InvalidDataException($"The package contains an empty or duplicate {entity} name.");
            }
        }

        private static void EnsureEquivalent(WorkflowVisibilityGroup existing, WorkflowVisibilityGroupTransferData imported)
        {
            bool equalMembers = existing.Members.Select(member => member.MemberDn).ToHashSet(StringComparer.OrdinalIgnoreCase)
                .SetEquals(imported.Members);
            if (NormalizeOptional(existing.Description) != NormalizeOptional(imported.Description) || !equalMembers)
            {
                throw new InvalidDataException($"Visibility group '{imported.Name}' already exists with a different definition.");
            }
        }

        private static void EnsureEquivalent(StateMatrixTransitionGroup existing, WorkflowTransitionGroupTransferData imported)
        {
            string? existingVisibilityGroup = existing.VisibilityGroup?.Name;
            bool equalTransitions = existing.Transitions.OrderBy(transition => transition.FromStateId).ThenBy(transition => transition.SortOrder)
                .Select(transition => (transition.FromStateId, transition.ToStateId, transition.SortOrder))
                .SequenceEqual(imported.Transitions.OrderBy(transition => transition.FromStateId).ThenBy(transition => transition.SortOrder)
                    .Select(transition => (transition.FromStateId, transition.ToStateId, transition.SortOrder)));
            if (NormalizeOptional(existing.Description) != NormalizeOptional(imported.Description)
                || !string.Equals(NormalizeOptional(existing.Phase), NormalizeOptional(imported.Phase), StringComparison.OrdinalIgnoreCase)
                || existing.Exclusive != imported.Exclusive
                || !string.Equals(existingVisibilityGroup, imported.VisibilityGroup, StringComparison.OrdinalIgnoreCase)
                || !equalTransitions)
            {
                throw new InvalidDataException($"Transition group '{imported.Name}' already exists with a different definition.");
            }
        }

        private async Task RollBack(List<int> transitionGroupIds, List<int> visibilityGroupIds, List<int> stateIds)
        {
            List<Exception> errors = [];
            foreach (int id in transitionGroupIds.AsEnumerable().Reverse())
            {
                try
                {
                    await apiConnection.SendQueryAsync<ReturnId>(RequestQueries.deleteStateMatrixTransitionGroup, new { id });
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }
            foreach (int id in visibilityGroupIds.AsEnumerable().Reverse())
            {
                try
                {
                    await apiConnection.SendQueryAsync<ReturnId>(RequestQueries.deleteWorkflowVisibilityGroup, new { id });
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }
            foreach (int id in stateIds.AsEnumerable().Reverse())
            {
                try
                {
                    await apiConnection.SendQueryAsync<object>(RequestQueries.deleteState, new { id });
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }
            if (errors.Count > 0)
            {
                throw new AggregateException("Could not fully roll back workflow configuration import.", errors);
            }
        }

        private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string PhaseKey(WorkflowConfigurationPhase mapping) => $"{mapping.TaskType}:{mapping.Phase}";

        private static string PhaseKey(WorkflowPhaseTransferData phase) => $"{phase.TaskType}:{phase.Phase}";

        private static string StatisticsText(WorkflowConfigurationImportStatistics statistics) =>
            $"states={statistics.CreatedStates}, transitionGroups={statistics.CreatedTransitionGroups}, "
            + $"visibilityGroups={statistics.CreatedVisibilityGroups}, addedPhases={statistics.AddedPhases}, "
            + $"mergedPhases={statistics.MergedPhases}, derivedStates={statistics.AddedDerivedStates}, "
            + $"transitionGroupLinks={statistics.AddedTransitionGroupLinks}";
    }
}

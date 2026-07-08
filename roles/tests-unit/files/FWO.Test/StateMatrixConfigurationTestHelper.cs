using FWO.Data.Workflow;
using FWO.Services.Workflow;
using System.Text.Json;

namespace FWO.Test
{
    internal static class StateMatrixConfigurationTestHelper
    {
        public static List<WorkflowConfiguration> FromLegacyJson(string json, WfTaskType taskType = WfTaskType.master)
        {
            GlobalStateMatrix source = JsonSerializer.Deserialize<GlobalStateMatrix>(json)
                ?? throw new JsonException("State matrix test data could not be parsed.");
            return FromGlobalMatrix(source, taskType);
        }

        public static List<WorkflowConfiguration> FromGlobalMatrix(GlobalStateMatrix source, WfTaskType taskType = WfTaskType.master)
        {
            WorkflowConfiguration configuration = new()
            {
                Id = 1,
                Name = "current",
                IsActive = true
            };

            foreach (WorkflowPhases phase in Enum.GetValues<WorkflowPhases>())
            {
                StateMatrix matrix = source.GlobalMatrix.GetValueOrDefault(phase) ?? new();
                int phaseId = (int)phase + 1;
                int transitionGroupId = 100 + phaseId;
                StateMatrixTransitionGroup transitionGroup = new()
                {
                    Id = transitionGroupId,
                    Name = $"current_{taskType}_{phase}_transitions",
                    Transitions = matrix.Matrix.SelectMany(entry => entry.Value.Select((target, sortOrder) => new StateMatrixTransition
                    {
                        FromStateId = entry.Key,
                        ToStateId = target,
                        SortOrder = sortOrder
                    })).ToList()
                };
                configuration.Phases.Add(new()
                {
                    TaskType = taskType.ToString(),
                    Phase = phase.ToString(),
                    PhaseMatrixId = phaseId,
                    PhaseMatrix = new()
                    {
                        Id = phaseId,
                        Name = $"current_{taskType}_{phase}",
                        Phase = phase.ToString(),
                        Active = matrix.Active,
                        LowestInputState = matrix.LowestInputState,
                        LowestStartState = matrix.LowestStartedState,
                        LowestEndState = matrix.LowestEndState,
                        DerivedStates = matrix.DerivedStates
                            .Where(entry => entry.Key != entry.Value)
                            .Select(entry => new StateMatrixDerivedState
                            {
                                FromStateId = entry.Key,
                                DerivedStateId = entry.Value
                            }).ToList(),
                        TransitionGroups =
                        [
                            new()
                            {
                                TransitionGroupId = transitionGroupId,
                                TransitionGroup = transitionGroup
                            }
                        ]
                    }
                });
            }
            return [configuration];
        }
    }
}

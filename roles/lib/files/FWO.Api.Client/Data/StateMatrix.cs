using FWO.Api.Client.Queries;
using FWO.Api.Data;
using System.Text.Json.Serialization; 
using Newtonsoft.Json; 

namespace FWO.Api.Client
{
    public enum WorkflowPhases
    {
        request = 0,
        approval = 1,
        planning = 2,
        verification = 3,
        implementation = 4,
        review = 5,
        recertification = 6
    }

    public class StateMatrix
    {
        [JsonProperty("matrix"), JsonPropertyName("matrix")]
        public Dictionary<int, List<int>> Matrix { get; set; } = new Dictionary<int, List<int>>();

        [JsonProperty("derived_states"), JsonPropertyName("derived_states")]
        public Dictionary<int, int> DerivedStates { get; set; } = new Dictionary<int, int>();

        [JsonProperty("lowest_input_state"), JsonPropertyName("lowest_input_state")]
        public int LowestInputState { get; set; }

        [JsonProperty("lowest_start_state"), JsonPropertyName("lowest_start_state")]
        public int LowestStartedState { get; set; }

        [JsonProperty("lowest_end_state"), JsonPropertyName("lowest_end_state")]
        public int LowestEndState { get; set; }

        [JsonProperty("active"), JsonPropertyName("active")]
        public bool Active { get; set; }

        public Dictionary<WorkflowPhases, bool> PhaseActive = new Dictionary<WorkflowPhases, bool>();
        public bool IsLastActivePhase = true;
        public int MinImplTasksNeeded;

        public async Task Init(WorkflowPhases phase, ApiConnection apiConnection, TaskType taskType = TaskType.access)
        {
            GlobalStateMatrix glbStateMatrix = new GlobalStateMatrix();
            await glbStateMatrix.Init(apiConnection, taskType);
            Matrix = glbStateMatrix.GlobalMatrix[phase].Matrix;
            DerivedStates = glbStateMatrix.GlobalMatrix[phase].DerivedStates;
            LowestInputState = glbStateMatrix.GlobalMatrix[phase].LowestInputState;
            LowestStartedState = glbStateMatrix.GlobalMatrix[phase].LowestStartedState;
            LowestEndState = glbStateMatrix.GlobalMatrix[phase].LowestEndState;
            Active = glbStateMatrix.GlobalMatrix[phase].Active;
            foreach (var phas in glbStateMatrix.GlobalMatrix)
            {
                PhaseActive.Add(phas.Key, glbStateMatrix.GlobalMatrix[phas.Key].Active);
                if(glbStateMatrix.GlobalMatrix[phas.Key].Active && phas.Key > phase)
                {
                    IsLastActivePhase = false;
                }
            }
            MinImplTasksNeeded = glbStateMatrix.GlobalMatrix[WorkflowPhases.implementation].LowestInputState;
        }

        public List<int> getAllowedTransitions(int stateIn)
        {
            List<int> statesOut = new List<int>();
            if(Matrix.ContainsKey(stateIn))
            {
                statesOut = Matrix[stateIn];
            }
            return statesOut;
        }

        public int getDerivedStateFromSubStates(List<int> statesIn)
        {
            if(statesIn.Count == 0)
            {
                return 0;
            }
            int stateOut = 0;
            int initState = 0;
            int inWorkState = LowestEndState;
            int minFinishedState = 999;
            int openTasks = 0;
            int inWorkTasks = 0;
            int finishedTasks = 0;
            foreach(int state in statesIn)
            {
                if(state < LowestStartedState)
                {
                    openTasks++;
                    initState = state;
                }
                else if(state < LowestEndState)
                {
                    inWorkTasks++;
                    if(state < inWorkState)
                    {
                        inWorkState = state;
                    }
                }
                else
                {
                    finishedTasks++;
                    if(state < minFinishedState)
                    {
                        minFinishedState = state;
                    }
                }
            }
            if(inWorkTasks > 0)
            {
                stateOut = inWorkState;
            }
            else if(finishedTasks == statesIn.Count)
            {
                stateOut = minFinishedState;
            }
            else if(openTasks == statesIn.Count)
            {
                stateOut = initState;
            }
            else
            {
                stateOut = LowestStartedState;
            }
            if(DerivedStates.ContainsKey(stateOut))
            {
                return DerivedStates[stateOut];
            }
            return stateOut;
        }
    }
    public class GlobalStateMatrix
    {
        [JsonProperty("config_value"), JsonPropertyName("config_value")]
        public Dictionary<WorkflowPhases, StateMatrix> GlobalMatrix { get; set; } = new Dictionary<WorkflowPhases, StateMatrix>();


        public async Task Init(ApiConnection apiConnection, TaskType taskType = TaskType.master)
        {
            string matrixKey = taskType switch
            {
                TaskType.master => "reqMasterStateMatrix",
                TaskType.generic => "reqGenStateMatrix",
                TaskType.access => "reqAccStateMatrix",
                _ => throw new Exception($"Error: wrong task type:" + taskType.ToString()),
            };

            List<GlobalStateMatrixHelper> confData = await apiConnection.SendQueryAsync<List<GlobalStateMatrixHelper>>(ConfigQueries.getConfigItemByKey, new { key = matrixKey });
            GlobalStateMatrix glbStateMatrix = System.Text.Json.JsonSerializer.Deserialize<GlobalStateMatrix>(confData[0].ConfData) ?? throw new Exception("Config data could not be parsed.");
            GlobalMatrix = glbStateMatrix.GlobalMatrix;
        }
    }

    public class GlobalStateMatrixHelper
    {
        [JsonProperty("config_value"), JsonPropertyName("config_value")]
        public string ConfData = "";
    }

    public class StateMatrixDict
    {
        public Dictionary<string, StateMatrix> Matrices { get; set; } = new Dictionary<string, StateMatrix>();

        public async Task Init(WorkflowPhases phase, ApiConnection apiConnection)
        {
            Matrices = new Dictionary<string, StateMatrix>();
            foreach(TaskType taskType in Enum.GetValues(typeof(TaskType)))
            {
                Matrices.Add(taskType.ToString(), new StateMatrix());
                await Matrices[taskType.ToString()].Init(phase, apiConnection, taskType);
            }
        }
    }
}

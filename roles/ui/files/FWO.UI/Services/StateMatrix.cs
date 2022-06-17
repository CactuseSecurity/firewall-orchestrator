using FWO.Api.Client;
using FWO.Api.Client.Queries;
using System.Text.Json.Serialization; 
using Newtonsoft.Json; 

namespace FWO.Ui.Services
{
    public enum WorkflowPhases
    {
        request = 0,
        approval = 1,
        planning = 2,
        implementation = 4
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

        public async Task Init(WorkflowPhases phase, ApiConnection apiConnection)
        {
            GlobalStateMatrix glbStateMatrix = new GlobalStateMatrix();
            await glbStateMatrix.Init(apiConnection);
            Matrix = glbStateMatrix.GlobalMatrix[phase].Matrix;
            DerivedStates = glbStateMatrix.GlobalMatrix[phase].DerivedStates;
            LowestInputState = glbStateMatrix.GlobalMatrix[phase].LowestInputState;
            LowestStartedState = glbStateMatrix.GlobalMatrix[phase].LowestStartedState;
            LowestEndState = glbStateMatrix.GlobalMatrix[phase].LowestEndState;
            Active = glbStateMatrix.GlobalMatrix[phase].Active;
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

        public int getRequestStateFromTaskStates(List<int> statesIn)
        {
            if(statesIn.Count == 0)
            {
                return 0;
            }
            int stateOut = 0;
            int initState = 0;
            int inWorkState = LowestEndState;
            int maxState = 0;
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
                    if(state > maxState)
                    {
                        maxState = state;
                    }
                }
            }
            if(inWorkTasks > 0)
            {
                stateOut = inWorkState;
            }
            else if(finishedTasks == statesIn.Count)
            {
                stateOut = maxState;
            }
            else if(openTasks == statesIn.Count)
            {
                stateOut = initState;
            }
            else
            {
                stateOut = LowestStartedState;
            }
            return DerivedStates[stateOut];
        }
    }
    public class GlobalStateMatrix
    {
        [JsonProperty("config_value"), JsonPropertyName("config_value")]
        public Dictionary<WorkflowPhases, StateMatrix> GlobalMatrix { get; set; } = new Dictionary<WorkflowPhases, StateMatrix>();

        public async Task Init(ApiConnection apiConnection)
        {
            List<GlobalStateMatrixHelper> confData = await apiConnection.SendQueryAsync<List<GlobalStateMatrixHelper>>(ConfigQueries.getConfigItemByKey, new { key = "stateMatrix" });
            GlobalStateMatrix glbStateMatrix = System.Text.Json.JsonSerializer.Deserialize<GlobalStateMatrix>(confData[0].ConfData) ?? throw new Exception("Config data could not be parsed.");
            GlobalMatrix = glbStateMatrix.GlobalMatrix;
        }
    }

    public class GlobalStateMatrixHelper
    {
        [JsonProperty("config_value"), JsonPropertyName("config_value")]
        public string ConfData = "";
    }
}

using FWO.Api.Client;
using System.Text.Json.Serialization; 
using Newtonsoft.Json;


namespace FWO.Api.Data
{
    public class WfState
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("actions"), JsonPropertyName("actions")]
        public List<WfStateActionDataHelper> Actions { get; set; } = new ();


        public WfState(){}

        public WfState(WfState state)
        {
            Id = state.Id;
            Name = state.Name;
            Actions = state.Actions;
        }

        public string ActionList()
        {
            List<string> actionNames = new ();
            foreach(var action in Actions)
            {
                actionNames.Add(action.Action.Name);
            }
            return string.Join(", ", actionNames);
        }
    }

    public class WfStateDict
    {
        public Dictionary<int, string> Name = new ();

        public async Task Init(ApiConnection apiConnection)
        {
            List<WfState> states = await apiConnection.SendQueryAsync<List<WfState>>(Client.Queries.RequestQueries.getStates);
            Name = new ();
            foreach(var state in states)
            {
                Name.Add(state.Id, state.Name);
            }
        }
    }
}

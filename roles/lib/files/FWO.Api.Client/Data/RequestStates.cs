using FWO.Api.Client;
using System.Text.Json.Serialization; 
using Newtonsoft.Json;


namespace FWO.Api.Data
{
    public class RequestState
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("actions"), JsonPropertyName("actions")]
        public List<RequestStateActionDataHelper> Actions { get; set; } = new ();


        public RequestState(){}

        public RequestState(RequestState state)
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

    public class RequestStateDict
    {
        public Dictionary<int, string> Name = new ();

        public async Task Init(ApiConnection apiConnection)
        {
            List<RequestState> states = await apiConnection.SendQueryAsync<List<RequestState>>(FWO.Api.Client.Queries.RequestQueries.getStates);
            foreach(var state in states)
            {
                Name.Add(state.Id, state.Name);
            }
        }
    }
}

using FWO.Api.Client;
using System.Text.Json.Serialization; 
using Newtonsoft.Json;


namespace FWO.Ui.Services
{
    public class RequestState
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        public RequestState(){}

        public RequestState(RequestState state)
        {
            Id = state.Id;
            Name = state.Name;
        }
    }

    public class RequestStateDict
    {
        public Dictionary<int, string> Name = new Dictionary<int, string>();

        public async Task Init(ApiConnection apiConnection)
        {
            List<RequestState> states = await apiConnection.SendQueryAsync<List<RequestState>>(FWO.Api.Client.Queries.StmQueries.getStates);
            foreach(var state in states)
            {
                Name.Add(state.Id, state.Name);
            }
        }
    }
}

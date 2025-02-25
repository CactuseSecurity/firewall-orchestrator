using System.Text.Json.Serialization; 
using Newtonsoft.Json;


namespace FWO.Data.Workflow
{
    public class WfState
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("actions"), JsonPropertyName("actions")]
        public List<WfStateActionDataHelper> Actions { get; set; } = [];


        public WfState(){}

        public WfState(WfState state)
        {
            Id = state.Id;
            Name = state.Name;
            Actions = state.Actions;
        }

        public string ActionList()
        {
            List<string> actionNames = [];
            foreach(var action in Actions)
            {
                actionNames.Add(action.Action.Name);
            }
            return string.Join(", ", actionNames);
        }
    }
}

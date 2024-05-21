using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public enum StateActionTypes
    {
        DoNothing = 0,
        AutoPromote = 1,
        AddApproval = 2,
        SetAlert = 5,
        TrafficPathAnalysis = 6,
        ExternalCall = 10,
        SendEmail = 15,
        CreateConnection = 20,
        UpdateConnectionOwner = 21,
        UpdateConnectionRelease = 22,
        DisplayConnection = 23,
        UpdateConnectionReject = 24
        // CreateReport = 30
    }

    public enum StateActionEvents
    {
        None = 0,
        OnSet = 1,
        OnLeave = 2,
        // WhileSet = 3,
        OfferButton = 4,
        OwnerChange = 10,
        OnAssignment = 15
    }

    public class RequestStateAction
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("action_type"), JsonPropertyName("action_type")]
        public string ActionType { get; set; } = StateActionTypes.DoNothing.ToString();

        [JsonProperty("scope"), JsonPropertyName("scope")]
        public string Scope { get; set; } = RequestObjectScopes.None.ToString();

        [JsonProperty("task_type"), JsonPropertyName("task_type")]
        public string TaskType { get; set; } = "";

        [JsonProperty("phase"), JsonPropertyName("phase")]
        public string Phase { get; set; } = "";

        [JsonProperty("event"), JsonPropertyName("event")]
        public string? Event { get; set; } = StateActionEvents.None.ToString();

        [JsonProperty("button_text"), JsonPropertyName("button_text")]
        public string? ButtonText { get; set; } = "";

        [JsonProperty("external_parameters"), JsonPropertyName("external_parameters")]
        public string ExternalParams { get; set; } = "";


        public RequestStateAction()
        { }

        public static bool IsReadonlyType(string actionTypeString)
        {
            if( Enum.TryParse<StateActionTypes>(actionTypeString, out StateActionTypes actionType))
            {
                return actionType switch
                {
                    StateActionTypes.TrafficPathAnalysis => true,
                    StateActionTypes.DisplayConnection => true,
                    _ => false,
                };
            }
            return false;
        }
    }

    public class RequestStateActionDataHelper
    {
        [JsonProperty("action"), JsonPropertyName("action")]
        public RequestStateAction Action { get; set; } = new RequestStateAction();
    }
}

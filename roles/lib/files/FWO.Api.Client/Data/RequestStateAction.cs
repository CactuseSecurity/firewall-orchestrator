using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public enum ActionTypes
    {
        DoNothing = 0,
        AutoPromote = 1,
        SetAlert = 2,
        AddApproval = 3,
        ExternalCall = 10
    }

    public enum ActionScopes
    {
        None = 0,
        Ticket = 1,
        RequestTask = 2,
        ImplementationTask = 3,
        Approval = 4
    }

    public enum ActionEvents
    {
        None = 0,
        OnSet = 1,
        OnLeave = 2,
        // WhileSet = 3,
        OfferButton = 4
    }

    public class RequestStateAction
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("action_type"), JsonPropertyName("action_type")]
        public string ActionType { get; set; } = ActionTypes.DoNothing.ToString();

        [JsonProperty("scope"), JsonPropertyName("scope")]
        public string Scope { get; set; } = ActionScopes.None.ToString();

        [JsonProperty("task_type"), JsonPropertyName("task_type")]
        public string TaskType { get; set; } = "";

        [JsonProperty("phase"), JsonPropertyName("phase")]
        public string Phase { get; set; } = "";

        [JsonProperty("event"), JsonPropertyName("event")]
        public string? Event { get; set; } = ActionEvents.None.ToString();

        [JsonProperty("external_parameters"), JsonPropertyName("external_parameters")]
        public string ExternalParams { get; set; } = "";


        public RequestStateAction()
        { }
    }

    public class RequestStateActionDataHelper
    {
        [JsonProperty("action"), JsonPropertyName("action")]
        public RequestStateAction Action { get; set; } = new RequestStateAction();
    }
}

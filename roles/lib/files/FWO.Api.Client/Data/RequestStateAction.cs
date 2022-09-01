using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public enum ActionTypes
    {
        DoNothing = 0,
        Redirect = 1,
        AddApproval = 2,
        ExternalCall = 10
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
        public string? Scope { get; set; } = "";

        [JsonProperty("event"), JsonPropertyName("event")]
        public string? Event { get; set; } = "";

        [JsonProperty("external_parameters"), JsonPropertyName("external_parameters")]
        public string? ExternalParams { get; set; } = "";


        public RequestStateAction()
        { }
    }

    public class RequestStateActionDataHelper
    {
        [JsonProperty("action"), JsonPropertyName("action")]
        public RequestStateAction Action { get; set; } = new RequestStateAction();
    }
}

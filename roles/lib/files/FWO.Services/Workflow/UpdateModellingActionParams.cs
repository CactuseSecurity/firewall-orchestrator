using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Services.Workflow
{
    public class UpdateModellingActionParams
    {
        [JsonProperty("modelling_state"), JsonPropertyName("modelling_state")]
        public string ModellingState { get; set; } = "";

        [JsonProperty("confirm_ui_message"), JsonPropertyName("confirm_ui_message")]
        public bool ConfirmUiMessage { get; set; }

        public static UpdateModellingActionParams FromExternalParams(string externalParams)
        {
            return System.Text.Json.JsonSerializer.Deserialize<UpdateModellingActionParams>(externalParams) ?? new();
        }
    }
}

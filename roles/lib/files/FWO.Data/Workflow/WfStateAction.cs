using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace FWO.Data.Workflow
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

    public enum ToBeCalled
    {
        PolicyCheck = 1
    }

    public class WfStateAction
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("action_type"), JsonPropertyName("action_type")]
        public string ActionType { get; set; } = StateActionTypes.DoNothing.ToString();

        [JsonProperty("scope"), JsonPropertyName("scope")]
        public string Scope { get; set; } = WfObjectScopes.None.ToString();

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


        public WfStateAction()
        { }

        public static bool IsReadonlyType(string actionTypeString)
        {
            if (Enum.TryParse<StateActionTypes>(actionTypeString, out StateActionTypes actionType))
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

        public static bool TryParseAutoPromoteParams(string externalParams, out int? toStateId, out ConditionalAutoPromoteParams? conditionalParams)
        {
            toStateId = null;
            conditionalParams = null;

            if (string.IsNullOrWhiteSpace(externalParams))
            {
                return true;
            }

            if (int.TryParse(externalParams, out int parsedStateId))
            {
                toStateId = parsedStateId;
                return true;
            }

            try
            {
                conditionalParams = System.Text.Json.JsonSerializer.Deserialize<ConditionalAutoPromoteParams>(externalParams);
                return conditionalParams != null;
            }
            catch
            {
                return false;
            }
        }
    }

    public class ConditionalAutoPromoteParams
    {
        [Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
        [System.Text.Json.Serialization.JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonProperty("to_be_called"), JsonPropertyName("to_be_called")]
        public ToBeCalled ToBeCalled { get; set; } = ToBeCalled.PolicyCheck;

        [JsonProperty("policy_ids"), JsonPropertyName("policy_ids")]
        public List<int> PolicyIds { get; set; } = [];

        [JsonProperty("check_result_label"), JsonPropertyName("check_result_label")]
        public string CheckResultLabel { get; set; } = "";

        [JsonProperty("if_compliant_state"), JsonPropertyName("if_compliant_state")]
        public int IfCompliantState { get; set; }

        [JsonProperty("if_not_compliant_state"), JsonPropertyName("if_not_compliant_state")]
        public int IfNotCompliantState { get; set; }
    }

    public class WfStateActionDataHelper
    {
        [JsonProperty("action"), JsonPropertyName("action")]
        public WfStateAction Action { get; set; } = new WfStateAction();
    }
}

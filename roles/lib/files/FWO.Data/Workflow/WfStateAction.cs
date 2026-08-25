using System.Text.Json.Serialization;
using FWO.Logging;
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
        UpdateConnectionReject = 24,
        UpdateModelling = 25,
        // CreateReport = 30

        CreateFlow = 31,
        BundleTasks = 32
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

    public enum BundleTaskType
    {
        TwoOutOfThree = 1
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

        private List<WfStateActionStateHelper> _stateActions = [];

        [JsonProperty("state_actions"), JsonPropertyName("state_actions")]
        public List<WfStateActionStateHelper> StateActions
        {
            get => _stateActions;
            set => _stateActions = value ?? [];
        }

        public int StateCount => StateActions.Count;


        public WfStateAction()
        { }

        public WfStateAction(WfStateAction action)
        {
            Id = action.Id;
            Name = action.Name;
            ActionType = action.ActionType;
            Scope = action.Scope;
            TaskType = action.TaskType;
            Phase = action.Phase;
            Event = action.Event;
            ButtonText = action.ButtonText;
            ExternalParams = action.ExternalParams;
            StateActions = [.. action.StateActions.Select(stateAction => new WfStateActionStateHelper(stateAction))];
        }

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

    public class ActionResultStateParams
    {
        [JsonProperty("success_state"), JsonPropertyName("success_state")]
        public int? SuccessState { get; set; }

        [JsonProperty("error_state"), JsonPropertyName("error_state")]
        public int? ErrorState { get; set; }

        [JsonProperty("confirm_ui_message"), JsonPropertyName("confirm_ui_message")]
        public bool ConfirmUiMessage { get; set; }
    }

    public class BundleTasksActionParams
    {
        [JsonPropertyName("bundle_type")]
        public BundleTaskType BundleType { get; set; } = BundleTaskType.TwoOutOfThree;

        [JsonPropertyName("clean_zones")]
        public bool CleanZones { get; set; }

        [JsonPropertyName("policy_id")]
        public int? PolicyId { get; set; }

        private static readonly System.Text.Json.JsonSerializerOptions SerializerOptions = new()
        {
            Converters = { new JsonStringEnumConverter() }
        };

        public static BundleTasksActionParams FromExternalParams(string externalParams)
        {
            if (string.IsNullOrWhiteSpace(externalParams))
            {
                return new();
            }

            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<BundleTasksActionParams>(externalParams, SerializerOptions) ?? new();
            }
            catch (System.Text.Json.JsonException exception)
            {
                Log.WriteWarning("Bundle Tasks", $"Configured bundle task parameters are invalid JSON. Falling back to defaults. {exception.Message}");
                return new();
            }
        }

        public string ToExternalParams()
        {
            return System.Text.Json.JsonSerializer.Serialize(this, SerializerOptions);
        }
    }

    public class WfStateActionDataHelper
    {
        [JsonProperty("sort_order"), JsonPropertyName("sort_order")]
        public int SortOrder { get; set; }

        [JsonProperty("action"), JsonPropertyName("action")]
        public WfStateAction Action { get; set; } = new WfStateAction();
    }

    public class WfStateActionStateHelper
    {
        private WfState _state = new();

        [JsonProperty("sort_order"), JsonPropertyName("sort_order")]
        public int SortOrder { get; set; }

        [JsonProperty("state"), JsonPropertyName("state")]
        public WfState State
        {
            get => _state;
            set => _state = value ?? new WfState();
        }

        public WfStateActionStateHelper()
        { }

        public WfStateActionStateHelper(WfStateActionStateHelper stateActionStateHelper)
        {
            SortOrder = stateActionStateHelper.SortOrder;
            State = new WfState(stateActionStateHelper.State);
        }
    }
}

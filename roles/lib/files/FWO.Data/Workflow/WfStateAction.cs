using System.Text.Json;
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
        BundleTasks = 32,
        CreateImplTasks = 33,
        CreateExtRequests = 34
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
        public const int AutomaticStateId = -1;

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
                toStateId = parsedStateId == AutomaticStateId ? null : parsedStateId;
                return true;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(externalParams);
                if (document.RootElement.TryGetProperty("to_state_id", out JsonElement simpleState)
                    && simpleState.TryGetInt32(out int parsedSimpleStateId))
                {
                    if (parsedSimpleStateId == AutomaticStateId)
                    {
                        return true;
                    }
                    if (parsedSimpleStateId > 0)
                    {
                        toStateId = parsedSimpleStateId;
                        return true;
                    }
                }

                conditionalParams = System.Text.Json.JsonSerializer.Deserialize<ConditionalAutoPromoteParams>(externalParams);
                return conditionalParams != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Determines whether serialized action parameters request a confirmation message in the UI.
        /// </summary>
        /// <param name="externalParams">The serialized action parameters to inspect.</param>
        /// <returns><see langword="true"/> when <c>confirm_ui_message</c> is explicitly enabled; otherwise, <see langword="false"/>.</returns>
        public static bool HasConfirmUiMessage(string externalParams)
        {
            if (string.IsNullOrWhiteSpace(externalParams) || int.TryParse(externalParams, out _))
            {
                return false;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(externalParams);
                return document.RootElement.TryGetProperty("confirm_ui_message", out JsonElement value)
                    && value.ValueKind == JsonValueKind.True;
            }
            catch (System.Text.Json.JsonException)
            {
                return false;
            }
        }
    }

    public class ConditionalAutoPromoteParams
    {
        [JsonProperty("confirm_ui_message"), JsonPropertyName("confirm_ui_message")]
        public bool ConfirmUiMessage { get; set; }

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

    public class SimpleAutoPromoteParams
    {
        [JsonProperty("to_state_id"), JsonPropertyName("to_state_id")]
        public int ToStateId { get; set; }

        [JsonProperty("confirm_ui_message"), JsonPropertyName("confirm_ui_message")]
        public bool ConfirmUiMessage { get; set; }
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

        [JsonPropertyName("confirm_ui_message")]
        public bool ConfirmUiMessage { get; set; }

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

    /// <summary>
    /// Parameters of the state action "Create implementation tasks" (action type CreateImplTasks).
    /// </summary>
    public class CreateImplTasksActionParams
    {
        /// <summary>
        /// State the request tasks handed to an external ticket system are promoted to, as that system plans them
        /// itself. It should be the state the action "Create external requests" is attached to. Not set: the lowest
        /// input state of the implementation in the state matrix of each request task.
        /// </summary>
        [JsonPropertyName("external_ticket_system_target_state_id")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? ExternalTicketSystemTargetStateId { get; set; }

        /// <summary>
        /// Reads the parameters of an action; empty or invalid parameters give the defaults.
        /// </summary>
        /// <param name="externalParams">The external parameters stored with the action.</param>
        public static CreateImplTasksActionParams FromExternalParams(string externalParams)
        {
            if (string.IsNullOrWhiteSpace(externalParams))
            {
                return new();
            }

            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<CreateImplTasksActionParams>(externalParams) ?? new();
            }
            catch (System.Text.Json.JsonException exception)
            {
                Log.WriteWarning("Create Implementation Tasks", $"Configured action parameters are invalid JSON. Falling back to defaults. {exception.Message}");
                return new();
            }
        }

        /// <summary>The parameters as stored with the action.</summary>
        public string ToExternalParams()
        {
            return System.Text.Json.JsonSerializer.Serialize(this);
        }

        /// <summary>
        /// Whether the target state is not set, so the state matrix gives it, or exists and is not below the given
        /// state, so that a promoted request task no longer holds the ticket in a phase before the implementation.
        /// </summary>
        /// <param name="existingStateIds">Ids of all defined states.</param>
        /// <param name="requiredStateId">Lowest allowed target state, the lowest input state of the implementation.</param>
        public bool HasValidTargetState(IEnumerable<int> existingStateIds, int requiredStateId)
        {
            if (ExternalTicketSystemTargetStateId is not int targetStateId)
            {
                return true;
            }
            return targetStateId >= requiredStateId && existingStateIds.Contains(targetStateId);
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

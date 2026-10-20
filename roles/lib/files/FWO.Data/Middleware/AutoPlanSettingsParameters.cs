using System.Text.Json.Serialization;

namespace FWO.Data.Middleware
{
    /// <summary>
    /// Asks the middleware for the effective auto plan settings of gateways and managements
    /// (POST api/workflow/getAutoPlanSettings). The global settings are always returned.
    /// </summary>
    public class AutoPlanSettingsParameters
    {
        /// <summary>
        /// Gateways whose effective settings are requested.
        /// </summary>
        [JsonPropertyName("deviceIds")]
        public List<int> DeviceIds { get; set; } = [];

        /// <summary>
        /// Managements whose effective settings are requested.
        /// </summary>
        [JsonPropertyName("managementIds")]
        public List<int> ManagementIds { get; set; } = [];

        /// <summary>
        /// Paging options; the page size is left to the middleware default, which is its maximum.
        /// </summary>
        [JsonPropertyName("options")]
        public ImplTaskTargetPaging Options { get; set; } = new();
    }

    /// <summary>
    /// Scope types of the auto plan settings returned by the middleware.
    /// </summary>
    public static class AutoPlanScopeTypes
    {
        /// <summary>The global level, without id.</summary>
        public const string kGlobal = "global";

        /// <summary>A management, identified by its database id.</summary>
        public const string kManagement = "management";

        /// <summary>A gateway, identified by its database id.</summary>
        public const string kGateway = "gateway";
    }

    /// <summary>
    /// The effective auto plan settings of one level, inherited along the device specific settings hierarchy.
    /// </summary>
    public class AutoPlanScopeSettings
    {
        /// <summary>
        /// Scope type of the level, one of <see cref="AutoPlanScopeTypes"/>.
        /// </summary>
        [JsonPropertyName("scopeType")]
        public string ScopeType { get; set; } = AutoPlanScopeTypes.kGlobal;

        /// <summary>
        /// Database id of the management or gateway; null for the global level.
        /// </summary>
        [JsonPropertyName("id")]
        public int? Id { get; set; }

        /// <summary>
        /// Whether implementation tasks are created automatically for every task type.
        /// </summary>
        [JsonPropertyName("autoPlanImplementation")]
        public bool AutoPlanImplementation { get; set; }

        /// <summary>
        /// Whether implementation tasks of rule tasks are created automatically, only together with
        /// <see cref="AutoPlanImplementation"/>.
        /// </summary>
        [JsonPropertyName("autoPlanImplementationForRules")]
        public bool AutoPlanImplementationForRules { get; set; }

        /// <summary>
        /// Whether implementation tasks of the given kind of task are created automatically on this level.
        /// </summary>
        /// <param name="isRuleTask">True for access, rule_modify and rule_delete tasks.</param>
        public bool IsAutomatic(bool isRuleTask)
        {
            return AutoPlanImplementation && (!isRuleTask || AutoPlanImplementationForRules);
        }
    }

    /// <summary>
    /// Effective auto plan settings returned by the middleware.
    /// </summary>
    public class AutoPlanSettingsResult
    {
        /// <summary>
        /// The global settings first, then the requested managements and gateways that exist.
        /// </summary>
        [JsonPropertyName("settings")]
        public List<AutoPlanScopeSettings> Settings { get; set; } = [];
    }
}

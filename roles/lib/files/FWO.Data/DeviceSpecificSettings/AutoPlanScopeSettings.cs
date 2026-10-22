namespace FWO.Data.DeviceSpecificSettings
{
    /// <summary>
    /// Scope types of the resolved auto plan settings.
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
        public string ScopeType { get; set; } = AutoPlanScopeTypes.kGlobal;

        /// <summary>
        /// Database id of the management or gateway; null for the global level.
        /// </summary>
        public int? Id { get; set; }

        /// <summary>
        /// Whether implementation tasks are created automatically for every task type.
        /// </summary>
        public bool AutoPlanImplementation { get; set; }

        /// <summary>
        /// Whether implementation tasks of rule tasks are created automatically, only together with
        /// <see cref="AutoPlanImplementation"/>.
        /// </summary>
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
}

using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Workflow;
using FWO.Logging;
using FWO.Services.PathAnalysis;

namespace FWO.Services.Workflow
{
    /// <summary>
    /// Decides with the device specific settings autoPlanImplementation and autoPlanImplementationForRules which
    /// implementation tasks are created automatically, and determines the gateways of access tasks.
    /// </summary>
    public partial class WfHandler
    {
        private const string kAutoPlanLogTitle = "Auto Plan Implementation";

        /// <summary>
        /// Effective auto plan settings resolved in the current creation run, by scope type and id (null for global).
        /// </summary>
        private readonly Dictionary<(string ScopeType, int? Id), AutoPlanScopeSettings> autoPlanCache = [];

        /// <summary>
        /// Replaces the path analyzer selected by the configuration; used by tests.
        /// </summary>
        internal IPathAnalyzer? PathAnalyzerOverride { get; set; }

        /// <summary>
        /// No gateways or managements to resolve, only the global settings.
        /// </summary>
        private static readonly List<int> kNoIds = [];

        private static readonly List<string> kRuleTaskTypes =
            [WfTaskType.access.ToString(), WfTaskType.rule_modify.ToString(), WfTaskType.rule_delete.ToString()];

        /// <summary>
        /// Returns whether a task type orders a rule, so that autoPlanImplementationForRules applies to it.
        /// </summary>
        public static bool IsRuleTaskType(string taskType)
        {
            return kRuleTaskTypes.Contains(taskType);
        }

        private void ResetAutoPlanCache()
        {
            autoPlanCache.Clear();
        }

        /// <summary>
        /// Creates the implementation tasks of an access task: one per entered or found gateway whose settings allow the
        /// automatic creation, or one without gateway when all devices are selected and one task is wanted for them.
        /// When no gateway can be determined at all, the fallback task without gateway is created.
        /// </summary>
        /// <param name="reqTask">Access task to create the implementation tasks for.</param>
        private async Task AutoCreateAccessImplTasks(WfReqTask reqTask)
        {
            List<int?> targets = await DetermineAccessTargets(reqTask);
            // the fallback is only for a missing target, not for targets planned manually
            bool noTargetFound = targets.Count == 0;
            List<int> targetDeviceIds = [.. targets.OfType<int>()];
            await EnsureAutoPlanSettings(targetDeviceIds, kNoIds);
            targets = [.. targets.Where(deviceId => IsAutoPlanned(true, AutoPlanScopeTypes.kGateway, deviceId))];
            List<int> deviceIds = [.. targets.OfType<int>()];
            if (targets.Contains(null))
            {
                await CreateAccessImplTask(reqTask, null, false);
            }
            await CreateImplTasksForDevices(reqTask, deviceIds);
            if (noTargetFound && reqTask.ImplementationTasks.Count == 0)
            {
                await CreateFallbackImplTask(reqTask);
            }
        }

        /// <summary>
        /// Returns the gateways of an access task: entered devices take precedence over the path analysis algorithm.
        /// A null entry stands for one implementation task without gateway.
        /// </summary>
        private async Task<List<int?>> DetermineAccessTargets(WfReqTask reqTask)
        {
            if (reqTask.HasAllDevicesSelected() && userConfig.ReqDeviceInput == ReqDeviceInputOptions.oneTaskForAllDevices)
            {
                return [null];
            }
            if (reqTask.GetDeviceList().Count > 0)
            {
                return [.. reqTask.GetResolvedDeviceList(Devices).Distinct().Select(deviceId => (int?)deviceId)];
            }
            return [.. (await GetAlgorithmDeviceIds(reqTask)).Select(deviceId => (int?)deviceId)];
        }

        /// <summary>
        /// Runs the configured path analysis algorithm on the sources and destinations of the request task. A failing
        /// analysis is logged and finds no gateway, so the fallback applies instead of aborting the state change.
        /// </summary>
        private async Task<List<int>> GetAlgorithmDeviceIds(WfReqTask reqTask)
        {
            IPathAnalyzer? analyzer = CreatePathAnalyzer();
            if (analyzer == null)
            {
                Log.WriteWarning(kAutoPlanLogTitle, $"No path analysis possible for request task {reqTask.Id}: no configuration or API connection.");
                return [];
            }
            try
            {
                PathAnalysisRequest request = new()
                {
                    Sources = ReqElementIpRanges.ToRanges(reqTask.Elements, ElemFieldType.source),
                    Destinations = ReqElementIpRanges.ToRanges(reqTask.Elements, ElemFieldType.destination)
                };
                PathAnalysisResult result = await analyzer.AnalyzeAsync(request);
                return [.. result.Devices.Select(device => device.Id).Distinct()];
            }
            catch (Exception exception)
            {
                Log.WriteError(kAutoPlanLogTitle, $"Path analysis failed for request task {reqTask.Id}; no gateway found.", exception);
                return [];
            }
        }

        private IPathAnalyzer? CreatePathAnalyzer()
        {
            if (PathAnalyzerOverride != null)
            {
                return PathAnalyzerOverride;
            }
            if (apiConnection == null || userConfig.GlobalConfig == null)
            {
                return null;
            }
            return new PathAnalyzerFactory(apiConnection, userConfig.GlobalConfig).Create();
        }

        /// <summary>
        /// Creates the single implementation task of a rule_modify or rule_delete task if the settings of the gateway of
        /// the rule, or the global settings without gateway, allow the automatic creation.
        /// </summary>
        private async Task AutoCreateRuleChangeImplTask(WfReqTask reqTask)
        {
            int ruleDeviceId = reqTask.GetRuleDeviceId();
            int? deviceId = ruleDeviceId > 0 ? ruleDeviceId : null;
            List<int> ruleDeviceIds = deviceId == null ? [] : [deviceId.Value];
            await EnsureAutoPlanSettings(ruleDeviceIds, kNoIds);
            if (!IsAutoPlanned(true, AutoPlanScopeTypes.kGateway, deviceId))
            {
                return;
            }
            if (reqTask.ImplementationTasks.Count == 0)
            {
                await CreateGenericImplTask(reqTask);
            }
        }

        /// <summary>
        /// Creates the single implementation task of a task type without gateway or management, if the global settings
        /// allow the automatic creation.
        /// </summary>
        private async Task AutoCreateOtherImplTask(WfReqTask reqTask)
        {
            await EnsureAutoPlanSettings(kNoIds, kNoIds);
            if (!IsAutoPlanned(IsRuleTaskType(reqTask.TaskType), AutoPlanScopeTypes.kGlobal, null))
            {
                return;
            }
            if (reqTask.ImplementationTasks.Count == 0)
            {
                await CreateGenericImplTask(reqTask);
            }
        }

        /// <summary>
        /// Keeps the target managements of an object task whose settings allow the automatic creation.
        /// </summary>
        private async Task<List<Management>> FilterAutoPlannedManagements(List<Management> targets)
        {
            List<int> targetManagementIds = [.. targets.Select(target => target.Id)];
            await EnsureAutoPlanSettings(kNoIds, targetManagementIds);
            return [.. targets.Where(target => IsAutoPlanned(false, AutoPlanScopeTypes.kManagement, target.Id))];
        }

        /// <summary>
        /// Creates one implementation task without gateway or management when the automatic creation found no target,
        /// so that the ticket does not get stuck without implementation task. Only done when the global settings allow
        /// the automatic creation.
        /// </summary>
        private async Task CreateFallbackImplTask(WfReqTask reqTask)
        {
            await EnsureAutoPlanSettings(kNoIds, kNoIds);
            if (!IsAutoPlanned(IsRuleTaskType(reqTask.TaskType), AutoPlanScopeTypes.kGlobal, null))
            {
                return;
            }
            Log.WriteWarning(kAutoPlanLogTitle, $"No target found for request task {reqTask.Id} ({reqTask.TaskType}); " +
                "creating one implementation task without gateway or management.");
            await CreateGenericImplTask(reqTask);
        }

        /// <summary>
        /// Returns whether implementation tasks are created automatically on a level. A level that was not resolved
        /// inherits the global settings; without any resolved settings the creation stays automatic as before.
        /// </summary>
        private bool IsAutoPlanned(bool isRuleTask, string scopeType, int? id)
        {
            if (id != null && autoPlanCache.TryGetValue((scopeType, id), out AutoPlanScopeSettings? settings))
            {
                return settings.IsAutomatic(isRuleTask);
            }
            if (autoPlanCache.TryGetValue((AutoPlanScopeTypes.kGlobal, null), out AutoPlanScopeSettings? globalSettings))
            {
                return globalSettings.IsAutomatic(isRuleTask);
            }
            return true;
        }

        /// <summary>
        /// Resolves the settings of the given gateways and managements not resolved yet in this run, together with the
        /// global settings. A failure is logged; the levels then count as automatic.
        /// </summary>
        private async Task EnsureAutoPlanSettings(List<int> deviceIds, List<int> managementIds)
        {
            List<int> missingDeviceIds = [.. deviceIds.Distinct().Where(id => !autoPlanCache.ContainsKey((AutoPlanScopeTypes.kGateway, id)))];
            List<int> missingManagementIds = [.. managementIds.Distinct().Where(id => !autoPlanCache.ContainsKey((AutoPlanScopeTypes.kManagement, id)))];
            if (missingDeviceIds.Count == 0 && missingManagementIds.Count == 0 && autoPlanCache.ContainsKey((AutoPlanScopeTypes.kGlobal, null)))
            {
                return;
            }
            try
            {
                List<AutoPlanScopeSettings>? settings = await ResolveAutoPlanSettings(missingDeviceIds, missingManagementIds);
                if (settings == null)
                {
                    Log.WriteWarning(kAutoPlanLogTitle, "Auto plan settings cannot be resolved here; implementation tasks are created automatically.");
                    return;
                }
                foreach (AutoPlanScopeSettings setting in settings)
                {
                    autoPlanCache[(setting.ScopeType, setting.Id)] = setting;
                }
            }
            catch (Exception exception)
            {
                Log.WriteError(kAutoPlanLogTitle, "Could not resolve the auto plan settings; implementation tasks are created automatically.", exception);
            }
        }

        /// <summary>
        /// Resolves the settings through the own api connection. Implementation tasks are created by the state action
        /// "Create implementation tasks", which runs in the middleware with the rights of the middleware server; the
        /// monitoring of an admin may read the settings as well.
        /// </summary>
        /// <returns>The settings, or null without api connection.</returns>
        private async Task<List<AutoPlanScopeSettings>?> ResolveAutoPlanSettings(List<int> deviceIds, List<int> managementIds)
        {
            if (apiConnection == null)
            {
                return null;
            }
            return await new ImplTaskAutoPlanResolver(apiConnection).Resolve(deviceIds, managementIds);
        }
    }
}

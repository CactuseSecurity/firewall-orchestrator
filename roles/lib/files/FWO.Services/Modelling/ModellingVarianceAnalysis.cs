using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Report;
using FWO.Data.Workflow;
using FWO.Logging;
using FWO.Services.Workflow;
using System.Diagnostics;
using System.Threading;
using System.Text.Json;


namespace FWO.Services.Modelling
{
    /// <summary>
    /// Variance Analysis Class
    /// </summary>
    public partial class ModellingVarianceAnalysis(ApiConnection apiConnection, ExtStateHandler extStateHandler,
            UserConfig userConfig, FwoOwner owner, Action<Exception?, string, string, bool> displayMessageInUi)
    {
        private readonly ModellingNamingConvention namingConvention = JsonSerializer.Deserialize<ModellingNamingConvention>(userConfig.ModNamingConvention) ?? new();
        private readonly RuleRecognitionOption ruleRecognitionOption = string.IsNullOrEmpty(userConfig.RuleRecognitionOption) ? new() :
            JsonSerializer.Deserialize<RuleRecognitionOption>(userConfig.RuleRecognitionOption) ?? new();
        private readonly ModellingAppZoneHandler AppZoneHandler = new(apiConnection, userConfig, owner, displayMessageInUi);
        private AppServerComparer appServerComparer = new(new());
        private List<Management> RelevantManagements { get; set; } = [];

        private List<WfReqTask> TaskList = [];
        private List<WfReqTask> AddAccessTaskList = [];
        private List<WfReqTask> ChangeAccessTaskList = [];
        private List<WfReqTask> DeleteAccessTaskList = [];
        private List<WfReqTask> DeleteObjectTasksList = [];
        private int taskNumber = 0;
        private List<WfReqElement> ReqElements = [];

        private ModellingVarianceResult varianceResult = new();

        private Dictionary<int, List<Rule>> allModelledRules = [];
        private readonly Dictionary<int, Dictionary<long, List<Rule>>> connectionsByConnId = [];
        private List<ModellingAppRole> allModelledAppRoles = [];

        private readonly Dictionary<int, List<ModellingAppRole>> allProdAppRoles = [];
        private readonly Dictionary<int, Dictionary<int, long>> allExistingAppServersHashes = [];
        private readonly Dictionary<int, List<ModellingAppServer>> alreadyCreatedAppServers = [];
        private List<ModellingConnection> DeletedConns = [];
        private List<ModellingNetworkArea> AllAreas = [];

        private readonly Dictionary<int, List<DeviceReport>> DeviceRules = [];
        private readonly AsyncLocal<string?> varianceTimingRunId = new();

        public ModellingAppZone? PlannedAppZoneDbUpdate { get; set; } = default;

        public async Task AnalyseConnsForStatus(List<ModellingConnection> connections)
        {
            connections = [.. connections.Where(x => !x.IsDocumentationOnly())];
            varianceResult = await AnalyseRulesVsModelledConnections(connections, new(), false);
            foreach (var conn in connections)
            {
                conn.AddProperty(ConState.VarianceChecked.ToString());
                if (varianceResult.ConnsNotImplemented.FirstOrDefault(c => c.Id == conn.Id) != null)
                {
                    conn.AddProperty(ConState.NotImplemented.ToString());
                }
                else
                {
                    conn.RemoveProperty(ConState.NotImplemented.ToString());
                }
                if (varianceResult.RuleDifferences.FirstOrDefault(c => c.ModelledConnection.Id == conn.Id) != null)
                {
                    conn.AddProperty(ConState.VarianceFound.ToString());
                }
                else
                {
                    conn.RemoveProperty(ConState.VarianceFound.ToString());
                }
            }
        }

        public async Task<bool> AnalyseConnsForStatusAsync(List<ModellingConnection> connections)
        {
            try
            {
                await AnalyseConnsForStatus(connections);
                foreach (var conn in connections)
                {
                    await UpdateConnectionStatus(conn);
                }
            }
            catch (Exception exc)
            {
                Log.WriteError("Analyse Conns For Status Async", $" Error: ", exc);
                return false;
            }
            return true;
        }

        public async Task<ModellingVarianceResult> AnalyseRulesVsModelledConnections(List<ModellingConnection> connections,
            ModellingFilter modellingFilter, bool fullAnalysis = true, bool ignoreGroups = false)
        {
            string? previousRunId = varianceTimingRunId.Value;
            varianceTimingRunId.Value = $"va-{Guid.NewGuid():N}".Substring(0, 11);
            Stopwatch totalTimer = Stopwatch.StartNew();
            Stopwatch phaseTimer = Stopwatch.StartNew();
            try
            {
                await InitManagements();
                phaseTimer.Stop();
                LogTiming("init managements", phaseTimer.ElapsedMilliseconds, $"mgmt_count={RelevantManagements.Count}");
                phaseTimer.Restart();
                await LoadAreas();
                phaseTimer.Stop();
                LogTiming("load areas", phaseTimer.ElapsedMilliseconds, $"area_count={AllAreas.Count}");
                varianceResult = new() { Managements = RelevantManagements };
                if (ruleRecognitionOption.NwSeparateGroupAnalysis && fullAnalysis && !ignoreGroups)
                {
                    phaseTimer.Restart();
                    await GetNwObjectsProductionState();
                    phaseTimer.Stop();
                    LogTiming("load production objects", phaseTimer.ElapsedMilliseconds);
                    phaseTimer.Restart();
                    PreAnalyseAllAppRoles(connections);
                    phaseTimer.Stop();
                    LogTiming("pre-analyse app roles", phaseTimer.ElapsedMilliseconds, $"modelled_app_roles={allModelledAppRoles.Count}");
                }
                phaseTimer.Restart();
                if (await GetModelledRulesProductionState(modellingFilter))
                {
                    phaseTimer.Stop();
                    LogTiming("load modelled rules production state", phaseTimer.ElapsedMilliseconds);

                    Dictionary<int, long> connTimes = [];
                    phaseTimer.Restart();
                    foreach (var conn in connections.Where(c => !c.IsInterface).OrderBy(c => c.Id))
                    {
                        Stopwatch connTimer = Stopwatch.StartNew();
                        AnalyseRules(conn, fullAnalysis);
                        connTimer.Stop();
                        connTimes[conn.Id] = connTimer.ElapsedMilliseconds;
                    }
                    phaseTimer.Stop();
                    LogTiming("analyse rules vs modelled connections", phaseTimer.ElapsedMilliseconds, $"conn_count={connTimes.Count}");
                    if (connTimes.Count > 0)
                    {
                        string slowest = string.Join(", ", connTimes.OrderByDescending(c => c.Value).Take(5).Select(c => $"{c.Key}:{c.Value}ms"));
                        LogTiming("top slow connections", connTimes.Values.Sum(), slowest);
                    }
                    if (modellingFilter.RulesForDeletedConns)
                    {
                        phaseTimer.Restart();
                        await GetRulesForDeletedConns([.. connections.Where(c => c.IsDocumentationOnly())]);
                        phaseTimer.Stop();
                        LogTiming("collect rules for deleted connections", phaseTimer.ElapsedMilliseconds);
                    }
                }
                varianceResult.DeviceRules = DeviceRules;
                totalTimer.Stop();
                LogTiming("variance total", totalTimer.ElapsedMilliseconds,
                    $"fullAnalysis={fullAnalysis}, ignoreGroups={ignoreGroups}, conn_count={connections.Count}");
                return varianceResult;
            }
            finally
            {
                varianceTimingRunId.Value = previousRunId;
            }
        }

        public async Task<List<WfReqTask>> AnalyseModelledConnectionsForRequest(List<ModellingConnection> connections)
        {
            appServerComparer = new(namingConvention);
            await InitManagements();
            await LoadAreas();
            await GetModelledRulesProductionState(new() { AnalyseRemainingRules = false });
            await GetNwObjectsProductionState();
            await GetDeletedConnections();

            TaskList = [];
            AddAccessTaskList = [];
            ChangeAccessTaskList = [];
            DeleteAccessTaskList = [];
            DeleteObjectTasksList = [];
            foreach (Management mgt in RelevantManagements)
            {
                await AnalyseAppZoneForRequest(mgt);
                foreach (var conn in connections.Where(c => !c.IsRequested && !c.IsDocumentationOnly()).OrderBy(c => c.Id))
                {
                    ReqElements = [];
                    AnalyseNetworkAreasForRequest(conn);
                    AnalyseAppRolesForRequest(conn, mgt);
                    AnalyseAppServersForRequest(conn);
                    AnalyseServiceGroupsForRequest(conn, mgt);
                    AnalyseServicesForRequest(conn);
                    if (ReqElements.Count > 0) // NOSONAR: populated via side effects in Analyse*ForRequest methods
                    {
                        AnalyseConnectionForRequest(mgt, conn);
                    }
                }
                AnalyseDeletedConnsForRequest(mgt, [.. connections.Where(c => c.IsDocumentationOnly())]);
            }
            TaskList.AddRange(AddAccessTaskList);
            TaskList.AddRange(ChangeAccessTaskList);
            TaskList.AddRange(DeleteAccessTaskList);
            TaskList.AddRange(DeleteObjectTasksList);
            taskNumber = 1;
            foreach (WfReqTask task in TaskList)
            {
                task.TaskNumber = taskNumber++;
                task.Owners = [new() { Owner = owner }];
                task.StateId = extStateHandler.GetInternalStateId(ExtStates.ExtReqInitialized) ?? 0;
            }
            return TaskList;
        }

        public async Task<string> GetSuccessfulRequestState()
        {
            try
            {
                List<TicketId> ticketIds = await apiConnection.SendQueryAsync<List<TicketId>>(ExtRequestQueries.getLatestTicketIds, new { ownerId = owner.Id });
                if (ticketIds.Count == 0)
                {
                    return userConfig.GetText("never_requested");
                }
                else
                {
                    foreach (var ticketId in ticketIds)
                    {
                        WfTicket? intTicket = await apiConnection.SendQueryAsync<WfTicket>(RequestQueries.getTicketById, new { ticketId.Id });
                        if (extStateHandler.IsDone(intTicket.StateId))
                        {
                            return $"{userConfig.GetText("last_successful")}: {intTicket.CreationDate.ToString("yyyy-MM-dd HH:mm:ss")}, {userConfig.GetText("implemented")}: {intTicket.CompletionDate?.ToString("yyyy-MM-dd HH:mm:ss")}, {intTicket.Requester?.Name}";
                        }
                    }
                    return userConfig.GetText("never_succ_req");
                }
            }
            catch (Exception exception)
            {
                Log.WriteError(userConfig.GetText("impl_state"), exception.Message);
            }
            return "";
        }

        public async Task<(bool, List<ModellingAppZone?>)> CheckExistingAppZone()
        {
            List<ModellingAppZone?> MgtsWithVariance = [];
            ModellingAppZone? modelledAppZone = await AppZoneHandler.GetExistingModelledAppZone();
            if (modelledAppZone != null)
            {
                AppZoneComparer appZoneComparer = new(namingConvention);
                foreach (var mgt in RelevantManagements)
                {
                    await CollectGroupObjects(mgt.Id);
                    ModellingAppRole? prodAppRole = ResolveProdAppRole(modelledAppZone, mgt);
                    if (prodAppRole == null)
                    {
                        MgtsWithVariance.Add(new() { ManagementName = mgt.Name });
                    }
                    else
                    {
                        ModellingAppZone prodAppZone = new(prodAppRole);
                        if (!appZoneComparer.Equals(prodAppZone, modelledAppZone))
                        {
                            prodAppZone.ManagementName = mgt.Name;
                            MgtsWithVariance.Add(prodAppZone);
                        }
                    }
                }
                return (true, MgtsWithVariance);
            }
            return (false, []);
        }

        private void PreAnalyseAllAppRoles(List<ModellingConnection> connections)
        {
            CollectModelledAppRoles(connections);
            foreach (Management mgt in RelevantManagements)
            {
                varianceResult.MissingAppRoles.Add(mgt.Id, []);
                varianceResult.DifferingAppRoles.Add(mgt.Id, []);
                foreach (var modelledAppRole in allModelledAppRoles)
                {
                    AnalyseAppRole(modelledAppRole, mgt);
                }
            }
            varianceResult.AppRoleStats.ModelledAppRolesCount = allModelledAppRoles.Count;
            varianceResult.AppRoleStats.AppRolesOk = allModelledAppRoles.Count(a => !a.IsMissing && !a.HasDifference);
            varianceResult.AppRoleStats.AppRolesMissingCount = allModelledAppRoles.Count(a => a.IsMissing);
            varianceResult.AppRoleStats.AppRolesDifferenceCount = allModelledAppRoles.Count(a => a.HasDifference);
        }

        private void CollectModelledAppRoles(List<ModellingConnection> connections)
        {
            AppRoleComparer appRoleComparer = new();
            foreach (var conn in connections.Where(c => !c.IsInterface).OrderBy(c => c.Id))
            {
                foreach (var modelledAppRole in ModellingAppRoleWrapper.Resolve(conn.SourceAppRoles))
                {
                    allModelledAppRoles.Add(modelledAppRole);
                }
                foreach (var modelledAppRole in ModellingAppRoleWrapper.Resolve(conn.DestinationAppRoles))
                {
                    allModelledAppRoles.Add(modelledAppRole);
                }
            }
            allModelledAppRoles = [.. allModelledAppRoles.Distinct(appRoleComparer).OrderBy(a => a.Id)];
        }

        private void AnalyseAppRole(ModellingAppRole modelledAppRole, Management mgt)
        {
            if (ResolveProdAppRole(modelledAppRole, mgt) == null)
            {
                modelledAppRole.IsMissing = true;
                varianceResult.MissingAppRoles[mgt.Id].Add(new(modelledAppRole) { ManagementName = mgt.Name });
            }
            else if (AppRoleChanged(modelledAppRole))
            {
                modelledAppRole.HasDifference = true;
                ModellingAppRole changedAppRole = new(modelledAppRole) { ManagementName = mgt.Name, SurplusAppServers = deletedAppServers };
                foreach (var appServer in changedAppRole.AppServers.Select(a => a.Content))
                {
                    appServer.NotImplemented = newAppServers.FirstOrDefault(a => appServerComparer.Equals(a.Content, appServer)) != null;
                }
                varianceResult.DifferingAppRoles[mgt.Id].Add(changedAppRole);
            }
        }

        private string ConstructComment(ModellingConnection conn)
        {
            string comment = userConfig.ModModelledMarker + conn.Id.ToString();
            if (conn.IsCommonService)
            {
                comment += ", ComSvc";
            }
            if (conn.ExtraConfigs.Count > 0 || conn.ExtraConfigsFromInterface.Count > 0)
            {
                comment += ", " + userConfig.GetText("impl_instructions") + ": " +
                    string.Join(", ", conn.ExtraConfigs.ConvertAll(x => x.Display()).Concat(conn.ExtraConfigsFromInterface.ConvertAll(x => x.Display())));
            }
            return comment;
        }

        private void LogTiming(string phase, long milliseconds, string details = "")
        {
            string runId = varianceTimingRunId.Value ?? "va-none";
            string message = $"[{runId}] {phase}: {milliseconds} ms{(string.IsNullOrWhiteSpace(details) ? "" : $" ({details})")}";
            Log.WriteDebug("Variance Timing", message);
            try
            {
                displayMessageInUi(null, "Variance timing", message, false);
            }
            catch
            {
                // Timing logging must never impact variance analysis execution.
            }
        }

        private async Task UpdateConnectionStatus(ModellingConnection conn)
        {
            try
            {
                var Variables = new
                {
                    id = conn.Id,
                    connProp = conn.Properties
                };
                await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.updateConnectionProperties, Variables);
            }
            catch (Exception exc)
            {
                Log.WriteError("Update Connection Properties", $"Could not change state for Connection {conn.Id}: ", exc);
            }
        }

        private async Task LoadAreas()
        {
            AllAreas = await apiConnection.SendQueryAsync<List<ModellingNetworkArea>>(ModellingQueries.getNwGroupObjects, new { grpType = (int)ModellingTypes.ModObjectType.NetworkArea });
        }
    }
}

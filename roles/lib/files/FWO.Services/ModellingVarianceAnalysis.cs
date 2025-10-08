using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Report;
using FWO.Data.Workflow;
using FWO.Logging;
using System.Text.Json; 


namespace FWO.Services
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
        private List<Management> RelevantManagements {get; set; } = [];

        private List<WfReqTask> TaskList = [];
        private List<WfReqTask> AddAccessTaskList = [];
        private List<WfReqTask> ChangeAccessTaskList = [];
        private List<WfReqTask> DeleteAccessTaskList = [];
        private List<WfReqTask> DeleteObjectTasksList = [];
        private int taskNumber = 0;
        private List<WfReqElement> elements = [];

        private ModellingVarianceResult varianceResult = new();
        
        private Dictionary<int, List<Rule>> allModelledRules = [];
        private List<ModellingAppRole> allModelledAppRoles = [];

        private readonly Dictionary<int, List<ModellingAppRole>> allProdAppRoles = [];
        private readonly Dictionary<int, Dictionary<int, long>> allExistingAppServersHashes = [];
        private readonly Dictionary<int, List<ModellingAppServer>> alreadyCreatedAppServers = [];
        private List<ModellingConnection> DeletedConns = [];

        public ModellingAppZone? PlannedAppZoneDbUpdate { get; set; } = default;

        public async Task AnalyseConnsForStatus(List<ModellingConnection> connections)
        {
            connections = [.. connections.Where(x => !x.IsDocumentationOnly())];
            varianceResult = await AnalyseRulesVsModelledConnections(connections, new(), false);
            foreach(var conn in connections)
            {
                conn.AddProperty(ConState.VarianceChecked.ToString());
                if(varianceResult.ConnsNotImplemented.FirstOrDefault(c => c.Id == conn.Id) != null)
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
                foreach(var conn in connections)
                {
                    await UpdateConnectionStatus(conn);
                }
            }
            catch(Exception exc)
            {
                Log.WriteError("Analyse Conns For Status Async", $" Error: ", exc);
                return false;
            }
            return true;
        }

        public async Task<ModellingVarianceResult> AnalyseRulesVsModelledConnections(List<ModellingConnection> connections,
            ModellingFilter modellingFilter, bool fullAnalysis = true, bool ignoreGroups = false)
        {
            await InitManagements();
            varianceResult = new() { Managements = RelevantManagements };
            if(ruleRecognitionOption.NwSeparateGroupAnalysis && fullAnalysis && !ignoreGroups)
            {
                await GetNwObjectsProductionState();
                PreAnalyseAllAppRoles(connections);
            }
            if (await GetModelledRulesProductionState(modellingFilter))
            {
                foreach (var conn in connections.Where(c => !c.IsInterface).OrderBy(c => c.Id))
                {
                    await AnalyseRules(conn, fullAnalysis);
                }
                if (modellingFilter.RulesForDeletedConns)
                {
                    await GetRulesForDeletedConns([.. connections.Where(c => c.IsDocumentationOnly())]);
                }
            }
            return varianceResult;
        }

        public async Task<List<WfReqTask>> AnalyseModelledConnectionsForRequest(List<ModellingConnection> connections)
        {
            appServerComparer = new (namingConvention);
            await InitManagements();
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
                await AnalyseAppZone(mgt);
                foreach (var conn in connections.Where(c => !c.IsRequested && !c.IsDocumentationOnly()).OrderBy(c => c.Id))
                {
                    elements = [];
                    AnalyseNetworkAreasForRequest(conn);
                    AnalyseAppRolesForRequest(conn, mgt);
                    AnalyseAppServersForRequest(conn);
                    AnalyseServiceGroupsForRequest(conn, mgt);
                    AnalyseServicesForRequest(conn);
                    if (elements.Count > 0)
                    {
                        await AnalyseConnectionForRequest(mgt, conn);
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
                List<TicketId> ticketIds = await apiConnection.SendQueryAsync<List<TicketId>>(ExtRequestQueries.getLatestTicketIds, new{ownerId = owner.Id});
                if(ticketIds.Count == 0)
                {
                    return userConfig.GetText("never_requested");
                }
                else
                {
                    foreach(var ticketId in ticketIds)
                    {
                        WfTicket? intTicket = await apiConnection.SendQueryAsync<WfTicket>(RequestQueries.getTicketById, new { ticketId.Id });
                        if(extStateHandler.IsDone(intTicket.StateId))
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
            AppRoleComparer appRoleComparer = new ();
            foreach(var conn in connections.Where(c => !c.IsInterface).OrderBy(c => c.Id))
            {
                foreach(var modelledAppRole in ModellingAppRoleWrapper.Resolve(conn.SourceAppRoles))
                {
                    allModelledAppRoles.Add(modelledAppRole);
                }
                foreach(var modelledAppRole in ModellingAppRoleWrapper.Resolve(conn.DestinationAppRoles))
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
            if(conn.IsCommonService)
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
            catch(Exception exc)
            {
                Log.WriteError("Update Connection Properties", $"Could not change state for Connection {conn.Id}: ", exc);
            }
        }
    }
}

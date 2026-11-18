using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Report;
using FWO.Data.Workflow;
using FWO.Logging;
using FWO.Services.Workflow;
using System.Text.Json;


namespace FWO.Services.Modelling
{
    /// <summary>
    /// Variance Analysis Class
    /// </summary>
    public partial class ModellingVarianceAnalysis(ApiConnection apiConnection, ExtStateHandler extStateHandler,
            UserConfig userConfig, FwoOwner owner, Action<Exception?, string, string, bool> displayMessageInUi)
    {
        private const int kTicketIdChunkSize = 500;
        private readonly ModellingNamingConvention namingConvention = ModellingNamingConvention.FromJson(userConfig.ModNamingConvention);
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
        private List<ModellingAppRole> allModelledAppRoles = [];

        private readonly Dictionary<int, List<ModellingAppRole>> allProdAppRoles = [];
        private readonly Dictionary<int, Dictionary<ModellingAppServer, ModellingAppServer>> allExistingAppServers = [];
        private readonly Dictionary<int, List<ModellingAppServer>> alreadyCreatedAppServers = [];
        private List<ModellingConnection> DeletedConns = [];
        private List<ModellingNetworkArea> AllAreas = [];

        private readonly Dictionary<int, List<DeviceReport>> DeviceRules = [];

        public ModellingAppZone? PlannedAppZoneDbUpdate { get; set; } = default;

        /// <summary>
        /// Delay used while waiting for the rule_owner mapping job. Replaceable so tests do not have
        /// to wait in real time.
        /// </summary>
        public Func<TimeSpan, CancellationToken, Task> DelayAsync { get; set; } = Task.Delay;

        /// <summary>
        /// Cancels a wait for the rule_owner mapping job, for instance when the user navigates away.
        /// Callers without a token leave the default, which never cancels.
        /// </summary>
        public CancellationToken CancellationToken { get; set; }

        /// <summary>
        /// Writes a fall back from the rule_owner prefilter to the marker query into the database log.
        /// The background job turns it off: nobody waits for its result, and it would repeat the same
        /// global state for every owner in every run.
        /// </summary>
        public bool LogPrefilterFallbackToDb { get; set; } = true;

        /// <summary>
        /// Whether the mapping job was already waited for and the fallback already reported or
        /// logged. A caller that runs several owners for one request passes the same instance to
        /// each analysis, so these happen once per request instead of once per owner.
        /// </summary>
        public RuleOwnerWaitState WaitState { get; set; } = new();

        /// <summary>
        /// Determines the modelling state of the given connections. Which connections are analysed is
        /// decided by <see cref="ModellingVarianceConnectionSelector"/>, as for the report and the request.
        /// Excluded connections lose their variance flags, so no stale result is shown for them.
        /// </summary>
        /// <param name="connections">Connections to analyse, with used interfaces already extracted.</param>
        /// <param name="dummyAppRoleId">Id of the dummy app role, which never counts as empty.</param>
        /// <param name="allowWait">
        /// True where somebody waits for the result, so a pending rule_owner mapping run may be waited
        /// out instead of falling back to the much slower marker query. The background job leaves it
        /// false.
        /// </param>
        /// <param name="cancellationToken">Stops the whole analysis, for instance on middleware shutdown.</param>
        public async Task AnalyseConnsForStatus(List<ModellingConnection> connections, long dummyAppRoleId, bool allowWait = false, CancellationToken cancellationToken = default)
        {
            connections = [.. connections.Where(x => !x.IsDocumentationOnly())];
            ModellingVarianceConnectionSelection selection = ModellingVarianceConnectionSelector.Select(connections, dummyAppRoleId, userConfig.ModRolloutRemovedAppServers);
            varianceResult = await AnalyseRulesVsModelledConnections(selection.Analysed, new() { AllowWaitForRuleOwnerMapping = allowWait }, false, cancellationToken);
            await GetNwObjectsProductionState(cancellationToken);
            foreach (var conn in connections)
            {
                conn.UpdateProperty(ConState.ForeignNonProdObjects.ToString(), HasForeignNonProdObjects(conn));
                if (selection.IsExcluded(conn))
                {
                    conn.CleanUpVarianceResults();
                }
                else
                {
                    SetVarianceFlags(conn);
                }
            }
        }

        /// <summary>
        /// Writes the result of the last comparison into the state flags of an analysed connection.
        /// </summary>
        /// <param name="conn">Connection that was analysed.</param>
        private void SetVarianceFlags(ModellingConnection conn)
        {
            conn.AddProperty(ConState.VarianceChecked.ToString());
            conn.UpdateProperty(ConState.NotImplemented.ToString(), varianceResult.ConnsNotImplemented.Any(c => c.Id == conn.Id));
            conn.UpdateProperty(ConState.VarianceFound.ToString(), varianceResult.RuleDifferences.Any(c => c.ModelledConnection.Id == conn.Id));
        }

        private bool HasForeignNonProdObjects(ModellingConnection conn)
        {
            if (!userConfig.ModRequestOnlyOwnObjects || conn.IsInterface || conn.UsedInterfaceId == null)
            {
                return false;
            }

            foreach (Management mgt in RelevantManagements)
            {
                if (HasForeignNonProdObjects(conn, mgt))
                {
                    return true;
                }
            }
            return false;
        }

        private bool HasForeignNonProdObjects(ModellingConnection conn, Management mgt)
        {
            List<ModellingAppRole> foreignAppRoles =
            [
                .. ModellingAppRoleWrapper.Resolve(conn.SourceAppRoles).Where(ar => ar.AppId != owner.Id),
                .. ModellingAppRoleWrapper.Resolve(conn.DestinationAppRoles).Where(ar => ar.AppId != owner.Id)
            ];

            foreach (ModellingAppRole foreignAppRole in foreignAppRoles)
            {
                if (ResolveProdAppRole(foreignAppRole, mgt) == null)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Determines the modelling state of the given connections and writes it to the database.
        /// </summary>
        /// <param name="connections">Connections to analyse, with used interfaces already extracted.</param>
        /// <param name="dummyAppRoleId">Id of the dummy app role, which never counts as empty.</param>
        /// <param name="cancellationToken">Stops the whole analysis, for instance on middleware shutdown.</param>
        /// <returns>False if the analysis failed.</returns>
        public async Task<bool> AnalyseConnsForStatusAsync(List<ModellingConnection> connections, long dummyAppRoleId, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await AnalyseConnsForStatus(connections, dummyAppRoleId, cancellationToken: cancellationToken);
                // a stopped analysis has incomplete results, which must not be written as connection status
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var conn in connections)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await UpdateConnectionStatus(conn);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exc)
            {
                Log.WriteError("Analyse Conns For Status Async", $" Error: ", exc);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Compares the given connections with the production rules. The comparison is the same for
        /// every caller; documentation-only connections are not compared but regarded as deleted.
        /// </summary>
        /// <param name="connections">Connections to analyse, as selected by <see cref="ModellingVarianceConnectionSelector"/>.</param>
        /// <param name="modellingFilter">Options for loading the rules and the additional report chapters.</param>
        /// <param name="analyseAppRoles">
        /// Also compares the app role members separately (only with separate group analysis), as the
        /// report shows them in an own chapter.
        /// </param>
        /// <param name="cancellationToken">Stops the whole analysis.</param>
        public async Task<ModellingVarianceResult> AnalyseRulesVsModelledConnections(List<ModellingConnection> connections,
            ModellingFilter modellingFilter, bool analyseAppRoles = true, CancellationToken cancellationToken = default)
        {
            await InitManagements();
            await LoadAreas();
            varianceResult = new() { Managements = RelevantManagements };
            List<ModellingConnection> connectionsToCompare = [.. connections.Where(c => !c.IsInterface && !c.IsDocumentationOnly()).OrderBy(c => c.Id)];
            if (ruleRecognitionOption.NwSeparateGroupAnalysis && analyseAppRoles)
            {
                await LoadManagementSettings();
                await GetNwObjectsProductionState(cancellationToken);
                PreAnalyseAllAppRoles(connectionsToCompare);
            }
            if (await GetModelledRulesProductionState(modellingFilter, cancellationToken))
            {
                foreach (var conn in connectionsToCompare)
                {
                    AnalyseRules(conn);
                }
                if (modellingFilter.RulesForDeletedConns)
                {
                    await GetRulesForDeletedConns([.. connections.Where(c => c.IsDocumentationOnly())]);
                }
            }
            varianceResult.DeviceRules = DeviceRules;
            return varianceResult;
        }

        /// <summary>
        /// Builds the request tasks needed to implement the given connections.
        /// </summary>
        /// <param name="connections">Connections to request.</param>
        /// <param name="allowWait">
        /// True where somebody waits for the result, see <see cref="AnalyseConnsForStatus"/>.
        /// </param>
        /// <param name="cancellationToken">Stops the whole analysis, see <see cref="AnalyseConnsForStatus"/>.</param>
        public async Task<List<WfReqTask>> AnalyseModelledConnectionsForRequest(List<ModellingConnection> connections, bool allowWait = false, CancellationToken cancellationToken = default)
        {
            appServerComparer = new(namingConvention);
            await InitManagements();
            await LoadManagementSettings();
            await LoadAreas();
            await GetModelledRulesProductionState(new() { AnalyseRemainingRules = false, AllowWaitForRuleOwnerMapping = allowWait }, cancellationToken);
            await GetNwObjectsProductionState(cancellationToken);
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
                    if (!conn.IsInterface && ReqElements.Count > 0) // NOSONAR: populated via side effects in Analyse*ForRequest methods
                    {
                        AnalyseConnectionForRequest(mgt, conn);
                    }
                }
                AnalyseDeletedConnsForRequest(mgt, [.. connections.Where(c => c.IsDocumentationOnly())]);
                AnalyseNetworkAreaLabelsForRequest(mgt, connections.Where(c => !c.IsRequested && !c.IsDocumentationOnly()));
                await AnalyseObsoleteLabelsForRequest(mgt);
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

        /// <summary>
        /// Describes the newest done request ticket of the owner. Reads ticket states in descending batches,
        /// without task metadata, and stops as soon as a batch contains a done ticket.
        /// </summary>
        public async Task<string> GetSuccessfulRequestState()
        {
            try
            {
                List<TicketId> ticketIds = await apiConnection.SendQueryAsync<List<TicketId>>(ExtRequestQueries.getLatestTicketIds, new { ownerId = owner.Id });
                if (ticketIds.Count == 0)
                {
                    return userConfig.GetText("never_requested");
                }
                List<long> distinctTicketIds = [.. ticketIds.Select(ticketId => ticketId.Id).Distinct()];
                // getLatestTicketIds returns the newest ticket first.
                foreach (IEnumerable<long> ticketIdChunk in distinctTicketIds.Chunk(kTicketIdChunkSize))
                {
                    List<WfTicket> tickets = await apiConnection.SendQueryAsync<List<WfTicket>>(
                        RequestQueries.getTicketStatesByIds, new { ids = ticketIdChunk });
                    WfTicket? newestDoneTicket = tickets.OrderByDescending(ticket => ticket.Id)
                        .FirstOrDefault(ticket => extStateHandler.IsDone(ticket.StateId));
                    if (newestDoneTicket != null)
                    {
                        return $"{userConfig.GetText("last_successful")}: {newestDoneTicket.CreationDate.ToString("yyyy-MM-dd HH:mm:ss")}, {userConfig.GetText("implemented")}: {newestDoneTicket.CompletionDate?.ToString("yyyy-MM-dd HH:mm:ss")}, {newestDoneTicket.Requester?.Name}";
                    }
                }
                return userConfig.GetText("never_succ_req");
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
                    ModellingAppZone? variance = AppZoneVariance(modelledAppZone, mgt, appZoneComparer);
                    if (variance != null)
                    {
                        MgtsWithVariance.Add(variance);
                    }
                }
                return (true, MgtsWithVariance);
            }
            return (false, []);
        }

        /// <summary>
        /// Compares the modelled app zone with the one of a management: label managements compare by addresses, others
        /// with the app zone comparer.
        /// </summary>
        /// <returns>The app zone of the management (empty if it is missing) if it differs, else null.</returns>
        private ModellingAppZone? AppZoneVariance(ModellingAppZone modelledAppZone, Management mgt, AppZoneComparer appZoneComparer)
        {
            ModellingAppRole? prodAppRole = ResolveProdAppRole(modelledAppZone, mgt);
            if (prodAppRole == null)
            {
                return new() { ManagementName = mgt.Name };
            }
            if (WritesLabels(mgt))
            {
                return LabelMembersChanged(modelledAppZone.AppServers, prodAppRole) ? new(prodAppRole) { ManagementName = mgt.Name } : null;
            }
            ModellingAppZone prodAppZone = new(prodAppRole);
            if (appZoneComparer.Equals(prodAppZone, modelledAppZone))
            {
                return null;
            }
            prodAppZone.ManagementName = mgt.Name;
            return prodAppZone;
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
            if (IsExcluded(mgt, ModellingTypes.ModObjectType.AppRole))
            {
                return;
            }
            if (ResolveProdAppRole(modelledAppRole, mgt) == null)
            {
                modelledAppRole.IsMissing = true;
                varianceResult.MissingAppRoles[mgt.Id].Add(new(modelledAppRole) { ManagementName = mgt.Name });
            }
            else if (AppRoleChanged(modelledAppRole, mgt))
            {
                modelledAppRole.HasDifference = true;
                ModellingAppRole changedAppRole = new(modelledAppRole) { ManagementName = mgt.Name, SurplusAppServers = deletedAppServers };
                bool writesLabels = WritesLabels(mgt);
                foreach (var appServer in changedAppRole.AppServers.Select(a => a.Content))
                {
                    appServer.NotImplemented = newAppServers.FirstOrDefault(a => writesLabels
                        ? HasSameIpRange(a.Content, appServer)
                        : IsSameGroupMember(a.Content, appServer)) != null;
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

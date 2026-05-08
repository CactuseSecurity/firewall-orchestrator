using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.Logging;
using FWO.Services.Modelling;
using System.Globalization;

namespace FWO.Services.Workflow
{
    public partial class ActionHandler
    {
        public bool DisplayConnectionMode = false;
        public ModellingConnectionHandler? ConnHandler { get; set; }

        public async Task UpdateModelling(string modellingState, WfStatefulObject statefulObject, WfObjectScopes scope, long? ticketId)
        {
            string state = modellingState.Trim();
            string stateMarker = string.IsNullOrWhiteSpace(wfHandler.userConfig.ModIntegrationStateMarker)
                ? ModIntegrationStateConfig.DefaultMarker
                : wfHandler.userConfig.ModIntegrationStateMarker.Trim();
            Log.WriteDebug("UpdateModelling", $"Perform Action with state '{state}'.");
            if (string.IsNullOrWhiteSpace(state))
            {
                Log.WriteWarning("UpdateModelling", "No modelling state configured. Skipping modelling update action.");
                return;
            }
            string stateSetAt = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

            try
            {
                List<WfReqTask> scopedTasks = GetScopedRequestTasks(statefulObject, scope);
                await apiConnection.RunWithProperRole(wfHandler.AuthUser ?? throw new ArgumentException(NoAuthUser), [Roles.Modeller, Roles.Admin], async () =>
                {
                    await UpdateModellingConnections(stateMarker, state, stateSetAt, ticketId, scopedTasks);
                    await UpdateModellingGroups(stateMarker, state, stateSetAt, scopedTasks);
                });
            }
            catch (Exception exc)
            {
                Log.WriteError("Update Modelling", "Could not update requested modelling objects: ", exc);
            }
        }

        private List<WfReqTask> GetScopedRequestTasks(WfStatefulObject statefulObject, WfObjectScopes scope)
        {
            return scope switch
            {
                WfObjectScopes.Ticket when statefulObject is WfTicket ticket => [.. ticket.Tasks],
                WfObjectScopes.RequestTask when statefulObject is WfReqTask reqTask => [reqTask],
                WfObjectScopes.ImplementationTask when wfHandler.ActReqTask.Id > 0 => [wfHandler.ActReqTask],
                WfObjectScopes.Approval when wfHandler.ActReqTask.Id > 0 => [wfHandler.ActReqTask],
                _ when wfHandler.ActTicket.Tasks.Count > 0 => [.. wfHandler.ActTicket.Tasks],
                _ => []
            };
        }

        private async Task UpdateModellingConnections(string stateMarker, string state, string stateSetAt, long? ticketId, List<WfReqTask> scopedTasks)
        {
            HashSet<int> connectionIds = [.. scopedTasks
                .Where(task => task.TaskType == WfTaskType.access.ToString())
                .Select(task => task.GetAddInfoIntValue(AdditionalInfoKeys.ConnId))
                .Where(id => id != null)
                .Select(id => id ?? 0)];
            if (connectionIds.Count == 0)
            {
                return;
            }

            List<ModellingConnection> connections = [];
            if (ticketId != null)
            {
                connections = [.. (await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getConnectionsByTicketId, new { ticketId }))
                    .Where(connection => connectionIds.Contains(connection.Id))];
            }

            HashSet<int> loadedConnectionIds = [.. connections.Select(connection => connection.Id)];
            foreach (int connectionId in connectionIds.Except(loadedConnectionIds))
            {
                List<ModellingConnection> connectionById = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getConnectionById, new { id = connectionId });
                ModellingConnection? connection = connectionById.FirstOrDefault();
                if (connection != null)
                {
                    connections.Add(connection);
                }
            }

            foreach (ModellingConnection connection in connections.Where(connection => connection.Id > 0))
            {
                connection.RemoveProperty(stateMarker);
                connection.RemoveProperty(ModIntegrationStateConfig.TimestampMarker(stateMarker));
                connection.AddProperty(stateMarker, ModIntegrationStateConfig.BuildStateValue(state, stateSetAt));
                await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.updateConnectionProperties, new { id = connection.Id, connProp = connection.Properties });
            }
        }

        private async Task UpdateModellingGroups(string stateMarker, string state, string stateSetAt, List<WfReqTask> scopedTasks)
        {
            string markedState = $"{stateMarker}: {ModIntegrationStateConfig.BuildStateValue(state, stateSetAt)}";
            List<WfReqTask> groupTasks = [.. scopedTasks.Where(task => task.TaskType.StartsWith("group_", StringComparison.OrdinalIgnoreCase))];
            List<long> appRoleIds = [.. groupTasks
                .Select(task => task.GetAddInfoLongValue(AdditionalInfoKeys.AppRoleId))
                .Where(id => id != null)
                .Select(id => id ?? 0)
                .Distinct()];
            foreach (long appRoleId in appRoleIds)
            {
                ModellingAppRole appRole = await apiConnection.SendQueryAsync<ModellingAppRole>(ModellingQueries.getAppRoleById, new { id = appRoleId });
                string comment = UpdateMarkedComment(appRole?.Comment, stateMarker, markedState);
                await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.updateNwGroupComment, new { id = appRoleId, comment });
            }

            List<int> serviceGroupIds = [.. groupTasks
                .Select(task => task.GetAddInfoIntValue(AdditionalInfoKeys.SvcGrpId))
                .Where(id => id != null)
                .Select(id => id ?? 0)
                .Distinct()];
            foreach (int serviceGroupId in serviceGroupIds)
            {
                ModellingServiceGroup serviceGroup = await apiConnection.SendQueryAsync<ModellingServiceGroup>(ModellingQueries.getServiceGroupById, new { id = serviceGroupId });
                string comment = UpdateMarkedComment(serviceGroup?.Comment, stateMarker, markedState);
                await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.updateServiceGroupComment, new { id = serviceGroupId, comment });
            }
        }

        private static string UpdateMarkedComment(string? existingComment, string stateMarker, string markedState)
        {
            if (string.IsNullOrWhiteSpace(existingComment))
            {
                return markedState;
            }

            string markerPrefix = $"{ModIntegrationStateConfig.EffectiveMarker(stateMarker)}:";
            List<string> lines = [.. existingComment.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')];
            List<string> updatedLines = [];
            bool markerReplaced = false;

            foreach (string line in lines)
            {
                string trimmedLine = line.TrimStart();
                if (trimmedLine.StartsWith(markerPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    if (!markerReplaced)
                    {
                        updatedLines.Add(markedState);
                        markerReplaced = true;
                    }
                    continue;
                }
                updatedLines.Add(line);
            }

            if (!markerReplaced)
            {
                updatedLines.Add(markedState);
            }
            return string.Join(Environment.NewLine, updatedLines);
        }

        public async Task UpdateConnectionOwner(FwoOwner? owner, long? ticketId)
        {
            Log.WriteDebug("UpdateConnectionOwner", "Perform Action");
            try
            {
                if (owner != null && ticketId != null) // todo: role check
                {
                    await apiConnection.RunWithProperRole(wfHandler.AuthUser ?? throw new ArgumentException(NoAuthUser), [Roles.Modeller, Roles.Admin], async () =>
                    {
                        List<ModellingConnection> Connections = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getConnectionsByTicketId, new { ticketId });
                        foreach (var conn in Connections)
                        {
                            if (conn.IsRequested)
                            {
                                var Variables = new
                                {
                                    id = conn.Id,
                                    propAppId = owner.Id
                                };
                                await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.updateProposedConnectionOwner, Variables);
                                await ModellingHandlerBase.LogChange(new LogChangeRequest
                                {
                                    ChangeType = ModellingTypes.ChangeType.Update,
                                    ObjectType = ModellingTypes.ModObjectType.Connection,
                                    ObjectId = conn.Id,
                                    Text = $"Updated {(conn.IsInterface ? "Interface" : "Connection")}: {conn.Name}",
                                    ApiConnection = apiConnection,
                                    UserConfig = wfHandler.userConfig,
                                    ApplicationId = owner.Id,
                                    DisplayMessageInUi = DefaultInit.DoNothing
                                });
                            }
                        }
                    });
                }
            }
            catch (Exception exc)
            {
                Log.WriteError("Update Connection Owner", $"Could not change owner: ", exc);
            }
        }

        public async Task UpdateConnectionPublish(FwoOwner? owner, long? ticketId)
        {
            Log.WriteDebug("UpdateConnectionPublish", "Perform Action");
            try
            {
                if (owner != null && ticketId != null) // todo: role check
                {
                    await apiConnection.RunWithProperRole(wfHandler.AuthUser ?? throw new ArgumentException(NoAuthUser), [Roles.Modeller, Roles.Admin], async () =>
                    {
                        List<ModellingConnection> Connections = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getConnectionsByTicketId, new { ticketId });
                        foreach (var conn in Connections)
                        {
                            if (conn.IsRequested && !conn.IsPublished)
                            {
                                await PublishInterface(conn, owner);
                            }
                        }
                    });
                }
            }
            catch (Exception exc)
            {
                Log.WriteError("Update Connection Publish", $"Could not publish connection: ", exc);
            }
        }

        private async Task PublishInterface(ModellingConnection conn, FwoOwner owner)
        {
            if (conn.AppId == null && conn.ProposedAppId != null)
            {
                conn.AppId = conn.ProposedAppId;
                conn.ProposedAppId = null;
            }
            var Variables = new
            {
                id = conn.Id,
                isRequested = false,
                isPublished = true,
                appId = conn.AppId,
                proposedAppId = conn.ProposedAppId
            };
            await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.updateConnectionPublish, Variables);
            await ModellingHandlerBase.LogChange(new LogChangeRequest
            {
                ChangeType = ModellingTypes.ChangeType.Publish,
                ObjectType = ModellingTypes.ModObjectType.Connection,
                ObjectId = conn.Id,
                Text = $"Published {(conn.IsInterface ? "Interface" : "Connection")}: {conn.Name}",
                ApiConnection = apiConnection,
                UserConfig = wfHandler.userConfig,
                ApplicationId = owner.Id,
                DisplayMessageInUi = DefaultInit.DoNothing
            });
        }

        public async Task UpdateConnectionReject(FwoOwner? owner, long? ticketId)
        {
            Log.WriteDebug("UpdateConnectionReject", "Perform Action");
            try
            {
                if (owner != null && ticketId != null)
                {
                    await apiConnection.RunWithProperRole(wfHandler.AuthUser ?? throw new ArgumentException(NoAuthUser), [Roles.Modeller, Roles.Admin], async () =>
                    {
                        List<ModellingConnection> Connections = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getConnectionsByTicketId, new { ticketId });
                        foreach (var conn in Connections)
                        {
                            if (conn.IsRequested)
                            {
                                conn.AddProperty(ConState.Rejected.ToString());
                                var Variables = new
                                {
                                    id = conn.Id,
                                    connProp = conn.Properties
                                };
                                await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.updateConnectionProperties, Variables);
                                await ModellingHandlerBase.LogChange(new LogChangeRequest
                                {
                                    ChangeType = ModellingTypes.ChangeType.Reject,
                                    ObjectType = ModellingTypes.ModObjectType.Connection,
                                    ObjectId = conn.Id,
                                    Text = $"Rejected {(conn.IsInterface ? "Interface" : "Connection")}: {conn.Name}",
                                    ApiConnection = apiConnection,
                                    UserConfig = wfHandler.userConfig,
                                    ApplicationId = owner.Id,
                                    DisplayMessageInUi = DefaultInit.DoNothing
                                });
                            }
                        }
                    });
                }
            }
            catch (Exception exc)
            {
                Log.WriteError("Reject Connection", $"Could not change state: ", exc);
            }
        }

        public async Task DisplayConnection(WfStatefulObject statefulObject, WfObjectScopes scope)
        {
            try
            {
                Log.WriteDebug("DisplayConnection", "Perform Action");
                await SetScope(statefulObject, scope);
                WfReqTask? reqTask = wfHandler.ActTicket.Tasks.FirstOrDefault(x => x.TaskType == WfTaskType.new_interface.ToString());
                if (reqTask != null)
                {
                    wfHandler.SetReqTaskEnv(reqTask);
                }
                FwoOwner? owner = wfHandler.ActReqTask.Owners?.FirstOrDefault()?.Owner;
                if (owner != null && wfHandler.ActReqTask.GetAddInfoIntValue(AdditionalInfoKeys.ConnId) != null)
                {
                    await apiConnection.RunWithProperRole(wfHandler.AuthUser ?? throw new ArgumentException(NoAuthUser), [Roles.Modeller, Roles.Admin, Roles.Auditor], async () =>
                    {
                        List<ModellingConnection> Connections = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getConnections, new { appId = owner.Id });
                        ModellingConnection? conn = Connections.FirstOrDefault(c => c.Id == wfHandler.ActReqTask.GetAddInfoIntValue(AdditionalInfoKeys.ConnId));
                        if (conn != null)
                        {
                            ConnHandler = new ModellingConnectionHandler(apiConnection, wfHandler.userConfig, owner, Connections, conn, false, true, DefaultInit.DoNothing, DefaultInit.DoNothing, false);
                            await ConnHandler.Init();
                            DisplayConnectionMode = true;
                        }
                    });
                }
            }
            catch (Exception exc)
            {
                Log.WriteError("Display Connection", $"Could not display: ", exc);
            }
        }
    }
}

using FWO.Config.Api;
using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Logging;
using FWO.Api.Client.Data;

namespace FWO.Services
{
    public class ModellingVarianceAnalysis(ApiConnection apiConnection, ExtStateHandler extStateHandler, UserConfig userConfig, FwoOwner owner, Action<Exception?, string, string, bool> displayMessageInUi = null)
    {
        private ModellingNamingConvention namingConvention = System.Text.Json.JsonSerializer.Deserialize<ModellingNamingConvention>(userConfig.ModNamingConvention) ?? new();
        private ModellingAppZoneHandler AppZoneHandler = new(apiConnection, userConfig, owner, displayMessageInUi);
        private List<Management> managements = [];

        private List<WfReqTask> TaskList = [];
        private List<WfReqTask> AccessTaskList = [];
        private List<WfReqTask> DeleteTasksList = [];
        private int taskNumber = 0;
        private List<WfReqElement> elements = [];

        private Dictionary<int, List<ModellingAppRole>> allExistingAppRoles = [];
        private Dictionary<int, List<ModellingAppServer>> allExistingAppServers = [];
        private Dictionary<int, List<ModellingAppServer>> alreadyCreatedAppServers = [];

        private ModellingAppRole? existingApp;
        private List<ModellingAppServerWrapper> newAppServers = [];
        private List<ModellingAppServerWrapper> deletedAppServers = [];
        private List<ModellingAppServerWrapper> unchangedAppServers = [];
        private List<WfReqElement> newGroupMembers = [];
        private List<WfReqElement> newCreatedGroupMembers = [];
        private List<WfReqElement> deletedGroupMembers = [];
        private List<WfReqElement> unchangedGroupMembersDuringCreate = [];
        private List<WfReqElement> unchangedGroupMembers = [];

        public async Task<List<WfReqTask>> AnalyseModelledConnections(List<ModellingConnection> Connections)
        {
            // later: get rules + compare, bundle requests
            managements = await apiConnection.SendQueryAsync<List<Management>>(DeviceQueries.getManagementNames);
            managements = managements.Where(m => !string.IsNullOrEmpty(m.ExtMgtData)).ToList();
            foreach (Management mgt in managements)
            {
                if (!alreadyCreatedAppServers.ContainsKey(mgt.Id))
                {
                    alreadyCreatedAppServers.Add(mgt.Id, []);
                }
            }

            await GetProductionState();

            TaskList = [];
            AccessTaskList = [];
            DeleteTasksList = [];
            foreach (ModellingConnection? conn in Connections.Where(c => !c.IsRequested))
            {
                foreach (Management mgt in managements)
                {
                    elements = [];
                    AnalyseNetworkAreas(conn);
                    AnalyseAppRoles(conn, mgt);
                    AnalyseAppServers(conn);
                    AnalyseServiceGroups(conn, mgt);
                    AnalyseServices(conn);
                    await AnalyseAppZone(mgt);
                    if (elements.Count > 0)
                    {
                        Dictionary<string, string>? addInfo = new() { { AdditionalInfoKeys.ConnId, conn.Id.ToString() } };
                        AccessTaskList.Add(new()
                        {
                            Title = userConfig.GetText("new_connection") + ": " + conn.Name ?? "",
                            TaskType = WfTaskType.access.ToString(),
                            ManagementId = mgt.Id,
                            OnManagement = mgt,
                            Elements = elements,
                            RuleAction = 1,  // Todo ??
                            Tracking = 1,  // Todo ??
                            AdditionalInfo = System.Text.Json.JsonSerializer.Serialize(addInfo),
                            Comments = [new() { Comment = new() { CommentText = ConstructComment(conn) } }]
                        });
                    }
                }
            }
            TaskList.AddRange(AccessTaskList);
            TaskList.AddRange(DeleteTasksList);
            taskNumber = 1;
            foreach (WfReqTask task in TaskList)
            {
                task.TaskNumber = taskNumber++;
                task.Owners = [new() { Owner = owner }];
                task.StateId = extStateHandler.GetInternalStateId(ExtStates.ExtReqInitialized) ?? 0;
            }
            return TaskList;
        }

        private string ConstructComment(ModellingConnection conn)
        {
            string comment = "FWOC" + conn.Id.ToString();
            if (conn.ExtraConfigs.Count > 0)
            {
                comment += ", " + userConfig.GetText("impl_instructions") + ": " + string.Join(", ", conn.ExtraConfigs.ConvertAll(x => x.Display()));
            }
            return comment;
        }

        private async Task GetProductionState()
        {
            try
            {
                int aRCount = 0;
                int aSCount = 0;
                foreach (Management mgt in managements)
                {
                    List<NetworkObject>? objGrpByMgt = await GetObjects(mgt.Id, [2]);
                    if (objGrpByMgt != null)
                    {
                        foreach (NetworkObject objGrp in objGrpByMgt)
                        {
                            // Todo: filter for naming convention??
                            if (!allExistingAppRoles.ContainsKey(mgt.Id))
                            {
                                allExistingAppRoles.Add(mgt.Id, []);
                            }

                            allExistingAppRoles[mgt.Id].Add(new(objGrp, namingConvention));

                            aRCount++;
                        }
                    }

                    List<NetworkObject>? objByMgt = await GetObjects(mgt.Id, [1, 3, 12]);
                    if (objByMgt != null)
                    {
                        foreach (NetworkObject obj in objByMgt)
                        {
                            if (!allExistingAppServers.ContainsKey(mgt.Id))
                            {
                                allExistingAppServers.Add(mgt.Id, []);
                            }
                            allExistingAppServers[mgt.Id].Add(new(obj));
                            aSCount++;
                        }
                    }
                }

                string aRappRoles = "";
                string aRappServers = "";
                foreach (int mgt in allExistingAppRoles.Keys)
                {
                    aRappRoles += $" Management {mgt}: " + string.Join(",", allExistingAppRoles[mgt].Where(a => a.Name.StartsWith("AR")).ToList().ConvertAll(x => $"{x.Name}({x.IdString})").ToList());
                }
                foreach (int mgt in allExistingAppServers.Keys)
                {
                    aRappServers += $" Management {mgt}: " + string.Join(",", allExistingAppServers[mgt].ConvertAll(x => $"{x.Name}({x.Ip})").ToList());
                }

                Log.WriteDebug("GetProductionState",
                    $"Found {aRCount} AppRoles, {aSCount} AppServer. AppRoles with AR: {aRappRoles},  AppServers: {aRappServers}");
            }
            catch (Exception exception)
            {
                Log.WriteError(userConfig.GetText("fetch_data"), "Get Production State leads to error: ", exception);
            }
        }

        private async Task<List<NetworkObject>?> GetObjects(int mgtId, int[] objTypeIds)
        {
            var ObjGroupVariables = new
            {
                mgmId = mgtId,
                objTypeIds = objTypeIds
            };
            return await apiConnection.SendQueryAsync<List<NetworkObject>>(ObjectQueries.getNetworkObjectsForManagement, ObjGroupVariables);
        }

        private void AnalyseNetworkAreas(ModellingConnection conn)
        {
            foreach (ModellingNwGroup nwGroup in ModellingNwGroupWrapper.Resolve(conn.SourceNwGroups))
            {
                elements.Add(new()
                {
                    RequestAction = RequestAction.create.ToString(),
                    Field = ElemFieldType.source.ToString(),
                    GroupName = nwGroup.IdString
                });
            }
            foreach (ModellingNwGroup nwGroup in ModellingNwGroupWrapper.Resolve(conn.DestinationNwGroups))
            {
                elements.Add(new()
                {
                    RequestAction = RequestAction.create.ToString(),
                    Field = ElemFieldType.destination.ToString(),
                    GroupName = nwGroup.IdString
                });
            }
        }

        private void AnalyseAppRoles(ModellingConnection conn, Management mgt)
        {
            foreach (ModellingAppRole srcAppRole in ModellingAppRoleWrapper.Resolve(conn.SourceAppRoles))
            {
                AnalyseAppRole(srcAppRole, mgt, true);
            }
            foreach (ModellingAppRole dstAppRole in ModellingAppRoleWrapper.Resolve(conn.DestinationAppRoles))
            {
                AnalyseAppRole(dstAppRole, mgt);
            }
        }

        private void AnalyseAppRole(ModellingAppRole appRole, Management mgt, bool isSource = false)
        {
            if (!ResolveExistingApp(appRole, mgt))
            {
                if (TaskList.FirstOrDefault(x => x.Title == userConfig.GetText("new_app_role") + appRole.IdString && x.OnManagement?.Id == mgt.Id) == null)
                {
                    RequestNewApp(appRole, mgt);
                }
            }
            else if (AppChanged(appRole) &&
                TaskList.FirstOrDefault(x => x.Title == userConfig.GetText("update_app_role") + appRole.IdString + userConfig.GetText("add_members") && x.OnManagement?.Id == mgt.Id) == null &&
                DeleteTasksList.FirstOrDefault(x => x.Title == userConfig.GetText("update_app_role") + appRole.IdString + userConfig.GetText("remove_members") && x.OnManagement?.Id == mgt.Id) == null)
            {
                RequestUpdateApp(appRole, mgt);
            }

            elements.Add(new()
            {
                RequestAction = RequestAction.create.ToString(),
                Field = isSource ? ElemFieldType.source.ToString() : ElemFieldType.destination.ToString(),
                GroupName = appRole.IdString
            });
        }

        private async Task AnalyseAppZone(Management mgt)
        {
            if (!userConfig.CreateAppZones)
            {
                return;
            }

            ModellingAppZone? existingAppZone = await AppZoneHandler.GetExistingAppZone();

            if (existingAppZone is not null)
            {
                WfReqTask? taskEntryNewAppZone = TaskList.FirstOrDefault(x => x.Title == userConfig.GetText("new_app_zone") + existingAppZone.IdString && x.OnManagement?.Id == mgt.Id);
                WfReqTask? taskEntryUpdateAppZone = TaskList.FirstOrDefault(x => x.Title == userConfig.GetText("update_app_zone") + existingAppZone.IdString + userConfig.GetText("add_members") && x.OnManagement?.Id == mgt.Id);
                WfReqTask? taskEntryDeleteAppZone = DeleteTasksList.FirstOrDefault(x => x.Title == userConfig.GetText("update_app_zone") + existingAppZone.IdString + userConfig.GetText("remove_members") && x.OnManagement?.Id == mgt.Id);

                if (!ResolveExistingApp(existingAppZone, mgt) && taskEntryNewAppZone is null)
                {
                    RequestNewApp(existingAppZone, mgt);
                }
                else if (AppChanged(existingAppZone) && taskEntryUpdateAppZone is null && taskEntryDeleteAppZone is null)
                {
                    RequestUpdateApp(existingAppZone, mgt);
                }
            }
        }

        private bool ResolveExistingApp(ModellingNwGroup app, Management mgt)
        {
            string appType = "";

            if (app.GetType() == typeof(ModellingAppRole))
            {
                appType = "AppRole";
            }
            else if (app.GetType() == typeof(ModellingAppZone))
            {
                appType = "AppZone";
            }

            Log.WriteDebug($"Search {appType}", $"Name: {app.Name}, IdString: {app.IdString}, Management: {mgt.Name}");

            bool shortened = false;
            string sanitizedARName = Sanitizer.SanitizeJsonFieldMand(app.IdString, ref shortened);
            if (allExistingAppRoles.ContainsKey(mgt.Id))
            {
                existingApp = allExistingAppRoles[mgt.Id].FirstOrDefault(a => a.Name == app.IdString || a.Name == sanitizedARName);
            }
            if (existingApp != null)
            {
                Log.WriteDebug($"Search {appType}", $"Found!!");
            }

            return existingApp != null;
        }

        private (long?, bool) ResolveAppServerId(ModellingAppServer appServer, Management mgt)
        {
            Log.WriteDebug("Search AppServer", $"Name: {appServer.Name}, Ip: {appServer.Ip}, Management: {mgt.Name}");

            ModellingAppServer? existingAppServer = allExistingAppServers[mgt.Id].FirstOrDefault(a => AreEqual(a, appServer));
            if (existingAppServer != null)
            {
                Log.WriteDebug("Search AppServer", $"Found!!");
                return (existingAppServer?.Id, true);
            }
            else if (alreadyCreatedAppServers[mgt.Id].FirstOrDefault(a => AreEqual(a, appServer)) != null)
            {
                return (null, true);
            }
            else
            {
                alreadyCreatedAppServers[mgt.Id].Add(appServer);
                return (null, false);
            }
        }

        private static string ConstructAppServerName(ModellingAppServer appServer, ModellingNamingConvention namingConvention)
        {
            return string.IsNullOrEmpty(appServer.Name) ? namingConvention.AppServerPrefix + appServer.Ip :
                ( char.IsLetter(appServer.Name[0]) ? appServer.Name : namingConvention?.AppServerPrefix + appServer.Name );
        }

        private bool AreEqual(ModellingAppServer appServer1, ModellingAppServer appServer2)
        {
            string appServer2Name = ConstructAppServerName(appServer2, namingConvention);
            string sanitizedAS2Name = new(appServer2Name);
            bool shortened = false;
            sanitizedAS2Name = Sanitizer.SanitizeJsonFieldMand(sanitizedAS2Name, ref shortened);
            return appServer1.Name.ToLower().Trim() == appServer2Name.ToLower().Trim() ||
                appServer1.Name.ToLower().Trim() == sanitizedAS2Name.ToLower().Trim();
        }

        private bool AppChanged(ModellingNwGroup app)
        {
            newAppServers = [];
            deletedAppServers = [];
            unchangedAppServers = [];

            if (existingApp is null)
            {
                return false;
            }

            foreach (ModellingAppServerWrapper appserver in ( (ModellingAppRole)app ).AppServers)
            {
                if (existingApp.AppServers.FirstOrDefault(a => AreEqual(a.Content, appserver.Content)) != null)
                {
                    unchangedAppServers.Add(appserver);
                }
                else
                {
                    newAppServers.Add(appserver);
                }
            }
            foreach (ModellingAppServerWrapper exAppserver in existingApp.AppServers)
            {
                if (( (ModellingAppRole)app ).AppServers.FirstOrDefault(a => AreEqual(exAppserver.Content, a.Content)) == null)
                {
                    deletedAppServers.Add(exAppserver);
                }
            }
            return newAppServers.Count > 0 || deletedAppServers.Count > 0;
        }

        private void RequestNewApp(ModellingNwGroup app, Management mgt)
        {
            string title = "";
            string additionalInfoKeys = "";
            string groupName = "";

            if (app.GetType() == typeof(ModellingAppRole))
            {
                title = userConfig.GetText("new_app_role");
                additionalInfoKeys = AdditionalInfoKeys.AppRoleId;
                groupName = app.IdString;
            }
            else if (app.GetType() == typeof(ModellingAppZone))
            {
                title = userConfig.GetText("new_app_zone");
                additionalInfoKeys = AdditionalInfoKeys.AppZoneId;
                groupName = app.Name;
            }

            List<WfReqElement> groupMembers = [];
            foreach (ModellingAppServer appServer in ModellingAppServerWrapper.Resolve(( (ModellingAppRole)app ).AppServers))
            {
                (long? networkId, bool alreadyRequested) = ResolveAppServerId(appServer, mgt);
                groupMembers.Add(new()
                {
                    RequestAction = alreadyRequested ? RequestAction.addAfterCreation.ToString() : RequestAction.create.ToString(),
                    Field = ElemFieldType.source.ToString(),
                    Name = appServer.Name,
                    IpString = appServer.Ip,
                    IpEnd = appServer.IpEnd,
                    GroupName = groupName,
                    NetworkId = networkId
                });
            }
            Dictionary<string, string>? addInfo = new() { { AdditionalInfoKeys.GrpName, groupName }, { additionalInfoKeys, app.Id.ToString() } };
            TaskList.Add(new()
            {
                Title = title + groupName,
                TaskType = WfTaskType.group_create.ToString(),
                RequestAction = RequestAction.create.ToString(),
                ManagementId = mgt.Id,
                OnManagement = mgt,
                Elements = groupMembers,
                AdditionalInfo = System.Text.Json.JsonSerializer.Serialize(addInfo)
            });
        }

        private void RequestUpdateApp(ModellingNwGroup app, Management mgt)
        {
            string title = "";
            string additionalInfoKeys = "";

            if (app.GetType() == typeof(ModellingAppRole))
            {
                title = userConfig.GetText("update_app_role");
                additionalInfoKeys = AdditionalInfoKeys.AppRoleId;
            }
            else if (app.GetType() == typeof(ModellingAppZone))
            {
                title = userConfig.GetText("update_app_zone");
                additionalInfoKeys = AdditionalInfoKeys.AppZoneId;
            }

            FillGroupMembers(app.IdString, mgt);
            Dictionary<string, string>? addInfo = new() { { AdditionalInfoKeys.GrpName, app.IdString }, { additionalInfoKeys, app.Id.ToString() } };
            if (newGroupMembers.Count > 0)
            {
                newGroupMembers.AddRange(unchangedGroupMembers);
                newGroupMembers.AddRange(unchangedGroupMembersDuringCreate); // will be deleted later
                TaskList.Add(new()
                {
                    Title = title + app.IdString + userConfig.GetText("add_members"),
                    TaskType = WfTaskType.group_modify.ToString(),
                    RequestAction = RequestAction.modify.ToString(),
                    ManagementId = mgt.Id,
                    OnManagement = mgt,
                    Elements = newGroupMembers,
                    AdditionalInfo = System.Text.Json.JsonSerializer.Serialize(addInfo)
                });
            }
            if (deletedGroupMembers.Count > 0)
            {
                deletedGroupMembers.AddRange(unchangedGroupMembers);
                deletedGroupMembers.AddRange(newCreatedGroupMembers);
                DeleteTasksList.Add(new()
                {
                    Title = title + app.IdString + userConfig.GetText("remove_members"),
                    TaskType = WfTaskType.group_modify.ToString(),
                    RequestAction = RequestAction.modify.ToString(),
                    ManagementId = mgt.Id,
                    OnManagement = mgt,
                    Elements = deletedGroupMembers,
                    AdditionalInfo = System.Text.Json.JsonSerializer.Serialize(addInfo)
                });
            }
        }

        private void FillGroupMembers(string idString, Management mgt)
        {
            newGroupMembers = [];
            newCreatedGroupMembers = [];
            deletedGroupMembers = [];
            unchangedGroupMembers = [];
            unchangedGroupMembersDuringCreate = [];
            foreach (ModellingAppServerWrapper appServer in newAppServers)
            {
                (long? networkId, bool alreadyRequested) = ResolveAppServerId(appServer.Content, mgt);
                newGroupMembers.Add(new()
                {
                    RequestAction = alreadyRequested ? RequestAction.addAfterCreation.ToString() : RequestAction.create.ToString(),
                    Field = ElemFieldType.source.ToString(),
                    Name = appServer.Content.Name,
                    IpString = appServer.Content.Ip,
                    IpEnd = appServer.Content.IpEnd,
                    GroupName = idString,
                    NetworkId = networkId
                });
                newCreatedGroupMembers.Add(new()
                {
                    RequestAction = RequestAction.unchanged.ToString(),
                    Field = ElemFieldType.source.ToString(),
                    Name = appServer.Content.Name,
                    IpString = appServer.Content.Ip,
                    IpEnd = appServer.Content.IpEnd,
                    GroupName = idString,
                    NetworkId = networkId
                });
            }
            foreach (ModellingAppServerWrapper appServer in unchangedAppServers)
            {
                unchangedGroupMembers.Add(new()
                {
                    RequestAction = RequestAction.unchanged.ToString(),
                    Field = ElemFieldType.source.ToString(),
                    Name = appServer.Content.Name,
                    IpString = appServer.Content.Ip,
                    IpEnd = appServer.Content.IpEnd,
                    GroupName = idString
                });
            }
            foreach (ModellingAppServerWrapper appServer in deletedAppServers)
            {
                unchangedGroupMembersDuringCreate.Add(new()
                {
                    RequestAction = RequestAction.unchanged.ToString(),
                    Field = ElemFieldType.source.ToString(),
                    Name = appServer.Content.Name,
                    IpString = appServer.Content.Ip,
                    IpEnd = appServer.Content.IpEnd,
                    GroupName = idString
                });
                deletedGroupMembers.Add(new()
                {
                    RequestAction = RequestAction.delete.ToString(),
                    Field = ElemFieldType.source.ToString(),
                    Name = appServer.Content.Name,
                    IpString = appServer.Content.Ip,
                    IpEnd = appServer.Content.IpEnd,
                    GroupName = idString
                });
            }
        }

        private void AnalyseAppServers(ModellingConnection conn)
        {
            foreach (ModellingAppServerWrapper srcAppServer in conn.SourceAppServers)
            {
                elements.Add(new()
                {
                    RequestAction = RequestAction.create.ToString(),
                    Field = ElemFieldType.source.ToString(),
                    Name = srcAppServer.Content.Name,
                    IpString = srcAppServer.Content.Ip,
                    IpEnd = srcAppServer.Content.IpEnd
                });
            }
            foreach (ModellingAppServerWrapper dstAppServer in conn.DestinationAppServers)
            {
                elements.Add(new()
                {
                    RequestAction = RequestAction.create.ToString(),
                    Field = ElemFieldType.destination.ToString(),
                    Name = dstAppServer.Content.Name,
                    IpString = dstAppServer.Content.Ip,
                    IpEnd = dstAppServer.Content.IpEnd
                });
            }
        }

        private void AnalyseServiceGroups(ModellingConnection conn, Management mgt)
        {
            foreach (ModellingServiceGroup svcGrp in ModellingServiceGroupWrapper.Resolve(conn.ServiceGroups))
            {
                if (userConfig.ModRolloutResolveServiceGroups)
                {
                    foreach (ModellingService svc in ModellingServiceWrapper.Resolve(svcGrp.Services))
                    {
                        elements.Add(new()
                        {
                            RequestAction = RequestAction.create.ToString(),
                            Field = ElemFieldType.service.ToString(),
                            Name = svc.Name,
                            Port = svc.Port,
                            PortEnd = svc.PortEnd,
                            ProtoId = svc.ProtoId
                        });
                    }
                }
                else
                {
                    if (TaskList.FirstOrDefault(x => x.Title == userConfig.GetText("new_svc_grp") + svcGrp.Name && x.OnManagement?.Id == mgt.Id) == null)
                    {
                        RequestNewServiceGroup(svcGrp, mgt);
                    }
                    elements.Add(new()
                    {
                        RequestAction = RequestAction.create.ToString(),
                        Field = ElemFieldType.service.ToString(),
                        GroupName = svcGrp.Name
                    });
                }
            }
        }

        private void RequestNewServiceGroup(ModellingServiceGroup svcGrp, Management mgt)
        {
            List<WfReqElement> groupMembers = [];
            foreach (ModellingService svc in ModellingServiceWrapper.Resolve(svcGrp.Services))
            {
                groupMembers.Add(new()
                {
                    RequestAction = RequestAction.create.ToString(),
                    Field = ElemFieldType.service.ToString(),
                    Name = svc.Name,
                    Port = svc.Port,
                    PortEnd = svc.PortEnd,
                    ProtoId = svc.ProtoId,
                    GroupName = svcGrp.Name
                });
            }
            Dictionary<string, string>? addInfo = new() { { AdditionalInfoKeys.GrpName, svcGrp.Name }, { AdditionalInfoKeys.SvcGrpId, svcGrp.Id.ToString() } };
            TaskList.Add(new()
            {
                Title = userConfig.GetText("new_svc_grp") + svcGrp.Name,
                TaskType = WfTaskType.group_create.ToString(),
                ManagementId = mgt.Id,
                OnManagement = mgt,
                Elements = groupMembers,
                AdditionalInfo = System.Text.Json.JsonSerializer.Serialize(addInfo)
            });
        }

        private void AnalyseServices(ModellingConnection conn)
        {
            foreach (ModellingService svc in ModellingServiceWrapper.Resolve(conn.Services))
            {
                elements.Add(new()
                {
                    RequestAction = RequestAction.create.ToString(),
                    Field = ElemFieldType.service.ToString(),
                    Name = svc.Name,
                    Port = svc.Port,
                    PortEnd = svc.PortEnd,
                    ProtoId = svc.ProtoId
                });
            }
        }
    }
}

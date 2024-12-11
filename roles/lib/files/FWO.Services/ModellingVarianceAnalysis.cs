using FWO.Config.Api;
using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Logging;
using FWO.Api.Client.Data;

namespace FWO.Services
{
    public class ModellingVarianceAnalysis(ApiConnection apiConnection, ExtStateHandler extStateHandler, UserConfig userConfig, FwoOwner owner, Action<Exception?, string, string, bool> displayMessageInUi)
    {
        private readonly ModellingNamingConvention namingConvention = System.Text.Json.JsonSerializer.Deserialize<ModellingNamingConvention>(userConfig.ModNamingConvention) ?? new();
        private readonly ModellingAppZoneHandler AppZoneHandler = new(apiConnection, userConfig, owner, displayMessageInUi);
        private AppServerComparer appServerComparer = new(new());
        private List<Management> managements = [];

        private List<WfReqTask> TaskList = [];
        private List<WfReqTask> AccessTaskList = [];
        private List<WfReqTask> DeleteTasksList = [];
        private int taskNumber = 0;
        private List<WfReqElement> elements = [];

        private readonly Dictionary<int, List<ModellingAppRole>> allExistingAppRoles = [];
        private readonly Dictionary<int, List<ModellingAppServer>> allExistingAppServers = [];
        private readonly Dictionary<int, List<ModellingAppServer>> alreadyCreatedAppServers = [];

        private ModellingAppRole? existingAppRole;
        private List<ModellingAppServerWrapper> newAppServers = [];
        private List<ModellingAppServerWrapper> deletedAppServers = [];
        private List<ModellingAppServerWrapper> unchangedAppServers = [];
        private List<WfReqElement> newGroupMembers = [];
        private List<WfReqElement> newCreatedGroupMembers = [];
        private List<WfReqElement> deletedGroupMembers = [];
        private List<WfReqElement> unchangedGroupMembersDuringCreate = [];
        private List<WfReqElement> unchangedGroupMembers = [];

        public async Task<List<WfReqTask>> AnalyseModelledConnections(List<ModellingConnection> connections)
        {
            // later: get rules + compare, bundle requests
            appServerComparer = new (namingConvention);
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
            foreach (Management mgt in managements)
            {
                await AnalyseAppZone(mgt);
                foreach(var conn in connections.Where(c => !c.IsRequested))
                {
                    elements = [];
                    AnalyseNetworkAreas(conn);
                    AnalyseAppRoles(conn, mgt);
                    AnalyseAppServers(conn);
                    AnalyseServiceGroups(conn, mgt);
                    AnalyseServices(conn);
                    if (elements.Count > 0)
                    {
                        Dictionary<string, string>? addInfo = new() { { AdditionalInfoKeys.ConnId, conn.Id.ToString() } };
                        AccessTaskList.Add(new()
                        {
                            Title = (conn.IsCommonService ? userConfig.GetText("new_common_service") : userConfig.GetText("new_connection")) + ": " + conn.Name ?? "",
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
            if(conn.IsCommonService)
            {
                comment += ", ComSvc";
            }
            if(conn.ExtraConfigs.Count > 0)
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
            foreach(var area in ModellingNetworkAreaWrapper.Resolve(conn.SourceAreas))
            {
                elements.Add(new()
                {
                    RequestAction = RequestAction.create.ToString(),
                    Field = ElemFieldType.source.ToString(),
                    GroupName = area.IdString
                });
            }
            foreach(var area in ModellingNetworkAreaWrapper.Resolve(conn.DestinationAreas))
            {
                elements.Add(new()
                {
                    RequestAction = RequestAction.create.ToString(),
                    Field = ElemFieldType.destination.ToString(),
                    GroupName = area.IdString
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
            if (!ResolveExistingNwGroup(appRole, mgt))
            {
                if (TaskList.FirstOrDefault(x => x.Title == userConfig.GetText("new_app_role") + appRole.IdString && x.OnManagement?.Id == mgt.Id) == null)
                {
                    RequestNewNwGroup(appRole, mgt);
                }
            }
            else if (NwGroupChanged(appRole) &&
                TaskList.FirstOrDefault(x => x.Title == userConfig.GetText("update_app_role") + appRole.IdString + userConfig.GetText("add_members") && x.OnManagement?.Id == mgt.Id) == null &&
                DeleteTasksList.FirstOrDefault(x => x.Title == userConfig.GetText("update_app_role") + appRole.IdString + userConfig.GetText("remove_members") && x.OnManagement?.Id == mgt.Id) == null)
            {
                RequestUpdateNwGroup(appRole, mgt);
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
                if (!ResolveExistingNwGroup(existingAppZone, mgt))
                {
                    RequestNewNwGroup(existingAppZone, mgt);
                }
                else if (NwGroupChanged(existingAppZone) )
                {
                    RequestUpdateNwGroup(existingAppZone, mgt);
                }
            }
        }

        private bool ResolveExistingNwGroup(ModellingNwGroup nwGroup, Management mgt)
        {
            string nwGroupType = nwGroup.GetType() == typeof(ModellingAppRole) ? "AppRole" : "AppZone"; 
            Log.WriteDebug($"Search {nwGroupType}", $"Name: {nwGroup.Name}, IdString: {nwGroup.IdString}, Management: {mgt.Name}");

            bool shortened = false;
            string sanitizedARName = Sanitizer.SanitizeJsonFieldMand(nwGroup.IdString, ref shortened);
            if (allExistingAppRoles.ContainsKey(mgt.Id))
            {
                existingAppRole = allExistingAppRoles[mgt.Id].FirstOrDefault(a => a.Name == nwGroup.IdString || a.Name == sanitizedARName);
            }
            if (existingAppRole != null)
            {
                Log.WriteDebug($"Search {nwGroupType}", $"Found!!");
            }

            return existingAppRole != null;
        }

        private (long?, bool) ResolveAppServerId(ModellingAppServer appServer, Management mgt)
        {
            Log.WriteDebug("Search AppServer", $"Name: {appServer.Name}, Ip: {appServer.Ip}, Management: {mgt.Name}");

            ModellingAppServer? existingAppServer = allExistingAppServers[mgt.Id].FirstOrDefault(a => appServerComparer.Equals(a, appServer));
            if (existingAppServer != null)
            {
                Log.WriteDebug("Search AppServer", $"Found!!");
                return (existingAppServer?.Id, true);
            }
            else if (alreadyCreatedAppServers[mgt.Id].FirstOrDefault(a => appServerComparer.Equals(a, appServer)) != null)
            {
                return (null, true);
            }
            else
            {
                alreadyCreatedAppServers[mgt.Id].Add(appServer);
                return (null, false);
            }
        }

        private bool NwGroupChanged(ModellingNwGroup nwGroup)
        {
            newAppServers = [];
            deletedAppServers = [];
            unchangedAppServers = [];

            if (existingAppRole is null)
            {
                return false;
            }

            foreach (ModellingAppServerWrapper appserver in ( (ModellingAppRole)nwGroup ).AppServers)
            {
                if (existingAppRole.AppServers.FirstOrDefault(a => appServerComparer.Equals(a.Content, appserver.Content)) != null)
                {
                    unchangedAppServers.Add(appserver);
                }
                else
                {
                    newAppServers.Add(appserver);
                }
            }
            foreach (ModellingAppServerWrapper exAppserver in existingAppRole.AppServers)
            {
                if (( (ModellingAppRole)nwGroup ).AppServers.FirstOrDefault(a => appServerComparer.Equals(exAppserver.Content, a.Content)) == null)
                {
                    deletedAppServers.Add(exAppserver);
                }
            }
            return newAppServers.Count > 0 || deletedAppServers.Count > 0;
        }

        private void RequestNewNwGroup(ModellingNwGroup nwGroup, Management mgt)
        {
            string title = "";
            string groupName = "";

            if (nwGroup.GetType() == typeof(ModellingAppRole))
            {
                title = userConfig.GetText("new_app_role");
            }
            else if (nwGroup.GetType() == typeof(ModellingAppZone))
            {
                title = userConfig.GetText("new_app_zone");
            }

            List<WfReqElement> groupMembers = [];
            foreach (ModellingAppServer appServer in ModellingAppServerWrapper.Resolve(( (ModellingAppRole)nwGroup ).AppServers))
            {
                (long? networkId, bool alreadyRequested) = ResolveAppServerId(appServer, mgt);
                groupMembers.Add(new()
                {
                    RequestAction = alreadyRequested ? RequestAction.addAfterCreation.ToString() : RequestAction.create.ToString(),
                    Field = ElemFieldType.source.ToString(),
                    Name = appServer.Name,
                    IpString = appServer.Ip,
                    IpEnd = appServer.IpEnd,
                    GroupName = nwGroup.IdString,
                    NetworkId = networkId
                });
            }
            Dictionary<string, string>? addInfo = new() { { AdditionalInfoKeys.GrpName, nwGroup.IdString }, { AdditionalInfoKeys.AppRoleId, nwGroup.Id.ToString() } };
            TaskList.Add(new()
            {
                Title = title + nwGroup.IdString,
                TaskType = WfTaskType.group_create.ToString(),
                RequestAction = RequestAction.create.ToString(),
                ManagementId = mgt.Id,
                OnManagement = mgt,
                Elements = groupMembers,
                AdditionalInfo = System.Text.Json.JsonSerializer.Serialize(addInfo)
            });
        }

        private void RequestUpdateNwGroup(ModellingNwGroup nwGroup, Management mgt)
        {
            string title = "";
            string groupName = "";

            if (nwGroup.GetType() == typeof(ModellingAppRole))
            {
                title = userConfig.GetText("update_app_role");
            }
            else if (nwGroup.GetType() == typeof(ModellingAppZone))
            {
                title = userConfig.GetText("update_app_zone");
            }

            FillGroupMembers(nwGroup.IdString, mgt);
            Dictionary<string, string>? addInfo = new() { { AdditionalInfoKeys.GrpName, nwGroup.IdString }, { AdditionalInfoKeys.AppRoleId, nwGroup.Id.ToString() } };
            if (newGroupMembers.Count > 0)
            {
                newGroupMembers.AddRange(unchangedGroupMembers);
                newGroupMembers.AddRange(unchangedGroupMembersDuringCreate); // will be deleted later
                TaskList.Add(new()
                {
                    Title = title + nwGroup.IdString + userConfig.GetText("add_members"),
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
                    Title = title + nwGroup.IdString + userConfig.GetText("remove_members"),
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

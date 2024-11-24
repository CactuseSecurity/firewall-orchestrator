using FWO.Config.Api;
using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Logging;
using FWO.Api.Client.Data;
using System.Linq;

namespace FWO.Services
{
    public class ModellingVarianceAnalysis(ApiConnection apiConnection, ExtStateHandler extStateHandler, UserConfig userConfig, Action<Exception?, string, string, bool> displayMessageInUi = null)
    {
        private List<Management> managements = [];
        private ModellingNamingConvention namingConvention = System.Text.Json.JsonSerializer.Deserialize<ModellingNamingConvention>(userConfig.ModNamingConvention) ?? new();
        private ModellingAppZoneHandler AppZoneHandler = new(apiConnection, userConfig, displayMessageInUi);

        private List<WfReqTask> TaskList = [];
        private List<WfReqTask> AccessTaskList = [];
        private List<WfReqTask> DeleteTasksList = [];
        private int taskNumber = 0;
        private List<WfReqElement> elements = [];

        private Dictionary<int, List<ModellingAppRole>> allExistingAppRoles = [];
        private Dictionary<int, List<ModellingAppServer>> allExistingAppServers = [];
        private Dictionary<int, List<ModellingAppServer>> alreadyCreatedAppServers = [];
        private Dictionary<int, List<ModellingAppZone>> allExistingAppZones = [];
        private Dictionary<int, List<ModellingAppZone>> alreadyCreatedAppZones = [];

        private ModellingAppRole? existingAppRole;
        private ModellingAppZone? existingAppZone;
        private List<ModellingAppServerWrapper> newAppServers = [];
        private List<ModellingAppServerWrapper> deletedAppServers = [];
        private List<ModellingAppServerWrapper> unchangedAppServers = [];
        private List<WfReqElement> newGroupMembers = [];
        private List<WfReqElement> newCreatedGroupMembers = [];
        private List<WfReqElement> deletedGroupMembers = [];
        private List<WfReqElement> unchangedGroupMembersDuringCreate = [];
        private List<WfReqElement> unchangedGroupMembers = [];

        public async Task<List<WfReqTask>> AnalyseModelledConnections(List<ModellingConnection> Connections, FwoOwner owner)
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

            await GetProductionState(owner);
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
                    await AnalyseAppZones(mgt, owner);
                    //RequestNewFWAppZone(newFWAppZone, mgt);
                    if (elements.Count > 0)
                    {
                        AccessTaskList.Add(new()
                        {
                            Title = userConfig.GetText("new_connection") + ": " + conn.Name ?? "",
                            TaskType = WfTaskType.access.ToString(),
                            ManagementId = mgt.Id,
                            OnManagement = mgt,
                            Elements = elements,
                            RuleAction = 1,  // Todo ??
                            Tracking = 1,  // Todo ??
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

        private async Task GetProductionState(FwoOwner owner)
        {
            try
            {
                int aRCount = 0;
                int aSCount = 0;
                int azCount = 0;
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
                    $"Found {aRCount} AppRoles, {aSCount} AppServer, AppZones: {azCount}. AppRoles with AR: {aRappRoles},  AppServers: {aRappServers}");
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
            if (!ResolveExistingAppRole(appRole, mgt))
            {
                if (TaskList.FirstOrDefault(x => x.Title == userConfig.GetText("new_app_role") + appRole.IdString && x.OnManagement?.Id == mgt.Id) == null)
                {
                    RequestNewAppRole(appRole, mgt);
                }
            }
            else if (AppRoleChanged(appRole) &&
                TaskList.FirstOrDefault(x => x.Title == userConfig.GetText("update_app_role") + appRole.IdString + userConfig.GetText("add_members") && x.OnManagement?.Id == mgt.Id) == null &&
                DeleteTasksList.FirstOrDefault(x => x.Title == userConfig.GetText("update_app_role") + appRole.IdString + userConfig.GetText("remove_members") && x.OnManagement?.Id == mgt.Id) == null)
            {
                RequestUpdateAppRole(appRole, mgt);
            }

            elements.Add(new()
            {
                RequestAction = RequestAction.create.ToString(),
                Field = isSource ? ElemFieldType.source.ToString() : ElemFieldType.destination.ToString(),
                GroupName = appRole.IdString
            });
        }

        private async Task AnalyseAppZones(Management mgt, FwoOwner owner)
        {
            if (!userConfig.CreateAppZones)
            {
                return;
            }

            allExistingAppZones = AppZoneStateHelper.GetManagementAppZones();

            (bool success, List<ModellingAppZone>? existingAppZones) = await AppZoneHandler.GetExistingAppZones(owner.Id);

            if (success && existingAppZones is not null)
            {
                foreach (ModellingAppZone appZone in existingAppZones)
                {
                    if (!ResolveExistingAppZone(appZone, mgt))
                    {
                        if (TaskList.FirstOrDefault(x => x.Title == userConfig.GetText("new_app_zone") + appZone.IdString && x.OnManagement?.Id == mgt.Id) == null)
                        {
                            ModellingAppZone? newFWAppZone = await AppZoneHandler.CreateAppZone(owner);
                            RequestNewFWAppZone(newFWAppZone, mgt);
                        }
                    }
                    else if (AppZoneChanged(appZone))
                    {
                        RequestUpdateAppZone(appZone, mgt);
                    }
                }

                allExistingAppZones[mgt.Id] = existingAppZones;
            }

            if (existingAppZones is null || existingAppZones.Count == 0)
            {
                ModellingAppZone? newFWAppZone = await AppZoneHandler.CreateAppZone(owner);
                RequestNewFWAppZone(newFWAppZone, mgt);
            }

            AppZoneStateHelper.SetManagementAppZones(allExistingAppZones);
        }

        private bool ResolveExistingAppRole(ModellingAppRole appRole, Management mgt)
        {
            Log.WriteDebug("Search AppRole", $"Name: {appRole.Name}, IdString: {appRole.IdString}, Management: {mgt.Name}");
            ModellingAppRole sanitizedAR = new(appRole);
            sanitizedAR.Sanitize();
            if (allExistingAppRoles.ContainsKey(mgt.Id))
            {
                existingAppRole = allExistingAppRoles[mgt.Id].FirstOrDefault(a => a.Name == appRole.IdString || a.Name == sanitizedAR.IdString);
            }
            if (existingAppRole != null)
            {
                Log.WriteDebug("Search AppRole", $"Found!!");
            }
            return existingAppRole != null;
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

        private bool ResolveExistingAppZone(ModellingAppZone appZone, Management mgt)
        {
            if (allExistingAppZones.TryGetValue(mgt.Id, out List<ModellingAppZone>? mgtAppZones))
            {
                existingAppZone = mgtAppZones.FirstOrDefault(_ => new AppZoneComparer().Equals(_, appZone));
            }
            if (existingAppZone != null)
            {
                Log.WriteDebug("Search AppZone", $"Found!!");
            }
            return existingAppZone != null;
        }

        private bool AppZoneAlreadyRequested(ModellingAppZone appZone, Management mgt)
        {
            Log.WriteDebug("Search AppZone", $"Name: {appZone.Name}, AppId: {appZone.AppId}, Management: {mgt.Name}");

            return ( alreadyCreatedAppZones.TryGetValue(mgt.Id, out List<ModellingAppZone>? alreadyRequestedAppZones)
                  && alreadyRequestedAppZones.Contains(appZone, new AppZoneComparer()) );
        }
        private bool AppServerAlreadyRequested(ModellingAppServer appServer, Management mgt)
        {
            Log.WriteDebug("Search AppServer", $"Name: {appServer.Name}, AppId: {appServer.AppId}, Management: {mgt.Name}");

            return ( alreadyCreatedAppServers.TryGetValue(mgt.Id, out List<ModellingAppServer>? alreadyRequestedAppServers)
                  && alreadyRequestedAppServers.Contains(appServer, new AppServerComparer()) );
        }

        private bool AreEqual(ModellingAppServer appServer1, ModellingAppServer appServer2)
        {
            string appServer2Name = string.IsNullOrEmpty(appServer2.Name) ? namingConvention.AppServerPrefix + appServer2.Ip : appServer2.Name;
            return appServer1.Name.ToLower().Trim() == appServer2Name.ToLower().Trim();
        }

        private bool AppRoleChanged(ModellingAppRole appRole)
        {
            newAppServers = [];
            deletedAppServers = [];
            unchangedAppServers = [];
            foreach (ModellingAppServerWrapper appserver in appRole.AppServers)
            {
                if (existingAppRole.AppServers.FirstOrDefault(a => AreEqual(a.Content, appserver.Content)) != null)
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
                if (appRole.AppServers.FirstOrDefault(a => AreEqual(a.Content, exAppserver.Content)) == null)
                {
                    deletedAppServers.Add(exAppserver);
                }
            }
            return newAppServers.Count > 0 || deletedAppServers.Count > 0;
        }

        private bool AppZoneChanged(ModellingAppZone appZone)
        {
            newAppServers = [];
            deletedAppServers = [];
            unchangedAppServers = [];

            List<ModellingAppServerWrapper> diff1 = appZone.AppServers.Except(existingAppZone.AppServers, new AppServerComparer())
                                                                                   .ToList();
            if (diff1.Count > 0)
            {
                newAppServers.AddRange(diff1);
            }

            List<ModellingAppServerWrapper> diff2 = existingAppZone.AppServers.Except(appZone.AppServers, new AppServerComparer())
                                                                                    .ToList();
            if (diff2.Count > 0)
            {
                deletedAppServers.AddRange(diff2);
            }

            List<ModellingAppServerWrapper> unchanged = existingAppZone.AppServers.Intersect(appZone.AppServers, new AppServerComparer())
                                                                                    .ToList();
            if (unchanged.Count > 0)
            {
                unchangedAppServers.AddRange(unchanged);
            }

            return newAppServers.Count > 0 || deletedAppServers.Count > 0;
        }

        private void RequestUpdateAppZone(ModellingAppZone appZone, Management mgt)
        {
            FillGroupMembersAppZone(appZone, mgt);
            Dictionary<string, string>? addInfo = new() { { AdditionalInfoKeys.GrpName, appZone.IdString } };
            if (newGroupMembers.Count > 0)
            {
                newGroupMembers.AddRange(unchangedGroupMembers);
                newGroupMembers.AddRange(unchangedGroupMembersDuringCreate); // will be deleted later
                TaskList.Add(new()
                {
                    Title = userConfig.GetText("update_app_zone") + appZone.IdString + userConfig.GetText("add_members"),
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
                    Title = userConfig.GetText("update_app_zone") + appZone.IdString + userConfig.GetText("remove_members"),
                    TaskType = WfTaskType.group_modify.ToString(),
                    RequestAction = RequestAction.modify.ToString(),
                    ManagementId = mgt.Id,
                    OnManagement = mgt,
                    Elements = deletedGroupMembers,
                    AdditionalInfo = System.Text.Json.JsonSerializer.Serialize(addInfo)
                });
            }
        }

        private void RequestNewAppRole(ModellingAppRole appRole, Management mgt)
        {
            List<WfReqElement> groupMembers = [];
            foreach (ModellingAppServer appServer in ModellingAppServerWrapper.Resolve(appRole.AppServers))
            {
                (long? networkId, bool alreadyRequested) = ResolveAppServerId(appServer, mgt);
                groupMembers.Add(new()
                {
                    RequestAction = alreadyRequested ? RequestAction.addAfterCreation.ToString() : RequestAction.create.ToString(),
                    Field = ElemFieldType.source.ToString(),
                    Name = appServer.Name,
                    IpString = appServer.Ip,
                    IpEnd = appServer.IpEnd,
                    GroupName = appRole.IdString,
                    NetworkId = networkId
                });
            }
            Dictionary<string, string>? addInfo = new() { { AdditionalInfoKeys.GrpName, appRole.IdString } };
            TaskList.Add(new()
            {
                Title = userConfig.GetText("new_app_role") + appRole.IdString,
                TaskType = WfTaskType.group_create.ToString(),
                RequestAction = RequestAction.create.ToString(),
                ManagementId = mgt.Id,
                OnManagement = mgt,
                Elements = groupMembers,
                AdditionalInfo = System.Text.Json.JsonSerializer.Serialize(addInfo)
            });
        }

        private void RequestNewFWAppZone(ModellingAppZone appZone, Management mgt)
        {
            List<WfReqElement> groupMembers = [];
            foreach (ModellingAppServer appServer in ModellingAppServerWrapper.Resolve(appZone.AppServers))
            {
                bool alreadyRequested = AppZoneAlreadyRequested(appZone, mgt);
                groupMembers.Add(new()
                {
                    RequestAction = alreadyRequested ? RequestAction.addAfterCreation.ToString() : RequestAction.create.ToString(),
                    Field = ElemFieldType.source.ToString(),
                    Name = appServer.Name,
                    IpString = appServer.Ip,
                    IpEnd = appServer.IpEnd,
                    GroupName = appZone.IdString
                });
            }
            Dictionary<string, string>? addInfo = new() { { AdditionalInfoKeys.GrpName, appZone.IdString } };
            TaskList.Add(new()
            {
                Title = userConfig.GetText("new_app_zone") + appZone.IdString,
                TaskType = WfTaskType.group_create.ToString(),
                RequestAction = RequestAction.create.ToString(),
                ManagementId = mgt.Id,
                OnManagement = mgt,
                Elements = groupMembers,
                AdditionalInfo = System.Text.Json.JsonSerializer.Serialize(addInfo)
            });

            if (!allExistingAppZones.ContainsKey(mgt.Id))
            {
                allExistingAppZones.Add(mgt.Id, []);
            }
            allExistingAppZones[mgt.Id].Add(appZone);

            if (!alreadyCreatedAppZones.ContainsKey(mgt.Id))
            {
                alreadyCreatedAppZones.Add(mgt.Id, []);
            }
            alreadyCreatedAppZones[mgt.Id].Add(appZone);
        }

        private void RequestUpdateAppRole(ModellingAppRole appRole, Management mgt)
        {
            FillGroupMembers(appRole, mgt);
            Dictionary<string, string>? addInfo = new() { { AdditionalInfoKeys.GrpName, appRole.IdString } };
            if (newGroupMembers.Count > 0)
            {
                newGroupMembers.AddRange(unchangedGroupMembers);
                newGroupMembers.AddRange(unchangedGroupMembersDuringCreate); // will be deleted later
                TaskList.Add(new()
                {
                    Title = userConfig.GetText("update_app_role") + appRole.IdString + userConfig.GetText("add_members"),
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
                    Title = userConfig.GetText("update_app_role") + appRole.IdString + userConfig.GetText("remove_members"),
                    TaskType = WfTaskType.group_modify.ToString(),
                    RequestAction = RequestAction.modify.ToString(),
                    ManagementId = mgt.Id,
                    OnManagement = mgt,
                    Elements = deletedGroupMembers,
                    AdditionalInfo = System.Text.Json.JsonSerializer.Serialize(addInfo)
                });
            }
        }

        private void FillGroupMembers(ModellingAppRole appRole, Management mgt)
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
                    GroupName = appRole.IdString,
                    NetworkId = networkId
                });
                newCreatedGroupMembers.Add(new()
                {
                    RequestAction = RequestAction.unchanged.ToString(),
                    Field = ElemFieldType.source.ToString(),
                    Name = appServer.Content.Name,
                    IpString = appServer.Content.Ip,
                    IpEnd = appServer.Content.IpEnd,
                    GroupName = appRole.IdString,
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
                    GroupName = appRole.IdString
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
                    GroupName = appRole.IdString
                });
                deletedGroupMembers.Add(new()
                {
                    RequestAction = RequestAction.delete.ToString(),
                    Field = ElemFieldType.source.ToString(),
                    Name = appServer.Content.Name,
                    IpString = appServer.Content.Ip,
                    IpEnd = appServer.Content.IpEnd,
                    GroupName = appRole.IdString
                });
            }
        }

        private void FillGroupMembersAppZone(ModellingAppZone appZone, Management mgt)
        {
            newGroupMembers = [];
            newCreatedGroupMembers = [];
            deletedGroupMembers = [];
            unchangedGroupMembers = [];
            unchangedGroupMembersDuringCreate = [];
            foreach (ModellingAppServerWrapper appServer in newAppServers)
            {
                bool alreadyRequested = AppServerAlreadyRequested(appServer.Content, mgt);

                newGroupMembers.Add(new()
                {
                    RequestAction = alreadyRequested ? RequestAction.addAfterCreation.ToString() : RequestAction.create.ToString(),
                    Field = ElemFieldType.source.ToString(),
                    Name = appServer.Content.Name,
                    IpString = appServer.Content.Ip,
                    IpEnd = appServer.Content.IpEnd,
                    GroupName = appZone.IdString,
                    //NetworkId = networkId
                });
                newCreatedGroupMembers.Add(new()
                {
                    RequestAction = RequestAction.unchanged.ToString(),
                    Field = ElemFieldType.source.ToString(),
                    Name = appServer.Content.Name,
                    IpString = appServer.Content.Ip,
                    IpEnd = appServer.Content.IpEnd,
                    GroupName = appZone.IdString,
                    //NetworkId = networkId
                });

                if (!alreadyCreatedAppServers.ContainsKey(mgt.Id))
                {
                    alreadyCreatedAppServers.Add(mgt.Id, []);
                }
                alreadyCreatedAppServers[mgt.Id].Add(appServer.Content);
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
                    GroupName = appZone.IdString
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
                    GroupName = appZone.IdString
                });
                deletedGroupMembers.Add(new()
                {
                    RequestAction = RequestAction.delete.ToString(),
                    Field = ElemFieldType.source.ToString(),
                    Name = appServer.Content.Name,
                    IpString = appServer.Content.Ip,
                    IpEnd = appServer.Content.IpEnd,
                    GroupName = appZone.IdString
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
            Dictionary<string, string>? addInfo = new() { { AdditionalInfoKeys.GrpName, svcGrp.Name } };
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

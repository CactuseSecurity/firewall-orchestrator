using FWO.Config.Api;
using FWO.GlobalConstants;
using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Logging;
using System.Text.Json;

namespace FWO.Services
{
    public class ModellingVarianceAnalysis
    {
        private ApiConnection apiConnection;
        private ExtStateHandler extStateHandler;
        private UserConfig userConfig;
        private List<Management> managements = [];
        private ModellingNamingConvention namingConvention = new();

        private List<WfReqTask> TaskList = [];
        private List<WfReqTask> AccessTaskList = [];
        private List<WfReqTask> DeleteTasksList = [];
        private int taskNumber = 0;
        private List<WfReqElement> elements = [];

        private Dictionary<int, List<ModellingAppRole>> allExistingAppRoles = [];
        private Dictionary<int, List<ModellingAppServer>> allExistingAppServers = [];
        private Dictionary<int, List<ModellingAppServer>> alreadyCreatedAppServers = [];

        private ModellingAppRole? existingAppRole;
        private List<ModellingAppServerWrapper> newAppServers = [];
        private List<ModellingAppServerWrapper> deletedAppServers = [];
        private List<ModellingAppServerWrapper> unchangedAppServers = [];
        private List<WfReqElement> newGroupMembers = [];
        private List<WfReqElement> newCreatedGroupMembers = [];
        private List<WfReqElement> deletedGroupMembers = [];
        private List<WfReqElement> unchangedGroupMembersDuringCreate = [];
        private List<WfReqElement> unchangedGroupMembers = [];

        private static readonly string newAppRoleText = "New AppRole: ";
        private static readonly string updateAppRoleText = "Update AppRole: ";
        private static readonly string newServiceGroupText = "New Servicegroup: ";
        private static readonly string addMembersText = ": Add Members";
        private static readonly string removeMembersText = ": Remove Members";


        public ModellingVarianceAnalysis(ApiConnection apiConnection, ExtStateHandler extStateHandler, UserConfig userConfig)
        {
            this.apiConnection = apiConnection;
            this.extStateHandler = extStateHandler;
            this.userConfig = userConfig;
            namingConvention = System.Text.Json.JsonSerializer.Deserialize<ModellingNamingConvention>(userConfig.ModNamingConvention) ?? new();
        }

        public async Task<List<WfReqTask>> AnalyseModelledConnections(List<ModellingConnection> Connections, FwoOwner owner)
        {
            // later: get rules + compare, bundle requests
            managements = await apiConnection.SendQueryAsync<List<Management>>(DeviceQueries.getManagementNames);
            managements = managements.Where(m => !string.IsNullOrEmpty(m.ExtMgtData)).ToList();
            foreach(var mgt in managements)
            {
                if(!alreadyCreatedAppServers.ContainsKey(mgt.Id))
                {
                    alreadyCreatedAppServers.Add(mgt.Id, []);
                }
            }
            await GetProductionState();
            TaskList = [];
            AccessTaskList = [];
            DeleteTasksList = [];
            foreach(var conn in Connections.Where(c => !c.IsRequested))
            {
                foreach(var mgt in managements)
                {
                    elements = [];
                    AnalyseNetworkAreas(conn);
                    AnalyseAppRoles(conn, mgt);
                    AnalyseAppServers(conn);
                    AnalyseServiceGroups(conn, mgt);
                    AnalyseServices(conn);
                    if(elements.Count > 0)
                    {
                        AccessTaskList.Add(new()
                        {
                            Title = "New Connection: " + conn.Name ?? "",
                            TaskType = WfTaskType.access.ToString(),
                            ManagementId = mgt.Id,
                            OnManagement = mgt,
                            Elements = elements,
                            Comments = [ new(){ Comment = new(){ CommentText = "FWOC" + conn.Id.ToString() }} ]
                        });
                    }
                }
            }
            TaskList.AddRange(AccessTaskList);
            TaskList.AddRange(DeleteTasksList);
            taskNumber = 1;
            foreach(var task in TaskList)
            {
                task.TaskNumber = taskNumber++;
                task.Owners = [new (){ Owner = owner }];
                task.StateId = extStateHandler.GetInternalStateId(ExtStates.ExtReqInitialized) ?? 0;
            }
            return TaskList;
        }

        private async Task GetProductionState()
        {
            try
            {
                int aRCount = 0;
                int aSCount = 0;
                foreach(var mgt in managements)
                {
                    List<NetworkObject>? objGrpByMgt = await GetObjects(mgt.Id, [2]);
                    if(objGrpByMgt != null)
                    {
                        foreach(var objGrp in objGrpByMgt)
                        {
                            // Todo: filter for naming convention??
                            if(!allExistingAppRoles.ContainsKey(mgt.Id))
                            {
                                allExistingAppRoles.Add(mgt.Id, []);
                            }
                            allExistingAppRoles[mgt.Id].Add(new(objGrp, namingConvention));
                            aRCount++;
                        }
                    }

                    List<NetworkObject>? objByMgt = await GetObjects(mgt.Id, [1,3,12]);
                    if(objByMgt != null)
                    {
                        foreach(var obj in objByMgt)
                        {
                            if(!allExistingAppServers.ContainsKey(mgt.Id))
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
                foreach(var mgt in allExistingAppRoles.Keys)
                {
                    aRappRoles += $" Management {mgt}: " + string.Join(",", allExistingAppRoles[mgt].Where(a => a.Name.StartsWith("AR")).ToList().ConvertAll(x => $"{x.Name}({x.IdString})").ToList());
                }
                foreach(var mgt in allExistingAppServers.Keys)
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
            foreach(var nwGroup in ModellingNwGroupWrapper.Resolve(conn.SourceNwGroups))
            {
                elements.Add(new()
                {
                    RequestAction = RequestAction.create.ToString(),
                    Field = ElemFieldType.source.ToString(),
                    GroupName = nwGroup.IdString
                });
            }
            foreach(var nwGroup in ModellingNwGroupWrapper.Resolve(conn.DestinationNwGroups))
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
            foreach(var srcAppRole in ModellingAppRoleWrapper.Resolve(conn.SourceAppRoles))
            {
                AnalyseAppRole(srcAppRole, mgt, true);
            }
            foreach(var dstAppRole in ModellingAppRoleWrapper.Resolve(conn.DestinationAppRoles))
            {
                AnalyseAppRole(dstAppRole, mgt);
            }
        }

        private void AnalyseAppRole(ModellingAppRole appRole, Management mgt, bool isSource = false)
        {
            if(!ResolveExistingAppRole(appRole, mgt))
            {
                if(TaskList.FirstOrDefault(x => x.Title == newAppRoleText + appRole.IdString && x.OnManagement?.Id == mgt.Id) == null)
                {
                    RequestNewAppRole(appRole, mgt);
                }
            }
            else if(AppRoleChanged(appRole) && 
                TaskList.FirstOrDefault(x => x.Title == updateAppRoleText + appRole.IdString + addMembersText && x.OnManagement?.Id == mgt.Id) == null &&
                DeleteTasksList.FirstOrDefault(x => x.Title == updateAppRoleText + appRole.IdString + removeMembersText && x.OnManagement?.Id == mgt.Id) == null)
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

        private bool ResolveExistingAppRole(ModellingAppRole appRole, Management mgt)
        {
            Log.WriteDebug("Search AppRole", $"Name: {appRole.Name}, IdString: {appRole.IdString}, Management: {mgt.Name}");
            ModellingAppRole sanitizedAR = new(appRole);
            sanitizedAR.Sanitize();
            if(allExistingAppRoles.ContainsKey(mgt.Id))
            {
                existingAppRole = allExistingAppRoles[mgt.Id].FirstOrDefault(a => a.Name == appRole.IdString || a.Name == sanitizedAR.IdString);
            }
            if(existingAppRole != null)
            {
                Log.WriteDebug("Search AppRole", $"Found!!");
            }
            return existingAppRole != null;
        }

        private long? ResolveAppServerId(ModellingAppServer appServer, Management mgt)
        {
            Log.WriteDebug("Search AppServer", $"Name: {appServer.Name}, Ip: {appServer.Ip}, Management: {mgt.Name}");
            ModellingAppServer? existingAppServer = allExistingAppServers[mgt.Id].FirstOrDefault(a => AreEqual(a, appServer));
            if(existingAppServer != null)
            {
                Log.WriteDebug("Search AppServer", $"Found!!");
                return existingAppServer?.Id;
            }
            else if(alreadyCreatedAppServers[mgt.Id].FirstOrDefault(a => AreEqual(a, appServer)) != null)
            {
                return 0;
            }
            else
            {
                alreadyCreatedAppServers[mgt.Id].Add(appServer);
                return null;
            }
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
            foreach(var appserver in appRole.AppServers)
            {
                if(existingAppRole.AppServers.FirstOrDefault(a => AreEqual(a.Content, appserver.Content)) != null)
                {
                    unchangedAppServers.Add(appserver);
                }
                else
                {
                    newAppServers.Add(appserver);
                }
            }
            foreach(var exAppserver in existingAppRole.AppServers)
            {
                if(appRole.AppServers.FirstOrDefault(a => AreEqual(a.Content, exAppserver.Content)) == null)
                {
                    deletedAppServers.Add(exAppserver);
                }
            }
            return newAppServers.Count > 0 || deletedAppServers.Count > 0;
        }

        private void RequestNewAppRole(ModellingAppRole appRole, Management mgt)
        {
            List<WfReqElement> groupMembers = [];
            foreach(var appServer in ModellingAppServerWrapper.Resolve(appRole.AppServers))
            {
                groupMembers.Add(new()
                {
                    RequestAction = RequestAction.create.ToString(),
                    Field = ElemFieldType.source.ToString(),
                    Name = appServer.Name,
                    IpString = appServer.Ip,
                    IpEnd = appServer.IpEnd,
                    GroupName = appRole.IdString,
                    NetworkId = ResolveAppServerId(appServer, mgt)
                });
            }
            Dictionary<string, string>? addInfo = new() { {AdditionalInfoKeys.GrpName, appRole.IdString} };
            TaskList.Add(new()
            {
                Title = newAppRoleText + appRole.IdString,
                TaskType = WfTaskType.group_create.ToString(),
                RequestAction = RequestAction.create.ToString(),
                ManagementId = mgt.Id,
                OnManagement = mgt,
                Elements = groupMembers,
                AdditionalInfo = System.Text.Json.JsonSerializer.Serialize(addInfo)
            });
        }

        private void RequestUpdateAppRole(ModellingAppRole appRole, Management mgt)
        {
            FillGroupMembers(appRole, mgt);
            Dictionary<string, string>? addInfo = new() { {AdditionalInfoKeys.GrpName, appRole.IdString} };
            if(newGroupMembers.Count > 0)
            {
                newGroupMembers.AddRange(unchangedGroupMembers);
                newGroupMembers.AddRange(unchangedGroupMembersDuringCreate); // will be deleted later
                TaskList.Add(new()
                {
                    Title = updateAppRoleText + appRole.IdString + addMembersText,
                    TaskType = WfTaskType.group_modify.ToString(),
                    RequestAction = RequestAction.modify.ToString(),
                    ManagementId = mgt.Id,
                    OnManagement = mgt,
                    Elements = newGroupMembers,
                    AdditionalInfo = System.Text.Json.JsonSerializer.Serialize(addInfo)
                });
            }
            if(deletedGroupMembers.Count > 0)
            {
                deletedGroupMembers.AddRange(unchangedGroupMembers);
                deletedGroupMembers.AddRange(newCreatedGroupMembers);
                DeleteTasksList.Add(new()
                {
                    Title = updateAppRoleText + appRole.IdString + removeMembersText,
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
            foreach(var appServer in newAppServers)
            {
                long? networkId = ResolveAppServerId(appServer.Content, mgt);
                newGroupMembers.Add(new()
                {
                    RequestAction = RequestAction.create.ToString(),
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
            foreach(var appServer in unchangedAppServers)
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
            foreach(var appServer in deletedAppServers)
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

        private void AnalyseAppServers(ModellingConnection conn)
        {
            foreach(var srcAppServer in conn.SourceAppServers)
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
            foreach(var dstAppServer in conn.DestinationAppServers)
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
            foreach (var svcGrp in ModellingServiceGroupWrapper.Resolve(conn.ServiceGroups))
            {
                if(userConfig.ModRolloutResolveServiceGroups)
                {
                    foreach(var svc in ModellingServiceWrapper.Resolve(svcGrp.Services))
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
                    if(TaskList.FirstOrDefault(x => x.Title == newServiceGroupText + svcGrp.Name && x.OnManagement?.Id == mgt.Id) == null)
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
            foreach(var svc in ModellingServiceWrapper.Resolve(svcGrp.Services))
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
            Dictionary<string, string>? addInfo = new() { {AdditionalInfoKeys.GrpName, svcGrp.Name} };
            TaskList.Add(new()
            {
                Title = newServiceGroupText + svcGrp.Name,
                TaskType = WfTaskType.group_create.ToString(),
                ManagementId = mgt.Id,
                OnManagement = mgt,
                Elements = groupMembers,
                AdditionalInfo = System.Text.Json.JsonSerializer.Serialize(addInfo)
            });
        }
        
        private void AnalyseServices(ModellingConnection conn)
        {
            foreach (var svc in ModellingServiceWrapper.Resolve(conn.Services))
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

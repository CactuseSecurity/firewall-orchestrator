using FWO.Basics;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.Logging;
using System.Text.Json;

namespace FWO.Services
{
    /// <summary>
    /// Part of Variance Analysis Class analysing the rules, network and service objects for request
    /// </summary>
    public partial class ModellingVarianceAnalysis
    {
        private ModellingAppRole? existingAppRole;
        private List<ModellingAppServerWrapper> newAppServers = [];
        private List<ModellingAppServerWrapper> deletedAppServers = [];
        private List<ModellingAppServerWrapper> unchangedAppServers = [];
        private List<WfReqElement> newGroupMembers = [];
        private List<WfReqElement> newCreatedGroupMembers = [];
        private List<WfReqElement> deletedGroupMembers = [];
        private List<WfReqElement> unchangedGroupMembersDuringCreate = [];
        private List<WfReqElement> unchangedGroupMembers = [];

        private async Task AnalyseConnectionForRequest(Management mgt, ModellingConnection conn)
        {
            varianceResult = new() { Managements = RelevantManagements };
            await AnalyseRules(conn, false);
            if(varianceResult.ConnsNotImplemented.Count > 0)
            {
                AddAccessTaskList.Add(ConstructCreateTask(mgt, conn));
            }
            else if(varianceResult.RuleDifferences.Count > 0)
            {
                foreach(var rule in varianceResult.RuleDifferences[0].ImplementedRules.Where(r => r.MgmtId == mgt.Id))
                {
                    ChangeAccessTaskList.Add(ConstructRuleTask(mgt, rule, conn, false, elements));
                }
            }
        }

        private async Task AnalyseDeletedConnsForRequest(Management mgt)
        {
            List<ModellingConnection> deletedConns = await GetDeletedConnections();
            List<int> DeletedConnectionIds = deletedConns.ConvertAll(c => c.Id);
            foreach (var rule in allModelledRules[mgt.Id].Where(r => !r.ModellFound))
            {
                if (int.TryParse(FindModelledMarker(rule), out int connId) && DeletedConnectionIds.Contains(connId))
                {
                    ModellingConnection deletedConn = deletedConns.FirstOrDefault(c => c.Id == connId) ?? throw new KeyNotFoundException("Connection not found.");
                    DeleteAccessTaskList.Add(ConstructRuleTask(mgt, rule, deletedConn, true, GetElementsFromRule(rule, deletedConn)));
                }
            }
        }

        private List<WfReqElement> GetElementsFromRule(Rule rule, ModellingConnection deletedConn)
        {
            List<WfReqElement> ruleElements = [];
             Dictionary<string, bool> specialUserObjects = deletedConn.GetSpecialUserObjectNames();
            if (specialUserObjects.Count > 0)
            {
                // Get from deleted conn as modelled objects are expected instead of specUser (Then deletion of links is suppressed in this case)
                ruleElements.AddRange(GetNwObjElementsFromConn(deletedConn));
            }
            foreach(var src in rule.Froms.Select(src => src.Object))
            {
                ruleElements.Add(new()
                {
                    RequestAction = RequestAction.unchanged.ToString(),
                    Field = ElemFieldType.source.ToString(),
                    Name = src.Name,
                    IpString = src.IP,
                    IpEnd = src.IpEnd,
                    GroupName = src.Type.Name == ObjectType.Group ? src.Name : null
                });
            }
            foreach(var dest in rule.Tos.Select(dest => dest.Object))
            {
                ruleElements.Add(new()
                {
                    RequestAction = RequestAction.unchanged.ToString(),
                    Field = ElemFieldType.destination.ToString(),
                    Name = dest.Name,
                    IpString = dest.IP,
                    IpEnd = dest.IpEnd,
                    GroupName = dest.Type.Name == ObjectType.Group ? dest.Name : null
                });
            }
            foreach(var svc in rule.Services.Select(svc => svc.Content))
            {
                ruleElements.Add(new()
                {
                    RequestAction = RequestAction.unchanged.ToString(),
                    Field = ElemFieldType.service.ToString(),
                    Name = svc.Name,
                    Port = svc.DestinationPort,
                    PortEnd = svc.DestinationPortEnd,
                    ProtoId = svc.ProtoId,
                    GroupName = svc.Type.Name == ServiceType.Group ? svc.Name : null
                });
            }
            return ruleElements;
        }

        private List<WfReqElement> GetNwObjElementsFromConn(ModellingConnection deletedConn)
        {
            AnalyseNetworkAreasForRequest(deletedConn, true);
            foreach (ModellingAppRole srcAppRole in ModellingAppRoleWrapper.Resolve(deletedConn.SourceAppRoles))
            {
                elements.Add(new()
                {
                    RequestAction = RequestAction.create.ToString(),
                    Field = ElemFieldType.modelled_source.ToString(),
                    GroupName = srcAppRole.IdString
                });
            }
            foreach (ModellingAppRole dstAppRole in ModellingAppRoleWrapper.Resolve(deletedConn.DestinationAppRoles))
            {
                elements.Add(new()
                {
                    RequestAction = RequestAction.create.ToString(),
                    Field = ElemFieldType.modelled_destination.ToString(),
                    GroupName = dstAppRole.IdString
                });
            }
            AnalyseAppServersForRequest(deletedConn, true);
            return elements.ConvertAll(e => new WfReqElement(e) { RequestAction = RequestAction.unchanged.ToString() });
        }

        private WfReqTask ConstructCreateTask(Management mgt, ModellingConnection conn)
        {
            Dictionary<string, string>? addInfo = new() { { AdditionalInfoKeys.ConnId, conn.Id.ToString() } };
            return new()
            {
                Title = ConstructTitle(conn),
                TaskType = WfTaskType.access.ToString(),
                ManagementId = mgt.Id,
                OnManagement = mgt,
                Elements = elements,
                RuleAction = 1,  // Todo ??
                Tracking = 1,  // Todo ??
                AdditionalInfo = JsonSerializer.Serialize(addInfo),
                Comments = [new() { Comment = new() { CommentText = ConstructComment(conn) } }]
            };
        }

        private string ConstructTitle(ModellingConnection conn)
        {
            string commentString = $" ({ userConfig.ModModelledMarker + conn.Id.ToString() })";
            return (conn.IsCommonService ? userConfig.GetText("new_common_service") : userConfig.GetText("new_connection")) + ": " + (conn.Name ?? "") + commentString;
        }

        private WfReqTask ConstructRuleTask(Management mgt, Rule rule, ModellingConnection conn, bool delete, List<WfReqElement> ruleElements)
        {
            Dictionary<string, string>? addInfo = new() { { AdditionalInfoKeys.ConnId, conn.Id.ToString() } };
            ruleElements.Add(new()
            {
                Field = ElemFieldType.rule.ToString(),
                RuleUid = rule.Uid,
                DeviceId = rule.DeviceId,
                Name = rule.Name
            });
            WfReqTask ruleTask = new()
            {
                Title = (delete ? userConfig.GetText("delete_rule") : userConfig.GetText("change_rule")) + ": " + (rule.Name ?? ""),
                TaskType = delete ? WfTaskType.rule_delete.ToString() : WfTaskType.rule_modify.ToString(),
                RequestAction = delete ? RequestAction.delete.ToString() : RequestAction.modify.ToString(),
                ManagementId = mgt.Id,
                OnManagement = mgt,
                Elements = ruleElements,
                RuleAction = 1,  // Todo ??
                Tracking = 1,  // Todo ??
                AdditionalInfo = JsonSerializer.Serialize(addInfo),
                Comments = [new() { Comment = new() { CommentText = ConstructComment(conn) } }]
            };
            Device? device = mgt.Devices.FirstOrDefault(d => d.Id == rule.DeviceId);
            ruleTask.SetDeviceList(device != null ? [device] : []);
            return ruleTask;
        }

        private void AnalyseNetworkAreasForRequest(ModellingConnection conn, bool modelled = false)
        {
            foreach(var area in ModellingNetworkAreaWrapper.Resolve(conn.SourceAreas))
            {
                elements.Add(new()
                {
                    RequestAction = RequestAction.create.ToString(),
                    Field = modelled ? ElemFieldType.modelled_source.ToString() : ElemFieldType.source.ToString(),
                    GroupName = area.IdString
                });
            }
            foreach(var area in ModellingNetworkAreaWrapper.Resolve(conn.DestinationAreas))
            {
                elements.Add(new()
                {
                    RequestAction = RequestAction.create.ToString(),
                    Field = modelled ? ElemFieldType.modelled_destination.ToString() : ElemFieldType.destination.ToString(),
                    GroupName = area.IdString
                });
            }
        }

        private void AnalyseAppRolesForRequest(ModellingConnection conn, Management mgt)
        {
            foreach (ModellingAppRole srcAppRole in ModellingAppRoleWrapper.Resolve(conn.SourceAppRoles))
            {
                AnalyseAppRoleForRequest(srcAppRole, mgt, true);
            }
            foreach (ModellingAppRole dstAppRole in ModellingAppRoleWrapper.Resolve(conn.DestinationAppRoles))
            {
                AnalyseAppRoleForRequest(dstAppRole, mgt);
            }
        }

        private void AnalyseAppRoleForRequest(ModellingAppRole appRole, Management mgt, bool isSource = false)
        {
            if (ResolveProdAppRole(appRole, mgt) == null)
            {
                if (TaskList.FirstOrDefault(x => x.Title == userConfig.GetText("new_app_role") + appRole.IdString && x.OnManagement?.Id == mgt.Id) == null)
                {
                    RequestNewAppRole(appRole, mgt);
                }
            }
            else if (AppRoleChanged(appRole) &&
                TaskList.FirstOrDefault(x => x.Title == userConfig.GetText("update_app_role") + appRole.IdString + userConfig.GetText("add_members") && x.OnManagement?.Id == mgt.Id) == null &&
                DeleteObjectTasksList.FirstOrDefault(x => x.Title == userConfig.GetText("update_app_role") + appRole.IdString + userConfig.GetText("remove_members") && x.OnManagement?.Id == mgt.Id) == null)
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

        private async Task AnalyseAppZone(Management mgt)
        {
            if (!userConfig.CreateAppZones)
            {
                return;
            }
            ModellingAppZone? oldAppZone = await AppZoneHandler.GetExistingModelledAppZone();
            PlannedAppZoneDbUpdate = await AppZoneHandler.PlanAppZoneDbUpdate(oldAppZone);

            ModellingAppRole? prodAppZone = oldAppZone == null ? null : ResolveProdAppRole(oldAppZone, mgt);
            if(prodAppZone == null)
            {
                RequestNewAppRole(AppZoneHandler.CreateNewAppZone() , mgt);
            }
            else
            {
                ModellingAppZone appZoneToRequest = AppZoneHandler.PlanAppZoneRequest(new ModellingAppZone(prodAppZone));
                if (appZoneToRequest.AppServersNew.Count > 0 || appZoneToRequest.AppServersRemoved.Count > 0)
                {
                    newAppServers = appZoneToRequest.AppServersNew;
                    deletedAppServers = appZoneToRequest.AppServersRemoved;
                    unchangedAppServers = appZoneToRequest.AppServersUnchanged;
                    RequestUpdateAppRole(appZoneToRequest, mgt);
                }
            }
        }

        private ModellingAppRole? ResolveProdAppRole(ModellingAppRole appRole, Management mgt)
        {
            string nwGroupType = appRole.GetType() == typeof(ModellingAppZone) ? "AppZone" : "AppRole"; 
            Log.WriteDebug($"Search {nwGroupType}", $"Name: {appRole.Name}, IdString: {appRole.IdString}, Management: {mgt.Name}");

            bool shortened = false;
            string sanitizedARName = Sanitizer.SanitizeJsonFieldMand(appRole.IdString, ref shortened);
            if (allProdAppRoles.ContainsKey(mgt.Id))
            {
                existingAppRole = allProdAppRoles[mgt.Id].FirstOrDefault(a => a.Name == appRole.IdString || a.Name == sanitizedARName);
            }
            if (existingAppRole != null)
            {
                Log.WriteDebug($"Search {nwGroupType}", $"Found!!");
            }
            return existingAppRole;
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

        private bool AppRoleChanged(ModellingAppRole appRole)
        {
            newAppServers = [];
            deletedAppServers = [];
            unchangedAppServers = [];

            if (existingAppRole is null)
            {
                return false;
            }

            if (appRole is ModellingAppZone appZone)
            {
                return appZone.AppServersNew.Count > 0 || appZone.AppServersRemoved.Count > 0;
            }

            foreach (ModellingAppServerWrapper appserver in appRole.AppServers)
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
                if (appRole.AppServers.FirstOrDefault(a => appServerComparer.Equals(exAppserver.Content, a.Content)) == null)
                {
                    deletedAppServers.Add(exAppserver);
                }
            }
            return newAppServers.Count > 0 || deletedAppServers.Count > 0;
        }

        private void RequestNewAppRole(ModellingAppRole appRole, Management mgt)
        {
            string title = appRole.GetType() == typeof(ModellingAppZone)? userConfig.GetText("new_app_zone"): userConfig.GetText("new_app_role");
            List<WfReqElement> groupMembers = [];
            foreach (ModellingAppServer appServer in ModellingAppServerWrapper.Resolve(appRole.AppServers))
            {
                (long? networkId, bool alreadyRequested) = ResolveAppServerId(appServer, mgt);
                groupMembers.Add(new()
                {
                    RequestAction = alreadyRequested ? RequestAction.addAfterCreation.ToString() : RequestAction.create.ToString(),
                    Field = ElemFieldType.source.ToString(),
                    Name = AppServerHelper.ConstructAppServerName(appServer, namingConvention),
                    IpString = appServer.Ip,
                    IpEnd = appServer.IpEnd,
                    GroupName = appRole.IdString,
                    NetworkId = networkId
                });
            }
            Dictionary<string, string>? addInfo = new() { { AdditionalInfoKeys.GrpName, appRole.IdString }, { AdditionalInfoKeys.AppRoleId, appRole.Id.ToString() } };
            TaskList.Add(new()
            {
                Title = title + appRole.IdString,
                TaskType = WfTaskType.group_create.ToString(),
                RequestAction = RequestAction.create.ToString(),
                ManagementId = mgt.Id,
                OnManagement = mgt,
                Elements = groupMembers,
                AdditionalInfo = JsonSerializer.Serialize(addInfo)
            });
        }

        private void RequestUpdateAppRole(ModellingAppRole appRole, Management mgt)
        {
            string title = appRole.GetType() == typeof(ModellingAppZone)? userConfig.GetText("update_app_zone"): userConfig.GetText("update_app_role");
            FillGroupMembers(appRole.IdString, mgt);
            Dictionary<string, string>? addInfo = new() { { AdditionalInfoKeys.GrpName, appRole.IdString }, { AdditionalInfoKeys.AppRoleId, appRole.Id.ToString() } };
            if (newGroupMembers.Count > 0)
            {
                newGroupMembers.AddRange(unchangedGroupMembers);
                newGroupMembers.AddRange(unchangedGroupMembersDuringCreate); // will be deleted later
                TaskList.Add(new()
                {
                    Title = title + appRole.IdString + userConfig.GetText("add_members"),
                    TaskType = WfTaskType.group_modify.ToString(),
                    RequestAction = RequestAction.modify.ToString(),
                    ManagementId = mgt.Id,
                    OnManagement = mgt,
                    Elements = newGroupMembers,
                    AdditionalInfo = JsonSerializer.Serialize(addInfo)
                });
            }
            if (deletedGroupMembers.Count > 0)
            {
                deletedGroupMembers.AddRange(unchangedGroupMembers);
                deletedGroupMembers.AddRange(newCreatedGroupMembers);
                DeleteObjectTasksList.Add(new()
                {
                    Title = title + appRole.IdString + userConfig.GetText("remove_members"),
                    TaskType = WfTaskType.group_modify.ToString(),
                    RequestAction = RequestAction.modify.ToString(),
                    ManagementId = mgt.Id,
                    OnManagement = mgt,
                    Elements = deletedGroupMembers,
                    AdditionalInfo = JsonSerializer.Serialize(addInfo)
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
            foreach (var appServer in newAppServers.Select(a => a.Content))
            {
                (long? networkId, bool alreadyRequested) = ResolveAppServerId(appServer, mgt);
                newGroupMembers.Add(new()
                {
                    RequestAction = alreadyRequested ? RequestAction.addAfterCreation.ToString() : RequestAction.create.ToString(),
                    Field = ElemFieldType.source.ToString(),
                    Name = AppServerHelper.ConstructAppServerName(appServer, namingConvention),
                    IpString = appServer.Ip,
                    IpEnd = appServer.IpEnd,
                    GroupName = idString,
                    NetworkId = networkId
                });
                newCreatedGroupMembers.Add(new()
                {
                    RequestAction = RequestAction.unchanged.ToString(),
                    Field = ElemFieldType.source.ToString(),
                    Name = appServer.Name,
                    IpString = appServer.Ip,
                    IpEnd = appServer.IpEnd,
                    GroupName = idString,
                    NetworkId = networkId
                });
            }
            foreach (var appServer in unchangedAppServers.Select(a => a.Content))
            {
                unchangedGroupMembers.Add(new()
                {
                    RequestAction = RequestAction.unchanged.ToString(),
                    Field = ElemFieldType.source.ToString(),
                    Name = appServer.Name,
                    IpString = appServer.Ip,
                    IpEnd = appServer.IpEnd,
                    GroupName = idString
                });
            }
            foreach (var appServer in deletedAppServers.Select(a => a.Content))
            {
                unchangedGroupMembersDuringCreate.Add(new()
                {
                    RequestAction = RequestAction.unchanged.ToString(),
                    Field = ElemFieldType.source.ToString(),
                    Name = appServer.Name,
                    IpString = appServer.Ip,
                    IpEnd = appServer.IpEnd,
                    GroupName = idString
                });
                deletedGroupMembers.Add(new()
                {
                    RequestAction = RequestAction.delete.ToString(),
                    Field = ElemFieldType.source.ToString(),
                    Name = appServer.Name,
                    IpString = appServer.Ip,
                    IpEnd = appServer.IpEnd,
                    GroupName = idString
                });
            }
        }

        private void AnalyseAppServersForRequest(ModellingConnection conn, bool modelled = false)
        {
            foreach (var srcAppServer in conn.SourceAppServers.Select(a => a.Content))
            {
                elements.Add(new()
                {
                    RequestAction = RequestAction.create.ToString(),
                    Field = modelled ? ElemFieldType.modelled_source.ToString() : ElemFieldType.source.ToString(),
                    Name = srcAppServer.Name,
                    IpString = srcAppServer.Ip,
                    IpEnd = srcAppServer.IpEnd
                });
            }
            foreach (var dstAppServer in conn.DestinationAppServers.Select(a => a.Content))
            {
                elements.Add(new()
                {
                    RequestAction = RequestAction.create.ToString(),
                    Field = modelled ? ElemFieldType.modelled_destination.ToString() : ElemFieldType.destination.ToString(),
                    Name = dstAppServer.Name,
                    IpString = dstAppServer.Ip,
                    IpEnd = dstAppServer.IpEnd
                });
            }
        }

        private void AnalyseServiceGroupsForRequest(ModellingConnection conn, Management mgt)
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
                AdditionalInfo = JsonSerializer.Serialize(addInfo)
            });
        }

        private void AnalyseServicesForRequest(ModellingConnection conn)
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

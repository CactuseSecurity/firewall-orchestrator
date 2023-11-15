using FWO.Config.Api;
using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;


namespace FWO.Ui.Services
{
    public class ModellingConnectionHandler : ModellingHandlerBase
    {
        public List<ModellingConnection> Connections { get; set; } = new();
        public ModellingConnection ActConn { get; set; } = new();
        public List<ModellingAppServer> AvailableAppServers { get; set; } = new();
        public List<ModellingAppRole> AvailableAppRoles { get; set; } = new();
        public List<ModellingNwGroupObjectWrapper> AvailableSelectedObjects { get; set; } = new();
        public List<KeyValuePair<int, long>> AvailableNwElems { get; set; } = new();
        public List<ModellingServiceGroup> AvailableServiceGroups { get; set; } = new();
        public List<ModellingService> AvailableServices { get; set; } = new();
        public List<KeyValuePair<int, int>> AvailableSvcElems { get; set; } = new();

        public string InterfaceName = "";
        public string Message = "";
        public bool ReadOnly = false;

        public bool srcReadOnly { get; set; } = false;
        public bool dstReadOnly { get; set; } = false;
        public bool svcReadOnly { get; set; } = false;

        public bool SearchNWObjectMode = false;
        public bool DeleteNwObjectMode = false;

        public ModellingAppServerHandler AppServerHandler;
        public List<ModellingAppServer> SrcAppServerToAdd { get; set; } = new();
        public List<ModellingAppServer> SrcAppServerToDelete { get; set; } = new();
        public List<ModellingAppServer> DstAppServerToAdd { get; set; } = new();
        public List<ModellingAppServer> DstAppServerToDelete { get; set; } = new();
        public bool AddAppServerMode = false;
        public bool EditAppServerMode = false;
        public bool DeleteAppServerMode = false;
        public bool ReactivateAppServerMode = false;

        public ModellingAppRoleHandler AppRoleHandler;
        public List<ModellingAppRole> SrcAppRolesToAdd { get; set; } = new();
        public List<ModellingAppRole> SrcAppRolesToDelete { get; set; } = new();
        public List<ModellingAppRole> DstAppRolesToAdd { get; set; } = new();
        public List<ModellingAppRole> DstAppRolesToDelete { get; set; } = new();
        public List<ModellingNwGroupObject> SrcNwGroupsToAdd { get; set; } = new();
        public List<ModellingNwGroupObject> SrcNwGroupsToDelete { get; set; } = new();
        public List<ModellingNwGroupObject> DstNwGroupsToAdd { get; set; } = new();
        public List<ModellingNwGroupObject> DstNwGroupsToDelete { get; set; } = new();
        public bool AddAppRoleMode = false;
        public bool EditAppRoleMode = false;
        public bool DeleteAppRoleMode = false;

        public ModellingServiceHandler ServiceHandler;
        public List<ModellingService> SvcToAdd { get; set; } = new();
        public List<ModellingService> SvcToDelete { get; set; } = new();
        public bool AddServiceMode = false;
        public bool EditServiceMode = false;
        public bool DeleteServiceMode = false;

        public ModellingServiceGroupHandler SvcGrpHandler;
        public List<ModellingServiceGroup> SvcGrpToAdd { get; set; } = new();
        public List<ModellingServiceGroup> SvcGrpToDelete { get; set; } = new();
        public bool AddSvcGrpMode = false;
        public bool EditSvcGrpMode = false;
        public bool DeleteSvcGrpMode = false;

        private ModellingAppServer actAppServer = new();
        private ModellingAppRole actAppRole = new();
        private ModellingNwGroupObject actNwGrpObj = new();
        private ModellingService actService = new();
        private ModellingServiceGroup actServiceGroup = new();
        private ModellingConnection ActConnOrig { get; set; } = new();


        public ModellingConnectionHandler(ApiConnection apiConnection, UserConfig userConfig, FwoOwner application, 
            List<ModellingConnection> connections, ModellingConnection conn, bool addMode, bool readOnly, 
            Action<Exception?, string, string, bool> displayMessageInUi)
            : base (apiConnection, userConfig, application, addMode, displayMessageInUi)
        {
            Connections = connections;
            ActConn = conn;
            ReadOnly = readOnly;
            ActConnOrig = new ModellingConnection(ActConn);
        }

        public async Task Init()
        {
            try
            {
                AvailableAppServers = await apiConnection.SendQueryAsync<List<ModellingAppServer>>(ModellingQueries.getAppServers, new { appId = Application.Id });
                AvailableAppRoles = await apiConnection.SendQueryAsync<List<ModellingAppRole>>(ModellingQueries.getAppRoles, new { appId = Application.Id });
                AvailableSelectedObjects = await apiConnection.SendQueryAsync<List<ModellingNwGroupObjectWrapper>>(ModellingQueries.getSelectedNwGroupObjects, new { appId = Application.Id });
                AvailableServiceGroups = await apiConnection.SendQueryAsync<List<ModellingServiceGroup>>(ModellingQueries.getServiceGroupsForApp, new { appId = Application.Id });
                AvailableServiceGroups.AddRange((await apiConnection.SendQueryAsync<List<ModellingServiceGroup>>(ModellingQueries.getGlobalServiceGroups)).Where(x => x.AppId != Application.Id));
                AvailableServices = await apiConnection.SendQueryAsync<List<ModellingService>>(ModellingQueries.getServicesForApp, new { appId = Application.Id });
                foreach(var svcGrp in AvailableServiceGroups)
                {
                    AvailableSvcElems.Add(new KeyValuePair<int, int>((int)ModellingTypes.ObjectType.ServiceGroup, svcGrp.Id));
                }
                if(userConfig.AllowServiceInConn)
                {
                    foreach(var svc in AvailableServices)
                    {
                        AvailableSvcElems.Add(new KeyValuePair<int, int>((int)ModellingTypes.ObjectType.Service, svc.Id));
                    }
                }
                RefreshSelectedNwObjects();
                if(ActConn.UsedInterfaceId != null)
                {
                    List<ModellingConnection> interf = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getInterfaceById, new {intId = ActConn.UsedInterfaceId});
                    if(interf.Count > 0)
                    {
                        InterfaceName = interf[0].Name ?? "";
                        if(interf[0].SourceAppServers.Count > 0 || interf[0].SourceNwGroups.Count > 0)
                        {
                            ActConn.SrcFromInterface = true;
                        }
                        if(interf[0].DestinationAppServers.Count > 0 || interf[0].DestinationNwGroups.Count > 0)
                        {
                            ActConn.DstFromInterface = true;
                        }
                    }  
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("fetch_data"), "", true);
            }
        }

        public bool RefreshSelectedNwObjects()
        {
            AvailableNwElems = new();
            foreach(var obj in AvailableSelectedObjects)
            {
                AvailableNwElems.Add(new KeyValuePair<int, long>(obj.Content.GroupType, obj.Content.Id));
            }
            foreach(var appRole in AvailableAppRoles)
            {
                AvailableNwElems.Add(new KeyValuePair<int, long>((int)ModellingTypes.ObjectType.AppRole, appRole.Id));
            }
            if(userConfig.AllowServerInConn)
            {
                foreach(var appServer in AvailableAppServers)
                {
                    AvailableNwElems.Add(new KeyValuePair<int, long>((int)ModellingTypes.ObjectType.AppServer, appServer.Id));
                }
            }
            return true;
        }

        public void InterfaceToConn(ModellingConnection interf)
        {
            InterfaceName = interf.Name ?? "";
            srcReadOnly = interf.SourceAppServers.Count > 0 || interf.SourceNwGroups.Count > 0;
            dstReadOnly = !srcReadOnly;
            svcReadOnly = true;
            ActConn.IsInterface = false;
            ActConn.UsedInterfaceId = interf.Id;
            if(srcReadOnly)
            {
                ActConn.SourceAppServers = new List<ModellingAppServerWrapper>(interf.SourceAppServers){};
                ActConn.SourceAppRoles = new List<ModellingAppRoleWrapper>(interf.SourceAppRoles){};
                ActConn.SourceNwGroups = new List<ModellingNwGroupObjectWrapper>(interf.SourceNwGroups){};
                ActConn.SrcFromInterface = true;
            }
            else
            {
                ActConn.DestinationAppServers = new List<ModellingAppServerWrapper>(interf.DestinationAppServers){};
                ActConn.DestinationAppRoles = new List<ModellingAppRoleWrapper>(interf.DestinationAppRoles){};
                ActConn.DestinationNwGroups = new List<ModellingNwGroupObjectWrapper>(interf.DestinationNwGroups){};
                ActConn.DstFromInterface = true;
            }
            ActConn.Services = new List<ModellingServiceWrapper>(interf.Services){};
            ActConn.ServiceGroups = new List<ModellingServiceGroupWrapper>(interf.ServiceGroups){};
        }

        public void RemoveInterf()
        {
            InterfaceName = "";
            if(srcReadOnly)
            {
                ActConn.SourceAppServers = new();
                ActConn.SourceAppRoles = new();
                ActConn.SourceNwGroups = new();
                ActConn.SrcFromInterface = false;
            }
            if(dstReadOnly)
            {
                ActConn.DestinationAppServers = new();
                ActConn.DestinationAppRoles = new();
                ActConn.DestinationNwGroups = new();
                ActConn.DstFromInterface = false;
            }
            ActConn.Services = new();
            ActConn.ServiceGroups = new();
            ActConn.UsedInterfaceId = null;
            srcReadOnly = false;
            dstReadOnly = false;
            svcReadOnly = false;
        }
        
        public void CreateAppServer()
        {
            AddAppServerMode = true;
            HandleAppServer(new ModellingAppServer(){ ImportSource = GlobalConfig.kManual });
        }

        public void EditAppServer(ModellingAppServer appServer)
        {
            AddAppServerMode = false;
            HandleAppServer(appServer);
        }

        public void HandleAppServer(ModellingAppServer appServer)
        {
            try
            {
                AppServerHandler = new ModellingAppServerHandler(apiConnection, userConfig, Application, appServer, AvailableAppServers, AddAppServerMode, DisplayMessageInUi);
                EditAppServerMode = true;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("edit_app_server"), "", true);
            }
        }

        public void RequestDeleteAppServer(ModellingAppServer appServer)
        {
            actAppServer = appServer;
            Message = userConfig.GetText("U9003") + appServer.Name + "?";
            DeleteAppServerMode = true;
        }

        public async Task DeleteAppServer()
        {
            try
            {
                DeleteAppServerMode = await DeleteAppServer(actAppServer, AvailableAppServers, AvailableNwElems);
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("delete_app_server"), "", true);
            }
        }

        public void RequestReactivateAppServer(ModellingAppServer appServer)
        {
            actAppServer = appServer;
            Message = userConfig.GetText("U9005") + appServer.Name + "?";
            ReactivateAppServerMode = true;
        }

        public async Task ReactivateAppServer()
        {
            try
            {
                ReactivateAppServerMode = await ReactivateAppServer(actAppServer);
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("delete_app_server"), "", true);
            }
        }

        public void AppServerToSource(List<ModellingAppServer> srcAppServers)
        {
            if(!SrcDropForbidden())
            {
                foreach(var srcAppServer in srcAppServers)
                {
                    if(!srcAppServer.IsDeleted && ActConn.SourceAppServers.FirstOrDefault(w => w.Content.Id == srcAppServer.Id) == null && !SrcAppServerToAdd.Contains(srcAppServer))
                    {
                        SrcAppServerToAdd.Add(srcAppServer);
                    }
                }
                CalcVisibility();
            }
        }

        public void AppServerToDestination(List<ModellingAppServer> dstAppServers)
        {
            if(!DstDropForbidden())
            {
                foreach(var dstAppServer in dstAppServers)
                {
                    if(!dstAppServer.IsDeleted && ActConn.DestinationAppServers.FirstOrDefault(w => w.Content.Id == dstAppServer.Id) == null && !DstAppServerToAdd.Contains(dstAppServer))
                    {
                        DstAppServerToAdd.Add(dstAppServer);
                    }
                }
                CalcVisibility();
            }
        }

        public void SearchNWObject()
        {
            SearchNWObjectMode = true;
        }

        public void RequestDeleteNwGrpObject(ModellingNwGroupObject nwGrpObj)
        {
            actNwGrpObj = nwGrpObj;
            Message = userConfig.GetText("U9006") + nwGrpObj.Name + "?";
            DeleteNwObjectMode = true;
        }

        public async Task DeleteNwGrpObject()
        {
            try
            {
                if((await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeSelectedNwGroupObject, new { appId = Application.Id, nwGroupId = actNwGrpObj.Id })).AffectedRows > 0)
                {
                    await LogChange(ModellingTypes.ChangeType.Delete, (ModellingTypes.ObjectType)actNwGrpObj.GroupType, actNwGrpObj.Id,
                        $"Deleted {actNwGrpObj.GroupType} Object: {actNwGrpObj.Display()}", Application.Id);
                    AvailableSelectedObjects.Remove(AvailableSelectedObjects.FirstOrDefault(x => x.Content.Id == actNwGrpObj.Id) ?? throw new Exception("Did not find object."));
                    AvailableNwElems.Remove(AvailableNwElems.FirstOrDefault(x => x.Key == actNwGrpObj.GroupType && x.Value == actNwGrpObj.Id));
                    DeleteNwObjectMode = false;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("delete_nw_object"), "", true);
            }
        }

        public void NwGroupToSource(List<ModellingNwGroupObject> nwGroups)
        {
            if(!SrcDropForbidden())
            {
                foreach(var nwGroup in nwGroups)
                {
                    if(ActConn.SourceNwGroups.FirstOrDefault(w => w.Content.Id == nwGroup.Id) == null && !SrcNwGroupsToAdd.Contains(nwGroup))
                    {
                        SrcNwGroupsToAdd.Add(nwGroup);
                    }
                }
                CalcVisibility();
            }
        }

        public void NwGroupToDestination(List<ModellingNwGroupObject> nwGroups)
        {
            if(!DstDropForbidden())
            {
                foreach(var nwGroup in nwGroups)
                {
                    if(ActConn.DestinationNwGroups.FirstOrDefault(w => w.Content.Id == nwGroup.Id) == null && !DstNwGroupsToAdd.Contains(nwGroup))
                    {
                        DstNwGroupsToAdd.Add(nwGroup);
                    }
                }
                CalcVisibility();
            }
        }

        public void CreateAppRole()
        {
            AddAppRoleMode = true;
            HandleAppRole(new ModellingAppRole(){});
        }

        public void EditAppRole(ModellingAppRole appRole)
        {
            AddAppRoleMode = false;
            HandleAppRole(appRole);
        }

        public void HandleAppRole(ModellingAppRole appRole)
        {
            try
            {
                AppRoleHandler = new ModellingAppRoleHandler(apiConnection, userConfig, Application, AvailableAppRoles,
                    appRole, AvailableAppServers, AvailableNwElems, AddAppRoleMode, DisplayMessageInUi);
                EditAppRoleMode = true;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("edit_app_role"), "", true);
            }
        }

        public void RequestDeleteAppRole(ModellingAppRole appRole)
        {
            actAppRole = appRole;
            Message = userConfig.GetText("U9002") + appRole.Name + "?";
            DeleteAppRoleMode = true;
        }

        public async Task DeleteAppRole()
        {
            try
            {
                if((await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.deleteAppRole, new { id = actAppRole.Id })).AffectedRows > 0)
                {
                    await LogChange(ModellingTypes.ChangeType.Delete, ModellingTypes.ObjectType.AppRole, actAppRole.Id,
                        $"Deleted App Role: {actAppRole.Display()}", Application.Id);
                    AvailableAppRoles.Remove(actAppRole);
                    AvailableNwElems.Remove(AvailableNwElems.FirstOrDefault(x => x.Key == (int)ModellingTypes.ObjectType.AppRole && x.Value == actAppRole.Id));
                    DeleteAppRoleMode = false;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("delete_app_role"), "", true);
            }
        }

        public void AppRolesToSource(List<ModellingAppRole> appRoles)
        {
            if(!SrcDropForbidden())
            {
                foreach(var appRole in appRoles)
                {
                    if(ActConn.SourceAppRoles.FirstOrDefault(w => w.Content.Id == appRole.Id) == null && !SrcAppRolesToAdd.Contains(appRole))
                    {
                        SrcAppRolesToAdd.Add(appRole);
                    }
                }
                CalcVisibility();
            }
        }

        public void AppRolesToDestination(List<ModellingAppRole> appRoles)
        {
            if(!DstDropForbidden())
            {
                foreach(var appRole in appRoles)
                {
                    if(ActConn.DestinationAppRoles.FirstOrDefault(w => w.Content.Id == appRole.Id) == null && !DstAppRolesToAdd.Contains(appRole))
                    {
                        DstAppRolesToAdd.Add(appRole);
                    }
                }
                CalcVisibility();
            }
        }


        public void CreateServiceGroup()
        {
            AddSvcGrpMode = true;
            HandleServiceGroup(new ModellingServiceGroup(){});
        }

        public void EditServiceGroup(ModellingServiceGroup serviceGroup)
        {
            AddSvcGrpMode = false;
            HandleServiceGroup(serviceGroup);
        }

        public void HandleServiceGroup(ModellingServiceGroup serviceGroup)
        {
            try
            {
                SvcGrpHandler = new ModellingServiceGroupHandler(apiConnection, userConfig, Application, AvailableServiceGroups,
                    serviceGroup, AvailableServices, AvailableSvcElems, AddSvcGrpMode, DisplayMessageInUi);
                EditSvcGrpMode = true;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("edit_service_group"), "", true);
            }
        }

        public void RequestDeleteServiceGrp(ModellingServiceGroup serviceGroup)
        {
            actServiceGroup = serviceGroup;
            Message = userConfig.GetText("U9004") + serviceGroup.Name + "?";
            DeleteSvcGrpMode = true;
        }

        public async Task DeleteServiceGroup()
        {
            try
            {
                if((await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.deleteServiceGroup, new { id = actServiceGroup.Id })).AffectedRows > 0)
                {
                    await LogChange(ModellingTypes.ChangeType.Delete, ModellingTypes.ObjectType.ServiceGroup, actServiceGroup.Id,
                        $"Deleted Service Group: {actServiceGroup.Display()}", Application.Id);
                    AvailableServiceGroups.Remove(actServiceGroup);
                    AvailableSvcElems.Remove(AvailableSvcElems.FirstOrDefault(x => x.Key == (int)ModellingTypes.ObjectType.ServiceGroup && x.Value == actServiceGroup.Id));
                    DeleteSvcGrpMode = false;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("delete_service_group"), "", true);
            }
        }

        public void ServiceGrpsToConn(List<ModellingServiceGroup> serviceGrps)
        {
            foreach(var grp in serviceGrps)
            {
                if(ActConn.ServiceGroups.FirstOrDefault(w => w.Content.Id == grp.Id) == null && !SvcGrpToAdd.Contains(grp))
                {
                    SvcGrpToAdd.Add(grp);
                }
            }
        }


        public void CreateService()
        {
            AddServiceMode = true;
            HandleService(new ModellingService(){});
        }

        public void EditService(ModellingService service)
        {
            AddServiceMode = false;
            HandleService(service);
        }

        public void HandleService(ModellingService service)
        {
            try
            {
                ServiceHandler = new ModellingServiceHandler(apiConnection, userConfig, Application, service, AvailableServices, AddServiceMode, DisplayMessageInUi);
                EditServiceMode = true;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("edit_service"), "", true);
            }
        }

        public void RequestDeleteService(ModellingService service)
        {
            actService = service;
            Message = userConfig.GetText("U9003") + service.Name + "?";
            DeleteServiceMode = true;
        }

        public async Task DeleteService()
        {
            try
            {
                DeleteServiceMode = await DeleteService(actService, AvailableServices, AvailableSvcElems);
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("delete_service"), "", true);
            }
        }

        public void ServicesToConn(List<ModellingService> services)
        {
            foreach(var svc in services)
            {
                if(ActConn.Services.FirstOrDefault(w => w.Content.Id == svc.Id) == null && !SvcToAdd.Contains(svc))
                {
                    SvcToAdd.Add(svc);
                }
            }
        }
        
        public bool CalcVisibility()
        {
            if(ActConn.IsInterface)
            {
                dstReadOnly = ActConn.SourceAppServers.Count > 0 || SrcAppServerToAdd.Count > 0;
                srcReadOnly = ActConn.DestinationAppServers.Count > 0 || DstAppServerToAdd.Count > 0;
                svcReadOnly = false;
            }
            else if (ActConn.UsedInterfaceId != null)
            {
                srcReadOnly = ActConn.SrcFromInterface;
                dstReadOnly = ActConn.DstFromInterface;
                svcReadOnly = true;
            }
            else
            {
                srcReadOnly = false;
                dstReadOnly = false;
                svcReadOnly = false;
            }
            return true;
        }

        public bool SrcDropForbidden()
        {
            return srcReadOnly || (ActConn.IsInterface && DstFilled());
        }

        public bool DstDropForbidden()
        {
            return dstReadOnly || (ActConn.IsInterface && SrcFilled());
        }

        public bool SrcFilled()
        {
            return ActConn.SourceNwGroups.Count - SrcAppRolesToDelete.Count > 0 || 
                ActConn.SourceAppServers.Count - SrcAppServerToDelete.Count > 0 ||
                SrcNwGroupsToAdd != null && SrcNwGroupsToAdd.Count > 0 ||
                SrcAppServerToAdd != null && SrcAppServerToAdd.Count > 0 ||
                SrcAppRolesToAdd != null && SrcAppRolesToAdd.Count > 0;
        }

        public bool DstFilled()
        {
            return ActConn.DestinationNwGroups.Count - DstAppRolesToDelete.Count > 0 || 
                ActConn.DestinationAppServers.Count - DstAppServerToDelete.Count > 0 ||
                DstNwGroupsToAdd != null && DstNwGroupsToAdd.Count > 0 ||
                DstAppServerToAdd != null && DstAppServerToAdd.Count > 0 ||
                DstAppRolesToAdd != null && DstAppRolesToAdd.Count > 0;
        }

        public bool SvcFilled()
        {
            return ActConn.Services.Count - SvcToDelete.Count > 0 || 
                ActConn.ServiceGroups.Count - SvcGrpToDelete.Count > 0 ||
                SvcToAdd != null && SvcToAdd.Count > 0 ||
                SvcGrpToAdd != null && SvcGrpToAdd.Count > 0;
        }

        public async Task<bool> Save()
        {
            try
            {
                if (ActConn.Sanitize())
                {
                    DisplayMessageInUi(null, userConfig.GetText("save_connection"), userConfig.GetText("U0001"), true);
                }
                if(CheckConn())
                {
                    if(!srcReadOnly)
                    {
                        foreach(var appServer in SrcAppServerToDelete)
                        {
                            ActConn.SourceAppServers.Remove(ActConn.SourceAppServers.FirstOrDefault(x => x.Content.Id == appServer.Id) ?? throw new Exception("Did not find app server."));
                        }
                        foreach(var appServer in SrcAppServerToAdd)
                        {
                            ActConn.SourceAppServers.Add(new ModellingAppServerWrapper(){ Content = appServer });
                        }
                        foreach(var appRole in SrcAppRolesToDelete)
                        {
                            ActConn.SourceAppRoles.Remove(ActConn.SourceAppRoles.FirstOrDefault(x => x.Content.Id == appRole.Id) ?? throw new Exception("Did not find app role."));
                        }
                        foreach(var appRole in SrcAppRolesToAdd)
                        {
                            ActConn.SourceAppRoles.Add(new ModellingAppRoleWrapper(){ Content = appRole });
                        }
                        foreach(var nwGroup in SrcNwGroupsToDelete)
                        {
                            ActConn.SourceNwGroups.Remove(ActConn.SourceNwGroups.FirstOrDefault(x => x.Content.Id == nwGroup.Id) ?? throw new Exception("Did not find nwgroup."));
                        }
                        foreach(var nwGroup in SrcNwGroupsToAdd)
                        {
                            ActConn.SourceNwGroups.Add(new ModellingNwGroupObjectWrapper(){ Content = nwGroup });
                        }
                    }
                    if(!dstReadOnly)
                    {
                        foreach(var appServer in DstAppServerToDelete)
                        {
                            ActConn.DestinationAppServers.Remove(ActConn.DestinationAppServers.FirstOrDefault(x => x.Content.Id == appServer.Id)  ?? throw new Exception("Did not find app server."));
                        }
                        foreach(var appServer in DstAppServerToAdd)
                        {
                            ActConn.DestinationAppServers.Add(new ModellingAppServerWrapper(){ Content = appServer });
                        }
                        foreach(var appRole in DstAppRolesToDelete)
                        {
                            ActConn.DestinationAppRoles.Remove(ActConn.DestinationAppRoles.FirstOrDefault(x => x.Content.Id == appRole.Id) ?? throw new Exception("Did not find app role."));
                        }
                        foreach(var appRole in DstAppRolesToAdd)
                        {
                            ActConn.DestinationAppRoles.Add(new ModellingAppRoleWrapper(){ Content = appRole });
                        }
                        foreach(var nwGroup in SrcNwGroupsToDelete)
                        {
                            ActConn.DestinationNwGroups.Remove(ActConn.DestinationNwGroups.FirstOrDefault(x => x.Content.Id == nwGroup.Id) ?? throw new Exception("Did not find nwgroup."));
                        }
                        foreach(var nwGroup in SrcNwGroupsToAdd)
                        {
                            ActConn.DestinationNwGroups.Add(new ModellingNwGroupObjectWrapper(){ Content = nwGroup });
                        }
                    }
                    if(!svcReadOnly)
                    {
                        foreach(var svc in SvcToDelete)
                        {
                            ActConn.Services.Remove(ActConn.Services.FirstOrDefault(x => x.Content.Id == svc.Id) ?? throw new Exception("Did not find service."));
                        }
                        foreach(var svc in SvcToAdd)
                        {
                            ActConn.Services.Add(new ModellingServiceWrapper(){ Content = svc });
                        }
                        foreach(var svcGrp in SvcGrpToDelete)
                        {
                            ActConn.ServiceGroups.Remove(ActConn.ServiceGroups.FirstOrDefault(x => x.Content.Id == svcGrp.Id) ?? throw new Exception("Did not find service group."));
                        }
                        foreach(var svcGrp in SvcGrpToAdd)
                        {
                            ActConn.ServiceGroups.Add(new ModellingServiceGroupWrapper(){ Content = svcGrp });
                        }
                    }
                    if(AddMode)
                    {
                        await AddConnectionToDb();
                    }
                    else
                    {
                        await UpdateConnectionInDb();
                    }
                    Close();
                    return true;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("save_connection"), "", true);
            }
            return false;
        }

        private bool CheckConn()
        {
            if(ActConn.Name == null || ActConn.Name == "" || ActConn.Reason == null || ActConn.Reason == "")
            {
                DisplayMessageInUi(null, userConfig.GetText("edit_connection"), userConfig.GetText("E5102"), true);
                return false;
            }
            if(ActConn.IsInterface && (!(SrcFilled() || DstFilled()) || !SvcFilled()))
            {
                DisplayMessageInUi(null, userConfig.GetText("edit_connection"), userConfig.GetText("E9004"), true);
                return false;
            }
            return true;
        }

        private async Task AddConnectionToDb()
        {
            try
            {
                var Variables = new
                {
                    name = ActConn.Name,
                    appId = Application.Id,
                    reason = ActConn.Reason,
                    isInterface = ActConn.IsInterface,
                    usedInterfaceId = ActConn.UsedInterfaceId,
                    creator = userConfig.User.Name
                };
                ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<NewReturning>(ModellingQueries.newConnection, Variables)).ReturnIds;
                if (returnIds != null)
                {
                    ActConn.Id = returnIds[0].NewId;
                    await LogChange(ModellingTypes.ChangeType.Insert, ModellingTypes.ObjectType.Connection, ActConn.Id,
                        $"New {(ActConn.IsInterface? "Interface" : "Connection")}: {ActConn.Name}", Application.Id);
                    foreach(var appServer in ActConn.SourceAppServers)
                    {
                        var srcParams = new { appServerId = appServer.Content.Id, connectionId = ActConn.Id, connectionField = (int)ModellingTypes.ConnectionField.Source };
                        await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addAppServerToConnection, srcParams);
                        await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ObjectType.Connection, ActConn.Id,
                            $"Added App Server {appServer.Content.Display()} to {(ActConn.IsInterface? "Interface" : "Connection")}: {ActConn.Name}: Source", Application.Id);
                    }
                    foreach(var appServer in ActConn.DestinationAppServers)
                    {
                        var dstParams = new { appServerId = appServer.Content.Id, connectionId = ActConn.Id, connectionField = (int)ModellingTypes.ConnectionField.Destination };
                        await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addAppServerToConnection, dstParams);
                        await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ObjectType.Connection, ActConn.Id,
                            $"Added App Server {appServer.Content.Display()} to {(ActConn.IsInterface? "Interface" : "Connection")}: {ActConn.Name}: Destination", Application.Id);
                    }
                    foreach(var appRole in ActConn.SourceAppRoles)
                    {
                        var srcParams = new { nwGroupId = appRole.Content.Id, connectionId = ActConn.Id, connectionField = (int)ModellingTypes.ConnectionField.Source };
                        await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addNwGroupToConnection, srcParams);
                        await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ObjectType.Connection, ActConn.Id,
                            $"Added App Role {appRole.Content.Display()} to {(ActConn.IsInterface? "Interface" : "Connection")}: {ActConn.Name}: Source", Application.Id);
                    }
                    foreach(var nwGroup in ActConn.SourceNwGroups)
                    {
                        var srcParams = new { nwGroupId = nwGroup.Content.Id, connectionId = ActConn.Id, connectionField = (int)ModellingTypes.ConnectionField.Source };
                        await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addNwGroupToConnection, srcParams);
                        await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ObjectType.Connection, ActConn.Id,
                            $"Added Object {nwGroup.Content.Display()} to {(ActConn.IsInterface? "Interface" : "Connection")}: {ActConn.Name}: Source", Application.Id);
                    }
                    foreach(var appRole in ActConn.DestinationAppRoles)
                    {
                        var dstParams = new { nwGroupId = appRole.Content.Id, connectionId = ActConn.Id, connectionField = (int)ModellingTypes.ConnectionField.Destination };
                        await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addNwGroupToConnection, dstParams);
                        await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ObjectType.Connection, ActConn.Id,
                            $"Added App Role {appRole.Content.Display()} to {(ActConn.IsInterface? "Interface" : "Connection")}: {ActConn.Name}: Destination", Application.Id);
                    }
                    foreach(var nwGroup in ActConn.DestinationNwGroups)
                    {
                        var dstParams = new { nwGroupId = nwGroup.Content.Id, connectionId = ActConn.Id, connectionField = (int)ModellingTypes.ConnectionField.Destination };
                        await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addNwGroupToConnection, dstParams);
                        await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ObjectType.Connection, ActConn.Id,
                            $"Added Object {nwGroup.Content.Display()} to {(ActConn.IsInterface? "Interface" : "Connection")}: {ActConn.Name}: Destination", Application.Id);
                    }
                    foreach(var service in ActConn.Services)
                    {
                        var svcParams = new { serviceId = service.Content.Id, connectionId = ActConn.Id };
                        await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addServiceToConnection, svcParams);
                        await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ObjectType.Connection, ActConn.Id,
                            $"Added Service {service.Content.Display()} to {(ActConn.IsInterface? "Interface" : "Connection")}: {ActConn.Name}", Application.Id);
                    }
                    foreach(var serviceGrp in ActConn.ServiceGroups)
                    {
                        var svcGrpParams = new { serviceGroupId = serviceGrp.Content.Id, connectionId = ActConn.Id };
                        await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addServiceGroupToConnection, svcGrpParams);
                        await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ObjectType.Connection, ActConn.Id,
                            $"Added Service Group {serviceGrp.Content.Display()} to {(ActConn.IsInterface? "Interface" : "Connection")}: {ActConn.Name}", Application.Id);
                    }
                    ActConn.Creator = userConfig.User.Name;
                    ActConn.CreationDate = DateTime.Now;
                    Connections.Add(ActConn);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("add_connection"), "", true);
            }
        }

        private async Task UpdateConnectionInDb()
        {
            try
            {
                var Variables = new
                {
                    id = ActConn.Id,
                    name = ActConn.Name,
                    appId = Application.Id,
                    reason = ActConn.Reason,
                    isInterface = ActConn.IsInterface,
                    usedInterfaceId = ActConn.UsedInterfaceId
                };
                await apiConnection.SendQueryAsync<NewReturning>(ModellingQueries.updateConnection, Variables);
                await LogChange(ModellingTypes.ChangeType.Update, ModellingTypes.ObjectType.Connection, ActConn.Id,
                    $"Updated {(ActConn.IsInterface? "Interface" : "Connection")}: {ActConn.Name}", Application.Id);

                foreach(var appServer in SrcAppServerToDelete)
                {
                    var srcParams = new { appServerId = appServer.Id, connectionId = ActConn.Id, connectionField = (int)ModellingTypes.ConnectionField.Source };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeAppServerFromConnection, srcParams);
                    await LogChange(ModellingTypes.ChangeType.Unassign, ModellingTypes.ObjectType.Connection, ActConn.Id,
                        $"Removed App Server {appServer.Display()} from {(ActConn.IsInterface? "Interface" : "Connection")}: {ActConn.Name}: Source", Application.Id);
                }
                foreach(var appServer in SrcAppServerToAdd)
                {
                    var srcParams = new { appServerId = appServer.Id, connectionId = ActConn.Id, connectionField = (int)ModellingTypes.ConnectionField.Source };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addAppServerToConnection, srcParams);
                    await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ObjectType.Connection, ActConn.Id,
                        $"Added App Server {appServer.Display()} to {(ActConn.IsInterface? "Interface" : "Connection")}: {ActConn.Name}: Source", Application.Id);
                }
                foreach(var appServer in DstAppServerToDelete)
                {
                    var dstParams = new { appServerId = appServer.Id, connectionId = ActConn.Id, connectionField = (int)ModellingTypes.ConnectionField.Destination };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeAppServerFromConnection, dstParams);
                    await LogChange(ModellingTypes.ChangeType.Unassign, ModellingTypes.ObjectType.Connection, ActConn.Id,
                        $"Removed App Server {appServer.Display()} from {(ActConn.IsInterface? "Interface" : "Connection")}: {ActConn.Name}: Destination", Application.Id);
                }
                foreach(var appServer in DstAppServerToAdd)
                {
                    var dstParams = new { appServerId = appServer.Id, connectionId = ActConn.Id, connectionField = (int)ModellingTypes.ConnectionField.Destination };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addAppServerToConnection, dstParams);
                    await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ObjectType.Connection, ActConn.Id,
                        $"Added App Server {appServer.Display()} to {(ActConn.IsInterface? "Interface" : "Connection")}: {ActConn.Name}: Destination", Application.Id);
                }
                foreach(var appRole in SrcAppRolesToDelete)
                {
                    var srcParams = new { nwGroupId = appRole.Id, connectionId = ActConn.Id, connectionField = (int)ModellingTypes.ConnectionField.Source };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeNwGroupFromConnection, srcParams);
                    await LogChange(ModellingTypes.ChangeType.Unassign, ModellingTypes.ObjectType.Connection, ActConn.Id,
                        $"Removed App Role {appRole.Display()} from {(ActConn.IsInterface? "Interface" : "Connection")}: {ActConn.Name}: Source", Application.Id);
                }
                foreach(var appRole in SrcAppRolesToAdd)
                {
                    var srcParams = new { nwGroupId = appRole.Id, connectionId = ActConn.Id, connectionField = (int)ModellingTypes.ConnectionField.Source };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addNwGroupToConnection, srcParams);
                    await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ObjectType.Connection, ActConn.Id,
                        $"Added App Role {appRole.Display()} to {(ActConn.IsInterface? "Interface" : "Connection")}: {ActConn.Name}: Source", Application.Id);
                }
                foreach(var appRole in DstAppRolesToDelete)
                {
                    var dstParams = new { nwGroupId = appRole.Id, connectionId = ActConn.Id, connectionField = (int)ModellingTypes.ConnectionField.Destination };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeNwGroupFromConnection, dstParams);
                    await LogChange(ModellingTypes.ChangeType.Unassign, ModellingTypes.ObjectType.Connection, ActConn.Id,
                        $"Removed App Role {appRole.Display()} from {(ActConn.IsInterface? "Interface" : "Connection")}: {ActConn.Name}: Destination", Application.Id);
                }
                foreach(var appRole in DstAppRolesToAdd)
                {
                    var dstParams = new { nwGroupId = appRole.Id, connectionId = ActConn.Id, connectionField = (int)ModellingTypes.ConnectionField.Destination };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addNwGroupToConnection, dstParams);
                    await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ObjectType.Connection, ActConn.Id,
                        $"Added App Role {appRole.Display()} to {(ActConn.IsInterface? "Interface" : "Connection")}: {ActConn.Name}: Destination", Application.Id);
                }
                foreach(var nwGroup in SrcNwGroupsToDelete)
                {
                    var srcParams = new { nwGroupId = nwGroup.Id, connectionId = ActConn.Id, connectionField = (int)ModellingTypes.ConnectionField.Source };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeNwGroupFromConnection, srcParams);
                    await LogChange(ModellingTypes.ChangeType.Unassign, ModellingTypes.ObjectType.Connection, ActConn.Id,
                        $"Removed Object {nwGroup.Display()} from {(ActConn.IsInterface? "Interface" : "Connection")}: {ActConn.Name}: Source", Application.Id);
                }
                foreach(var nwGroup in SrcNwGroupsToAdd)
                {
                    var srcParams = new { nwGroupId = nwGroup.Id, connectionId = ActConn.Id, connectionField = (int)ModellingTypes.ConnectionField.Source };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addNwGroupToConnection, srcParams);
                    await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ObjectType.Connection, ActConn.Id,
                        $"Added Object {nwGroup.Display()} to {(ActConn.IsInterface? "Interface" : "Connection")}: {ActConn.Name}: Source", Application.Id);
                }
                foreach(var nwGroup in DstNwGroupsToDelete)
                {
                    var dstParams = new { nwGroupId = nwGroup.Id, connectionId = ActConn.Id, connectionField = (int)ModellingTypes.ConnectionField.Destination };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeNwGroupFromConnection, dstParams);
                    await LogChange(ModellingTypes.ChangeType.Unassign, ModellingTypes.ObjectType.Connection, ActConn.Id,
                        $"Removed Object {nwGroup.Display()} from {(ActConn.IsInterface? "Interface" : "Connection")}: {ActConn.Name}: Destination", Application.Id);
                }
                foreach(var nwGroup in DstNwGroupsToAdd)
                {
                    var dstParams = new { nwGroupId = nwGroup.Id, connectionId = ActConn.Id, connectionField = (int)ModellingTypes.ConnectionField.Destination };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addNwGroupToConnection, dstParams);
                    await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ObjectType.Connection, ActConn.Id,
                        $"Added Object {nwGroup.Display()} to {(ActConn.IsInterface? "Interface" : "Connection")}: {ActConn.Name}: Destination", Application.Id);
                }                
                foreach(var service in SvcToDelete)
                {
                    var svcParams = new { serviceId = service.Id, connectionId = ActConn.Id };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeServiceFromConnection, svcParams);
                    await LogChange(ModellingTypes.ChangeType.Unassign, ModellingTypes.ObjectType.Connection, ActConn.Id,
                        $"Removed Service {service.Display()} from {(ActConn.IsInterface? "Interface" : "Connection")}: {ActConn.Name}", Application.Id);
                }
                foreach(var service in SvcToAdd)
                {
                    var svcParams = new { serviceId = service.Id, connectionId = ActConn.Id };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addServiceToConnection, svcParams);
                    await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ObjectType.Connection, ActConn.Id,
                        $"Added Service {service.Display()} to {(ActConn.IsInterface? "Interface" : "Connection")}: {ActConn.Name}", Application.Id);
                }
                foreach(var serviceGrp in SvcGrpToDelete)
                {
                    var svcGrpParams = new { serviceGroupId = serviceGrp.Id, connectionId = ActConn.Id };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeServiceGroupFromConnection, svcGrpParams);
                    await LogChange(ModellingTypes.ChangeType.Unassign, ModellingTypes.ObjectType.Connection, ActConn.Id,
                        $"Removed Service Group {serviceGrp.Display()} from {(ActConn.IsInterface? "Interface" : "Connection")}: {ActConn.Name}", Application.Id);
                }
                foreach(var serviceGrp in SvcGrpToAdd)
                {
                    var svcGrpParams = new { serviceGroupId = serviceGrp.Id, connectionId = ActConn.Id };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addServiceGroupToConnection, svcGrpParams);
                    await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ObjectType.Connection, ActConn.Id,
                        $"Added Service Group {serviceGrp.Display()} to {(ActConn.IsInterface? "Interface" : "Connection")}: {ActConn.Name}", Application.Id);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("edit_connection"), "", true);
            }
        }

        public void Reset()
        {
            ActConn = ActConnOrig;
            if(!AddMode)
            {
                Connections[Connections.FindIndex(x => x.Id == ActConn.Id)] = ActConnOrig;
            }
        }

        public void Close()
        {
            SrcAppServerToAdd = new List<ModellingAppServer>();
            SrcAppServerToDelete = new List<ModellingAppServer>();
            DstAppServerToAdd = new List<ModellingAppServer>();
            DstAppServerToDelete = new List<ModellingAppServer>();
            SrcAppRolesToAdd = new List<ModellingAppRole>();
            SrcAppRolesToDelete = new List<ModellingAppRole>();
            DstAppRolesToAdd = new List<ModellingAppRole>();
            DstAppRolesToDelete = new List<ModellingAppRole>();
            SrcNwGroupsToAdd = new List<ModellingNwGroupObject>();
            SrcNwGroupsToDelete = new List<ModellingNwGroupObject>();
            DstNwGroupsToAdd = new List<ModellingNwGroupObject>();
            DstNwGroupsToDelete = new List<ModellingNwGroupObject>();
            SvcToAdd = new List<ModellingService>();
            SvcToDelete = new List<ModellingService>();
            SvcGrpToAdd = new List<ModellingServiceGroup>();
            SvcGrpToDelete = new List<ModellingServiceGroup>();
            SearchNWObjectMode = false;
            DeleteNwObjectMode = false;
            AddAppRoleMode = false;
            EditAppRoleMode = false;
            DeleteAppRoleMode = false;
            AddSvcGrpMode = false;
            EditSvcGrpMode = false;
            DeleteSvcGrpMode = false;
            AddServiceMode = false;
            EditServiceMode = false;
            DeleteServiceMode = false;
        }
    }
}

using FWO.Config.Api;
using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;


namespace FWO.Ui.Services
{
    public class ModellingConnectionHandler
    {
        public FwoOwner Application { get; set; } = new();
        public List<ModellingConnection> Connections { get; set; } = new();
        public ModellingConnection ActConn { get; set; } = new();
        public List<ModellingAppServer> AvailableAppServers { get; set; } = new();
        public List<ModellingAppRole> AvailableAppRoles { get; set; } = new();
        public List<ModellingServiceGroup> AvailableServiceGroups { get; set; } = new();
        public List<ModellingService> AvailableServices { get; set; } = new();
        public List<ModellingConnection> AvailableInterfaces { get; set; } = new();

        public string deleteMessage = "";
        public bool ReadOnly = false;
        public bool AddMode = false;

        public bool srcReadOnly { get; set; } = false;
        public bool dstReadOnly { get; set; } = false;
        public bool svcReadOnly { get; set; } = false;

        public ModellingAppServerHandler AppServerHandler;
        public List<ModellingAppServer> SrcAppServerToAdd { get; set; } = new();
        public List<ModellingAppServer> SrcAppServerToDelete { get; set; } = new();
        public List<ModellingAppServer> DstAppServerToAdd { get; set; } = new();
        public List<ModellingAppServer> DstAppServerToDelete { get; set; } = new();
        public bool AddAppServerMode = false;
        public bool EditAppServerMode = false;
        public bool DeleteAppServerMode = false;

        public ModellingAppRoleHandler AppRoleHandler;
        public List<ModellingAppRole> SrcAppRolesToAdd { get; set; } = new();
        public List<ModellingAppRole> SrcAppRolesToDelete { get; set; } = new();
        public List<ModellingAppRole> DstAppRolesToAdd { get; set; } = new();
        public List<ModellingAppRole> DstAppRolesToDelete { get; set; } = new();
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
        private ModellingService actService = new();
        private ModellingServiceGroup actServiceGroup = new();

        private readonly ApiConnection apiConnection;
        private readonly UserConfig userConfig;
        private Action<Exception?, string, string, bool> DisplayMessageInUi { get; set; } = DefaultInit.DoNothing;


        public ModellingConnectionHandler(ApiConnection apiConnection, UserConfig userConfig, FwoOwner application, 
            List<ModellingConnection> connections, ModellingConnection conn, bool addMode, bool readOnly, 
            Action<Exception?, string, string, bool> displayMessageInUi)
        {
            this.apiConnection = apiConnection;
            this.userConfig = userConfig;
            Application = application;
            Connections = connections;
            ActConn = conn;
            AddMode = addMode;
            ReadOnly = readOnly;
            DisplayMessageInUi = displayMessageInUi;
        }

        public async Task Init()
        {
            try
            {
                AvailableAppServers = await apiConnection.SendQueryAsync<List<ModellingAppServer>>(ModellingQueries.getAppServers, new { appId = Application.Id });
                AvailableAppRoles = await apiConnection.SendQueryAsync<List<ModellingAppRole>>(ModellingQueries.getAppRoles, new { appId = Application.Id });
                AvailableServiceGroups = await apiConnection.SendQueryAsync<List<ModellingServiceGroup>>(ModellingQueries.getServiceGroupsForApp, new { appId = Application.Id });
                AvailableServices = await apiConnection.SendQueryAsync<List<ModellingService>>(ModellingQueries.getServicesForApp, new { appId = Application.Id });
                AvailableInterfaces = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getInterfaces);
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("fetch_data"), "", true);
            }
        }

        public void InterfaceToConn(ModellingConnection interf)
        {
            srcReadOnly = interf.SourceAppServers.Count > 0;
            dstReadOnly = !srcReadOnly;
            svcReadOnly = true;
            ActConn.IsInterface = false;
            ActConn.UsedInterfaceId = interf.Id;
            if(srcReadOnly)
            {
                ActConn.SourceAppServers = new List<ModellingAppServerWrapper>(interf.SourceAppServers){};
            }
            else
            {
                ActConn.DestinationAppServers = new List<ModellingAppServerWrapper>(interf.DestinationAppServers){};
            }
            ActConn.Services = new List<ModellingServiceWrapper>(interf.Services){};
        }

        public void RemoveInterf()
        {
            if(srcReadOnly)
            {
                ActConn.SourceAppServers = new();
            }
            if(dstReadOnly)
            {
                ActConn.DestinationAppServers = new();
            }
            ActConn.Services = new();
            ActConn.UsedInterfaceId = null;
            srcReadOnly = false;
            dstReadOnly = false;
            svcReadOnly = false;
        }
        
        public async Task CreateAppServer()
        {
            AddAppServerMode = true;
            await HandleAppServer(new ModellingAppServer(){ ImportSource = GlobalConfig.kManual });
        }

        public async Task EditAppServer(ModellingAppServer appServer)
        {
            AddAppServerMode = false;
            await HandleAppServer(appServer);
        }

        public async Task HandleAppServer(ModellingAppServer appServer)
        {
            AppServerHandler = new ModellingAppServerHandler(apiConnection, userConfig, Application, appServer, AvailableAppServers, AddAppServerMode, DisplayMessageInUi);
            EditAppServerMode = true;
        }

        public void RequestDeleteAppServer(ModellingAppServer appServer)
        {
            actAppServer = appServer;
            deleteMessage = userConfig.GetText("U9003") + appServer.Name + "?";
            DeleteAppServerMode = true;
        }

        public async Task DeleteAppServer()
        {
            try
            {
                DeleteAppServerMode = await ModellingAppServerHandler.DeleteAppServer(actAppServer, AvailableAppServers, apiConnection);
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

        public async Task CreateAppRole()
        {
            AddAppRoleMode = true;
            await HandleAppRole(new ModellingAppRole(){});
        }

        public async Task EditAppRole(ModellingAppRole appRole)
        {
            AddAppRoleMode = false;
            await HandleAppRole(appRole);
        }

        public async Task HandleAppRole(ModellingAppRole appRole)
        {
            AppRoleHandler = new ModellingAppRoleHandler(apiConnection, userConfig, Application, AvailableAppRoles, appRole, AvailableAppServers, AddAppRoleMode, DisplayMessageInUi);
            EditAppRoleMode = true;
        }

        public void RequestDeleteAppRole(ModellingAppRole appRole)
        {
            actAppRole = appRole;
            deleteMessage = userConfig.GetText("U9002") + appRole.Name + "?";
            DeleteAppRoleMode = true;
        }

        public async Task DeleteAppRole()
        {
            try
            {
                if((await apiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.ModellingQueries.deleteAppRole, new { id = actAppRole.Id })).AffectedRows > 0)
                {
                    AvailableAppRoles.Remove(actAppRole);
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

        public async Task CreateServiceGroup()
        {
            AddSvcGrpMode = true;
            await HandleServiceGroup(new ModellingServiceGroup(){});
        }

        public async Task EditServiceGroup(ModellingServiceGroup serviceGroup)
        {
            AddSvcGrpMode = false;
            await HandleServiceGroup(serviceGroup);
        }

        public async Task HandleServiceGroup(ModellingServiceGroup serviceGroup)
        {
            SvcGrpHandler = new ModellingServiceGroupHandler(apiConnection, userConfig, Application, AvailableServiceGroups, serviceGroup, AvailableServices, AddSvcGrpMode, DisplayMessageInUi);
            EditSvcGrpMode = true;
        }

        public void RequestDeleteServiceGrp(ModellingServiceGroup serviceGroup)
        {
            actServiceGroup = serviceGroup;
            deleteMessage = userConfig.GetText("U9004") + serviceGroup.Name + "?";
            DeleteSvcGrpMode = true;
        }

        public async Task DeleteServiceGroup()
        {
            try
            {
                if((await apiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.ModellingQueries.deleteServiceGroup, new { id = actServiceGroup.Id })).AffectedRows > 0)
                {
                    AvailableServiceGroups.Remove(actServiceGroup);
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


        public async Task CreateService()
        {
            AddServiceMode = true;
            await HandleService(new ModellingService(){});
        }

        public async Task EditService(ModellingService service)
        {
            AddServiceMode = false;
            await HandleService(service);
        }

        public async Task HandleService(ModellingService service)
        {
            ServiceHandler = new ModellingServiceHandler(apiConnection, userConfig, Application, service, AvailableServices, AddServiceMode, DisplayMessageInUi);
            EditServiceMode = true;
        }

        public void RequestDeleteService(ModellingService service)
        {
            actService = service;
            deleteMessage = userConfig.GetText("U9003") + service.Name + "?";
            DeleteServiceMode = true;
        }

        public async Task DeleteService()
        {
            try
            {
                if((await apiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.ModellingQueries.deleteService, new { id = actService.Id })).AffectedRows > 0)
                {
                    AvailableServices.Remove(actService);
                    DeleteServiceMode = false;
                }
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
                srcReadOnly = ActConn.SourceAppServers.Count > 0;
                dstReadOnly = !srcReadOnly;
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
            return ActConn.SourceAppRoles != null && ActConn.SourceAppRoles.Count > 0 || 
                ActConn.SourceAppServers != null && ActConn.SourceAppServers.Count > 0 ||
                SrcAppServerToAdd != null && SrcAppServerToAdd.Count > 0 ||
                SrcAppRolesToAdd != null && SrcAppRolesToAdd.Count > 0;
        }

        public bool DstFilled()
        {
            return ActConn.DestinationAppRoles != null && ActConn.DestinationAppRoles.Count > 0 || 
                ActConn.DestinationAppServers != null && ActConn.DestinationAppServers.Count > 0 ||
                DstAppServerToAdd != null && DstAppServerToAdd.Count > 0 ||
                DstAppRolesToAdd != null && DstAppRolesToAdd.Count > 0;
        }

        public async Task Save()
        {
            if(checkConn())
            {
                if(!srcReadOnly)
                {
                    foreach(var appServer in SrcAppServerToDelete)
                    {
                        ActConn.SourceAppServers.Remove(ActConn.SourceAppServers.FirstOrDefault(x => x.Content.Id == appServer.Id));
                    }
                    foreach(var appServer in SrcAppServerToAdd)
                    {
                        ActConn.SourceAppServers.Add(new ModellingAppServerWrapper(){ Content = appServer });
                    }
                    foreach(var appRole in SrcAppRolesToDelete)
                    {
                        ActConn.SourceAppRoles.Remove(ActConn.SourceAppRoles.FirstOrDefault(x => x.Content.Id == appRole.Id));
                    }
                    foreach(var appRole in SrcAppRolesToAdd)
                    {
                        ActConn.SourceAppRoles.Add(new ModellingAppRoleWrapper(){ Content = appRole });
                    }
                }
                if(!dstReadOnly)
                {
                    foreach(var appServer in DstAppServerToDelete)
                    {
                        ActConn.DestinationAppServers.Remove(ActConn.DestinationAppServers.FirstOrDefault(x => x.Content.Id == appServer.Id));
                    }
                    foreach(var appServer in DstAppServerToAdd)
                    {
                        ActConn.DestinationAppServers.Add(new ModellingAppServerWrapper(){ Content = appServer });
                    }
                    foreach(var appRole in DstAppRolesToDelete)
                    {
                        ActConn.DestinationAppRoles.Remove(ActConn.DestinationAppRoles.FirstOrDefault(x => x.Content.Id == appRole.Id));
                    }
                    foreach(var appRole in DstAppRolesToAdd)
                    {
                        ActConn.DestinationAppRoles.Add(new ModellingAppRoleWrapper(){ Content = appRole });
                    }
                }
                if(!svcReadOnly)
                {
                    foreach(var svc in SvcToDelete)
                    {
                        ActConn.Services.Remove(ActConn.Services.FirstOrDefault(x => x.Content.Id == svc.Id));
                    }
                    foreach(var svc in SvcToAdd)
                    {
                        ActConn.Services.Add(new ModellingServiceWrapper(){ Content = svc });
                    }
                    foreach(var svcGrp in SvcGrpToDelete)
                    {
                        ActConn.ServiceGroups.Remove(ActConn.ServiceGroups.FirstOrDefault(x => x.Content.Id == svcGrp.Id));
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
            }
        }

        private bool checkConn()
        {
            if(ActConn.IsInterface && !SrcFilled() && !DstFilled())
            {
                DisplayMessageInUi(null, userConfig.GetText("edit_connection"), userConfig.GetText("Exxxx"), true);
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
                    usedInterfaceId = ActConn.UsedInterfaceId
                };
                ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<NewReturning>(ModellingQueries.newConnection, Variables)).ReturnIds;
                if (returnIds != null)
                {
                    ActConn.Id = returnIds[0].NewId;
                    foreach(var appServer in ActConn.SourceAppServers)
                    {
                        var srcParams = new { appServerId = appServer.Content.Id, connectionId = ActConn.Id, connectionField = (int)ConnectionField.Source };
                        await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addAppServerToConnection, srcParams);
                    }
                    foreach(var appServer in ActConn.DestinationAppServers)
                    {
                        var dstParams = new { appServerId = appServer.Content.Id, connectionId = ActConn.Id, connectionField = (int)ConnectionField.Destination };
                        await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addAppServerToConnection, dstParams);
                    }
                    foreach(var appRole in ActConn.SourceAppRoles)
                    {
                        var srcParams = new { appRoleId = appRole.Content.Id, connectionId = ActConn.Id, connectionField = (int)ConnectionField.Source };
                        await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addAppRoleToConnection, srcParams);
                    }
                    foreach(var appRole in ActConn.DestinationAppRoles)
                    {
                        var dstParams = new { appRoleId = appRole.Content.Id, connectionId = ActConn.Id, connectionField = (int)ConnectionField.Destination };
                        await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addAppRoleToConnection, dstParams);
                    }
                    foreach(var service in ActConn.Services)
                    {
                        var svcParams = new { serviceId = service.Content.Id, connectionId = ActConn.Id };
                        await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addServiceToConnection, svcParams);
                    }
                    foreach(var serviceGrp in ActConn.ServiceGroups)
                    {
                        var svcGrpParams = new { serviceGroupId = serviceGrp.Content.Id, connectionId = ActConn.Id };
                        await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addServiceGroupToConnection, svcGrpParams);
                    }
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

                foreach(var appServer in SrcAppServerToDelete)
                {
                    var srcParams = new { appServerId = appServer.Id, connectionId = ActConn.Id, connectionField = (int)ConnectionField.Source };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeAppServerFromConnection, srcParams);
                }
                foreach(var appServer in SrcAppServerToAdd)
                {
                    var srcParams = new { appServerId = appServer.Id, connectionId = ActConn.Id, connectionField = (int)ConnectionField.Source };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addAppServerToConnection, srcParams);
                }
                foreach(var appServer in DstAppServerToDelete)
                {
                    var dstParams = new { appServerId = appServer.Id, connectionId = ActConn.Id, connectionField = (int)ConnectionField.Destination };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeAppServerFromConnection, dstParams);
                }
                foreach(var appServer in DstAppServerToAdd)
                {
                    var dstParams = new { appServerId = appServer.Id, connectionId = ActConn.Id, connectionField = (int)ConnectionField.Destination };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addAppServerToConnection, dstParams);
                }
                foreach(var appRole in SrcAppRolesToDelete)
                {
                    var srcParams = new { appRoleId = appRole.Id, connectionId = ActConn.Id, connectionField = (int)ConnectionField.Source };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeAppRoleFromConnection, srcParams);
                }
                foreach(var appRole in SrcAppRolesToAdd)
                {
                    var srcParams = new { appRoleId = appRole.Id, connectionId = ActConn.Id, connectionField = (int)ConnectionField.Source };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addAppRoleToConnection, srcParams);
                }
                foreach(var appRole in DstAppRolesToDelete)
                {
                    var dstParams = new { appRoleId = appRole.Id, connectionId = ActConn.Id, connectionField = (int)ConnectionField.Destination };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeAppRoleFromConnection, dstParams);
                }
                foreach(var appRole in DstAppRolesToAdd)
                {
                    var dstParams = new { appRoleId = appRole.Id, connectionId = ActConn.Id, connectionField = (int)ConnectionField.Destination };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addAppRoleToConnection, dstParams);
                }
                foreach(var service in SvcToDelete)
                {
                    var svcParams = new { serviceId = service.Id, connectionId = ActConn.Id };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeServiceFromConnection, svcParams);
                }
                foreach(var service in SvcToAdd)
                {
                    var svcParams = new { serviceId = service.Id, connectionId = ActConn.Id };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addServiceToConnection, svcParams);
                }
                foreach(var serviceGrp in SvcGrpToDelete)
                {
                    var svcGrpParams = new { serviceGroupId = serviceGrp.Id, connectionId = ActConn.Id };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeServiceGroupFromConnection, svcGrpParams);
                }
                foreach(var serviceGrp in SvcGrpToAdd)
                {
                    var svcGrpParams = new { serviceGroupId = serviceGrp.Id, connectionId = ActConn.Id };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addServiceGroupToConnection, svcGrpParams);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("add_connection"), "", true);
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
            SvcToAdd = new List<ModellingService>();
            SvcToDelete = new List<ModellingService>();
            SvcGrpToAdd = new List<ModellingServiceGroup>();
            SvcGrpToDelete = new List<ModellingServiceGroup>();
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

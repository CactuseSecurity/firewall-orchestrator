using FWO.Config.Api;
using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Logging;


namespace FWO.Ui.Services
{
    public class ModellingConnectionHandler
    {
        public FwoOwner Application { get; set; } = new();
        public List<NetworkConnection> Connections { get; set; } = new();
        public NetworkConnection ActConn { get; set; } = new();
        public List<NetworkObject> AvailableAppServer { get; set; } = new();
        public List<AppRole> AvailableAppRoles { get; set; } = new();
        public List<ServiceGroup> AvailableServiceGroups { get; set; } = new();
        public List<ModellingService> AvailableServices { get; set; } = new();
        public List<NetworkConnection> AvailableInterfaces { get; set; } = new();

        public List<ModellingService> SvcToAdd { get; set; } = new();
        public List<ModellingService> SvcToDelete { get; set; } = new();

        // public bool DisplayInterfaces { get; set; } = false;
        // public bool ToSrcAllowed { get; set; } = true;
        // public bool ToDestAllowed { get; set; } = true;
        // public bool ToSvcAllowed { get; set; } = true;

        public bool srcReadOnly { get; set; } = false;
        public bool dstReadOnly { get; set; } = false;
        public bool svcReadOnly { get; set; } = false;

        public List<NetworkObject> SrcAppServerToAdd { get; set; } = new List<NetworkObject>();
        public List<NetworkObject> SrcAppServerToDelete { get; set; } = new List<NetworkObject>();
        public List<NetworkObject> DstAppServerToAdd { get; set; } = new List<NetworkObject>();
        public List<NetworkObject> DstAppServerToDelete { get; set; } = new List<NetworkObject>();

        public List<AppRole> SrcAppRolesToAdd { get; set; } = new List<AppRole>();
        public List<AppRole> SrcAppRolesToDelete { get; set; } = new List<AppRole>();
        public List<AppRole> DstAppRolesToAdd { get; set; } = new List<AppRole>();
        public List<AppRole> DstAppRolesToDelete { get; set; } = new List<AppRole>();
        public bool AddAppRoleMode = false;
        public bool EditAppRoleMode = false;
        public bool DeleteAppRoleMode = false;
        public ModellingAppRoleHandler AppRoleHandler;

        public List<ServiceGroup> SvcGrpToAdd { get; set; } = new();
        public List<ServiceGroup> SvcGrpToDelete { get; set; } = new();
        public bool AddSvcGrpMode = false;
        public bool EditSvcGrpMode = false;
        public bool DeleteSvcGrpMode = false;
        public ModellingServiceGroupHandler SvcGrpHandler;
        public bool AddServiceMode = false;
        public bool EditServiceMode = false;
        public bool DeleteServiceMode = false;

        public ModellingServiceHandler ServiceHandler;
        public string deleteMessage = "";
        public bool ReadOnly = false;
        public bool AddMode = false;


        private AppRole actAppRole = new();
        private ServiceGroup actServiceGroup = new();
        private ModellingService actService = new();

        private readonly ApiConnection apiConnection;
        private readonly UserConfig userConfig;
        private Action<Exception?, string, string, bool> DisplayMessageInUi { get; set; } = DefaultInit.DoNothing;


        public ModellingConnectionHandler(ApiConnection apiConnection, UserConfig userConfig, FwoOwner application, 
            List<NetworkConnection> connections, NetworkConnection conn, bool addMode, bool readOnly, 
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
                AvailableAppServer = await apiConnection.SendQueryAsync<List<NetworkObject>>(FWO.Api.Client.Queries.ModellingQueries.getAppServer, new { appId = Application.Id });
                AvailableAppRoles = await apiConnection.SendQueryAsync<List<AppRole>>(FWO.Api.Client.Queries.ModellingQueries.getAppRoles, new { appId = Application.Id });
                AvailableServiceGroups = await apiConnection.SendQueryAsync<List<ServiceGroup>>(FWO.Api.Client.Queries.ModellingQueries.getServiceGroupsForApp, new { appId = Application.Id });
                AvailableServices = await apiConnection.SendQueryAsync<List<ModellingService>>(FWO.Api.Client.Queries.ModellingQueries.getServicesForApp, new { appId = Application.Id });
                AvailableInterfaces = await apiConnection.SendQueryAsync<List<NetworkConnection>>(FWO.Api.Client.Queries.ModellingQueries.getInterfaces);
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("fetch_data"), "", true);
            }
        }

        public void InterfaceToConn(NetworkConnection interf)
        {
            srcReadOnly = interf.Sources.Count > 0;
            dstReadOnly = !srcReadOnly;
            svcReadOnly = true;
            ActConn.IsInterface = false;
            ActConn.UsedInterfaceId = interf.Id;
            if(srcReadOnly)
            {
                ActConn.Sources = new List<NetworkObject>(interf.Sources){};
            }
            else
            {
                ActConn.Destinations = new List<NetworkObject>(interf.Destinations){};
            }
            ActConn.Services = new List<ModellingService>(interf.Services){};
        }

        public void RemoveInterf()
        {
            if(srcReadOnly)
            {
                ActConn.Sources = new();
            }
            if(dstReadOnly)
            {
                ActConn.Destinations = new();
            }
            ActConn.Services = new();
            ActConn.UsedInterfaceId = null;
            srcReadOnly = false;
            dstReadOnly = false;
            svcReadOnly = false;
        }
        
        public void AppServerToSource(List<NetworkObject> sources)
        {
            if(!SrcDropForbidden())
            {
                foreach(var source in sources)
                {
                    if(!ActConn.Sources.Contains(source) && !SrcAppServerToAdd.Contains(source))
                    {
                        SrcAppServerToAdd.Add(source);
                    }
                }
                CalcVisibility();
            }
        }

        public void AppServerToDestination(List<NetworkObject> dests)
        {
            if(!DstDropForbidden())
            {
                foreach(var dest in dests)
                {
                    if(!ActConn.Destinations.Contains(dest) && !DstAppServerToAdd.Contains(dest))
                    {
                        DstAppServerToAdd.Add(dest);
                    }
                }
                CalcVisibility();
            }
        }

        public async Task CreateAppRole()
        {
            AddAppRoleMode = true;
            await HandleAppRole(new AppRole(){});
        }

        public async Task EditAppRole(AppRole appRole)
        {
            AddAppRoleMode = false;
            await HandleAppRole(appRole);
        }

        public async Task HandleAppRole(AppRole appRole)
        {
            AppRoleHandler = new ModellingAppRoleHandler(apiConnection, userConfig, Application, AvailableAppRoles, appRole, AvailableAppServer, AddAppRoleMode, DisplayMessageInUi);
            EditAppRoleMode = true;
        }

        public void RequestDeleteAppRole(AppRole appRole)
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

        public void AppRolesToSource(List<AppRole> appRoles)
        {
            if(!SrcDropForbidden())
            {
                foreach(var appRole in appRoles)
                {
                    if(!ActConn.SrcAppRoles.Contains(appRole) && !SrcAppRolesToAdd.Contains(appRole))
                    {
                        SrcAppRolesToAdd.Add(appRole);
                    }
                }
                CalcVisibility();
            }
        }

        public void AppRolesToDestination(List<AppRole> dests)
        {
            if(!DstDropForbidden())
            {
                foreach(var dest in dests)
                {
                    if(!ActConn.DstAppRoles.Contains(dest) && !DstAppRolesToAdd.Contains(dest))
                    {
                        DstAppRolesToAdd.Add(dest);
                    }
                }
                CalcVisibility();
            }
        }

        public async Task CreateServiceGroup()
        {
            AddSvcGrpMode = true;
            await HandleServiceGroup(new ServiceGroup(){});
        }

        public async Task EditServiceGroup(ServiceGroup serviceGroup)
        {
            AddSvcGrpMode = false;
            await HandleServiceGroup(serviceGroup);
        }

        public async Task HandleServiceGroup(ServiceGroup serviceGroup)
        {
            SvcGrpHandler = new ModellingServiceGroupHandler(apiConnection, userConfig, Application, AvailableServiceGroups, serviceGroup, AvailableServices, AddSvcGrpMode, DisplayMessageInUi);
            EditSvcGrpMode = true;
        }

        public void RequestDeleteServiceGrp(ServiceGroup serviceGroup)
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

        public void ServiceGrpsToConn(List<ServiceGroup> serviceGrps)
        {
            foreach(var grp in serviceGrps)
            {
                if(!ActConn.ServiceGroups.Contains(grp) && !SvcGrpToAdd.Contains(grp))
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
            ServiceHandler = new ModellingServiceHandler(apiConnection, userConfig, Application, service, AddServiceMode, DisplayMessageInUi);
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
                if(!ActConn.Services.Contains(svc) && !SvcToAdd.Contains(svc))
                {
                    SvcToAdd.Add(svc);
                }
            }
        }
        
        public bool CalcVisibility()
        {
            if(ActConn.IsInterface)
            {
                dstReadOnly = ActConn.Sources.Count > 0 || SrcAppServerToAdd.Count > 0;
                srcReadOnly = ActConn.Destinations.Count > 0 || DstAppServerToAdd.Count > 0;
                svcReadOnly = false;
            }
            else if (ActConn.UsedInterfaceId != null)
            {
                srcReadOnly = ActConn.Sources.Count > 0;
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
            return ActConn.SrcAppRoles != null && ActConn.SrcAppRoles.Count > 0 || 
                ActConn.Sources != null && ActConn.Sources.Count > 0 ||
                SrcAppServerToAdd != null && SrcAppServerToAdd.Count > 0 ||
                SrcAppRolesToAdd != null && SrcAppRolesToAdd.Count > 0;
        }

        public bool DstFilled()
        {
            return ActConn.DstAppRoles != null && ActConn.DstAppRoles.Count > 0 || 
                ActConn.Destinations != null && ActConn.Destinations.Count > 0 ||
                DstAppServerToAdd != null && DstAppServerToAdd.Count > 0 ||
                DstAppRolesToAdd != null && DstAppRolesToAdd.Count > 0;
        }

        public async Task Save()
        {
            if(checkConn())
            {
                if(!srcReadOnly)
                {
                    foreach(var ip in SrcAppServerToDelete)
                    {
                        ActConn.Sources.Remove(ip);
                    }
                    foreach(var ip in SrcAppServerToAdd)
                    {
                        ActConn.Sources.Add(ip);
                    }
                    foreach(var appRole in SrcAppRolesToDelete)
                    {
                        ActConn.SrcAppRoles.Remove(appRole);
                    }
                    foreach(var appRole in SrcAppRolesToAdd)
                    {
                        ActConn.SrcAppRoles.Add(appRole);
                    }
                }
                if(!dstReadOnly)
                {
                    foreach(var ip in DstAppServerToDelete)
                    {
                        ActConn.Destinations.Remove(ip);
                    }
                    foreach(var ip in DstAppServerToAdd)
                    {
                        ActConn.Destinations.Add(ip);
                    }
                    foreach(var appRole in DstAppRolesToDelete)
                    {
                        ActConn.DstAppRoles.Remove(appRole);
                    }
                    foreach(var appRole in DstAppRolesToAdd)
                    {
                        ActConn.DstAppRoles.Add(appRole);
                    }
                }
                if(!svcReadOnly)
                {
                    foreach(var svc in SvcToDelete)
                    {
                        ActConn.Services.Remove(svc);
                    }
                    foreach(var svc in SvcToAdd)
                    {
                        ActConn.Services.Add(svc);
                    }
                    foreach(var svcGrp in SvcGrpToDelete)
                    {
                        ActConn.ServiceGroups.Remove(svcGrp);
                    }
                    foreach(var svcGrp in SvcGrpToAdd)
                    {
                        ActConn.ServiceGroups.Add(svcGrp);
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
                ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<NewReturning>(FWO.Api.Client.Queries.ModellingQueries.newConnection, Variables)).ReturnIds;
                if (returnIds != null)
                {
                    ActConn.Id = returnIds[0].NewId;
                    foreach(var appServer in ActConn.Sources)
                    {
                        var srcParams = new { appServerId = appServer.Id, connectionId = ActConn.Id, connectionField = ConnectionField.Source };
                        await apiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.ModellingQueries.addAppServerToConnection, srcParams);
                    }
                    foreach(var appServer in ActConn.Destinations)
                    {
                        var dstParams = new { appServerId = appServer.Id, connectionId = ActConn.Id, connectionField = ConnectionField.Destination };
                        await apiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.ModellingQueries.addAppServerToConnection, dstParams);
                    }
                    foreach(var appRole in ActConn.SrcAppRoles)
                    {
                        var srcParams = new { appRoleId = appRole.Id, connectionId = ActConn.Id, connectionField = ConnectionField.Source };
                        await apiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.ModellingQueries.addAppRoleToConnection, srcParams);
                    }
                    foreach(var appRole in ActConn.DstAppRoles)
                    {
                        var dstParams = new { appRoleId = appRole.Id, connectionId = ActConn.Id, connectionField = ConnectionField.Destination };
                        await apiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.ModellingQueries.addAppRoleToConnection, dstParams);
                    }
                    foreach(var service in ActConn.Services)
                    {
                        var svcParams = new { serviceId = service.Id, connectionId = ActConn.Id };
                        await apiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.ModellingQueries.addServiceToConnection, svcParams);
                    }
                    foreach(var serviceGrp in ActConn.ServiceGroups)
                    {
                        var svcGrpParams = new { serviceGroupId = serviceGrp.Id, connectionId = ActConn.Id };
                        await apiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.ModellingQueries.addServiceGroupToConnection, svcGrpParams);
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
                await apiConnection.SendQueryAsync<NewReturning>(FWO.Api.Client.Queries.ModellingQueries.updateConnection, Variables);

                foreach(var appServer in SrcAppServerToDelete)
                {
                    var srcParams = new { appServerId = appServer.Id, connectionId = ActConn.Id, connectionField = ConnectionField.Source };
                    await apiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.ModellingQueries.removeAppServerFromConnection, srcParams);
                }
                foreach(var appServer in SrcAppServerToAdd)
                {
                    var srcParams = new { appServerId = appServer.Id, connectionId = ActConn.Id, connectionField = ConnectionField.Source };
                    await apiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.ModellingQueries.addAppServerToConnection, srcParams);
                }
                foreach(var appServer in DstAppServerToDelete)
                {
                    var dstParams = new { appServerId = appServer.Id, connectionId = ActConn.Id, connectionField = ConnectionField.Destination };
                    await apiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.ModellingQueries.removeAppServerFromConnection, dstParams);
                }
                foreach(var appServer in DstAppServerToAdd)
                {
                    var dstParams = new { appServerId = appServer.Id, connectionId = ActConn.Id, connectionField = ConnectionField.Destination };
                    await apiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.ModellingQueries.addAppServerToConnection, dstParams);
                }
                foreach(var appRole in SrcAppRolesToDelete)
                {
                    var srcParams = new { appRoleId = appRole.Id, connectionId = ActConn.Id, connectionField = ConnectionField.Source };
                    await apiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.ModellingQueries.removeAppRoleFromConnection, srcParams);
                }
                foreach(var appRole in SrcAppRolesToAdd)
                {
                    var srcParams = new { appRoleId = appRole.Id, connectionId = ActConn.Id, connectionField = ConnectionField.Source };
                    await apiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.ModellingQueries.addAppRoleToConnection, srcParams);
                }
                foreach(var appRole in DstAppRolesToDelete)
                {
                    var dstParams = new { appRoleId = appRole.Id, connectionId = ActConn.Id, connectionField = ConnectionField.Destination };
                    await apiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.ModellingQueries.removeAppRoleFromConnection, dstParams);
                }
                foreach(var appRole in DstAppRolesToAdd)
                {
                    var dstParams = new { appRoleId = appRole.Id, connectionId = ActConn.Id, connectionField = ConnectionField.Destination };
                    await apiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.ModellingQueries.addAppRoleToConnection, dstParams);
                }
                foreach(var service in SvcToDelete)
                {
                    var svcParams = new { serviceId = service.Id, connectionId = ActConn.Id };
                    await apiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.ModellingQueries.removeServiceFromConnection, svcParams);
                }
                foreach(var service in SvcToAdd)
                {
                    var svcParams = new { serviceId = service.Id, connectionId = ActConn.Id };
                    await apiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.ModellingQueries.addServiceToConnection, svcParams);
                }
                foreach(var serviceGrp in SvcGrpToDelete)
                {
                    var svcGrpParams = new { serviceGroupId = serviceGrp.Id, connectionId = ActConn.Id };
                    await apiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.ModellingQueries.removeServiceGroupFromConnection, svcGrpParams);
                }
                foreach(var serviceGrp in SvcGrpToAdd)
                {
                    var svcGrpParams = new { serviceGroupId = serviceGrp.Id, connectionId = ActConn.Id };
                    await apiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.ModellingQueries.addServiceGroupToConnection, svcGrpParams);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("add_connection"), "", true);
            }
        }


        public void Close()
        {
            SrcAppServerToAdd = new List<NetworkObject>();
            SrcAppServerToDelete = new List<NetworkObject>();
            DstAppServerToAdd = new List<NetworkObject>();
            DstAppServerToDelete = new List<NetworkObject>();
            SrcAppRolesToAdd = new List<AppRole>();
            SrcAppRolesToDelete = new List<AppRole>();
            DstAppRolesToAdd = new List<AppRole>();
            DstAppRolesToDelete = new List<AppRole>();
            SvcToAdd = new List<ModellingService>();
            SvcToDelete = new List<ModellingService>();
            SvcGrpToAdd = new List<ServiceGroup>();
            SvcGrpToDelete = new List<ServiceGroup>();
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

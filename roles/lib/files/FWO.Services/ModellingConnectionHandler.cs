using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using FWO.Middleware.Client;
using System.Data;
using Microsoft.AspNetCore.Components;


namespace FWO.Services
{
    public class ModellingConnectionHandler : ModellingHandlerBase
    {
        public List<FwoOwner> AllApps { get; set; } = [];
        public List<ModellingConnection> Connections { get; set; } = [];
        public List<ModellingConnection> PreselectedInterfaces { get; set; } = [];
        public ModellingConnection ActConn { get; set; } = new();
        public List<ModellingAppRole> AvailableAppRoles { get; set; } = [];
        public List<ModellingNwGroupWrapper> AvailableSelectedObjects { get; set; } = [];
        public List<ModellingNwGroupWrapper> AvailableCommonAreas { get; set; } = [];
        public List<CommonAreaConfig> CommonAreaConfigItems { get; set; } = [];
        public List<ModellingServiceGroup> AvailableServiceGroups { get; set; } = [];
    
        public string InterfaceName = "";

        public bool SrcReadOnly { get; set; } = false;
        public bool DstReadOnly { get; set; } = false;
        public bool SvcReadOnly { get; set; } = false;

        public bool AddExtraConfigMode = false;
        public bool SearchNWObjectMode = false;
        public bool RemoveNwObjectMode = false;
        public bool RemovePreselectedInterfaceMode = false;
        public bool DisplaySelectedInterfaceMode = false;
        public bool ReplaceMode = false;
        public ModellingConnectionHandler? IntConnHandler;
        public int RequesterId = 0;

        public List<ModellingAppServer> SrcAppServerToAdd { get; set; } = [];
        public List<ModellingAppServer> SrcAppServerToDelete { get; set; } = [];
        public List<ModellingAppServer> DstAppServerToAdd { get; set; } = [];
        public List<ModellingAppServer> DstAppServerToDelete { get; set; } = [];
        public ModellingAppServerHandler AppServerHandler;
        public bool DisplayAppServerMode = false;

        public ModellingAppRoleHandler? AppRoleHandler;
        public List<ModellingAppRole> SrcAppRolesToAdd { get; set; } = [];
        public List<ModellingAppRole> SrcAppRolesToDelete { get; set; } = [];
        public List<ModellingAppRole> DstAppRolesToAdd { get; set; } = [];
        public List<ModellingAppRole> DstAppRolesToDelete { get; set; } = [];
        public List<ModellingNwGroup> SrcNwGroupsToAdd { get; set; } = [];
        public List<ModellingNwGroup> SrcNwGroupsToDelete { get; set; } = [];
        public List<ModellingNwGroup> DstNwGroupsToAdd { get; set; } = [];
        public List<ModellingNwGroup> DstNwGroupsToDelete { get; set; } = [];
        public bool AddAppRoleMode = false;
        public bool EditAppRoleMode = false;
        public bool DeleteAppRoleMode = false;
        public bool DisplayAppRoleMode = false;

        public ModellingServiceHandler? ServiceHandler;
        public List<ModellingService> SvcToDelete { get; set; } = [];
        public bool AddServiceMode = false;
        public bool EditServiceMode = false;
        public bool DeleteServiceMode = false;

        public ModellingServiceGroupHandler? SvcGrpHandler;
        public List<ModellingServiceGroup> SvcGrpToAdd { get; set; } = [];
        public List<ModellingServiceGroup> SvcGrpToDelete { get; set; } = [];
        public bool AddSvcGrpMode = false;
        public bool EditSvcGrpMode = false;
        public bool DeleteSvcGrpMode = false;
        public bool DisplaySvcGrpMode = false;
        public Func<Task> RefreshParent = DefaultInit.DoNothing;
        public ModellingAppRole DummyAppRole = new();
        public int LastWidth = GlobalConst.kGlobLibraryWidth;
        public bool LastCollapsed = false;

        private bool SrcFix = false;
        private bool DstFix = false;
        private ModellingAppRole actAppRole = new();
        private ModellingNwGroup actNwGrpObj = new();
        private ModellingConnection actInterface = new();
        private ModellingServiceGroup actServiceGroup = new();
        private ModellingConnection ActConnOrig { get; set; } = new();
        private bool InitOngoing = false;


        public ModellingConnectionHandler(ApiConnection apiConnection, UserConfig userConfig, FwoOwner application, 
            List<ModellingConnection> connections, ModellingConnection conn, bool addMode, bool readOnly, 
            Action<Exception?, string, string, bool> displayMessageInUi, Func<Task> refreshParent, bool isOwner = true)
            : base (apiConnection, userConfig, application, addMode, displayMessageInUi, readOnly, isOwner)
        {
            Connections = connections;
            ActConn = conn;
            ActConnOrig = new ModellingConnection(ActConn);
            RefreshParent = refreshParent;
        }

        public async Task Init()
        {
            try
            {
                if(!InitOngoing)
                {
                    InitOngoing = true;
                    await RefreshObjects();
                    InterfaceName = await ExtractUsedInterface(ActConn);
                    AllApps = await apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwnersWithConn);
                    AppRoleHandler = new (apiConnection, userConfig, new(), [], new(), [], [], false, DisplayMessageInUi);
                    DummyAppRole = await AppRoleHandler.GetDummyAppRole();
                    if(!AddMode && !ReadOnly && ActConn.IsInterface)
                    {
                        // if(await CheckInterfaceInUse(ActConn))
                        // {
                            SrcFix = ActConn.SourceFilled();
                            DstFix = ActConn.DestinationFilled();
                        // }
                    }
                    InitOngoing = false;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("fetch_data"), "", true);
            }
        }

        public async Task PartialInit()
        {
            try
            {
                if(!InitOngoing)
                {
                    InitOngoing = true;
                    AppRoleHandler = new (apiConnection, userConfig, new(), [], new(), [], [], false, DisplayMessageInUi);
                    DummyAppRole = await AppRoleHandler.GetDummyAppRole();
                    InitOngoing = false;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("fetch_data"), "", true);
            }
        }

        public async Task ReInit()
        {
            try
            {
                await RefreshActConn();
                await RefreshObjects();
                await RefreshParent();
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("fetch_data"), "", true);
            }
        }

        private async Task RefreshObjects()
        {
            await RefreshPreselectedInterfaces();
            PreselectedInterfaces = ModellingConnectionWrapper.Resolve(await apiConnection.SendQueryAsync<List<ModellingConnectionWrapper>>(ModellingQueries.getSelectedConnections, new { appId = Application.Id })).ToList();
            await InitAvailableNWObjects();
            await InitAvailableSvcObjects();
            RefreshSelectableNwObjects();
        }

        private async Task RefreshActConn()
        {
            try
            {
                List<ModellingConnection> conns = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getInterfaceById, new {intId = ActConn.Id});
                if(conns.Count > 0)
                {
                    ActConn = conns.First();
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("fetch_data"), "", true);
            }
        }

        public async Task RefreshPreselectedInterfaces()
        {
            try
            {
                PreselectedInterfaces = ModellingConnectionWrapper.Resolve(
                    await apiConnection.SendQueryAsync<List<ModellingConnectionWrapper>>(ModellingQueries.getSelectedConnections, new { appId = Application.Id })).ToList();
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("fetch_data"), "", true);
            }
        }

        public async Task InitAvailableNWObjects()
        {
            try
            {
                AvailableAppServers = await apiConnection.SendQueryAsync<List<ModellingAppServer>>(ModellingQueries.getAppServers, new { appId = Application.Id });
                AvailableAppRoles = await apiConnection.SendQueryAsync<List<ModellingAppRole>>(ModellingQueries.getAppRoles, new { appId = Application.Id });

                List<ModellingNwGroup> allAreas = await apiConnection.SendQueryAsync<List<ModellingNwGroup>>(ModellingQueries.getNwGroupObjects, new { grpType = (int)ModellingTypes.ModObjectType.NetworkArea });
                CommonAreaConfigItems = [];
                if(userConfig.ModCommonAreas != "")
                {
                    CommonAreaConfigItems = JsonSerializer.Deserialize<List<CommonAreaConfig>>(userConfig.ModCommonAreas) ?? new();
                }
                AvailableCommonAreas = [];
                foreach(var comAreaConfig in CommonAreaConfigItems)
                {
                    ModellingNwGroup? area = allAreas.FirstOrDefault(a => a.Id == comAreaConfig.AreaId);
                    if(area != null)
                    {
                        AvailableCommonAreas.Add(new () { Content = area });
                    }
                }
                
                AvailableSelectedObjects = await apiConnection.SendQueryAsync<List<ModellingNwGroupWrapper>>(ModellingQueries.getSelectedNwGroupObjects, new { appId = Application.Id });
                AvailableSelectedObjects = AvailableSelectedObjects.Where(x => !AvailableCommonAreas.Contains(x)).ToList();
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("fetch_data"), "", true);
            }
        }

        public async Task InitAvailableSvcObjects()
        {
            try
            {
                AvailableSvcElems = [];
                AvailableServiceGroups = (await apiConnection.SendQueryAsync<List<ModellingServiceGroup>>(ModellingQueries.getGlobalServiceGroups)).Where(x => x.AppId != Application.Id).ToList();
                AvailableServiceGroups.AddRange(await apiConnection.SendQueryAsync<List<ModellingServiceGroup>>(ModellingQueries.getServiceGroupsForApp, new { appId = Application.Id }));
                AvailableServices = await apiConnection.SendQueryAsync<List<ModellingService>>(ModellingQueries.getGlobalServices);
                AvailableServices.AddRange(await apiConnection.SendQueryAsync<List<ModellingService>>(ModellingQueries.getServicesForApp, new { appId = Application.Id }));
                foreach(var svcGrp in AvailableServiceGroups)
                {
                    AvailableSvcElems.Add(new KeyValuePair<int, int>((int)ModellingTypes.ModObjectType.ServiceGroup, svcGrp.Id));
                }
                if(userConfig.AllowServiceInConn)
                {
                    foreach(var svc in AvailableServices)
                    {
                        AvailableSvcElems.Add(new KeyValuePair<int, int>((int)ModellingTypes.ModObjectType.Service, svc.Id));
                    }
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("fetch_data"), "", true);
            }
        }

        public void AddExtraConfig()
        {
            AddExtraConfigMode = true;
        }

        public async Task<bool> SaveExtraConfig(ModellingExtraConfig extraConfig)
        {
            extraConfig.Id = ActConn.ExtraConfigs.Count > 0 ? ActConn.ExtraConfigs.OrderByDescending(x => x.Id).First().Id + 1 : 1;
            List<ModellingExtraConfig> actList = ActConn.ExtraConfigs;
            actList.Add(extraConfig);
            ActConn.ExtraConfigs = actList;
            return true;
        }

        public async Task UpdateExtraConfig(ChangeEventArgs e, ModellingExtraConfig extraConfig)
        {
            List<ModellingExtraConfig> actList = ActConn.ExtraConfigs;
            ModellingExtraConfig? actConfig = actList.FirstOrDefault(x => x.Id == extraConfig.Id);
            if(actConfig != null)
            {
                actConfig.ExtraConfigText = e.Value.ToString();
            }
            ActConn.ExtraConfigs = actList;
        }

        public async Task<bool> DeleteExtraConfig(ModellingExtraConfig extraConfig)
        {
            List<ModellingExtraConfig> actList = ActConn.ExtraConfigs;
            actList.Remove(actList.FirstOrDefault(x => x.Id == extraConfig.Id) ?? throw new Exception("Did not find service group."));
            ActConn.ExtraConfigs = actList;
            return true;
        }

        public async Task<long> CreateNewRequestedInterface(long ticketId, bool asSource, string name, string reason)
        {
            ActConn.TicketId = ticketId;
            ActConn.Name = name + " (Ticket: "+ ActConn.TicketId.ToString() + ")";
            ActConn.Reason = $"{userConfig.GetText("from_ticket")} {ticketId} ({userConfig.GetText("U9012")}): \r\n{reason}";
            ActConn.IsInterface = true;
            ActConn.IsRequested = true;
            if(asSource)
            {
                ActConn.SourceAppRoles.Add(new() { Content = DummyAppRole });
            }
            else
            {
                ActConn.DestinationAppRoles.Add(new() { Content = DummyAppRole });
            }
            await AddConnectionToDb(true);

            ActConn.AppId = RequesterId;
            await AddToPreselectedList(ActConn);
            return ActConn.Id;
        }

        public string DisplayInterface(ModellingConnection? inter)
        {
            if(inter != null)
            {
                FwoOwner? app = AllApps.FirstOrDefault(x => x.Id == inter.AppId);
                if(app != null)
                {
                    return inter.DisplayNameWithOwner(app);
                }
                return inter.Name ?? "";
            }
            return "";
        }

        public bool RefreshSelectableNwObjects()
        {
            AvailableNwElems = [];
            foreach(var obj in AvailableCommonAreas)
            {
                AvailableNwElems.Add(new KeyValuePair<int, long>(obj.Content.GroupType, obj.Content.Id));
            }
            foreach(var obj in AvailableSelectedObjects)
            {
                AvailableNwElems.Add(new KeyValuePair<int, long>(obj.Content.GroupType, obj.Content.Id));
            }
            foreach(var appRole in AvailableAppRoles)
            {
                AvailableNwElems.Add(new KeyValuePair<int, long>((int)ModellingTypes.ModObjectType.AppRole, appRole.Id));
            }
            if(userConfig.AllowServerInConn)
            {
                foreach(var appServer in AvailableAppServers.Where(x => !x.IsDeleted))
                {
                    AvailableNwElems.Add(new KeyValuePair<int, long>((int)ModellingTypes.ModObjectType.AppServer, appServer.Id));
                }
            }
            return true;
        }

        public async Task RequestReplaceInterface(ModellingConnection interf)
        {
            if(interf.SourceFilled() != ActConn.SourceFilled() || interf.DestinationFilled() != ActConn.DestinationFilled())
            {
                DisplayMessageInUi(null, userConfig.GetText("replace"), userConfig.GetText("E9017"), true);
                return;
            }
            ReplaceMode = true;
            await DisplaySelectedInterface(interf);
        }

        public async Task ReplaceInterface(Task<AuthenticationState> authenticationStateTask, MiddlewareClient middlewareClient)
        {
            try
            {
                if(IntConnHandler != null)
                {
                    await ReplaceLinks();
                    await RemoveFromAllSelections();
                    if(await DeleteRequestedInterface())
                    {
                        await UpdateTicket(authenticationStateTask, middlewareClient);
                    }
                    await RefreshParent();
                    Close();
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("replace"), "", true);
            }
        }

        private async Task ReplaceLinks()
        {
            if(await CheckInterfaceInUse(ActConn))
            {
                var Variables = new
                {
                    usedInterfaceIdOld = ActConn.Id,
                    usedInterfaceIdNew = IntConnHandler?.ActConn.Id
                };
                int usingConns = (await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.replaceUsedInterface, Variables)).AffectedRows;
                await LogChange(ModellingTypes.ChangeType.Replace, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                    $"Replaced Used Interface: {ActConn.Name} by: {IntConnHandler?.ActConn.Name} for {usingConns} Connections", Application.Id);
            }
        }

        public async Task RemoveFromAllSelections()
        {
            await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeSelectedConnection, new { connectionId = ActConn.Id });
        }

        private async Task<bool> DeleteRequestedInterface()
        {
            if(await CheckInterfaceInUse(ActConn))
            {
                DisplayMessageInUi(null, userConfig.GetText("replace"), userConfig.GetText("E9016"), true);
                return false;
            }
            if((await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.deleteConnection, new { id = ActConn.Id })).DeletedId == ActConn.Id)
            {
                await LogChange(ModellingTypes.ChangeType.Delete, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                    $"Deleted Interface: {ActConn.Name}", Application.Id);
                Connections.Remove(ActConn);
            }
            return true;
        }

        private async Task UpdateTicket(Task<AuthenticationState> authenticationStateTask, MiddlewareClient middlewareClient)
        {
            if(ActConn.TicketId != null)
            {
                try
                {
                    // change referred connId ?
                    string comment = $"{userConfig.GetText("U9016")}: {IntConnHandler?.ActConn.Name}";
                   TicketCreator ticketCreator = new (apiConnection, userConfig, authenticationStateTask!.Result.User, middlewareClient, WorkflowPhases.implementation);
                    if(await ticketCreator.PromoteNewInterfaceImplTask(Application, (long)ActConn.TicketId, ExtStates.Done, comment))
                    {
                        DisplayMessageInUi(null, comment, userConfig.GetText("U9013"), false);
                    }
                }
                catch(Exception exception)
                {
                    DisplayMessageInUi(exception, userConfig.GetText("replace"), "", true);
                }
            }
        }

        public void InterfaceToConn(ModellingConnection interf)
        {
            InterfaceName = interf.Name ?? "";
            SrcReadOnly = interf.SourceFilled();
            DstReadOnly = interf.DestinationFilled();
            SvcReadOnly = true;
            ActConn.IsInterface = false;
            ActConn.UsedInterfaceId = interf.Id;
            ActConn.InterfaceIsRequested = interf.IsRequested;
            ActConn.InterfaceIsRejected = interf.GetBoolProperty(ConState.Rejected.ToString());
            ActConn.TicketId = interf.TicketId;
            if(SrcReadOnly)
            {
                ActConn.SourceAppServers = new List<ModellingAppServerWrapper>(interf.SourceAppServers){};
                ActConn.SourceAppRoles = new List<ModellingAppRoleWrapper>(interf.SourceAppRoles){};
                ActConn.SourceNwGroups = new List<ModellingNwGroupWrapper>(interf.SourceNwGroups){};
                ActConn.SrcFromInterface = true;
            }
            else
            {
                ActConn.DestinationAppServers = new List<ModellingAppServerWrapper>(interf.DestinationAppServers){};
                ActConn.DestinationAppRoles = new List<ModellingAppRoleWrapper>(interf.DestinationAppRoles){};
                ActConn.DestinationNwGroups = new List<ModellingNwGroupWrapper>(interf.DestinationNwGroups){};
                ActConn.DstFromInterface = true;
            }
            ActConn.Services = new List<ModellingServiceWrapper>(interf.Services){};
            ActConn.ServiceGroups = new List<ModellingServiceGroupWrapper>(interf.ServiceGroups){};
        }

        public void RemoveInterf()
        {
            InterfaceName = "";
            if(SrcReadOnly)
            {
                ActConn.SourceAppServers = [];
                ActConn.SourceAppRoles = [];
                ActConn.SourceNwGroups = [];
                ActConn.SrcFromInterface = false;
            }
            if(DstReadOnly)
            {
                ActConn.DestinationAppServers = [];
                ActConn.DestinationAppRoles = [];
                ActConn.DestinationNwGroups = [];
                ActConn.DstFromInterface = false;
            }
            ActConn.Services = [];
            ActConn.ServiceGroups = [];
            ActConn.UsedInterfaceId = null;
            ActConn.InterfaceIsRequested = false;
            ActConn.InterfaceIsRejected = false;
            ActConn.TicketId = null;
            SrcReadOnly = false;
            DstReadOnly = false;
            SvcReadOnly = false;
        }

        public void RequestRemovePreselectedInterface(ModellingConnection interf)
        {
            actInterface = interf;
            Message = userConfig.GetText("U9006") + interf.Name + "?";
            RemovePreselectedInterfaceMode = true;
        }

        public async Task RemovePreselectedInterface()
        {
            try
            {
                if((await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeSelectedConnectionFromApp, new { appId = Application.Id, connectionId = actInterface.Id })).AffectedRows > 0)
                {
                    PreselectedInterfaces.Remove(PreselectedInterfaces.FirstOrDefault(x => x.Id == actInterface.Id) ?? throw new Exception("Did not find object."));
                    RemovePreselectedInterfaceMode = false;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("remove_interface"), "", true);
            }
        }

        public async Task DisplaySelectedInterface(ModellingConnection interf)
        {
            FwoOwner? app = AllApps.FirstOrDefault(x => x.Id == interf.AppId);
            IntConnHandler = new ModellingConnectionHandler(apiConnection, userConfig, app ?? new(), Connections, interf, false, true, DisplayMessageInUi, DefaultInit.DoNothing, false);
            await IntConnHandler.Init();
            DisplaySelectedInterfaceMode = true;
        }

        public void DisplayAppServer(ModellingAppServer? appServer)
        {
            if(appServer != null)
            {
                try
                {
                    AppServerHandler = new (apiConnection, userConfig, Application, appServer, [], false, DisplayMessageInUi){ ReadOnly = true };
                    DisplayAppServerMode = true;
                }
                catch (Exception exception)
                {
                    DisplayMessageInUi(exception, userConfig.GetText("display_app_server"), "", true);
                }
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

        public void RequestRemoveNwGrpObject(ModellingNwGroup? nwGrpObj)
        {
            if(nwGrpObj != null)
            {
                actNwGrpObj = nwGrpObj;
                Message = userConfig.GetText("U9006") + nwGrpObj.Name + "?";
                RemoveNwObjectMode = true;
            }
        }

        public async Task RemoveNwGrpObject()
        {
            try
            {
                if((await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeSelectedNwGroupObject, new { appId = Application.Id, nwGroupId = actNwGrpObj.Id })).AffectedRows > 0)
                {
                    AvailableSelectedObjects.Remove(AvailableSelectedObjects.FirstOrDefault(x => x.Content.Id == actNwGrpObj.Id) ?? throw new Exception("Did not find object."));
                    AvailableNwElems.Remove(AvailableNwElems.FirstOrDefault(x => x.Key == actNwGrpObj.GroupType && x.Value == actNwGrpObj.Id));
                    RemoveNwObjectMode = false;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("remove_nw_object"), "", true);
            }
        }

        public void NwGroupToSource(List<ModellingNwGroup> nwGroups)
        {
            if(!SrcDropForbidden())
            {
                foreach(var nwGroup in nwGroups)
                {
                    if(ActConn.SourceNwGroups.FirstOrDefault(w => w.Content.Id == nwGroup.Id) == null && !SrcNwGroupsToAdd.Contains(nwGroup) &&
                        (CommonAreaConfigItems.FirstOrDefault(x => x.AreaId == nwGroup.Id)?.UseInSrc ?? true))
                    {
                        SrcNwGroupsToAdd.Add(nwGroup);
                    }
                    else
                    {
                        DisplayMessageInUi(null, userConfig.GetText("insert_forbidden"), userConfig.GetText("E9015"), true);
                    }
                }
                CalcVisibility();
            }
        }

        public void NwGroupToDestination(List<ModellingNwGroup> nwGroups)
        {
            if(!DstDropForbidden())
            {
                foreach(var nwGroup in nwGroups)
                {
                    if(ActConn.DestinationNwGroups.FirstOrDefault(w => w.Content.Id == nwGroup.Id) == null && !DstNwGroupsToAdd.Contains(nwGroup) &&
                        (CommonAreaConfigItems.FirstOrDefault(x => x.AreaId == nwGroup.Id)?.UseInDst ?? true))
                    {
                        DstNwGroupsToAdd.Add(nwGroup);
                    }
                    else
                    {
                        DisplayMessageInUi(null, userConfig.GetText("insert_forbidden"), userConfig.GetText("E9015"), true);
                    }
                }
                CalcVisibility();
            }
        }

        public void CreateAppRole()
        {
            DisplayAppRoleMode = false;
            AddAppRoleMode = true;
            HandleAppRole(new ModellingAppRole(){});
        }

        public void EditAppRole(ModellingAppRole? appRole)
        {
            if(appRole != null)
            {
                DisplayAppRoleMode = false;
                AddAppRoleMode = false;
                HandleAppRole(appRole);
            }
        }

        public void DisplayAppRole(ModellingAppRole? appRole)
        {
            if(appRole != null)
            {
                DisplayAppRoleMode = true;
                AddAppRoleMode = false;
                HandleAppRole(appRole);
            }
        }

        public void HandleAppRole(ModellingAppRole appRole)
        {
            try
            {
                AppRoleHandler = new ModellingAppRoleHandler(apiConnection, userConfig, Application, AvailableAppRoles,
                    appRole, AvailableAppServers, AvailableNwElems, AddAppRoleMode, DisplayMessageInUi, IsOwner, DisplayAppRoleMode);
                EditAppRoleMode = true;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("edit_app_role"), "", true);
            }
        }

        public async Task RequestDeleteAppRole(ModellingAppRole? appRole)
        {
            if(appRole != null)
            {
                actAppRole = appRole;
                DeleteAllowed = !await CheckAppRoleIsInUse();
                Message = DeleteAllowed ? userConfig.GetText("U9002") + actAppRole.Name + "?" : userConfig.GetText("E9009") + actAppRole.Name;
                DeleteAppRoleMode = true;
            }
        }

        private async Task<bool> CheckAppRoleIsInUse()
        {
            try
            {
                if(SrcAppRolesToAdd.FirstOrDefault(s => s.Id == actAppRole.Id) == null && DstAppRolesToAdd.FirstOrDefault(s => s.Id == actAppRole.Id) == null)
                {
                    List<ModellingConnection> foundConnections = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getConnectionIdsForNwGroup, new { id = actAppRole.Id });
                    if (foundConnections.Count == 0)
                    {
                        return false;
                    }
                }
                return true;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("is_in_use"), "", true);
                return true;
            }
        }

        public async Task DeleteAppRole()
        {
            try
            {
                if((await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.deleteNwGroup, new { id = actAppRole.Id })).AffectedRows > 0)
                {
                    await LogChange(ModellingTypes.ChangeType.Delete, ModellingTypes.ModObjectType.AppRole, actAppRole.Id,
                        $"Deleted App Role: {actAppRole.Display()}", Application.Id);
                    AvailableAppRoles.Remove(actAppRole);
                    AvailableNwElems.Remove(AvailableNwElems.FirstOrDefault(x => x.Key == (int)ModellingTypes.ModObjectType.AppRole && x.Value == actAppRole.Id));
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
            DisplaySvcGrpMode = false;
            AddSvcGrpMode = true;
            HandleServiceGroup(new ModellingServiceGroup(){});
        }

        public void EditServiceGroup(ModellingServiceGroup? serviceGroup)
        {
            if(serviceGroup != null)
            {
                DisplaySvcGrpMode = false;
                AddSvcGrpMode = false;
                HandleServiceGroup(serviceGroup);
            }
        }

        public void DisplayServiceGroup(ModellingServiceGroup? serviceGroup)
        {
            if(serviceGroup != null)
            {
                DisplaySvcGrpMode = true;
                AddSvcGrpMode = false;
                HandleServiceGroup(serviceGroup);
            }
        }

        public void HandleServiceGroup(ModellingServiceGroup serviceGroup)
        {
            try
            {
                SvcGrpHandler = new ModellingServiceGroupHandler(apiConnection, userConfig, Application, AvailableServiceGroups,
                    serviceGroup, AvailableServices, AvailableSvcElems, AddSvcGrpMode, DisplayMessageInUi, ReInit, IsOwner, DisplaySvcGrpMode);
                EditSvcGrpMode = true;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("edit_service_group"), "", true);
            }
        }

        public async Task RequestDeleteServiceGrp(ModellingServiceGroup? serviceGroup)
        {
            if(serviceGroup != null)
            {
                actServiceGroup = serviceGroup;
                DeleteAllowed = !await CheckServiceGroupIsInUse();
                Message = DeleteAllowed ? userConfig.GetText("U9004") + serviceGroup.Name + "?" : userConfig.GetText("E9008") + serviceGroup.Name;
                DeleteSvcGrpMode = true;
            }
        }

        private async Task<bool> CheckServiceGroupIsInUse()
        {
            try
            {
                if(SvcGrpToAdd.FirstOrDefault(s => s.Id == actServiceGroup.Id) == null)
                {
                    List<ModellingConnection> foundConnections = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getConnectionIdsForServiceGroup, new { serviceGroupId = actServiceGroup.Id });
                    if (foundConnections.Count == 0)
                    {
                        return false;
                    }
                }
                return true;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("is_in_use"), "", true);
                return true;
            }
        }

        public async Task DeleteServiceGroup()
        {
            try
            {
                if((await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.deleteServiceGroup, new { id = actServiceGroup.Id })).AffectedRows > 0)
                {
                    await LogChange(ModellingTypes.ChangeType.Delete, ModellingTypes.ModObjectType.ServiceGroup, actServiceGroup.Id,
                        $"Deleted Service Group: {actServiceGroup.Display()}", Application.Id);
                    AvailableServiceGroups.Remove(actServiceGroup);
                    AvailableSvcElems.Remove(AvailableSvcElems.FirstOrDefault(x => x.Key == (int)ModellingTypes.ModObjectType.ServiceGroup && x.Value == actServiceGroup.Id));
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
                if((ActConn.ServiceGroups.FirstOrDefault(w => w.Content.Id == grp.Id) == null) && (SvcGrpToAdd.FirstOrDefault(g => g.Id == grp.Id) == null))
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

        public void EditService(ModellingService? service)
        {
            if(service != null)
            {
                AddServiceMode = false;
                HandleService(service);
            }
        }
        
        public void HandleService(ModellingService service)
        {
            try
            {
                ServiceHandler = new ModellingServiceHandler(apiConnection, userConfig, Application, service,
                    AvailableServices, AvailableSvcElems, AddServiceMode, DisplayMessageInUi, IsOwner);
                EditServiceMode = true;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("edit_service"), "", true);
            }
        }

        public async Task RequestDeleteService(ModellingService? service)
        {
            if(service != null)
            {
                await RequestDeleteServiceBase(service);
                DeleteServiceMode = true;
            }
        }

        public async Task DeleteService()
        {
            try
            {
                DeleteServiceMode = await DeleteService(AvailableServices, AvailableSvcElems);
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
                if((ActConn.Services.FirstOrDefault(w => w.Content.Id == svc.Id) == null) && (SvcToAdd.FirstOrDefault(s => s.Id == svc.Id) == null))
                {
                    SvcToAdd.Add(svc);
                }
            }
        }
        
        public bool CalcVisibility()
        {
            if(ActConn.IsInterface)
            {
                DstReadOnly = SrcFix || SrcFilledInWork();
                SrcReadOnly = DstFix || DstFilledInWork();
                SvcReadOnly = false;
            }
            else if (ActConn.UsedInterfaceId != null)
            {
                SrcReadOnly = ActConn.SrcFromInterface;
                DstReadOnly = ActConn.DstFromInterface;
                SvcReadOnly = true;
            }
            else
            {
                SrcReadOnly = false;
                DstReadOnly = false;
                SvcReadOnly = false;
            }
            return true;
        }

        public bool SrcDropForbidden()
        {
            return SrcReadOnly || (ActConn.IsInterface && DstFilledInWork());
        }

        public bool DstDropForbidden()
        {
            return DstReadOnly || (ActConn.IsInterface && SrcFilledInWork());
        }

        public bool SrcFilledInWork(int dummyARCount = 0)
        {
            return ActConn.SourceNwGroups.Count - SrcNwGroupsToDelete.Count > 0 || 
                ActConn.SourceAppRoles.Count - dummyARCount - SrcAppRolesToDelete.Count > 0 ||
                ActConn.SourceAppServers.Count - SrcAppServerToDelete.Count > 0 ||
                SrcNwGroupsToAdd.Count > 0 ||
                SrcAppServerToAdd.Count > 0 ||
                SrcAppRolesToAdd.Count > 0;
        }

        public bool DstFilledInWork(int dummyARCount = 0)
        {
            return ActConn.DestinationNwGroups.Count - DstNwGroupsToDelete.Count > 0 || 
                ActConn.DestinationAppRoles.Count - dummyARCount - DstAppRolesToDelete.Count > 0 || 
                ActConn.DestinationAppServers.Count - DstAppServerToDelete.Count > 0 ||
                DstNwGroupsToAdd.Count > 0 ||
                DstAppServerToAdd.Count > 0 ||
                DstAppRolesToAdd.Count > 0;
        }

        public bool SvcFilledInWork()
        {
            return ActConn.Services.Count - SvcToDelete.Count > 0 || 
                ActConn.ServiceGroups.Count - SvcGrpToDelete.Count > 0 ||
                SvcToAdd != null && SvcToAdd.Count > 0 ||
                SvcGrpToAdd != null && SvcGrpToAdd.Count > 0;
        }

        public async Task AddToPreselectedList(ModellingConnection? requestedInterface)
        {
            try
            {
                if(requestedInterface != null)
                {
                    var Variables = new
                    {
                        appId = requestedInterface.AppId,
                        connectionId = requestedInterface.Id
                    };
                    await apiConnection.SendQueryAsync<NewReturning>(ModellingQueries.addSelectedConnection, Variables);
                    PreselectedInterfaces.Add(requestedInterface);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("add_interface"), "", true);
            }
        }

        public async Task<bool> Save(bool noCheck = false)
        {
            try
            {
                if (ActConn.Sanitize())
                {
                    DisplayMessageInUi(null, userConfig.GetText("save_connection"), userConfig.GetText("U0001"), true);
                }
                if(noCheck || CheckConn())
                {
                    if(!SrcReadOnly)
                    {
                        SyncSrcChanges();
                    }
                    if(!DstReadOnly)
                    {
                        SyncDstChanges();
                    }
                    if(!SvcReadOnly)
                    {
                        SyncSvcChanges();
                    }
                    ActConn.SyncState();
                    if(AddMode)
                    {
                        await AddConnectionToDb();                        
                    }
                    else
                    {
                        await UpdateConnectionInDb();
                    }
                    await ReInit();
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

        public bool CheckConn()
        {
            if(ActConn.Name == null || ActConn.Name == "" || ActConn.Reason == null || ActConn.Reason == "")
            {
                DisplayMessageInUi(null, userConfig.GetText("edit_connection"), userConfig.GetText("E5102"), true);
                return false;
            }
            if(ActConn.IsInterface)
            {
                int srcDummyARCount = ActConn.SourceAppRoles.Where(x => x.Content.Id == DummyAppRole.Id).Count();
                int dstDummyARCount = ActConn.DestinationAppRoles.Where(x => x.Content.Id == DummyAppRole.Id).Count();
                if(!(SrcFilledInWork(srcDummyARCount) || DstFilledInWork(dstDummyARCount)) || !SvcFilledInWork())
                {
                    DisplayMessageInUi(null, userConfig.GetText("edit_connection"), userConfig.GetText("E9004"), true);
                    return false;
                }
                if(!AddMode && (SrcFilledInWork(srcDummyARCount) != ActConnOrig.SourceFilled() || DstFilledInWork(dstDummyARCount) != ActConnOrig.DestinationFilled()))
                {
                    DisplayMessageInUi(null, userConfig.GetText("edit_connection"), userConfig.GetText("E9005"), true);
                    return false;
                }
            }
            else
            {
                if(!(ActConn.SrcFromInterface || SrcFilledInWork()) || 
                    !(ActConn.DstFromInterface || DstFilledInWork()) ||
                    !(ActConn.UsedInterfaceId != null || SvcFilledInWork()))
                {
                    DisplayMessageInUi(null, userConfig.GetText("edit_connection"), userConfig.GetText("E9006"), true);
                    return false;
                }
            }
            return true;
        }

        private void SyncSrcChanges()
        {
            if(ActConn.IsInterface && SrcFilledInWork(1))
            {
                ModellingAppRoleWrapper? linkedDummyAR = ActConn.SourceAppRoles.FirstOrDefault(x => x.Content.Id == DummyAppRole.Id);
                if (linkedDummyAR != null)
                {
                    SrcAppRolesToDelete.Add(linkedDummyAR.Content);
                }
            }
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
                ActConn.SourceNwGroups.Add(new ModellingNwGroupWrapper(){ Content = nwGroup });
            }
        }

        private void SyncDstChanges()
        {
            if(ActConn.IsInterface && DstFilledInWork(1))
            {
                ModellingAppRoleWrapper? linkedDummyAR = ActConn.DestinationAppRoles.FirstOrDefault(x => x.Content.Id == DummyAppRole.Id);
                if (linkedDummyAR != null)
                {
                    DstAppRolesToDelete.Add(linkedDummyAR.Content);
                }
            }
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
            foreach(var nwGroup in DstNwGroupsToDelete)
            {
                ActConn.DestinationNwGroups.Remove(ActConn.DestinationNwGroups.FirstOrDefault(x => x.Content.Id == nwGroup.Id) ?? throw new Exception("Did not find nwgroup."));
            }
            foreach(var nwGroup in DstNwGroupsToAdd)
            {
                ActConn.DestinationNwGroups.Add(new ModellingNwGroupWrapper(){ Content = nwGroup });
            }
        }

        private void SyncSvcChanges()
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

        private async Task AddConnectionToDb(bool propose = false)
        {
            try
            {
                int? appId = propose ? null : Application.Id;
                int? proposedAppId = propose ? Application.Id : null;

                var Variables = new
                {
                    name = ActConn.Name,
                    appId = appId,
                    proposedAppId = proposedAppId,
                    reason = ActConn.Reason,
                    isInterface = ActConn.IsInterface,
                    usedInterfaceId = ActConn.UsedInterfaceId,
                    isRequested = ActConn.IsRequested,
                    ticketId = ActConn.TicketId,
                    creator = userConfig.User.Name,
                    commonSvc = ActConn.IsCommonService,
                    connProp = ActConn.Properties,
                    extraParams = ActConn.ExtraParams
                };
                ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<NewReturning>(ModellingQueries.newConnection, Variables)).ReturnIds;
                if (returnIds != null)
                {
                    ActConn.Id = returnIds[0].NewId;
                    await LogChange(ModellingTypes.ChangeType.Insert, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                        $"New {(ActConn.IsInterface? "Interface" : "Connection")}: {ActConn.Name}", appId);
                    if(ActConn.UsedInterfaceId == null || ActConn.DstFromInterface)
                    {
                        await AddNwObjects(ModellingAppServerWrapper.Resolve(ActConn.SourceAppServers).ToList(), 
                            ModellingAppRoleWrapper.Resolve(ActConn.SourceAppRoles).ToList(),
                            ModellingNwGroupWrapper.Resolve(ActConn.SourceNwGroups).ToList(),
                            ModellingTypes.ConnectionField.Source);
                    }
                    if(ActConn.UsedInterfaceId == null || ActConn.SrcFromInterface)
                    {
                        await AddNwObjects(ModellingAppServerWrapper.Resolve(ActConn.DestinationAppServers).ToList(),
                            ModellingAppRoleWrapper.Resolve(ActConn.DestinationAppRoles).ToList(),
                            ModellingNwGroupWrapper.Resolve(ActConn.DestinationNwGroups).ToList(),
                            ModellingTypes.ConnectionField.Destination); 
                    }
                    if(ActConn.UsedInterfaceId == null)
                    {
                        await AddSvcObjects(ModellingServiceWrapper.Resolve(ActConn.Services).ToList(),
                            ModellingServiceGroupWrapper.Resolve(ActConn.ServiceGroups).ToList());          
                    }
                    ActConn.Creator = userConfig.User.Name;
                    ActConn.CreationDate = DateTime.Now;
                    Connections.Add(ActConn);
                    Connections.Sort((ModellingConnection a, ModellingConnection b) => a?.CompareTo(b) ?? -1);
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
                    appId = ActConn.AppId,
                    proposedAppId = ActConn.ProposedAppId,
                    reason = ActConn.Reason,
                    isInterface = ActConn.IsInterface,
                    usedInterfaceId = ActConn.UsedInterfaceId,
                    isRequested = ActConn.IsRequested,
                    isPublished = ActConn.IsPublished,
                    commonSvc = ActConn.IsCommonService,
                    connProp = ActConn.Properties,
                    extraParams = ActConn.ExtraParams
                };
                await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.updateConnection, Variables);
                await LogChange(ModellingTypes.ChangeType.Update, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                    $"Updated {(ActConn.IsInterface? "Interface" : "Connection")}: {ActConn.Name}", Application.Id);

                if(ActConn.UsedInterfaceId == null || ActConn.DstFromInterface)
                {
                    await RemoveNwObjects(SrcAppServerToDelete, SrcAppRolesToDelete, SrcNwGroupsToDelete, ModellingTypes.ConnectionField.Source);
                    await AddNwObjects(SrcAppServerToAdd, SrcAppRolesToAdd, SrcNwGroupsToAdd, ModellingTypes.ConnectionField.Source);
                }
                if(ActConn.UsedInterfaceId == null || ActConn.SrcFromInterface)
                {
                    await RemoveNwObjects(DstAppServerToDelete, DstAppRolesToDelete, DstNwGroupsToDelete, ModellingTypes.ConnectionField.Destination);
                    await AddNwObjects(DstAppServerToAdd, DstAppRolesToAdd, DstNwGroupsToAdd, ModellingTypes.ConnectionField.Destination);
                }
                if(ActConn.UsedInterfaceId == null)
                {
                    await RemoveSvcObjects();
                    await AddSvcObjects(SvcToAdd, SvcGrpToAdd);
                }
                Connections[Connections.FindIndex(x => x.Id == ActConn.Id)] = ActConn;
                foreach(var conn in Connections.Where(x => x.UsedInterfaceId == ActConn.Id))
                {
                    await ExtractUsedInterface(conn);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("edit_connection"), "", true);
            }
        }

        private async Task AddNwObjects(List<ModellingAppServer> appServers, List<ModellingAppRole> appRoles, List<ModellingNwGroup> nwGroups, ModellingTypes.ConnectionField field)
        {
            try
            {
                foreach(var appServer in appServers)
                {
                    var Variables = new { nwObjectId = appServer.Id, connectionId = ActConn.Id, connectionField = (int)field };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addAppServerToConnection, Variables);
                    await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                        $"Added App Server {appServer.Display()} to {(ActConn.IsInterface? "Interface" : "Connection")}: {ActConn.Name}: {field}", Application.Id);
                }
                foreach(var appRole in appRoles)
                {
                    var Variables = new { nwGroupId = appRole.Id, connectionId = ActConn.Id, connectionField = (int)field };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addNwGroupToConnection, Variables);
                    await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                        $"Added App Role {appRole.Display()} to {(ActConn.IsInterface? "Interface" : "Connection")}: {ActConn.Name}: {field}", Application.Id);
                }
                foreach(var nwGroup in nwGroups)
                {
                    var Variables = new { nwGroupId = nwGroup.Id, connectionId = ActConn.Id, connectionField = (int)field };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addNwGroupToConnection, Variables);
                    await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                        $"Added Object {nwGroup.Display()} to {(ActConn.IsInterface? "Interface" : "Connection")}: {ActConn.Name}: {field}", Application.Id);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("edit_connection"), "", true);
            }
        }

        private async Task RemoveNwObjects(List<ModellingAppServer> appServers, List<ModellingAppRole> appRoles, List<ModellingNwGroup> nwGroups, ModellingTypes.ConnectionField field)
        {
            try
            {
                foreach(var appServer in appServers)
                {
                    var Variables = new { nwObjectId = appServer.Id, connectionId = ActConn.Id, connectionField = (int)field };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeAppServerFromConnection, Variables);
                    await LogChange(ModellingTypes.ChangeType.Unassign, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                        $"Removed App Server {appServer.Display()} from {(ActConn.IsInterface? "Interface" : "Connection")}: {ActConn.Name}: {field}", Application.Id);
                }
                foreach(var appRole in appRoles)
                {
                    var Variables = new { nwGroupId = appRole.Id, connectionId = ActConn.Id, connectionField = (int)field };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeNwGroupFromConnection, Variables);
                    await LogChange(ModellingTypes.ChangeType.Unassign, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                        $"Removed App Role {appRole.Display()} from {(ActConn.IsInterface? "Interface" : "Connection")}: {ActConn.Name}: {field}", Application.Id);
                }
                foreach(var nwGroup in nwGroups)
                {
                    var Variables = new { nwGroupId = nwGroup.Id, connectionId = ActConn.Id, connectionField = (int)field };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeNwGroupFromConnection, Variables);
                    await LogChange(ModellingTypes.ChangeType.Unassign, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                        $"Removed Object {nwGroup.Display()} from {(ActConn.IsInterface? "Interface" : "Connection")}: {ActConn.Name}: {field}", Application.Id);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("edit_connection"), "", true);
            }
        }

        private async Task AddSvcObjects(List<ModellingService> services, List<ModellingServiceGroup> serviceGroups)
        {
            try
            {
                foreach(var service in services)
                {
                    var svcParams = new { serviceId = service.Id, connectionId = ActConn.Id };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addServiceToConnection, svcParams);
                    await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                        $"Added Service {service.Display()} to {(ActConn.IsInterface? "Interface" : "Connection")}: {ActConn.Name}", Application.Id);
                }
                foreach(var serviceGrp in serviceGroups)
                {
                    var svcGrpParams = new { serviceGroupId = serviceGrp.Id, connectionId = ActConn.Id };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addServiceGroupToConnection, svcGrpParams);
                    await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                        $"Added Service Group {serviceGrp.Display()} to {(ActConn.IsInterface? "Interface" : "Connection")}: {ActConn.Name}", Application.Id);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("edit_connection"), "", true);
            }
        }

        private async Task RemoveSvcObjects()
        {
            try
            {
                foreach(var service in SvcToDelete)
                {
                    var svcParams = new { serviceId = service.Id, connectionId = ActConn.Id };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeServiceFromConnection, svcParams);
                    await LogChange(ModellingTypes.ChangeType.Unassign, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                        $"Removed Service {service.Display()} from {(ActConn.IsInterface? "Interface" : "Connection")}: {ActConn.Name}", Application.Id);
                }
                foreach(var serviceGrp in SvcGrpToDelete)
                {
                    var svcGrpParams = new { serviceGroupId = serviceGrp.Id, connectionId = ActConn.Id };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeServiceGroupFromConnection, svcGrpParams);
                    await LogChange(ModellingTypes.ChangeType.Unassign, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                        $"Removed Service Group {serviceGrp.Display()} from {(ActConn.IsInterface? "Interface" : "Connection")}: {ActConn.Name}", Application.Id);
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
            SrcAppServerToAdd = [];
            SrcAppServerToDelete = [];
            DstAppServerToAdd = [];
            DstAppServerToDelete = [];
            SrcAppRolesToAdd = [];
            SrcAppRolesToDelete = [];
            DstAppRolesToAdd = [];
            DstAppRolesToDelete = [];
            SrcNwGroupsToAdd = [];
            SrcNwGroupsToDelete = [];
            DstNwGroupsToAdd = [];
            DstNwGroupsToDelete = [];
            SvcToAdd = [];
            SvcToDelete = [];
            SvcGrpToAdd = [];
            SvcGrpToDelete = [];
            SearchNWObjectMode = false;
            AddExtraConfigMode = false;
            RemoveNwObjectMode = false;
            RemovePreselectedInterfaceMode = false;
            DisplaySelectedInterfaceMode = false;
            ReplaceMode = false;
            DisplayAppServerMode = false;
            AddAppRoleMode = false;
            EditAppRoleMode = false;
            DeleteAppRoleMode = false;
            DisplayAppRoleMode = false;
            AddSvcGrpMode = false;
            EditSvcGrpMode = false;
            DeleteSvcGrpMode = false;
            DisplaySvcGrpMode = false;
            AddServiceMode = false;
            EditServiceMode = false;
            DeleteServiceMode = false;
        }
    }
}

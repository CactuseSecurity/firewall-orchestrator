using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Modelling;
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
        public List<ModellingConnection> Connections { get; set; }
        public List<ModellingConnection> PreselectedInterfaces { get; set; } = [];
        public ModellingConnection ActConn { get; set; }
        public List<ModellingAppRole> AvailableAppRoles { get; set; } = [];
        public List<ModellingNwGroupWrapper> AvailableSelectedObjects { get; set; } = [];
        public List<ModellingNetworkAreaWrapper> AvailableCommonAreas { get; set; } = [];
        public List<ModellingNwGroupWrapper> AvailableNwGroups { get; set; } = [];
        public List<CommonAreaConfig> CommonAreaConfigItems { get; set; } = [];
        public List<ModellingServiceGroup> AvailableServiceGroups { get; set; } = [];

        public string InterfaceName { get; set; } = "";

        public bool SrcReadOnly { get; set; } = false;
        public bool DstReadOnly { get; set; } = false;
        public bool SvcReadOnly { get; set; } = false;

        public bool AddExtraConfigMode { get; set; } = false;
        public bool SearchNWObjectMode { get; set; } = false;
        public bool RemoveNwObjectMode { get; set; } = false;
        public bool RemovePreselectedInterfaceMode { get; set; } = false;
        public bool DisplaySelectedInterfaceMode { get; set; } = false;
        public bool ReplaceMode { get; set; } = false;
        public ModellingConnectionHandler? IntConnHandler { get; set; }
        public int RequesterId { get; set; } = 0;

        public List<ModellingAppServer> SrcAppServerToAdd { get; set; } = [];
        public List<ModellingAppServer> SrcAppServerToDelete { get; set; } = [];
        public List<ModellingAppServer> DstAppServerToAdd { get; set; } = [];
        public List<ModellingAppServer> DstAppServerToDelete { get; set; } = [];
        public ModellingAppServerHandler? AppServerHandler { get; set; }
        public bool DisplayAppServerMode { get; set; } = false;

        public ModellingAppRoleHandler? AppRoleHandler { get; set; }
        public List<ModellingAppRole> SrcAppRolesToAdd { get; set; } = [];
        public List<ModellingAppRole> SrcAppRolesToDelete { get; set; } = [];
        public List<ModellingAppRole> DstAppRolesToAdd { get; set; } = [];
        public List<ModellingAppRole> DstAppRolesToDelete { get; set; } = [];
        public List<ModellingNetworkArea> SrcAreasToAdd { get; set; } = [];
        public List<ModellingNetworkArea> SrcAreasToDelete { get; set; } = [];
        public List<ModellingNetworkArea> DstAreasToAdd { get; set; } = [];
        public List<ModellingNetworkArea> DstAreasToDelete { get; set; } = [];
        public List<ModellingNwGroup> SrcNwGroupsToAdd { get; set; } = [];
        public List<ModellingNwGroup> SrcNwGroupsToDelete { get; set; } = [];
        public List<ModellingNwGroup> DstNwGroupsToAdd { get; set; } = [];
        public List<ModellingNwGroup> DstNwGroupsToDelete { get; set; } = [];
        public bool AddAppRoleMode { get; set; } = false;
        public bool EditAppRoleMode { get; set; } = false;
        public bool DeleteAppRoleMode { get; set; } = false;
        public bool DisplayAppRoleMode { get; set; } = false;

        public ModellingServiceHandler? ServiceHandler { get; set; }
        public List<ModellingService> SvcToDelete { get; set; } = [];
        public bool AddServiceMode { get; set; } = false;
        public bool EditServiceMode { get; set; } = false;
        public bool DeleteServiceMode { get; set; } = false;

        public ModellingServiceGroupHandler? SvcGrpHandler { get; set; }
        public List<ModellingServiceGroup> SvcGrpToAdd { get; set; } = [];
        public List<ModellingServiceGroup> SvcGrpToDelete { get; set; } = [];
        public bool AddSvcGrpMode { get; set; } = false;
        public bool EditSvcGrpMode { get; set; } = false;
        public bool DeleteSvcGrpMode { get; set; } = false;
        public bool DisplaySvcGrpMode { get; set; } = false;
        public Func<Task> RefreshParent { get; set; }
        public ModellingAppRole DummyAppRole { get; set; } = new();
        public int LastWidth { get; set; } = GlobalConst.kGlobLibraryWidth;
        public bool LastCollapsed { get; set; } = false;
        public bool ActConnNeedsRefresh { get; set; } = true;

        private bool SrcFix = false;
        private bool DstFix = false;
        private ModellingAppRole actAppRole = new();
        private ModellingNwGroup actNwGrpObj = new();
        private ModellingConnection actInterface = new();
        private ModellingServiceGroup actServiceGroup = new();
        private ModellingConnection ActConnOrig { get; set; }
        private bool InitOngoing = false;
        private const string kConnection = "Connection";
        private const string kInterface = "Interface";
        private readonly string EditConnection = "edit_connection";
		private readonly string InitEnvironment = "init_environment";
		private readonly string InsertForbidden = "insert_forbidden";



        public ModellingConnectionHandler(ApiConnection apiConnection, UserConfig userConfig, FwoOwner application,
            List<ModellingConnection> connections, ModellingConnection conn, bool addMode, bool readOnly,
            Action<Exception?, string, string, bool> displayMessageInUi, Func<Task> refreshParent, bool isOwner = true)
            : base(apiConnection, userConfig, application, addMode, displayMessageInUi, readOnly, isOwner)
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
                if (!InitOngoing)
                {
                    InitOngoing = true;
                    await RefreshObjects();
                    InterfaceName = await ExtractUsedInterface(ActConn);
                    AllApps = await apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwnersWithConn);
                    AppRoleHandler = new(apiConnection, userConfig, new(), [], new(), [], [], false, DisplayMessageInUi);
                    DummyAppRole = await AppRoleHandler.GetDummyAppRole();
                    if (!AddMode && !ReadOnly && ActConn.IsInterface)
                    {
                        SrcFix = ActConn.SourceFilled();
                        DstFix = ActConn.DestinationFilled();
                    }
                    InitOngoing = false;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText(InitEnvironment), "", true);
            }
        }

        public async Task PartialInit()
        {
            try
            {
                if (!InitOngoing)
                {
                    InitOngoing = true;
                    AppRoleHandler = new(apiConnection, userConfig, new(), [], new(), [], [], false, DisplayMessageInUi);
                    DummyAppRole = await AppRoleHandler.GetDummyAppRole();
                    InitOngoing = false;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText(InitEnvironment), "", true);
            }
        }

        public async Task ReInit()
        {
            try
            {
                // exclude the cases where ActConn has to be held (e.g. if there is an ui binding)
                if (ActConnNeedsRefresh)
                {
                    await RefreshActConn();
                }

                await RefreshObjects();
                await RefreshParent();
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText(InitEnvironment), "", true);
            }
        }

        public async Task RefreshObjects()
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
                List<ModellingConnection> conns = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getConnectionById, new { id = ActConn.Id });
                if (conns.Count > 0)
                {
                    ActConn = conns[0];
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
                AvailableAppServers = await apiConnection.SendQueryAsync<List<ModellingAppServer>>(ModellingQueries.getAppServersForOwner, new { appId = Application.Id });
                AvailableAppRoles = await apiConnection.SendQueryAsync<List<ModellingAppRole>>(ModellingQueries.getAppRoles, new { appId = Application.Id });

                List<ModellingNetworkArea> allAreas = await apiConnection.SendQueryAsync<List<ModellingNetworkArea>>(ModellingQueries.getNwGroupObjects, new { grpType = (int)ModellingTypes.ModObjectType.NetworkArea });
                CommonAreaConfigItems = [];
                if (userConfig.ModCommonAreas != "")
                {
                    CommonAreaConfigItems = JsonSerializer.Deserialize<List<CommonAreaConfig>>(userConfig.ModCommonAreas) ?? new();
                }
                AvailableCommonAreas = [];
                foreach (var comAreaConfig in CommonAreaConfigItems)
                {
                    ModellingNetworkArea? area = allAreas.FirstOrDefault(a => a.Id == comAreaConfig.AreaId);
                    if (area != null)
                    {
                        AvailableCommonAreas.Add(new() { Content = area });
                    }
                }
                AvailableNwGroups = [];

                AvailableSelectedObjects = await apiConnection.SendQueryAsync<List<ModellingNwGroupWrapper>>(ModellingQueries.getSelectedNwGroupObjects, new { appId = Application.Id });
                AvailableSelectedObjects = AvailableSelectedObjects.Where(x => AvailableCommonAreas.FirstOrDefault(a => a.Content.Id == x.Content.Id) == null).ToList();
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText(InitEnvironment), "", true);
            }
        }

        public async Task InitAvailableSvcObjects()
        {
            try
            {
                AvailableSvcElems = [];
                AvailableServiceGroups = ( await apiConnection.SendQueryAsync<List<ModellingServiceGroup>>(ModellingQueries.getGlobalServiceGroups) ).Where(x => x.AppId != Application.Id).ToList();
                AvailableServiceGroups.AddRange(await apiConnection.SendQueryAsync<List<ModellingServiceGroup>>(ModellingQueries.getServiceGroupsForApp, new { appId = Application.Id }));
                AvailableServices = await apiConnection.SendQueryAsync<List<ModellingService>>(ModellingQueries.getGlobalServices);
                AvailableServices.AddRange(await apiConnection.SendQueryAsync<List<ModellingService>>(ModellingQueries.getServicesForApp, new { appId = Application.Id }));
                foreach (var svcGrp in AvailableServiceGroups)
                {
                    AvailableSvcElems.Add(new KeyValuePair<int, int>((int)ModellingTypes.ModObjectType.ServiceGroup, svcGrp.Id));
                }
                if (userConfig.AllowServiceInConn)
                {
                    foreach (var svc in AvailableServices)
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

        /// <summary>
        /// Checks the given interface object if it can be used with network areas that are added to the connection.
        /// </summary>
        /// <param name="interf"></param>
        /// <returns></returns>
        public bool InterfaceAllowedWithNetworkArea(ModellingConnection interf)
        {
            if (!ActConn.IsInterface && !ActConn.IsCommonService && interf.AppId != ActConn.AppId &&
                ( ActConn.DestinationAreas.Count > 0 || DstAreasToAdd.Count > 0 ||
                ActConn.SourceAreas.Count > 0 || SrcAreasToAdd.Count > 0 ))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks the selected interface if it is foreign to the modelled connection
        /// </summary>
        /// <returns></returns>
        public bool IsNotInterfaceForeignToApp()
        {
            if (!ActConn.IsInterface && !ActConn.IsCommonService && ActConn.UsedInterfaceId != null &&
                ActConn.UsedInterfaceId > 0 && PreselectedInterfaces.FirstOrDefault(_ => _.Id == ActConn.UsedInterfaceId)?.AppId != ActConn.AppId)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks the opposite direction if it already contains a network area.
        /// </summary>
        private bool IsAreaForbiddenInDirection(Direction direction)
        {
            return direction switch
            {
                Direction.Source => ActConn.DestinationAreas.Count > 0 || DstAreasToAdd.Count > 0,
                Direction.Destination => ActConn.SourceAreas.Count > 0 || SrcAreasToAdd.Count > 0,
                _ => false,
            };
        }

        /// <summary>
        /// Checks if the given network areas are allowed in the current connection/interface/service.
        /// </summary>
        /// <param name="networkAreas">The list of network areas to check.</param>
        /// <param name="reason">Out parameter to give context as a reason why it's not allowed, otherwise is's empty.</param>
        public bool NetworkAreaUseAllowed(List<ModellingNetworkArea> networkAreas, Direction direction, out (string Title, string Text) reason)
        {
            if (ActConn.IsCommonService)
            {
                reason.Title = userConfig.GetText("edit_common_service");
            }
            else if (ActConn.IsInterface)
            {
                reason.Title = userConfig.GetText("edit_interface");
            }
            else
            {
                reason.Title = userConfig.GetText(EditConnection);
            }

           reason.Text = "";
            if (IsAreaForbiddenInDirection(direction))
            {
                reason.Text = userConfig.GetText("U9022");
                return false;
            }

            if (ActConn.IsInterface)
            {
                reason.Text = userConfig.GetText("U9021");
                return false;
            }

            bool hasCommonNetworkAreas = HasCommonNetworkAreas(networkAreas);
            if (!hasCommonNetworkAreas && ActConn.IsCommonService)
            {
                return true;
            }
            else if (hasCommonNetworkAreas && ( ActConn.IsCommonService || ( !ActConn.IsInterface && !ActConn.IsCommonService ) ))
            {
                return true;
            }

            reason.Text = userConfig.GetText("U9023");
            return false;
        }

        /// <summary>
        /// Checks the given list of network areas against common network area settings.
        /// </summary>
        /// <param name="networkAreas"></param>
        /// <returns></returns>
        private bool HasCommonNetworkAreas(List<ModellingNetworkArea> networkAreas)
        {
            return networkAreas.Any(a => CommonAreaConfigItems.Any(_ => _.AreaId == a.Id));
        }

        public bool SaveExtraConfig(ModellingExtraConfig extraConfig)
        {
            extraConfig.Id = ActConn.ExtraConfigs.Count > 0 ? ActConn.ExtraConfigs.OrderByDescending(x => x.Id).First().Id + 1 : 1;
            List<ModellingExtraConfig> actList = ActConn.ExtraConfigs;
            actList.Add(extraConfig);
            ActConn.ExtraConfigs = actList;
            return true;
        }

        public void UpdateExtraConfig(ChangeEventArgs e, ModellingExtraConfig extraConfig)
        {
            List<ModellingExtraConfig> actList = ActConn.ExtraConfigs;
            ModellingExtraConfig? actConfig = actList.FirstOrDefault(x => x.Id == extraConfig.Id);
            if (actConfig != null)
            {
                actConfig.ExtraConfigText = e.Value?.ToString() ?? "";
                actConfig.Sanitize();
            }
            ActConn.ExtraConfigs = actList;
        }

        public bool DeleteExtraConfig(ModellingExtraConfig extraConfig)
        {
            List<ModellingExtraConfig> actList = ActConn.ExtraConfigs;
            actList.Remove(actList.FirstOrDefault(x => x.Id == extraConfig.Id) ?? throw new KeyNotFoundException("Did not find service group."));
            ActConn.ExtraConfigs = actList;
            return true;
        }

        public async Task<long> CreateNewRequestedInterface(long ticketId, bool asSource, string name, string reason)
        {
            ActConn.TicketId = ticketId;
            ActConn.Name = name + " (Ticket: " + ActConn.TicketId.ToString() + ")";
            ActConn.Reason = $"{userConfig.GetText("from_ticket")} {ticketId} ({userConfig.GetText("U9012")}): \r\n{reason}";
            ActConn.IsInterface = true;
            ActConn.IsRequested = true;
            if (asSource)
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
            if (inter != null)
            {
                FwoOwner? app = AllApps.FirstOrDefault(x => x.Id == inter.AppId);
                if (app != null)
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
            foreach (var obj in AvailableCommonAreas.Select(o => o.Content))
            {
                AvailableNwElems.Add(new KeyValuePair<int, long>(obj.GroupType, obj.Id));
            }
            foreach (var obj in AvailableSelectedObjects.Select(o => o.Content))
            {
                AvailableNwElems.Add(new KeyValuePair<int, long>(obj.GroupType, obj.Id));
            }
            foreach (var appRole in AvailableAppRoles)
            {
                AvailableNwElems.Add(new KeyValuePair<int, long>((int)ModellingTypes.ModObjectType.AppRole, appRole.Id));
            }
            if (userConfig.AllowServerInConn)
            {
                foreach (var appServer in AvailableAppServers.Where(x => !x.IsDeleted))
                {
                    AvailableNwElems.Add(new KeyValuePair<int, long>((int)ModellingTypes.ModObjectType.AppServer, appServer.Id));
                }
            }
            return true;
        }

        public async Task RequestReplaceInterface(ModellingConnection interf)
        {
            if (interf.SourceFilled() != ActConn.SourceFilled() || interf.DestinationFilled() != ActConn.DestinationFilled())
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
                if (IntConnHandler != null)
                {
                    await ReplaceLinks();
                    await RemoveFromAllSelections();
                    if (await DeleteRequestedInterface())
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
            if (await CheckInterfaceInUse(ActConn))
            {
                var Variables = new
                {
                    usedInterfaceIdOld = ActConn.Id,
                    usedInterfaceIdNew = IntConnHandler?.ActConn.Id
                };
                int usingConns = ( await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.replaceUsedInterface, Variables) ).AffectedRows;
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
            if (await CheckInterfaceInUse(ActConn))
            {
                DisplayMessageInUi(null, userConfig.GetText("delete_interface"), userConfig.GetText("E9016"), true);
                return false;
            }
            if(await DeleteConnection(ActConn))
            {
                await LogChange(ModellingTypes.ChangeType.Delete, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                    $"Deleted Interface: {ActConn.Name}", Application.Id);
                Connections.Remove(ActConn);
            }
            return true;
        }

        private async Task UpdateTicket(Task<AuthenticationState> authenticationStateTask, MiddlewareClient middlewareClient)
        {
            if (ActConn.TicketId != null)
            {
                try
                {
                    // change referred connId ?
                    string comment = $"{userConfig.GetText("U9016")}: {IntConnHandler?.ActConn.Name}";
                    TicketCreator ticketCreator = new(apiConnection, userConfig, authenticationStateTask!.Result.User, middlewareClient, WorkflowPhases.implementation);
                    if (await ticketCreator.PromoteNewInterfaceImplTask((long)ActConn.TicketId, ExtStates.Done, comment))
                    {
                        DisplayMessageInUi(null, comment, userConfig.GetText("U9013"), false);
                    }
                }
                catch (Exception exception)
                {
                    DisplayMessageInUi(exception, userConfig.GetText("update_ticket"), "", true);
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
            if (SrcReadOnly)
            {
                ActConn.SourceAppServers = [.. interf.SourceAppServers];
                ActConn.SourceAppRoles = [.. interf.SourceAppRoles];
                ActConn.SourceAreas = [.. interf.SourceAreas];
                ActConn.SourceOtherGroups = [.. interf.SourceOtherGroups];
                ActConn.SrcFromInterface = true;
            }
            else
            {
                ActConn.DestinationAppServers = [.. interf.DestinationAppServers];
                ActConn.DestinationAppRoles = [.. interf.DestinationAppRoles];
                ActConn.DestinationAreas = [.. interf.DestinationAreas];
                ActConn.DestinationOtherGroups = [.. interf.DestinationOtherGroups];
                ActConn.DstFromInterface = true;
            }
            ActConn.Services = [.. interf.Services];
            ActConn.ServiceGroups = [.. interf.ServiceGroups];
            ActConn.ExtraConfigsFromInterface = interf.ExtraConfigs;
        }

        public void RemoveInterf()
        {
            InterfaceName = "";
            if (SrcReadOnly)
            {
                ActConn.SourceAppServers = [];
                ActConn.SourceAppRoles = [];
                ActConn.SourceAreas = [];
                ActConn.SourceOtherGroups = [];
                ActConn.SrcFromInterface = false;
            }
            if (DstReadOnly)
            {
                ActConn.DestinationAppServers = [];
                ActConn.DestinationAppRoles = [];
                ActConn.DestinationAreas = [];
                ActConn.DestinationOtherGroups = [];
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
            ActConn.ExtraConfigsFromInterface = [];
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
                if (( await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeSelectedConnectionFromApp, new { appId = Application.Id, connectionId = actInterface.Id }) ).AffectedRows > 0)
                {
                    PreselectedInterfaces.Remove(PreselectedInterfaces.FirstOrDefault(x => x.Id == actInterface.Id) ?? throw new KeyNotFoundException("Did not find object."));
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
            if (appServer != null)
            {
                try
                {
                    AppServerHandler = new(apiConnection, userConfig, Application, appServer, [], false, DisplayMessageInUi) { ReadOnly = true };
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
            if (!SrcDropForbidden())
            {
                foreach (var srcAppServer in srcAppServers)
                {
                    if (!srcAppServer.IsDeleted && ActConn.SourceAppServers.FirstOrDefault(w => w.Content.Id == srcAppServer.Id) == null && !SrcAppServerToAdd.Contains(srcAppServer))
                    {
                        SrcAppServerToAdd.Add(srcAppServer);
                    }
                }
                CalcVisibility();
            }
        }

        public void AppServerToDestination(List<ModellingAppServer> dstAppServers)
        {
            if (!DstDropForbidden())
            {
                foreach (var dstAppServer in dstAppServers)
                {
                    if (!dstAppServer.IsDeleted && ActConn.DestinationAppServers.FirstOrDefault(w => w.Content.Id == dstAppServer.Id) == null && !DstAppServerToAdd.Contains(dstAppServer))
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
            if (nwGrpObj != null)
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
                if (( await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeSelectedNwGroupObject, new { appId = Application.Id, nwGroupId = actNwGrpObj.Id }) ).AffectedRows > 0)
                {
                    AvailableSelectedObjects.Remove(AvailableSelectedObjects.FirstOrDefault(x => x.Content.Id == actNwGrpObj.Id) ?? throw new KeyNotFoundException("Did not find object."));
                    AvailableNwElems.Remove(AvailableNwElems.FirstOrDefault(x => x.Key == actNwGrpObj.GroupType && x.Value == actNwGrpObj.Id));
                    RemoveNwObjectMode = false;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("remove_nw_object"), "", true);
            }
        }

        public void AreasToSource(List<ModellingNetworkArea> areas)
        {
            if (!SrcDropForbidden())
            {
                foreach (var area in areas)
                {
                    if (ActConn.SourceAreas.FirstOrDefault(w => w.Content.Id == area.Id) == null && !SrcAreasToAdd.Contains(area) &&
                        ( CommonAreaConfigItems.FirstOrDefault(x => x.AreaId == area.Id)?.UseInSrc ?? true ))
                    {
                        SrcAreasToAdd.Add(area);
                    }
                    else
                    {
                        DisplayMessageInUi(null, userConfig.GetText(InsertForbidden), userConfig.GetText("E9015"), true);
                    }
                }
                CalcVisibility();
            }
        }

        public void AreasToDestination(List<ModellingNetworkArea> areas)
        {
            if (!DstDropForbidden())
            {
                foreach (var area in areas)
                {
                    if (ActConn.DestinationAreas.FirstOrDefault(w => w.Content.Id == area.Id) == null && !DstAreasToAdd.Contains(area) &&
                        ( CommonAreaConfigItems.FirstOrDefault(x => x.AreaId == area.Id)?.UseInSrc ?? true ))
                    {
                        DstAreasToAdd.Add(area);
                    }
                    else
                    {
                        DisplayMessageInUi(null, userConfig.GetText(InsertForbidden), userConfig.GetText("E9015"), true);
                    }
                }
                CalcVisibility();
            }
        }

        public void NwGroupToSource(List<ModellingNwGroup> nwGroups)
        {
            if (!SrcDropForbidden())
            {
                foreach (var nwGroup in nwGroups)
                {
                    if (ActConn.SourceOtherGroups.FirstOrDefault(w => w.Content.Id == nwGroup.Id) == null && !SrcNwGroupsToAdd.Contains(nwGroup) &&
                        ( CommonAreaConfigItems.FirstOrDefault(x => x.AreaId == nwGroup.Id)?.UseInSrc ?? true ))
                    {
                        SrcNwGroupsToAdd.Add(nwGroup);
                    }
                    else
                    {
                        DisplayMessageInUi(null, userConfig.GetText(InsertForbidden), userConfig.GetText("E9015"), true);
                    }
                }
                CalcVisibility();
            }
        }

        public void NwGroupToDestination(List<ModellingNwGroup> nwGroups)
        {
            if (!DstDropForbidden())
            {
                foreach (var nwGroup in nwGroups)
                {
                    if (ActConn.DestinationOtherGroups.FirstOrDefault(w => w.Content.Id == nwGroup.Id) == null && !DstNwGroupsToAdd.Contains(nwGroup) &&
                        ( CommonAreaConfigItems.FirstOrDefault(x => x.AreaId == nwGroup.Id)?.UseInDst ?? true ))
                    {
                        DstNwGroupsToAdd.Add(nwGroup);
                    }
                    else
                    {
                        DisplayMessageInUi(null, userConfig.GetText(InsertForbidden), userConfig.GetText("E9015"), true);
                    }
                }
                CalcVisibility();
            }
        }

        public void CreateAppRole()
        {
            DisplayAppRoleMode = false;
            AddAppRoleMode = true;
            HandleAppRole(new ModellingAppRole() { });
        }

        public void EditAppRole(ModellingAppRole? appRole)
        {
            if (appRole != null)
            {
                DisplayAppRoleMode = false;
                AddAppRoleMode = false;
                HandleAppRole(appRole);
            }
        }

        public void DisplayAppRole(ModellingAppRole? appRole)
        {
            if (appRole != null)
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
            if (appRole != null)
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
                if (SrcAppRolesToAdd.FirstOrDefault(s => s.Id == actAppRole.Id) == null && DstAppRolesToAdd.FirstOrDefault(s => s.Id == actAppRole.Id) == null)
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
                if (( await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.deleteNwGroup, new { id = actAppRole.Id }) ).AffectedRows > 0)
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
            if (!SrcDropForbidden())
            {
                foreach (var appRole in appRoles)
                {
                    if (ActConn.SourceAppRoles.FirstOrDefault(w => w.Content.Id == appRole.Id) == null && !SrcAppRolesToAdd.Contains(appRole))
                    {
                        SrcAppRolesToAdd.Add(appRole);
                    }
                }
                CalcVisibility();
            }
        }

        public void AppRolesToDestination(List<ModellingAppRole> appRoles)
        {
            if (!DstDropForbidden())
            {
                foreach (var appRole in appRoles)
                {
                    if (ActConn.DestinationAppRoles.FirstOrDefault(w => w.Content.Id == appRole.Id) == null && !DstAppRolesToAdd.Contains(appRole))
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
            HandleServiceGroup(new ModellingServiceGroup() { });
        }

        public void EditServiceGroup(ModellingServiceGroup? serviceGroup)
        {
            if (serviceGroup != null)
            {
                DisplaySvcGrpMode = false;
                AddSvcGrpMode = false;
                HandleServiceGroup(serviceGroup);
            }
        }

        public void DisplayServiceGroup(ModellingServiceGroup? serviceGroup)
        {
            if (serviceGroup != null)
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
            if (serviceGroup != null)
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
                if (SvcGrpToAdd.FirstOrDefault(s => s.Id == actServiceGroup.Id) == null)
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
                if (( await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.deleteServiceGroup, new { id = actServiceGroup.Id }) ).AffectedRows > 0)
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
            foreach (var grp in serviceGrps)
            {
                if (( ActConn.ServiceGroups.FirstOrDefault(w => w.Content.Id == grp.Id) == null ) && ( SvcGrpToAdd.FirstOrDefault(g => g.Id == grp.Id) == null ))
                {
                    SvcGrpToAdd.Add(grp);
                }
            }
        }


        public void CreateService()
        {
            AddServiceMode = true;
            HandleService(new ModellingService() { });
        }

        public void EditService(ModellingService? service)
        {
            if (service != null)
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
            if (service != null)
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
            foreach (var svc in services)
            {
                if (( ActConn.Services.FirstOrDefault(w => w.Content.Id == svc.Id) == null ) && ( SvcToAdd.FirstOrDefault(s => s.Id == svc.Id) == null ))
                {
                    SvcToAdd.Add(svc);
                }
            }
        }

        public bool CalcVisibility()
        {
            if (ActConn.IsInterface)
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
            return SrcReadOnly || ( ActConn.IsInterface && DstFilledInWork() );
        }

        public bool DstDropForbidden()
        {
            return DstReadOnly || ( ActConn.IsInterface && SrcFilledInWork() );
        }

        public bool SrcFilledInWork(int dummyARCount = 0)
        {
            return ActConn.SourceAreas.Count - SrcAreasToDelete.Count > 0 ||
                ActConn.SourceOtherGroups.Count - SrcNwGroupsToDelete.Count > 0 ||
                ActConn.SourceAppRoles.Count - dummyARCount - SrcAppRolesToDelete.Count > 0 ||
                ActConn.SourceAppServers.Count - SrcAppServerToDelete.Count > 0 ||
                SrcAreasToAdd.Count > 0 ||
                SrcNwGroupsToAdd.Count > 0 ||
                SrcAppServerToAdd.Count > 0 ||
                SrcAppRolesToAdd.Count > 0;
        }

        public bool DstFilledInWork(int dummyARCount = 0)
        {
            return ActConn.DestinationAreas.Count - DstAreasToDelete.Count > 0 ||
                ActConn.DestinationOtherGroups.Count - DstNwGroupsToDelete.Count > 0 ||
                ActConn.DestinationAppRoles.Count - dummyARCount - DstAppRolesToDelete.Count > 0 ||
                ActConn.DestinationAppServers.Count - DstAppServerToDelete.Count > 0 ||
                DstAreasToAdd.Count > 0 ||
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
                if (requestedInterface != null)
                {
                    var Variables = new
                    {
                        appId = requestedInterface.AppId,
                        connectionId = requestedInterface.Id
                    };
                    await apiConnection.SendQueryAsync<ReturnIdWrapper>(ModellingQueries.addSelectedConnection, Variables);
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
                if (noCheck || CheckConn())
                {
                    SyncChanges();
                    ActConn.SyncState(DummyAppRole.Id);
                    if (AddMode)
                    {
                        await AddConnectionToDb();
                    }
                    else
                    {
                        await UpdateConnection();
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

        private async Task UpdateConnection()
        {
            if (userConfig.VarianceAnalysisSleepTime == 0 && (userConfig.VarianceAnalysisSync || userConfig.VarianceAnalysisRefresh))
            {
                ActConn.CleanUpVarianceResults();
            }
            await UpdateConnectionInDb();
            if(ActConn.IsInterface && ActConn.IsPublished)
            {
                await UpdateStatusInterfaceUsers(ActConn.Id);
            }
        }

        public bool CheckConn()
        {
            if (ActConn.Name == null || ActConn.Name == "" || ActConn.Reason == null || ActConn.Reason == "")
            {
                DisplayMessageInUi(null, userConfig.GetText(EditConnection), userConfig.GetText("E5102"), true);
                return false;
            }
            if (ActConn.IsInterface)
            {
                if (!CheckInterface())
                {
                    return false;
                }
            }
            else
            {
                if (!(ActConn.SrcFromInterface || SrcFilledInWork()) ||
                    !(ActConn.DstFromInterface || DstFilledInWork()) ||
                    !(ActConn.UsedInterfaceId != null || SvcFilledInWork()))
                {
                    DisplayMessageInUi(null, userConfig.GetText(EditConnection), userConfig.GetText("E9006"), true);
                    return false;
                }
            }
            return true;
        }

        private bool CheckInterface()
        {
            int srcDummyARCount = ActConn.SourceAppRoles.Count(x => x.Content.Id == DummyAppRole.Id);
            int dstDummyARCount = ActConn.DestinationAppRoles.Count(x => x.Content.Id == DummyAppRole.Id);
            if (!(SrcFilledInWork(srcDummyARCount) || DstFilledInWork(dstDummyARCount)) || !SvcFilledInWork())
            {
                DisplayMessageInUi(null, userConfig.GetText(EditConnection), userConfig.GetText("E9004"), true);
                return false;
            }
            if (!AddMode && (SrcFilledInWork(srcDummyARCount) != ActConnOrig.SourceFilled() || DstFilledInWork(dstDummyARCount) != ActConnOrig.DestinationFilled()))
            {
                DisplayMessageInUi(null, userConfig.GetText(EditConnection), userConfig.GetText("E9005"), true);
                return false;
            }
            return true;
        }

        private async Task UpdateStatusInterfaceUsers(int interfaceId)
        {
            try
            {
                List<ModellingConnection> usingConnections = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getInterfaceUsers, new { id = interfaceId });
                foreach (var conn in usingConnections.Where(c => c.GetBoolProperty(ConState.InterfaceRequested.ToString())))
                {
                    conn.RemoveProperty(ConState.InterfaceRequested.ToString());
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.updateConnectionProperties, new { id = conn.Id, connProp = conn.Properties });
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("update_interf_user"), "", true);
            }
        }

        private void SyncChanges()
        {
            if (!SrcReadOnly)
            {
                SyncSrcChanges();
            }
            if (!DstReadOnly)
            {
                SyncDstChanges();
            }
            if (!SvcReadOnly)
            {
                SyncSvcChanges();
            }
        }

        private void SyncSrcChanges()
        {
            if (ActConn.IsInterface && SrcFilledInWork(1))
            {
                ModellingAppRoleWrapper? linkedDummyAR = ActConn.SourceAppRoles.FirstOrDefault(x => x.Content.Id == DummyAppRole.Id);
                if (linkedDummyAR != null)
                {
                    SrcAppRolesToDelete.Add(linkedDummyAR.Content);
                }
            }
            foreach (var appServer in SrcAppServerToDelete)
            {
                ActConn.SourceAppServers.Remove(ActConn.SourceAppServers.FirstOrDefault(x => x.Content.Id == appServer.Id) ?? throw new KeyNotFoundException("Did not find app server."));
            }
            foreach (var appServer in SrcAppServerToAdd)
            {
                ActConn.SourceAppServers.Add(new ModellingAppServerWrapper() { Content = appServer });
            }
            foreach (var appRole in SrcAppRolesToDelete)
            {
                ActConn.SourceAppRoles.Remove(ActConn.SourceAppRoles.FirstOrDefault(x => x.Content.Id == appRole.Id) ?? throw new KeyNotFoundException("Did not find app role."));
            }
            foreach (var appRole in SrcAppRolesToAdd)
            {
                ActConn.SourceAppRoles.Add(new ModellingAppRoleWrapper() { Content = appRole });
            }
            foreach (var area in SrcAreasToDelete)
            {
                ActConn.SourceAreas.Remove(ActConn.SourceAreas.FirstOrDefault(x => x.Content.Id == area.Id) ?? throw new KeyNotFoundException("Did not find area."));
            }
            foreach (var area in SrcAreasToAdd)
            {
                ActConn.SourceAreas.Add(new ModellingNetworkAreaWrapper() { Content = area });
            }
            foreach (var nwGroup in SrcNwGroupsToDelete)
            {
                ActConn.SourceOtherGroups.Remove(ActConn.SourceOtherGroups.FirstOrDefault(x => x.Content.Id == nwGroup.Id) ?? throw new KeyNotFoundException("Did not find nwgroup."));
            }
            foreach (var nwGroup in SrcNwGroupsToAdd)
            {
                ActConn.SourceOtherGroups.Add(new ModellingNwGroupWrapper() { Content = nwGroup });
            }
        }

        private void SyncDstChanges()
        {
            if (ActConn.IsInterface && DstFilledInWork(1))
            {
                ModellingAppRoleWrapper? linkedDummyAR = ActConn.DestinationAppRoles.FirstOrDefault(x => x.Content.Id == DummyAppRole.Id);
                if (linkedDummyAR != null)
                {
                    DstAppRolesToDelete.Add(linkedDummyAR.Content);
                }
            }
            foreach (var appServer in DstAppServerToDelete)
            {
                ActConn.DestinationAppServers.Remove(ActConn.DestinationAppServers.FirstOrDefault(x => x.Content.Id == appServer.Id) ?? throw new KeyNotFoundException("Did not find app server."));
            }
            foreach (var appServer in DstAppServerToAdd)
            {
                ActConn.DestinationAppServers.Add(new ModellingAppServerWrapper() { Content = appServer });
            }
            foreach (var appRole in DstAppRolesToDelete)
            {
                ActConn.DestinationAppRoles.Remove(ActConn.DestinationAppRoles.FirstOrDefault(x => x.Content.Id == appRole.Id) ?? throw new KeyNotFoundException("Did not find app role."));
            }
            foreach (var appRole in DstAppRolesToAdd)
            {
                ActConn.DestinationAppRoles.Add(new ModellingAppRoleWrapper() { Content = appRole });
            }
            foreach (var area in DstAreasToDelete)
            {
                ActConn.DestinationAreas.Remove(ActConn.DestinationAreas.FirstOrDefault(x => x.Content.Id == area.Id) ?? throw new KeyNotFoundException("Did not find area."));
            }
            foreach (var area in DstAreasToAdd)
            {
                ActConn.DestinationAreas.Add(new ModellingNetworkAreaWrapper() { Content = area });
            }
            foreach (var nwGroup in DstNwGroupsToDelete)
            {
                ActConn.DestinationOtherGroups.Remove(ActConn.DestinationOtherGroups.FirstOrDefault(x => x.Content.Id == nwGroup.Id) ?? throw new KeyNotFoundException("Did not find nwgroup."));
            }
            foreach (var nwGroup in DstNwGroupsToAdd)
            {
                ActConn.DestinationOtherGroups.Add(new ModellingNwGroupWrapper() { Content = nwGroup });
            }
        }

        private void SyncSvcChanges()
        {
            foreach (var svc in SvcToDelete)
            {
                ActConn.Services.Remove(ActConn.Services.FirstOrDefault(x => x.Content.Id == svc.Id) ?? throw new KeyNotFoundException("Did not find service."));
            }
            foreach (var svc in SvcToAdd)
            {
                ActConn.Services.Add(new ModellingServiceWrapper() { Content = svc });
            }
            foreach (var svcGrp in SvcGrpToDelete)
            {
                ActConn.ServiceGroups.Remove(ActConn.ServiceGroups.FirstOrDefault(x => x.Content.Id == svcGrp.Id) ?? throw new KeyNotFoundException("Did not find service group."));
            }
            foreach (var svcGrp in SvcGrpToAdd)
            {
                ActConn.ServiceGroups.Add(new ModellingServiceGroupWrapper() { Content = svcGrp });
            }
        }

        private async Task AddConnectionToDb(bool propose = false)
        {
            try
            {
                int? AppId = propose ? null : Application.Id;
                int? ProposedAppId = propose ? Application.Id : null;

                var Variables = new
                {
                    name = ActConn.Name,
                    appId = AppId,
                    proposedAppId = ProposedAppId,
                    reason = ActConn.Reason,
                    isInterface = ActConn.IsInterface,
                    usedInterfaceId = ActConn.UsedInterfaceId,
                    isRequested = ActConn.IsRequested,
                    isPublished = ActConn.IsPublished,
                    ticketId = ActConn.TicketId,
                    creator = userConfig.User.Name,
                    commonSvc = ActConn.IsCommonService,
                    connProp = ActConn.Properties,
                    extraParams = ActConn.ExtraParams
                };
                ReturnId[]? returnIds = ( await apiConnection.SendQueryAsync<ReturnIdWrapper>(ModellingQueries.newConnection, Variables) ).ReturnIds;
                if (returnIds != null)
                {
                    ActConn.Id = returnIds[0].NewId;
                    await LogChange(ModellingTypes.ChangeType.Insert, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                        $"New {( ActConn.IsInterface ? kInterface : kConnection )}: {ActConn.Name}", AppId);
                    if (ActConn.UsedInterfaceId == null || ActConn.DstFromInterface)
                    {
                        await AddNwObjects(ModellingAppServerWrapper.Resolve(ActConn.SourceAppServers).ToList(),
                             ModellingAppRoleWrapper.Resolve(ActConn.SourceAppRoles).ToList(),
                             ModellingNetworkAreaWrapper.Resolve(ActConn.SourceAreas).ToList(),
                             ModellingNwGroupWrapper.Resolve(ActConn.SourceOtherGroups).ToList(),
                             ModellingTypes.ConnectionField.Source);
                    }
                    if (ActConn.UsedInterfaceId == null || ActConn.SrcFromInterface)
                    {
                        await AddNwObjects(ModellingAppServerWrapper.Resolve(ActConn.DestinationAppServers).ToList(),
                            ModellingAppRoleWrapper.Resolve(ActConn.DestinationAppRoles).ToList(),
                            ModellingNetworkAreaWrapper.Resolve(ActConn.DestinationAreas).ToList(),
                            ModellingNwGroupWrapper.Resolve(ActConn.DestinationOtherGroups).ToList(),
                            ModellingTypes.ConnectionField.Destination);
                    }
                    if (ActConn.UsedInterfaceId == null)
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
                    $"Updated {( ActConn.IsInterface ? kInterface : kConnection )}: {ActConn.Name}", Application.Id);

                if (ActConn.UsedInterfaceId == null || ActConn.DstFromInterface)
                {
                    await RemoveNwObjects(SrcAppServerToDelete, SrcAppRolesToDelete, SrcAreasToDelete, SrcNwGroupsToDelete, ModellingTypes.ConnectionField.Source);
                    await AddNwObjects(SrcAppServerToAdd, SrcAppRolesToAdd, SrcAreasToAdd, SrcNwGroupsToAdd, ModellingTypes.ConnectionField.Source);
                }
                if (ActConn.UsedInterfaceId == null || ActConn.SrcFromInterface)
                {
                    await RemoveNwObjects(DstAppServerToDelete, DstAppRolesToDelete, DstAreasToDelete, DstNwGroupsToDelete, ModellingTypes.ConnectionField.Destination);
                    await AddNwObjects(DstAppServerToAdd, DstAppRolesToAdd, DstAreasToAdd, DstNwGroupsToAdd, ModellingTypes.ConnectionField.Destination);
                }
                if (ActConn.UsedInterfaceId == null)
                {
                    await RemoveSvcObjects();
                    await AddSvcObjects(SvcToAdd, SvcGrpToAdd);
                }
                Connections[Connections.FindIndex(x => x.Id == ActConn.Id)] = ActConn;
                foreach (var conn in Connections.Where(x => x.UsedInterfaceId == ActConn.Id))
                {
                    await ExtractUsedInterface(conn);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText(EditConnection), "", true);
            }
        }

        private async Task AddNwObjects(List<ModellingAppServer> appServers, List<ModellingAppRole> appRoles,
            List<ModellingNetworkArea> areas, List<ModellingNwGroup> nwGroups, ModellingTypes.ConnectionField field)
        {
            try
            {
                foreach (var appServer in appServers)
                {
                    var Variables = new { nwObjectId = appServer.Id, connectionId = ActConn.Id, connectionField = (int)field };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addAppServerToConnection, Variables);
                    await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                        $"Added App Server {appServer.Display()} to {( ActConn.IsInterface ? kInterface : kConnection )}: {ActConn.Name}: {field}", Application.Id);
                }
                foreach (var appRole in appRoles)
                {
                    var Variables = new { nwGroupId = appRole.Id, connectionId = ActConn.Id, connectionField = (int)field };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addNwGroupToConnection, Variables);
                    await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                        $"Added App Role {appRole.Display()} to {( ActConn.IsInterface ? kInterface : kConnection )}: {ActConn.Name}: {field}", Application.Id);
                }
                foreach (var area in areas)
                {
                    var Variables = new { nwGroupId = area.Id, connectionId = ActConn.Id, connectionField = (int)field };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addNwGroupToConnection, Variables);
                    await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                        $"Added Area {area.Display()} to {( ActConn.IsInterface ? kInterface : kConnection )}: {ActConn.Name}: {field}", Application.Id);
                }
                foreach (var nwGroup in nwGroups)
                {
                    var Variables = new { nwGroupId = nwGroup.Id, connectionId = ActConn.Id, connectionField = (int)field };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addNwGroupToConnection, Variables);
                    await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                        $"Added Object {nwGroup.Display()} to {( ActConn.IsInterface ? kInterface : kConnection )}: {ActConn.Name}: {field}", Application.Id);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText(EditConnection), "", true);
            }
        }

        private async Task RemoveNwObjects(List<ModellingAppServer> appServers, List<ModellingAppRole> appRoles,
            List<ModellingNetworkArea> areas, List<ModellingNwGroup> nwGroups, ModellingTypes.ConnectionField field)
        {
            try
            {
                foreach (var appServer in appServers)
                {
                    var Variables = new { nwObjectId = appServer.Id, connectionId = ActConn.Id, connectionField = (int)field };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeAppServerFromConnection, Variables);
                    await LogChange(ModellingTypes.ChangeType.Unassign, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                        $"Removed App Server {appServer.Display()} from {( ActConn.IsInterface ? kInterface : kConnection )}: {ActConn.Name}: {field}", Application.Id);
                }
                foreach (var appRole in appRoles)
                {
                    var Variables = new { nwGroupId = appRole.Id, connectionId = ActConn.Id, connectionField = (int)field };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeNwGroupFromConnection, Variables);
                    await LogChange(ModellingTypes.ChangeType.Unassign, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                        $"Removed App Role {appRole.Display()} from {( ActConn.IsInterface ? kInterface : kConnection )}: {ActConn.Name}: {field}", Application.Id);
                }
                foreach (var area in areas)
                {
                    var Variables = new { nwGroupId = area.Id, connectionId = ActConn.Id, connectionField = (int)field };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeNwGroupFromConnection, Variables);
                    await LogChange(ModellingTypes.ChangeType.Unassign, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                        $"Removed Area {area.Display()} from {( ActConn.IsInterface ? kInterface : kConnection )}: {ActConn.Name}: {field}", Application.Id);
                }
                foreach (var nwGroup in nwGroups)
                {
                    var Variables = new { nwGroupId = nwGroup.Id, connectionId = ActConn.Id, connectionField = (int)field };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeNwGroupFromConnection, Variables);
                    await LogChange(ModellingTypes.ChangeType.Unassign, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                        $"Removed Object {nwGroup.Display()} from {( ActConn.IsInterface ? kInterface : kConnection )}: {ActConn.Name}: {field}", Application.Id);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText(EditConnection), "", true);
            }
        }

        private async Task AddSvcObjects(List<ModellingService> services, List<ModellingServiceGroup> serviceGroups)
        {
            try
            {
                foreach (var service in services)
                {
                    var svcParams = new { serviceId = service.Id, connectionId = ActConn.Id };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addServiceToConnection, svcParams);
                    await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                        $"Added Service {service.Display()} to {( ActConn.IsInterface ? kInterface : kConnection )}: {ActConn.Name}", Application.Id);
                }
                foreach (var serviceGrp in serviceGroups)
                {
                    var svcGrpParams = new { serviceGroupId = serviceGrp.Id, connectionId = ActConn.Id };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addServiceGroupToConnection, svcGrpParams);
                    await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                        $"Added Service Group {serviceGrp.Display()} to {( ActConn.IsInterface ? kInterface : kConnection )}: {ActConn.Name}", Application.Id);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText(EditConnection), "", true);
            }
        }

        private async Task RemoveSvcObjects()
        {
            try
            {
                foreach (var service in SvcToDelete)
                {
                    var svcParams = new { serviceId = service.Id, connectionId = ActConn.Id };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeServiceFromConnection, svcParams);
                    await LogChange(ModellingTypes.ChangeType.Unassign, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                        $"Removed Service {service.Display()} from {( ActConn.IsInterface ? kInterface : kConnection )}: {ActConn.Name}", Application.Id);
                }
                foreach (var serviceGrp in SvcGrpToDelete)
                {
                    var svcGrpParams = new { serviceGroupId = serviceGrp.Id, connectionId = ActConn.Id };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeServiceGroupFromConnection, svcGrpParams);
                    await LogChange(ModellingTypes.ChangeType.Unassign, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                        $"Removed Service Group {serviceGrp.Display()} from {( ActConn.IsInterface ? kInterface : kConnection )}: {ActConn.Name}", Application.Id);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText(EditConnection), "", true);
            }
        }

        public void Reset()
        {
            ActConn = ActConnOrig;
            if (!AddMode)
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
            SrcAreasToAdd = [];
            SrcAreasToDelete = [];
            DstAreasToAdd = [];
            DstAreasToDelete = [];
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

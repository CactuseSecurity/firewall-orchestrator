using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Middleware.Client;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Data;
using System.Text.Json;


namespace FWO.Services
{
    public partial class ModellingConnectionHandler : ModellingHandlerBase
    {
        public List<FwoOwner> AllApps { get; set; } = [];
        public List<ModellingConnection> Connections { get; set; }
        public ModellingConnection ActConn { get; set; }

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

        public List<ModellingAppServer> SrcAppServerToAdd { get; set; } = [];
        public List<ModellingAppServer> SrcAppServerToDelete { get; set; } = [];
        public List<ModellingAppServer> DstAppServerToAdd { get; set; } = [];
        public List<ModellingAppServer> DstAppServerToDelete { get; set; } = [];
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
        public List<FwoOwner> PermittedOwnersToAdd { get; set; } = [];
        public List<FwoOwner> PermittedOwnersToDelete { get; set; } = [];
        public Func<Task> RefreshParent { get; set; }
        public ModellingAppRole DummyAppRole { get; set; } = new();
        public int LastWidth { get; set; } = GlobalConst.kGlobLibraryWidth;
        public bool LastCollapsed { get; set; } = false;
        public bool ActConnNeedsRefresh { get; set; } = true;

        private bool SrcFix = false;
        private bool DstFix = false;
        private ModellingConnection ActConnOrig { get; set; }
        private bool InitOngoing = false;
        private const string kConnection = "Connection";
        private const string kInterface = "Interface";
        private readonly string EditConnection = "edit_connection";
        private readonly string InitEnvironment = "init_environment";


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

        // Init + Refresh

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
                    await RefreshInterfaceData();
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
                    await RefreshInterfaceData();
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

        // ExtraConfig

        public void AddExtraConfig()
        {
            AddExtraConfigMode = true;
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

        // Visibility

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
            return SrcReadOnly || (ActConn.IsInterface && DstFilledInWork());
        }

        public bool DstDropForbidden()
        {
            return DstReadOnly || (ActConn.IsInterface && SrcFilledInWork());
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

        // Save

        public async Task<bool> Save(bool noCheck = false, bool decommInterface = false)
        {
            if (ActConn.IsCommonService && ComSvcContainsOnlyCommonNetworkArea())
            {
                DisplayMessageInUi(default, userConfig.GetText("edit_common_service"), userConfig.GetText("U9030"), true);
                return false;
            }

            try
            {
                if (ActConn.Sanitize())
                {
                    DisplayMessageInUi(null, userConfig.GetText("save_connection"), userConfig.GetText("U0001"), true);
                }
                if (noCheck || CheckConn())
                {
                    ActConn.SyncState(DummyAppRole.Id);
                    if (decommInterface)
                    {
                        await DecommInterface();
                    }
                    else
                    {
                        SyncChanges();
                        if (AddMode)
                        {
                            await AddConnectionToDb();
                        }
                        else
                        {
                            await UpdateConnection();
                        }
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
            if (ActConn.IsInterface && ActConn.IsPublished)
            {
                await UpdateStatusInterfaceUsersPublished();
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
            SyncPermittedOwnersChanges();
        }

        private void SyncPermittedOwnersChanges()
        {
            foreach (var owner in PermittedOwnersToDelete)
            {
                ActConn.PermittedOwners.RemoveAll(o => o.Id == owner.Id);
            }
            foreach (var owner in PermittedOwnersToAdd)
            {
                if (!ActConn.PermittedOwners.Any(o => o.Id == owner.Id))
                {
                    ActConn.PermittedOwners.Add(owner);
                }
            }
        }

        private async Task AddConnectionToDb(bool propose = false)
        {
            try
            {
                ActConn.AppId = propose ? null : Application.Id;
                ActConn.ProposedAppId = propose ? Application.Id : null;
                ActConn.IsPublished = !propose;

                var Variables = new
                {
                    name = ActConn.Name,
                    appId = ActConn.AppId,
                    proposedAppId = ActConn.ProposedAppId,
                    reason = ActConn.Reason,
                    isInterface = ActConn.IsInterface,
                    usedInterfaceId = ActConn.UsedInterfaceId,
                    isRequested = ActConn.IsRequested,
                    isPublished = ActConn.IsPublished,
                    ticketId = ActConn.TicketId,
                    creator = userConfig.User.Name,
                    commonSvc = ActConn.IsCommonService,
                    connProp = ActConn.Properties,
                    extraParams = ActConn.ExtraParams,
                    interfacePermission = ActConn.InterfacePermission
                };
                ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<ReturnIdWrapper>(ModellingQueries.newConnection, Variables)).ReturnIds;
                if (returnIds != null)
                {
                    ActConn.Id = returnIds[0].NewId;
                    await LogChange(ModellingTypes.ChangeType.Insert, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                        $"New {(ActConn.IsInterface ? kInterface : kConnection)}: {ActConn.Name}", ActConn.AppId);
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
                    await ApplyPermittedOwnersOnInsert();
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
                    extraParams = ActConn.ExtraParams,
                    interfacePermission = ActConn.InterfacePermission
                };
                await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.updateConnection, Variables);
                await LogChange(ModellingTypes.ChangeType.Update, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                    $"Updated {(ActConn.IsInterface ? kInterface : kConnection)}: {ActConn.Name}", Application.Id);

                await RemoveNwObjects(SrcAppServerToDelete, SrcAppRolesToDelete, SrcAreasToDelete, SrcNwGroupsToDelete, ModellingTypes.ConnectionField.Source);
                await AddNwObjects(SrcAppServerToAdd, SrcAppRolesToAdd, SrcAreasToAdd, SrcNwGroupsToAdd, ModellingTypes.ConnectionField.Source);
                await RemoveNwObjects(DstAppServerToDelete, DstAppRolesToDelete, DstAreasToDelete, DstNwGroupsToDelete, ModellingTypes.ConnectionField.Destination);
                await AddNwObjects(DstAppServerToAdd, DstAppRolesToAdd, DstAreasToAdd, DstNwGroupsToAdd, ModellingTypes.ConnectionField.Destination);
                await RemoveSvcObjects();
                await AddSvcObjects(SvcToAdd, SvcGrpToAdd);
                await EnsureUsingAppsPermittedIfRestricted();
                await ApplyPermittedOwnersOnUpdate();

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

        private async Task EnsureUsingAppsPermittedIfRestricted()
        {
            if (!ActConn.IsInterface || ActConn.InterfacePermission != InterfacePermissions.Restricted.ToString() ||
                ActConnOrig.InterfacePermission == InterfacePermissions.Restricted.ToString())
            {
                return;
            }

            HashSet<int> existingIds = ActConn.PermittedOwners.Select(o => o.Id).ToHashSet();
            HashSet<int> pendingIds = PermittedOwnersToAdd.Select(o => o.Id).ToHashSet();

            IEnumerable<int> usingAppIds = UsingConnections.Where(c => c.AppId != null).Select(c => c.AppId!.Value).Distinct();

            foreach (int appId in usingAppIds)
            {
                if (existingIds.Contains(appId) || pendingIds.Contains(appId))
                {
                    continue;
                }

                FwoOwner? owner = AllApps.FirstOrDefault(o => o.Id == appId);
                if (owner != null)
                {
                    PermittedOwnersToAdd.Add(owner);
                    ActConn.PermittedOwners.Add(owner);
                }
            }
        }

        private async Task ApplyPermittedOwnersOnInsert()
        {
            if (!ActConn.IsInterface || ActConn.InterfacePermission != InterfacePermissions.Restricted.ToString())
            {
                PermittedOwnersToAdd.Clear();
                PermittedOwnersToDelete.Clear();
                return;
            }
            await AddPermittedOwners(PermittedOwnersToAdd);
        }

        private async Task ApplyPermittedOwnersOnUpdate()
        {
            if (!ActConn.IsInterface || ActConn.InterfacePermission != InterfacePermissions.Restricted.ToString())
            {
                await RemoveAllPermittedOwners();
                ActConn.PermittedOwners.Clear();
                PermittedOwnersToAdd.Clear();
                PermittedOwnersToDelete.Clear();
                return;
            }
            await RemovePermittedOwners(PermittedOwnersToDelete);
            await AddPermittedOwners(PermittedOwnersToAdd);
        }

        private async Task AddPermittedOwners(List<FwoOwner> owners)
        {
            try
            {
                foreach (int ownerId in owners.Select(o => o.Id).Distinct().Where(id => id > 0))
                {
                    var variables = new { connectionId = ActConn.Id, appId = ownerId };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addPermittedOwner, variables);
                    FwoOwner? owner = owners.FirstOrDefault(o => o.Id == ownerId);
                    string ownerLabel = owner != null ? owner.Display(userConfig.GetText("common_service")) : ownerId.ToString();
                    await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                        $"Added permitted owner {ownerLabel} to {kInterface}: {ActConn.Name}", Application.Id);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText(EditConnection), "", true);
            }
        }

        private async Task RemovePermittedOwners(List<FwoOwner> owners)
        {
            try
            {
                foreach (int ownerId in owners.Select(o => o.Id).Distinct().Where(id => id > 0))
                {
                    var variables = new { connectionId = ActConn.Id, appId = ownerId };
                    await apiConnection.SendQueryAsync<FwoOwner>(ModellingQueries.deletePermittedOwner, variables);
                    FwoOwner? owner = owners.FirstOrDefault(o => o.Id == ownerId);
                    string ownerLabel = owner != null ? owner.Display(userConfig.GetText("common_service")) : ownerId.ToString();
                    await LogChange(ModellingTypes.ChangeType.Unassign, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                        $"Removed permitted owner {ownerLabel} from {kInterface}: {ActConn.Name}", Application.Id);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText(EditConnection), "", true);
            }
        }

        private async Task RemoveAllPermittedOwners()
        {
            List<FwoOwner> existing = await apiConnection.SendQueryAsync<List<FwoOwner>>(
                ModellingQueries.getPermittedOwnersForConnection,
                new { connectionId = ActConn.Id });
            await RemovePermittedOwners(existing);
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
            PermittedOwnersToAdd = [];
            PermittedOwnersToDelete = [];
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

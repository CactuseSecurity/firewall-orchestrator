using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Logging;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;


namespace FWO.Services.Modelling
{
    public record LogChangeRequest
    {
        public ModellingTypes.ChangeType ChangeType { get; init; }
        public ModellingTypes.ModObjectType ObjectType { get; init; }
        public long ObjectId { get; init; }
        public string Text { get; init; } = "";
        public ApiConnection ApiConnection { get; init; } = null!;
        public UserConfig UserConfig { get; init; } = null!;
        public int? ApplicationId { get; init; }
        public Action<Exception?, string, string, bool> DisplayMessageInUi { get; init; } = null!;
        public string? Requester { get; init; }
        public string ChangeSource { get; init; } = GlobalConst.kManual;
    }

    public class ModellingHandlerBase
    {
        public FwoOwner Application { get; set; } = new();
        public bool AddMode { get; set; } = false;
        protected readonly ApiConnection apiConnection;
        protected readonly UserConfig userConfig;
        protected Action<Exception?, string, string, bool> DisplayMessageInUi { get; set; }

        public List<ModellingAppServer> AvailableAppServers { get; set; } = [];
        public List<KeyValuePair<int, long>> AvailableNwElems { get; set; } = [];
        public List<ModellingService> AvailableServices { get; set; } = [];
        public List<KeyValuePair<int, int>> AvailableSvcElems { get; set; } = [];
        public List<ModellingConnection> UsingConnections { get; set; } = [];

        public bool ReadOnly { get; set; } = false;
        public bool IsOwner { get; set; } = true;
        public string Message { get; set; } = "";
        public bool DeleteAllowed { get; set; } = true;
        public List<ModellingService> SvcToAdd { get; set; } = [];
        private ModellingService actService = new();
        private const string DeactMsg = "C9001";

        public ModellingHandlerBase(ApiConnection apiConnection, UserConfig userConfig, FwoOwner application,
            bool addMode, Action<Exception?, string, string, bool> displayMessageInUi, bool readOnly, bool isOwner)
        {
            this.apiConnection = apiConnection;
            this.userConfig = userConfig;
            Application = application;
            AddMode = addMode;
            DisplayMessageInUi = displayMessageInUi;
            ReadOnly = readOnly;
            IsOwner = isOwner;
        }

        public ModellingHandlerBase(ApiConnection apiConnection, UserConfig userConfig, Action<Exception?, string, string, bool> displayMessageInUi, bool addMode = false)
        {
            this.apiConnection = apiConnection;
            this.userConfig = userConfig;
            this.DisplayMessageInUi = displayMessageInUi;
            this.AddMode = addMode;
        }

        public string DisplayApp(FwoOwner app)
        {
            return DisplayApp(userConfig, app);
        }

        public static string DisplayApp(UserConfig userConfig, FwoOwner app)
        {
            string tooltipEmptyApp = app.ConnectionCount.Aggregate.Count > 0 ? "" : $"data-toggle=\"tooltip\" title=\"{userConfig.GetText("C9004")}\"";
            string tooltip = app.Active ? tooltipEmptyApp : $"data-toggle=\"tooltip\" title=\"{userConfig.GetText("C9003")}\"";
            string textMarkerEmptyApp = app.ConnectionCount.Aggregate.Count > 0 ? "" : "*";
            string textToDisplay = (app.Active ? textMarkerEmptyApp : "!") + app.Display(userConfig.GetText("common_service"));
            string classEmptyApp = app.ConnectionCount.Aggregate.Count > 0 ? "" : "text-success";
            string textClass = app.Active ? classEmptyApp : "text-danger";
            return $"<span class=\"{textClass}\" {tooltip}>{(app.Active ? "" : "<i>")}{textToDisplay}{(app.Active ? "" : "</i>")}</span>";
        }

        public static string DisplayReqInt(UserConfig userConfig, long? ticketId, bool otherOwner, bool rejected = false)
        {
            string tooltipKey = "C9008";
            {
                if (rejected)
                {
                    tooltipKey = "C9011";
                }
                else if (otherOwner)
                {
                    tooltipKey = "C9007";
                }
            }
            string tooltip = $"data-toggle=\"tooltip\" title=\"{userConfig.GetText(tooltipKey)}\"";
            string content = $"{userConfig.GetText(rejected ? "InterfaceRejected" : "interface_requested")}: ({userConfig.GetText("ticket")} {ticketId?.ToString()})";
            return $"<span class=\"{(rejected ? "text-danger" : "text-warning")}\" {tooltip}><i>{content}</i></span>";
        }

        protected async Task LogChange(ModellingTypes.ChangeType changeType, ModellingTypes.ModObjectType objectType, long objId, string text, int? applicationId, string changeSource = GlobalConst.kManual)
        {
            await LogChange(new LogChangeRequest
            {
                ChangeType = changeType,
                ObjectType = objectType,
                ObjectId = objId,
                Text = text,
                ApiConnection = apiConnection,
                UserConfig = userConfig,
                ApplicationId = applicationId,
                DisplayMessageInUi = DisplayMessageInUi,
                ChangeSource = changeSource
            });
        }

        public static async Task LogChange(LogChangeRequest request)
        {
            try
            {
                var Variables = new
                {
                    appId = request.ApplicationId,
                    changeType = (int)request.ChangeType,
                    objectType = (int)request.ObjectType,
                    objectId = request.ObjectId,
                    changeText = request.Text,
                    changer = request.Requester ?? request.UserConfig.User.Name,
                    changeSource = request.ChangeSource
                };
                await request.ApiConnection.SendQueryAsync<ReturnIdWrapper>(ModellingQueries.addHistoryEntry, Variables);
            }
            catch (Exception exception)
            {
                request.DisplayMessageInUi(exception, request.UserConfig.GetText("log_change"), "", true);
            }
        }

        public async Task RequestDeleteServiceBase(ModellingService service)
        {
            actService = service;
            DeleteAllowed = !await CheckServiceIsInUse();
            Message = DeleteAllowed ? userConfig.GetText("U9003") + service.Name + "?" : userConfig.GetText("E9007") + service.Name;
        }

        private async Task<bool> CheckServiceIsInUse()
        {
            try
            {
                if (SvcToAdd.FirstOrDefault(s => s.Id == actService.Id) == null)
                {
                    List<ModellingServiceGroup> foundServiceGroups = await apiConnection.SendQueryAsync<List<ModellingServiceGroup>>(ModellingQueries.getServiceGroupIdsForService, new { serviceId = actService.Id });
                    if (foundServiceGroups.Count == 0)
                    {
                        List<ModellingConnection> foundConnections = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getConnectionIdsForService, new { serviceId = actService.Id });
                        if (foundConnections.Count == 0)
                        {
                            return false;
                        }
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

        public async Task<bool> DeleteService(List<ModellingService> availableServices, List<KeyValuePair<int, int>>? availableSvcElems = null)
        {
            try
            {
                if ((await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.deleteService, new { id = actService.Id })).AffectedRows > 0)
                {
                    await LogChange(ModellingTypes.ChangeType.Delete, ModellingTypes.ModObjectType.Service, actService.Id,
                        $"Deleted Service: {actService.Display()}", Application.Id);
                    availableServices.Remove(actService);
                    availableSvcElems?.Remove(availableSvcElems.FirstOrDefault(x => x.Key == (int)ModellingTypes.ModObjectType.Service && x.Value == actService.Id));
                    return false;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("delete_service"), "", true);
            }
            return true;
        }

        protected async Task<bool> DeleteConnection(ModellingConnection ConnToDelete, bool removeObjectLinks = false)
        {
            if (!IsOwner)
            {
                DisplayMessageInUi(null, userConfig.GetText("delete_connection"), userConfig.GetText("C9012"), true);
                return false;
            }

            try
            {
                if (ConnToDelete.RequestedOnFw || ConnToDelete.IsPublished)
                {
                    if ((await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.updateConnectionRemove, new { id = ConnToDelete.Id, removalDate = DateTime.Now })).UpdatedId == ConnToDelete.Id)
                    {
                        if (removeObjectLinks)
                        {
                            await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeAllNwGroupsFromConnection, new { id = ConnToDelete.Id });
                            await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeAllAppServersFromConnection, new { id = ConnToDelete.Id });
                            await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeAllServiceGroupsFromConnection, new { id = ConnToDelete.Id });
                            await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeAllServicesFromConnection, new { id = ConnToDelete.Id });
                        }
                        await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeSelectedConnection, new { connectionId = ConnToDelete.Id });
                        return true;
                    }
                }
                else
                {
                    return (await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.deleteConnection, new { id = ConnToDelete.Id })).DeletedId == ConnToDelete.Id;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("delete_connection"), "", true);
            }
            return false;
        }

        public async Task<string> ExtractUsedInterface(ModellingConnection conn)
        {
            string interfaceName = "";
            try
            {
                if (conn.UsedInterfaceId != null)
                {
                    List<ModellingConnection> interf = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getConnectionById, new { id = conn.UsedInterfaceId });
                    if (interf.Count > 0)
                    {
                        conn.SrcFromInterface = interf[0].SourceFilled();
                        conn.DstFromInterface = interf[0].DestinationFilled();
                        conn.InterfaceIsDecommissioned = interf[0].GetBoolProperty(ConState.Decommissioned.ToString());
                        conn.InterfaceNoPermission = EvaluateInterfaceNoPermission(interf[0], conn.AppId ?? Application.Id);
                        {
                            conn.InterfaceIsRequested = true;
                            conn.InterfaceIsRejected = interf[0].GetBoolProperty(ConState.Rejected.ToString());
                            conn.TicketId = interf[0].TicketId;
                        }
                        else
                        {
                            interfaceName = ExtractFullInterface(conn, interf[0]);
                        }
                        SetRelevantProps(conn, interf[0]);
                    }
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("fetch_data"), "", true);
            }
            return interfaceName;
        }

        private static string ExtractFullInterface(ModellingConnection conn, ModellingConnection interf)
        {
            if (interf.SourceFilled())
            {
                conn.SourceAppServers = interf.SourceAppServers;
                conn.SourceAppRoles = interf.SourceAppRoles;
                conn.SourceAreas = interf.SourceAreas;
                conn.SourceOtherGroups = interf.SourceOtherGroups;
            }
            if (interf.DestinationFilled())
            {
                conn.DestinationAppServers = interf.DestinationAppServers;
                conn.DestinationAppRoles = interf.DestinationAppRoles;
                conn.DestinationAreas = interf.DestinationAreas;
                conn.DestinationOtherGroups = interf.DestinationOtherGroups;
            }
            conn.Services = interf.Services;
            conn.ServiceGroups = interf.ServiceGroups;
            conn.ExtraConfigsFromInterface = interf.ExtraConfigs;
            return interf.Name ?? "";
        }

        protected static bool EvaluateInterfaceNoPermission(ModellingConnection interf, int ownerId)
        {
            return interf.InterfacePermission switch
            {
                nameof(InterfacePermissions.Private) => interf.AppId != ownerId,
                nameof(InterfacePermissions.Public) => false,
                _ => !interf.PermittedOwnerWrappers.Any(w => w.Owner != null && w.Owner.Id == ownerId),
            };
        }

        private static void SetRelevantProps(ModellingConnection conn, ModellingConnection interf)
        {
            if (interf.GetBoolProperty(ConState.Rejected.ToString()))
            {
                conn.AddProperty(ConState.InterfaceRejected.ToString());
            }
            else if (conn.InterfaceIsRequested)
            {
                conn.AddProperty(ConState.InterfaceRequested.ToString());
            }
            else if (conn.InterfaceIsDecommissioned)
            {
                conn.AddProperty(ConState.InterfaceDecommissioned.ToString());
            }
            else if (conn.InterfaceNoPermission)
            {
                conn.AddProperty(ConState.InterfaceNoPermission.ToString());
            }
        }

        public async Task<ModellingConnection?> GetUsedInterface(ModellingConnection conn)
        {
            try
            {
                if (conn.UsedInterfaceId != null)
                {
                    List<ModellingConnection> interf = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getConnectionById, new { id = conn.UsedInterfaceId });
                    if (interf.Count > 0)
                    {
                        return interf[0];
                    }
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("fetch_data"), "", true);
            }
            return null;
        }

        protected async Task<bool> InitUsingConnections(int connId)
        {
            try
            {
                UsingConnections = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getInterfaceUsers, new { id = connId });
                return UsingConnections.Count > 0;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("is_in_use"), "", true);
                return true;
            }
        }

        protected async Task<bool> CheckAppServerInUse(ModellingAppServer appServer)
        {
            try
            {
                List<ModellingAppRole> foundAppRoles = await apiConnection.SendQueryAsync<List<ModellingAppRole>>(ModellingQueries.getAppRolesForAppServer, new { id = appServer.Id });
                if (foundAppRoles.Count == 0)
                {
                    List<ModellingConnection> foundConnections = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getConnectionIdsForAppServer, new { id = appServer.Id });
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

        public static List<string> GetSrcNames(ModellingConnection conn, UserConfig userConfig)
        {
            if ((conn.InterfaceIsRequested && conn.SrcFromInterface) || (conn.IsRequested && conn.SourceFilled()))
            {
                return [DisplayReqInt(userConfig, conn.TicketId, conn.InterfaceIsRequested,
                    conn.GetBoolProperty(ConState.Rejected.ToString()) || conn.GetBoolProperty(ConState.InterfaceRejected.ToString()))];
            }

            List<ModellingNetworkArea> areas = [.. ModellingNetworkAreaWrapper.Resolve(conn.SourceAreas)];
            foreach (var area in areas)
            {
                area.TooltipText = userConfig.GetText(DeactMsg);
            }
            List<string> names = areas.ConvertAll(s => s.DisplayWithIcon(conn.SrcFromInterface, conn.InterfaceIsDecommissioned));

            List<ModellingNwGroup> nwGroups = [.. ModellingNwGroupWrapper.Resolve(conn.SourceOtherGroups)];
            foreach (var nwGroup in nwGroups)
            {
                nwGroup.TooltipText = userConfig.GetText(DeactMsg);
            }
            names.AddRange(nwGroups.ConvertAll(s => s.DisplayWithIcon(conn.SrcFromInterface, conn.InterfaceIsDecommissioned)));

            foreach (ModellingAppRole appRole in ModellingAppRoleWrapper.Resolve(conn.SourceAppRoles))
            {
                if (appRole.AppServers.Count > 0 && !appRole.AppServers.Any(_ => _.Content.IsDeleted))
                {
                    names.Add(appRole.DisplayWithIcon(conn.SrcFromInterface, conn.InterfaceIsDecommissioned));
                }
                else
                {
                    names.Add(appRole.DisplayProblematicWithIcon(conn.SrcFromInterface, conn.InterfaceIsDecommissioned));
                }
            }

            List<ModellingAppServer> appServers = [.. ModellingAppServerWrapper.Resolve(conn.SourceAppServers)];
            foreach (var appServer in appServers)
            {
                appServer.TooltipText = userConfig.GetText(DeactMsg);
            }
            names.AddRange(appServers.ConvertAll(s => s.DisplayWithIcon(conn.SrcFromInterface, conn.InterfaceIsDecommissioned)));
            return names;
        }

        public static List<string> GetDstNames(ModellingConnection conn, UserConfig userConfig)
        {
            if ((conn.InterfaceIsRequested && conn.DstFromInterface) || (conn.IsRequested && conn.DestinationFilled()))
            {
                return [DisplayReqInt(userConfig, conn.TicketId, conn.InterfaceIsRequested,
                    conn.GetBoolProperty(ConState.Rejected.ToString()) || conn.GetBoolProperty(ConState.InterfaceRejected.ToString()))];
            }

            List<ModellingNetworkArea> areas = [.. ModellingNetworkAreaWrapper.Resolve(conn.DestinationAreas)];
            foreach (var area in areas)
            {
                area.TooltipText = userConfig.GetText(DeactMsg);
            }
            List<string> names = areas.ConvertAll(s => s.DisplayWithIcon(conn.DstFromInterface, conn.InterfaceIsDecommissioned));

            List<ModellingNwGroup> nwGroups = [.. ModellingNwGroupWrapper.Resolve(conn.DestinationOtherGroups)];
            foreach (var nwGroup in nwGroups)
            {
                nwGroup.TooltipText = userConfig.GetText(DeactMsg);
            }
            names.AddRange(nwGroups.ConvertAll(s => s.DisplayWithIcon(conn.DstFromInterface, conn.InterfaceIsDecommissioned)));

            foreach (ModellingAppRole appRole in ModellingAppRoleWrapper.Resolve(conn.DestinationAppRoles))
            {
                if (appRole.AppServers.Count > 0 && !appRole.AppServers.Any(_ => _.Content.IsDeleted))
                {
                    names.Add(appRole.DisplayWithIcon(conn.DstFromInterface, conn.InterfaceIsDecommissioned));
                }
                else
                {
                    names.Add(appRole.DisplayProblematicWithIcon(conn.DstFromInterface, conn.InterfaceIsDecommissioned));
                }
            }

            List<ModellingAppServer> appServers = [.. ModellingAppServerWrapper.Resolve(conn.DestinationAppServers)];
            foreach (var appServer in appServers)
            {
                appServer.TooltipText = userConfig.GetText(DeactMsg);
            }
            names.AddRange(appServers.ConvertAll(s => s.DisplayWithIcon(conn.DstFromInterface, conn.InterfaceIsDecommissioned)));
            return names;
        }

        public static List<string> GetSvcNames(ModellingConnection conn, UserConfig userConfig)
        {
            if (conn.InterfaceIsRequested || conn.IsRequested)
            {
                return [DisplayReqInt(userConfig, conn.TicketId, conn.InterfaceIsRequested,
                    conn.GetBoolProperty(ConState.Rejected.ToString()) || conn.GetBoolProperty(ConState.InterfaceRejected.ToString()))];
            }

            List<string> names = [];

            foreach (ModellingServiceGroup svcGrp in ModellingServiceGroupWrapper.Resolve(conn.ServiceGroups))
            {
                if (svcGrp.Services.Count > 0)
                {
                    names.Add(svcGrp.DisplayWithIcon(conn.UsedInterfaceId != null, conn.InterfaceIsDecommissioned));
                }
                else
                {
                    names.Add(svcGrp.DisplayProblematicWithIcon(conn.UsedInterfaceId != null, conn.InterfaceIsDecommissioned));
                }
            }

            names.AddRange(ModellingServiceWrapper.Resolve(conn.Services).ToList().ConvertAll(s => s.DisplayWithIcon(conn.UsedInterfaceId != null, conn.InterfaceIsDecommissioned)));

            return names;
        }

        public static async Task<List<FwoOwner>> GetOwnApps(Task<AuthenticationState> authenticationStateTask, UserConfig userConfig,
            ApiConnection apiConnection, Action<Exception?, string, string, bool> DisplayMessageInUi, bool withConn = false)
        {
            List<FwoOwner> apps = [];
            try
            {
                if (authenticationStateTask!.Result.User.IsInRole(Roles.Admin)
                    || authenticationStateTask!.Result.User.IsInRole(Roles.Auditor)
                    || authenticationStateTask!.Result.User.IsInRole(Roles.ReporterViewAll))
                {
                    apps = await apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwnersWithConn);
                }
                else
                {
                    string? username = authenticationStateTask.Result.User.Identity?.Name;
                    bool usedClaimFallback = false;
                    // Prefer ownerships already prepared during login; only fall back to claim parsing if needed.
                    if (userConfig.User.Ownerships.Count == 0)
                    {
                        usedClaimFallback = true;
                        UpdateOwnerships(authenticationStateTask, userConfig);
                        if (userConfig.User.Ownerships.Count == 0)
                        {
                            // Mitigate timing issues where user config hydration lags behind page initialization.
                            await Task.Delay(100);
                            UpdateOwnerships(authenticationStateTask, userConfig);
                        }
                    }
                    Log.WriteDebug("GetOwnApps", $"User={username ?? "unknown"}, usedClaimFallback={usedClaimFallback}, ownershipCount={userConfig.User.Ownerships.Count}, withConn={withConn}");

                    string query = withConn ? OwnerQueries.getEditableOwnersWithConn : OwnerQueries.getEditableOwners;
                    apps = await apiConnection.SendQueryAsync<List<FwoOwner>>(query, new { appIds = userConfig.User.Ownerships.ToArray() });
                    Log.WriteDebug("GetOwnApps", $"User={username ?? "unknown"}, editableAppsCount={apps.Count}, withConn={withConn}");
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("fetch_data"), "", true);
            }
            return apps;
        }

        private static void UpdateOwnerships(Task<AuthenticationState> authenticationStateTask, UserConfig userConfig)
        {
            List<Claim> claims = authenticationStateTask.Result.User.Claims.ToList();
            userConfig.User.Ownerships = JwtClaimParser.ExtractIntClaimValues(claims, "x-hasura-editable-owners");
        }
    }
}

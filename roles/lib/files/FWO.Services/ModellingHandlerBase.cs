using FWO.Config.Api;
using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;


namespace FWO.Services
{
    public class ModellingHandlerBase
    {
        public FwoOwner Application { get; set; } = new();
        public bool AddMode { get; set; } = false;
        protected readonly ApiConnection apiConnection;
        protected readonly UserConfig userConfig;
        protected Action<Exception?, string, string, bool> DisplayMessageInUi { get; set; } = DefaultInit.DoNothing;

        public List<ModellingAppServer> AvailableAppServers { get; set; } = [];
        public List<KeyValuePair<int, long>> AvailableNwElems { get; set; } = [];
        public List<ModellingService> AvailableServices { get; set; } = [];
        public List<KeyValuePair<int, int>> AvailableSvcElems { get; set; } = [];

        public bool ReadOnly = false;
        public bool IsOwner { get; set; } = true;
        public string Message { get; set; } = "";
        public bool DeleteAllowed { get; set; } = true;
        public List<ModellingService> SvcToAdd { get; set; } = [];
        private ModellingService actService = new();

        public ModellingHandlerBase(ApiConnection apiConnection, UserConfig userConfig, FwoOwner application, 
            bool addMode, Action<Exception?, string, string, bool> displayMessageInUi, bool readOnly = false, bool isOwner = true)
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

        public MarkupString DisplayButton(string text, string icon, string iconText = "", string objIcon = "")
        {
            return DisplayButton(userConfig, text, icon, iconText, objIcon);
        }

        public static MarkupString DisplayButton(UserConfig userConfig, string text, string icon, string iconText = "", string objIcon = "")
        {
            string tooltip = userConfig.ModIconify ? $"data-toggle=\"tooltip\" title=\"{@userConfig.PureLine(text)}\"" : "";
            string iconToDisplay = $"<span class=\"{icon}\" {@tooltip}/>";
            string iconTextPart = iconText != "" ? " <span class=\"stdtext\">" + userConfig.GetText(iconText) + "</span>" : "";
            string objIconToDisplay = objIcon != "" ? $" <span class=\"{objIcon}\"/>" : "";
            return (MarkupString)(userConfig.ModIconify ? iconToDisplay + iconTextPart + objIconToDisplay : userConfig.GetText(text));
        }

        public string DisplayApp(FwoOwner app)
        {
            return DisplayApp(userConfig, app);
        }

        public static string DisplayApp(UserConfig userConfig, FwoOwner app)
        {
            string tooltip = app.Active ? (app.ConnectionCount.Aggregate.Count > 0 ? "" : $"data-toggle=\"tooltip\" title=\"{userConfig.GetText("C9004")}\"")
                : $"data-toggle=\"tooltip\" title=\"{userConfig.GetText("C9003")}\"";
            string textToDisplay = (app.Active ? (app.ConnectionCount.Aggregate.Count > 0 ? "" : "*") : "!") + app.Display(userConfig.GetText("common_service"));
            string textClass = app.Active ? (app.ConnectionCount.Aggregate.Count > 0 ? "" : "text-success") : "text-danger";
            return $"<span class=\"{textClass}\" {tooltip}>{(app.Active ? "" : "<i>")}{textToDisplay}{(app.Active ? "" : "</i>")}</span>";
        }

        public static string DisplayReqInt(UserConfig userConfig, long? ticketId, bool otherOwner, bool rejected = false)
        {
            string tooltipKey = rejected ? "C9011": otherOwner ? "C9007" : "C9008";
            string tooltip = $"data-toggle=\"tooltip\" title=\"{userConfig.GetText(tooltipKey)}\"";
            string content = $"{userConfig.GetText(rejected ? "InterfaceRejected" : "interface_requested")}: ({userConfig.GetText("ticket")} {ticketId?.ToString()})";
            return $"<span class=\"{(rejected ? "text-danger" : "text-warning")}\" {tooltip}><i>{content}</i></span>";
        }

        protected async Task LogChange(ModellingTypes.ChangeType changeType, ModellingTypes.ModObjectType objectType, long objId, string text, int? applicationId)
        {
            try
            {
                var Variables = new
                {
                    appId = applicationId,
                    changeType = (int)changeType,
                    objectType = (int)objectType,
                    objectId = objId,
                    changeText = text,
                    changer = userConfig.User.Name
                };
                await apiConnection.SendQueryAsync<ReturnIdWrapper>(ModellingQueries.addHistoryEntry, Variables);
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("log_change"), "", true);
            }
        }

        public static async Task LogChange(ModellingTypes.ChangeType changeType, ModellingTypes.ModObjectType objectType, long objId, string text,
            ApiConnection apiConnection, UserConfig userConfig, int? applicationId, Action<Exception?, string, string, bool> displayMessageInUi, string? requester = null)
        {
            try
            {
                var Variables = new
                {
                    appId = applicationId,
                    changeType = (int)changeType,
                    objectType = (int)objectType,
                    objectId = objId,
                    changeText = text,
                    changer = requester ?? userConfig.User.Name
                };
                await apiConnection.SendQueryAsync<ReturnIdWrapper>(ModellingQueries.addHistoryEntry, Variables);
            }
            catch (Exception exception)
            {
                displayMessageInUi(exception, userConfig.GetText("log_change"), "", true);
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
                if(SvcToAdd.FirstOrDefault(s => s.Id == actService.Id) == null)
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
                if((await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.deleteService, new { id = actService.Id })).AffectedRows > 0)
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

        public async Task<string> ExtractUsedInterface(ModellingConnection conn)
        {
            string interfaceName = "";
            try
            {
                if(conn.UsedInterfaceId != null)
                {
                    List<ModellingConnection> interf = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getConnectionById, new {id = conn.UsedInterfaceId});
                    if(interf.Count > 0)
                    {
                        conn.SrcFromInterface = interf[0].SourceFilled();
                        conn.DstFromInterface = interf[0].DestinationFilled();
                        if(interf[0].IsRequested)
                        {
                            conn.InterfaceIsRequested = true;
                            conn.InterfaceIsRejected = interf[0].GetBoolProperty(ConState.Rejected.ToString());
                            conn.TicketId = interf[0].TicketId;
                        }
                        else
                        {
                            interfaceName = interf[0].Name ?? "";
                            if(interf[0].SourceFilled())
                            {
                                conn.SourceAppServers = interf[0].SourceAppServers;
                                conn.SourceAppRoles = interf[0].SourceAppRoles;
                                conn.SourceAreas = interf[0].SourceAreas;
                                conn.SourceOtherGroups = interf[0].SourceOtherGroups;
                            }
                            if(interf[0].DestinationFilled())
                            {
                                conn.DestinationAppServers = interf[0].DestinationAppServers;
                                conn.DestinationAppRoles = interf[0].DestinationAppRoles;
                                conn.DestinationAreas = interf[0].DestinationAreas;
                                conn.DestinationOtherGroups = interf[0].DestinationOtherGroups;
                            }
                            conn.Services = interf[0].Services;
                            conn.ServiceGroups = interf[0].ServiceGroups;
                            conn.ExtraConfigsFromInterface = interf[0].ExtraConfigs;
                        }
                        if(interf[0].GetBoolProperty(ConState.Rejected.ToString()))
                        {
                            conn.AddProperty(ConState.InterfaceRejected.ToString());
                        }
                        else if(interf[0].GetBoolProperty(ConState.Requested.ToString()))
                        {
                            conn.AddProperty(ConState.InterfaceRequested.ToString());
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("fetch_data"), "", true);
            }
            return interfaceName;
        }

        public async Task<ModellingConnection?> GetUsedInterface(ModellingConnection conn)
        {
            try
            {
                if(conn.UsedInterfaceId != null)
                {
                    List<ModellingConnection> interf = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getConnectionById, new {id = conn.UsedInterfaceId});
                    if(interf.Count > 0)
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

        protected async Task<bool> CheckInterfaceInUse(ModellingConnection conn)
        {
            try
            {
                List<ModellingConnection> foundConnections = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getInterfaceUsers, new { id = conn.Id });
                if (foundConnections.Count == 0)
                {
                    return false;
                }
                return true;
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
            if((conn.InterfaceIsRequested && conn.SrcFromInterface) || (conn.IsRequested && conn.SourceFilled()))
            {
                return [DisplayReqInt(userConfig, conn.TicketId, conn.InterfaceIsRequested,
                    conn.GetBoolProperty(ConState.Rejected.ToString()) || conn.GetBoolProperty(ConState.InterfaceRejected.ToString()))];
            }

            List<ModellingNetworkArea> areas = [.. ModellingNetworkAreaWrapper.Resolve(conn.SourceAreas)];
            foreach(var area in areas)
            {
                area.TooltipText = userConfig.GetText("C9001");
            }
            List<string> names = areas.ConvertAll(s => s.DisplayWithIcon(conn.SrcFromInterface));

            List<ModellingNwGroup> nwGroups = [.. ModellingNwGroupWrapper.Resolve(conn.SourceOtherGroups)];
            foreach(var nwGroup in nwGroups)
            {
                nwGroup.TooltipText = userConfig.GetText("C9001");
            }
            names.AddRange(nwGroups.ConvertAll(s => s.DisplayWithIcon(conn.SrcFromInterface)));

            names.AddRange(ModellingAppRoleWrapper.Resolve(conn.SourceAppRoles).ToList().ConvertAll(s => s.DisplayWithIcon(conn.SrcFromInterface)));

            List<ModellingAppServer> appServers = [.. ModellingAppServerWrapper.Resolve(conn.SourceAppServers)];
            foreach(var appServer in appServers)
            {
                appServer.TooltipText = userConfig.GetText("C9001");
            }
            names.AddRange(appServers.ConvertAll(s => s.DisplayWithIcon(conn.SrcFromInterface)));
            return names;
        }

        public static List<string> GetDstNames(ModellingConnection conn, UserConfig userConfig)
        {
            if((conn.InterfaceIsRequested && conn.DstFromInterface) || (conn.IsRequested && conn.DestinationFilled()))
            {
                return [DisplayReqInt(userConfig, conn.TicketId, conn.InterfaceIsRequested, 
                    conn.GetBoolProperty(ConState.Rejected.ToString()) || conn.GetBoolProperty(ConState.InterfaceRejected.ToString()))];
            }

            List<ModellingNetworkArea> areas = [.. ModellingNetworkAreaWrapper.Resolve(conn.DestinationAreas)];
            foreach(var area in areas)
            {
                area.TooltipText = userConfig.GetText("C9001");
            }
            List<string> names = areas.ConvertAll(s => s.DisplayWithIcon(conn.DstFromInterface));

            List<ModellingNwGroup> nwGroups = [.. ModellingNwGroupWrapper.Resolve(conn.DestinationOtherGroups)];
            foreach(var nwGroup in nwGroups)
            {
                nwGroup.TooltipText = userConfig.GetText("C9001");
            }
            names.AddRange(nwGroups.ConvertAll(s => s.DisplayWithIcon(conn.DstFromInterface)));

            names.AddRange(ModellingAppRoleWrapper.Resolve(conn.DestinationAppRoles).ToList().ConvertAll(s => s.DisplayWithIcon(conn.DstFromInterface)));

            List<ModellingAppServer> appServers = [.. ModellingAppServerWrapper.Resolve(conn.DestinationAppServers)];
            foreach(var appServer in appServers)
            {
                appServer.TooltipText = userConfig.GetText("C9001");
            }
            names.AddRange(appServers.ConvertAll(s => s.DisplayWithIcon(conn.DstFromInterface)));
            return names;
        }

        public static List<string> GetSvcNames(ModellingConnection conn, UserConfig userConfig)
        {
            if(conn.InterfaceIsRequested || conn.IsRequested)
            {
                return [DisplayReqInt(userConfig, conn.TicketId, conn.InterfaceIsRequested, 
                    conn.GetBoolProperty(ConState.Rejected.ToString()) || conn.GetBoolProperty(ConState.InterfaceRejected.ToString()))];
            }
            List<string> names = ModellingServiceGroupWrapper.Resolve(conn.ServiceGroups).ToList().ConvertAll(s => s.DisplayWithIcon(conn.UsedInterfaceId != null));
            names.AddRange(ModellingServiceWrapper.Resolve(conn.Services).ToList().ConvertAll(s => s.DisplayWithIcon(conn.UsedInterfaceId != null)));
            return names;
        }

        public static async Task<List<FwoOwner>> GetOwnApps(Task<AuthenticationState> authenticationStateTask, UserConfig userConfig,
            ApiConnection apiConnection, Action<Exception?, string, string, bool> DisplayMessageInUi, bool withConn = false)
        {
            List<FwoOwner> apps = [];
            try
            {
                if(authenticationStateTask!.Result.User.IsInRole(Roles.Admin) || authenticationStateTask!.Result.User.IsInRole(Roles.Auditor))
                {
                    apps = await apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwnersWithConn);
                }
                else
                {
                    UpdateOwnerships(authenticationStateTask,userConfig); // qad: userConfig may not be properly filled
                    if(withConn)
                    {
                        apps = await apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getEditableOwnersWithConn, new { appIds = userConfig.User.Ownerships.ToArray() });
                    }
                    else
                    {
                        apps = await apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getEditableOwners, new { appIds = userConfig.User.Ownerships.ToArray() });
                    }
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
            string? ownerString = authenticationStateTask.Result.User.Claims.FirstOrDefault(claim => claim.Type == "x-hasura-editable-owners")?.Value;
            if(ownerString != null)
            {
                string[] separatingStrings = [",", "{", "}"];
                string[] owners = ownerString.Split(separatingStrings, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                userConfig.User.Ownerships = Array.ConvertAll(owners, x => int.Parse(x)).ToList();
            }
        }
    }
}

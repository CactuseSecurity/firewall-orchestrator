using FWO.Config.Api;
using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Services;


namespace FWO.Ui.Services
{
    public class ModellingAppHandler : ModellingHandlerBase
    {
        public ModellingConnectionHandler? connHandler;
        public ModellingConnectionHandler? overviewConnHandler;
        public List<ModellingConnection> Connections = [];
        public ModellingConnection ConnToDelete = new();
        public bool AddConnMode = false;
        public bool EditConnMode = false;
        public bool DeleteConnMode = false;

        public Shared.TabSet tabset = new();
        public Shared.Tab? actTab;
        public int ActWidth = 0;
        public bool StartCollapsed = true;


        public ModellingAppHandler(ApiConnection apiConnection, UserConfig userConfig, FwoOwner application, 
            Action<Exception?, string, string, bool> displayMessageInUi, bool isOwner = true)
            : base (apiConnection, userConfig, application, false, displayMessageInUi, false, isOwner)
        {}
        
        public async Task Init(List<ModellingConnection>? connections = null)
        {
            try
            {
                if(connections == null)
                {
                    var queryParam = new
                    {
                        appId = Application.Id
                    };
                    Connections = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getConnections, queryParam);
                }
                else
                {
                    Connections = connections;
                }
                
                foreach(var conn in Connections)
                {
                    conn.ExtractNwGroups();
                    await ExtractUsedInterface(conn);
                    conn.SyncState();
                }
                ConnToDelete = Connections.FirstOrDefault() ?? new ModellingConnection();
                overviewConnHandler = new ModellingConnectionHandler(apiConnection, userConfig, Application, Connections, new(), true,
                    false, DisplayMessageInUi, ReInit, IsOwner)
                {
                    LastWidth = ActWidth,
                    LastCollapsed = StartCollapsed || ActWidth == 0
                };
                 
                await overviewConnHandler.Init();
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("fetch_data"), "", true);
            }
        }

        public async Task ReInit()
        {
            await Init();
        }

        public void InitActiveTab(ModellingConnection? conn = null)
        {
            int tab = 0;
            if(conn != null)
            {
                tab = GetTabFromConn(conn);
            }
            else if(GetRegularConnections().Count == 0)
            {
                if (GetInterfaces().Count > 0)
                {
                    tab = 1;
                }
                else if (Application.CommSvcPossible && GetCommonServices().Count > 0)
                {
                    tab = 2;
                }
            }
            tabset.SetActiveTab(tab);
        }

        public void RestoreTab(ModellingConnection? conn = null)
        {
            if(conn != null)
            {
                tabset.SetActiveTab(GetTabFromConn(conn));
            }
            else if(tabset.Tabs.Count > 0 && actTab != null)
            {
                Shared.Tab? tab = tabset.Tabs.FirstOrDefault(x => x.Position == actTab.Position);
                if(tab != null)
                {
                    tabset.SetActiveTab(tab);
                }
            }
        }

        private static int GetTabFromConn(ModellingConnection conn)
        {
            if(conn.IsInterface)
            {
                return 1;
            }
            if (conn.IsCommonService)
            {
                return 2;
            }
            return 0;
        }

        public List<ModellingConnection> GetInterfaces(bool showRejected = false)
        {
            List<ModellingConnection> tmpList = Connections.Where(x => x.IsInterface && (showRejected || !x.GetBoolProperty(ConState.Rejected.ToString()))).ToList();
            tmpList.Sort((ModellingConnection a, ModellingConnection b) => a.CompareTo(b));
            return tmpList;
        }

        public List<ModellingConnection> GetCommonServices()
        {
            return Connections.Where(x => !x.IsInterface && x.IsCommonService).ToList();
        }

        public List<ModellingConnection> GetRegularConnections(bool excludeRequestedAndEmptyARs = false)
        {
            return Connections.Where(x => !x.IsInterface && !x.IsCommonService && (!excludeRequestedAndEmptyARs || 
                !(x.GetBoolProperty(ConState.InterfaceRequested.ToString()) || x.GetBoolProperty(ConState.InterfaceRejected.ToString()) || x.EmptyAppRolesFound()))).ToList();
        }

        public List<string> GetSrcNames(ModellingConnection conn)
        {
            if((conn.InterfaceIsRequested && conn.SrcFromInterface) || (conn.IsRequested && conn.SourceFilled()))
            {
                return [DisplayReqInt(userConfig, conn.TicketId, conn.InterfaceIsRequested,
                    conn.GetBoolProperty(ConState.Rejected.ToString()) || conn.GetBoolProperty(ConState.InterfaceRejected.ToString()))];
            }

            List<ModellingNwGroup> nwGroups = ModellingNwGroupWrapper.Resolve(conn.SourceNwGroups).ToList();
            foreach(var nwGroup in nwGroups)
            {
                nwGroup.TooltipText = userConfig.GetText("C9001");
            }
            List<string> names = nwGroups.ConvertAll(s => s.DisplayWithIcon(conn.SrcFromInterface));

            names.AddRange(ModellingAppRoleWrapper.Resolve(conn.SourceAppRoles).ToList().ConvertAll(s => s.DisplayWithIcon(conn.SrcFromInterface)));

            List<ModellingAppServer> appServers = ModellingAppServerWrapper.Resolve(conn.SourceAppServers).ToList();
            foreach(var appServer in appServers)
            {
                appServer.TooltipText = userConfig.GetText("C9001");
            }
            names.AddRange(appServers.ConvertAll(s => s.DisplayWithIcon(conn.SrcFromInterface)));
            return names;
        }
        
        public List<string> GetDstNames(ModellingConnection conn)
        {
            if((conn.InterfaceIsRequested && conn.DstFromInterface) || (conn.IsRequested && conn.DestinationFilled()))
            {
                return [DisplayReqInt(userConfig, conn.TicketId, conn.InterfaceIsRequested, 
                    conn.GetBoolProperty(ConState.Rejected.ToString()) || conn.GetBoolProperty(ConState.InterfaceRejected.ToString()))];
            }
            List<ModellingNwGroup> nwGroups = ModellingNwGroupWrapper.Resolve(conn.DestinationNwGroups).ToList();
            foreach(var nwGroup in nwGroups)
            {
                nwGroup.TooltipText = userConfig.GetText("C9001");
            }
            List<string> names = nwGroups.ConvertAll(s => s.DisplayWithIcon(conn.DstFromInterface));

            names.AddRange(ModellingAppRoleWrapper.Resolve(conn.DestinationAppRoles).ToList().ConvertAll(s => s.DisplayWithIcon(conn.DstFromInterface)));

            List<ModellingAppServer> appServers = ModellingAppServerWrapper.Resolve(conn.DestinationAppServers).ToList();
            foreach(var appServer in appServers)
            {
                appServer.TooltipText = userConfig.GetText("C9001");
            }
            names.AddRange(appServers.ConvertAll(s => s.DisplayWithIcon(conn.DstFromInterface)));
            return names;
        }

        public List<string> GetSvcNames(ModellingConnection conn)
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

        public async Task AddConnection()
        {
            ReadOnly = false;
            AddConnMode = true;
            await HandleConn(new ModellingConnection() { AppId = Application.Id });
        }

        public async Task AddInterface()
        {
            ReadOnly = false;
            AddConnMode = true;
            await HandleConn(new ModellingConnection(){ AppId = Application.Id, IsInterface = true });
        }

        public async Task AddCommonService()
        {
            ReadOnly = false;
            AddConnMode = true;
            await HandleConn(new ModellingConnection(){ AppId = Application.Id, IsCommonService = true });
        }

        public async Task ShowDetails(ModellingConnection conn)
        {
            ReadOnly = true;
            AddConnMode = false;
            await HandleConn(conn);
        }

        public async Task EditConn(ModellingConnection conn)
        {
            ReadOnly = false;
            AddConnMode = false;
            await HandleConn(conn);
        }

        public async Task HandleConn(ModellingConnection conn)
        {
            actTab = tabset.ActiveTab;
            connHandler = new ModellingConnectionHandler(apiConnection, userConfig, Application, Connections, conn, AddConnMode, 
                ReadOnly, DisplayMessageInUi, ReInit, IsOwner);
            await connHandler.Init();
            EditConnMode = true;
        }

        public async Task RequestDeleteConnection(ModellingConnection conn)
        {
            actTab = tabset.ActiveTab;
            ConnToDelete = conn;
            if(ConnToDelete.IsInterface)
            {
                if(await CheckInterfaceInUse(ConnToDelete))
                {
                    Message = userConfig.GetText("E9013") + ConnToDelete.Name;
                    DeleteAllowed = false;
                }
                else
                {
                    Message = userConfig.GetText("U9014") + ConnToDelete.Name + "?";
                    DeleteAllowed = true;
                }
            }
            else
            {
                Message = userConfig.GetText("U9001") + ConnToDelete.Name + "?";
                DeleteAllowed = true;
            }
            DeleteConnMode = true;
        }

        public async Task DeleteConnection()
        {
            try
            {
                if((await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.deleteConnection, new { id = ConnToDelete.Id })).DeletedId == ConnToDelete.Id)
                {
                    await LogChange(ModellingTypes.ChangeType.Delete, ModellingTypes.ModObjectType.Connection, ConnToDelete.Id,
                        $"Deleted {(ConnToDelete.IsInterface? "Interface" : "Connection")}: {ConnToDelete.Name}", Application.Id);
                    Connections.Remove(ConnToDelete);
                    DeleteConnMode = false;
                    RestoreTab();
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("delete_connection"), "", true);
            }
        }
    }
}

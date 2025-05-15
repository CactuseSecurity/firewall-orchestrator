using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
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
        private long dummyAppRoleId = 0;


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
                    Connections = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getConnectionsResolved, queryParam);
                }
                else
                {
                    Connections = connections;
                }
                
                List<ModellingAppRole> dummyAppRoles = await apiConnection.SendQueryAsync<List<ModellingAppRole>>(ModellingQueries.getDummyAppRole);
                if (dummyAppRoles.Count > 0)
                {
                    dummyAppRoleId = dummyAppRoles.First().Id;
                }

                await PrepareConnections(Connections);

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

        private async Task PrepareConnections(List<ModellingConnection> connections)
        {
            if(userConfig.VarianceAnalysisSync)
            {
                await AnalyseStatus(connections);
             }

            foreach(var conn in connections)
            {
                await ExtractUsedInterface(conn);
                conn.SyncState(dummyAppRoleId);
            }
        }

        public async Task AnalyseStatus(List<ModellingConnection> connections)
        {
            ExtStateHandler extStateHandler = new(apiConnection);
            ModellingVarianceAnalysis varianceAnalysis = new(apiConnection, extStateHandler, userConfig, Application, DisplayMessageInUi);
            await varianceAnalysis.AnalyseConnsForStatus([.. connections.Where(x => !x.IsDocumentationOnly())]);
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

        public List<ModellingConnection> GetRegularConnections()
        {
            return Connections.Where(x => !x.IsInterface && !x.IsCommonService).ToList();
        }

        public List<ModellingConnection> GetConnectionsToRequest()
        {
            return [.. Connections.Where(x => !(x.IsInterface ||
                x.GetBoolProperty(ConState.InterfaceRequested.ToString()) ||
                x.GetBoolProperty(ConState.InterfaceRejected.ToString()) || 
                x.EmptyAppRolesFound(dummyAppRoleId) ||
                x.DeletedObjectsFound() ||
                x.EmptyServiceGroupsFound()
                )).OrderByDescending(y => y.IsCommonService)];
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

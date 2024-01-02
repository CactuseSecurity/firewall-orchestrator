using FWO.Config.Api;
using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;


namespace FWO.Ui.Services
{
    public class ModellingAppHandler : ModellingHandlerBase
    {
        public ModellingConnectionHandler? connHandler;
        public List<ModellingConnection> Connections = new();
        public ModellingConnection actConn = new();
        public bool AddConnMode = false;
        public bool EditConnMode = false;
        public bool DeleteConnMode = false;

        public bool readOnly = false;
    

        public ModellingAppHandler(ApiConnection apiConnection, UserConfig userConfig, FwoOwner application, 
            Action<Exception?, string, string, bool> displayMessageInUi, bool isOwner = true)
            : base (apiConnection, userConfig, application, false, displayMessageInUi, isOwner)
        {}
        
        public async Task Init()
        {
            try
            {
                var queryParam = new
                {
                    appId = Application.Id
                };
                Connections = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getConnections, queryParam);
                foreach(var conn in Connections)
                {
                    conn.ExtractNwGroups();
                    await ExtractUsedInterface(conn);
                }
                actConn = Connections.FirstOrDefault() ?? new ModellingConnection();
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("fetch_data"), "", true);
            }
        }

        public List<string> GetSrcNames(ModellingConnection conn)
        {
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
            List<string> names = ModellingServiceGroupWrapper.Resolve(conn.ServiceGroups).ToList().ConvertAll(s => s.DisplayWithIcon(conn.UsedInterfaceId != null));
            names.AddRange(ModellingServiceWrapper.Resolve(conn.Services).ToList().ConvertAll(s => s.DisplayWithIcon(conn.UsedInterfaceId != null)));
            return names;
        }

        public async Task AddConn()
        {
            readOnly = false;
            AddConnMode = true;
            await HandleConn(new ModellingConnection() { AppId = Application.Id });
        }

        public async Task AddInterface()
        {
            readOnly = false;
            AddConnMode = true;
            await HandleConn(new ModellingConnection(){ AppId = Application.Id, IsInterface = true });
        }

        public async Task ShowDetails(ModellingConnection conn)
        {
            readOnly = true;
            AddConnMode = false;
            await HandleConn(conn);
        }

        public async Task EditConn(ModellingConnection conn)
        {
            readOnly = false;
            AddConnMode = false;
            await HandleConn(conn);
        }

        public async Task HandleConn(ModellingConnection conn)
        {
            connHandler = new ModellingConnectionHandler(apiConnection, userConfig, Application, Connections, conn, AddConnMode, readOnly, DisplayMessageInUi, IsOwner);
            await connHandler.Init();
            EditConnMode = true;
        }

        public void RequestDeleteConnection(ModellingConnection conn)
        {
            actConn = conn;
            Message = userConfig.GetText("U9001") + actConn.Name + "?";
            DeleteConnMode = true;
        }

        public async Task DeleteConnection()
        {
            try
            {
                if((await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.deleteConnection, new { id = actConn.Id })).AffectedRows > 0)
                {
                    await LogChange(ModellingTypes.ChangeType.Delete, ModellingTypes.ObjectType.Connection, actConn.Id,
                        $"Deleted {(actConn.IsInterface? "Interface" : "Connection")}: {actConn.Name}", Application.Id);
                    Connections.Remove(actConn);
                    DeleteConnMode = false;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("delete_connection"), "", true);
            }
        }
    }
}

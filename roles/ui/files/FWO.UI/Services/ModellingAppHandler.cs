using FWO.Config.Api;
using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;


namespace FWO.Ui.Services
{
    public class ModellingAppHandler : ModellingHandlerBase
    {
        public ModellingConnectionHandler connHandler;
        public List<ModellingConnection> coproOfApp = new();
        public ModellingConnection actConn = new();
        public bool AddConnMode = false;
        public bool EditConnMode = false;
        public bool DeleteConnMode = false;

        public bool readOnly = false;
        public string Message = "";


        public ModellingAppHandler(ApiConnection apiConnection, UserConfig userConfig, FwoOwner application, 
            Action<Exception?, string, string, bool> displayMessageInUi)
            : base (apiConnection, userConfig, application, false, displayMessageInUi)
        {}
        
        public async Task Init()
        {
            try
            {
                var queryParam = new
                {
                    appId = Application.Id
                };
                coproOfApp = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getConnections, queryParam);
                actConn = coproOfApp.FirstOrDefault() ?? new ModellingConnection();
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("fetch_data"), "", true);
            }
        }

        public List<string> GetSrcNames(ModellingConnection conn)
        {
            List<string> names = ModellingNwGroupObjectWrapper.Resolve(conn.SourceNwGroups).ToList().ConvertAll(s => s.DisplayWithIcon());
            names.AddRange(ModellingAppRoleWrapper.Resolve(conn.SourceAppRoles).ToList().ConvertAll(s => s.DisplayWithIcon()));
            names.AddRange(ModellingAppServerWrapper.Resolve(conn.SourceAppServers).ToList().ConvertAll(s => s.DisplayWithIcon()));
            return names;
        }
        
        public List<string> GetDstNames(ModellingConnection conn)
        {
            List<string> names = ModellingNwGroupObjectWrapper.Resolve(conn.DestinationNwGroups).ToList().ConvertAll(s => s.DisplayWithIcon());
            names.AddRange(ModellingAppRoleWrapper.Resolve(conn.DestinationAppRoles).ToList().ConvertAll(s => s.DisplayWithIcon()));
            names.AddRange(ModellingAppServerWrapper.Resolve(conn.DestinationAppServers).ToList().ConvertAll(s => s.DisplayWithIcon()));
            return names;
        }

        public List<string> GetSvcNames(ModellingConnection conn)
        {
            List<string> names = ModellingServiceGroupWrapper.Resolve(conn.ServiceGroups).ToList().ConvertAll(s => s.DisplayWithIcon());
            names.AddRange(ModellingServiceWrapper.Resolve(conn.Services).ToList().ConvertAll(s => s.DisplayWithIcon()));
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
            connHandler = new ModellingConnectionHandler(apiConnection, userConfig, Application, coproOfApp, conn, AddConnMode, readOnly, DisplayMessageInUi);
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
                    coproOfApp.Remove(actConn);
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

using NetTools;
using FWO.Config.Api;
using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;


namespace FWO.Ui.Services
{
    public class ModellingAppServerHandler
    {
        public FwoOwner Application { get; set; } = new();
        public ModellingAppServer ActAppServer { get; set; } = new();
        public List<ModellingAppServer> AvailableAppServers { get; set; } = new();
        public bool AddMode { get; set; } = false;
        private readonly ApiConnection ApiConnection;
        private readonly UserConfig userConfig;
        private Action<Exception?, string, string, bool> DisplayMessageInUi { get; set; } = DefaultInit.DoNothing;


        public ModellingAppServerHandler(ApiConnection apiConnection, UserConfig userConfig, FwoOwner application, 
            ModellingAppServer appServer, List<ModellingAppServer> availableAppServers, bool addMode, Action<Exception?, string, string, bool> displayMessageInUi)
        {
            ApiConnection = apiConnection;
            this.userConfig = userConfig;
            Application = application;
            ActAppServer = appServer;
            AvailableAppServers = availableAppServers;
            AddMode = addMode;
            DisplayMessageInUi = displayMessageInUi;
        }
        
        public async Task<bool> Save()
        {
            try
            {
                if (ActAppServer.Sanitize())
                {
                    DisplayMessageInUi(null, userConfig.GetText("save_app_server"), userConfig.GetText("U0001"), true);
                }
                if(CheckAppServer())
                {
                    if(AddMode)
                    {
                        await AddAppServerToDb();
                    }
                    else
                    {
                        await UpdateAppServerInDb();
                    }
                    return true;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("edit_app_server"), "", true);
            }
            return false;
        }

        private bool CheckAppServer()
        {
            if(ActAppServer.Name == null || ActAppServer.Name == "")
            {
                DisplayMessageInUi(null, userConfig.GetText("edit_app_server"), userConfig.GetText("E5102"), true);
                return false;
            }
            return true;
        }

        private async Task AddAppServerToDb()
        {
            try
            {
                var Variables = new
                {
                    name = ActAppServer.Name,
                    appId = Application.Id,
                    ip = IPAddressRange.Parse(ActAppServer.Ip).ToCidrString(),   // todo ?
                    importSource = GlobalConfig.kManual  // todo
                };
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<NewReturning>(ModellingQueries.newAppServer, Variables)).ReturnIds;
                if (returnIds != null)
                {
                    ActAppServer.Id = returnIds[0].NewId;
                    AvailableAppServers.Add(ActAppServer);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("add_app_server"), "", true);
            }
        }

        private async Task UpdateAppServerInDb()
        {
            try
            {
                var Variables = new
                {
                    id = ActAppServer.Id,
                    name = ActAppServer.Name,
                    appId = Application.Id,
                    ip = IPAddressRange.Parse(ActAppServer.Ip).ToCidrString(),   // todo ?
                    importSource = GlobalConfig.kManual  // todo
                };
                await ApiConnection.SendQueryAsync<ReturnId>(ModellingQueries.updateAppServer, Variables);
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("edit_app_server"), "", true);
            }
        }

        public static async Task<bool> DeleteAppServer(ModellingAppServer appServer, List<ModellingAppServer> AvailableAppServers, ApiConnection apiConnection)
        {
            if(await checkAlreadyUsed(appServer, apiConnection))
            {
                await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.markAppServerDeleted, new { id = appServer.Id });
                appServer.IsDeleted = true;
                return false;
            }
            else if((await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.deleteAppServer, new { id = appServer.Id })).AffectedRows > 0)
            {
                AvailableAppServers.Remove(appServer);
                return false;
            }
            return true;
        }

        private static async Task<bool> checkAlreadyUsed(ModellingAppServer appServer, ApiConnection apiConnection)
        {
            List<ModellingAppRole> foundAppRoles = await apiConnection.SendQueryAsync<List<ModellingAppRole>>(ModellingQueries.getAppRolesForAppServer, new { id = appServer.Id });
            if (foundAppRoles.Count == 0)
            {
                List<ModellingConnection> foundConnections = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getConnectionsForAppServer, new { id = appServer.Id });
                if (foundConnections.Count == 0)
                {
                    return false;
                }
            }
            return true;
        }
    }
}

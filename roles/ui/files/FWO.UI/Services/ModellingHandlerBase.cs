using FWO.Config.Api;
using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;


namespace FWO.Ui.Services
{
    public class ModellingHandlerBase
    {
        public FwoOwner Application { get; set; } = new();
        public bool AddMode { get; set; } = false;
        protected readonly ApiConnection apiConnection;
        protected readonly UserConfig userConfig;
        protected Action<Exception?, string, string, bool> DisplayMessageInUi { get; set; } = DefaultInit.DoNothing;


        public ModellingHandlerBase(ApiConnection apiConnection, UserConfig userConfig, FwoOwner application, 
            bool addMode, Action<Exception?, string, string, bool> displayMessageInUi)
        {
            this.apiConnection = apiConnection;
            this.userConfig = userConfig;
            Application = application;
            AddMode = addMode;
            DisplayMessageInUi = displayMessageInUi;
        }
        
        protected async Task LogChange(ModellingTypes.ChangeType changeType, ModellingTypes.ObjectType objectType, long objId, string text)
        {
            try
            {
                var Variables = new
                {
                    appId = Application.Id,
                    changeType = (int)changeType,
                    objectType = (int)objectType,
                    objectId = objId,
                    changeText = text,
                    changer = userConfig.User.Name
                };
                await apiConnection.SendQueryAsync<NewReturning>(ModellingQueries.addHistoryEntry, Variables);
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("log_change"), "", true);
            }
        }

        public async Task<bool> DeleteAppServer(ModellingAppServer appServer, List<ModellingAppServer> AvailableAppServers)
        {
            if(await CheckAlreadyUsed(appServer))
            {
                await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.markAppServerDeleted, new { id = appServer.Id });
                await LogChange(ModellingTypes.ChangeType.MarkDeleted, ModellingTypes.ObjectType.AppServer, appServer.Id, $"Mark App Server as deleted: {appServer.Name}");
                appServer.IsDeleted = true;
                return false;
            }
            else if((await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.deleteAppServer, new { id = appServer.Id })).AffectedRows > 0)
            {
                await LogChange(ModellingTypes.ChangeType.Delete, ModellingTypes.ObjectType.AppServer, appServer.Id, $"Deleted App Server: {appServer.Name}");
                AvailableAppServers.Remove(appServer);
                return false;
            }
            return true;
        }

        private async Task<bool> CheckAlreadyUsed(ModellingAppServer appServer)
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

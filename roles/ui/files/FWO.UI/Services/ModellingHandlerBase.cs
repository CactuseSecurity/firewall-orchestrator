using FWO.Config.Api;
using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Ui.Display;


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
        
        protected async Task LogChange(ModellingTypes.ChangeType changeType, ModellingTypes.ObjectType objectType, long objId, string text, int? applicationId)
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
                await apiConnection.SendQueryAsync<NewReturning>(ModellingQueries.addHistoryEntry, Variables);
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("log_change"), "", true);
            }
        }

        public static async Task LogChange(ModellingTypes.ChangeType changeType, ModellingTypes.ObjectType objectType, long objId, string text,
            ApiConnection apiConnection, UserConfig userConfig, int? applicationId, Action<Exception?, string, string, bool> displayMessageInUi)
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
                await apiConnection.SendQueryAsync<NewReturning>(ModellingQueries.addHistoryEntry, Variables);
            }
            catch (Exception exception)
            {
                displayMessageInUi(exception, userConfig.GetText("log_change"), "", true);
            }
        }

        public async Task<bool> DeleteAppServer(ModellingAppServer appServer, List<ModellingAppServer> availableAppServers, List<KeyValuePair<int, long>>? availableNwElems = null)
        {
            if(await CheckAppServerInUse(appServer))
            {
                await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.setAppServerDeletedState, new { id = appServer.Id, deleted = true });
                await LogChange(ModellingTypes.ChangeType.MarkDeleted, ModellingTypes.ObjectType.AppServer, appServer.Id,
                    $"Mark App Server as deleted: {appServer.Display()}", Application.Id);
                appServer.IsDeleted = true;
                return false;
            }
            else if((await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.deleteAppServer, new { id = appServer.Id })).AffectedRows > 0)
            {
                await LogChange(ModellingTypes.ChangeType.Delete, ModellingTypes.ObjectType.AppServer, appServer.Id,
                    $"Deleted App Server: {appServer.Display()}", Application.Id);
                availableAppServers.Remove(appServer);
                availableNwElems?.Remove(availableNwElems.FirstOrDefault(x => x.Key == (int)ModellingTypes.ObjectType.AppServer && x.Value == appServer.Id));
                return false;
            }
            return true;
        }

        public async Task<bool> ReactivateAppServer(ModellingAppServer appServer)
        {
            if(appServer.IsDeleted)
            {
                await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.setAppServerDeletedState, new { id = appServer.Id, deleted = false });
                await LogChange(ModellingTypes.ChangeType.Reactivate, ModellingTypes.ObjectType.AppServer, appServer.Id,
                    $"Reactivate App Server: {appServer.Display()}", Application.Id);
                appServer.IsDeleted = false;
                return false;
            }
            return true;
        }

        private async Task<bool> CheckAppServerInUse(ModellingAppServer appServer)
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

        public async Task<bool> DeleteService(ModellingService service, List<ModellingService> availableServices, List<KeyValuePair<int, int>>? availableSvcElems = null)
        {
            try
            {
                if((await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.deleteService, new { id = service.Id })).AffectedRows > 0)
                {
                    await LogChange(ModellingTypes.ChangeType.Delete, ModellingTypes.ObjectType.Service, service.Id,
                        $"Deleted Service: {service.Display()}", Application.Id);
                    availableServices.Remove(service);
                    availableSvcElems?.Remove(availableSvcElems.FirstOrDefault(x => x.Key == (int)ModellingTypes.ObjectType.Service && x.Value == service.Id));
                    return false;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("delete_service"), "", true);
            }
            return true;
        }
    }
}

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

        public string Message { get; set; } = "";
        public bool DeleteAllowed { get; set; } = true;
        public List<ModellingService> SvcToAdd { get; set; } = new();
        private ModellingService actService = new();

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
                    await LogChange(ModellingTypes.ChangeType.Delete, ModellingTypes.ObjectType.Service, actService.Id,
                        $"Deleted Service: {actService.Display()}", Application.Id);
                    availableServices.Remove(actService);
                    availableSvcElems?.Remove(availableSvcElems.FirstOrDefault(x => x.Key == (int)ModellingTypes.ObjectType.Service && x.Value == actService.Id));
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
                    List<ModellingConnection> interf = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getInterfaceById, new {intId = conn.UsedInterfaceId});
                    if(interf.Count > 0)
                    {
                        interfaceName = interf[0].Name ?? "";
                        if(interf[0].SourceFilled())
                        {
                            conn.SrcFromInterface = true;
                            conn.SourceAppServers = interf[0].SourceAppServers;
                            conn.SourceAppRoles = interf[0].SourceAppRoles;
                            conn.SourceNwGroups = interf[0].SourceNwGroups;
                        }
                        if(interf[0].DestinationFilled())
                        {
                            conn.DstFromInterface = true;
                            conn.DestinationAppServers = interf[0].DestinationAppServers;
                            conn.DestinationAppRoles = interf[0].DestinationAppRoles;
                            conn.DestinationNwGroups = interf[0].DestinationNwGroups;
                        }
                        conn.Services = interf[0].Services;
                        conn.ServiceGroups = interf[0].ServiceGroups;
                    }  
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("fetch_data"), "", true);
            }
            return interfaceName;
        }
    }
}

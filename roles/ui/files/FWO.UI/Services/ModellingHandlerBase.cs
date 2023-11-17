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
                        }
                        if(interf[0].DestinationFilled())
                        {
                            conn.DstFromInterface = true;
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
    }
}

using FWO.Config.Api;
using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Logging;


namespace FWO.Ui.Services
{
    public class ModellingAppRoleHandler
    {
        public FwoOwner Application { get; set; } = new();
        public List<AppRole> AppRoles { get; set; } = new();
        public AppRole ActAppRole { get; set; } = new();
        public List<NetworkObject> AvailableAppServer { get; set; } = new();
        public bool AddMode { get; set; } = false;

        public List<NetworkObject> AppServerToAdd { get; set; } = new();
        public List<NetworkObject> AppServerToDelete { get; set; } = new();

        private readonly ApiConnection ApiConnection;
        private readonly UserConfig userConfig;
        private Action<Exception?, string, string, bool> DisplayMessageInUi { get; set; } = DefaultInit.DoNothing;



        private readonly ApiConnection apiConnection;


        public ModellingAppRoleHandler(ApiConnection apiConnection)
        {
            this.apiConnection = apiConnection;
        }
        public ModellingAppRoleHandler(ApiConnection apiConnection, UserConfig userConfig, FwoOwner application, 
            List<AppRole> appRoles, AppRole appRole, List<NetworkObject> availableAppServer,
            bool addMode, Action<Exception?, string, string, bool> displayMessageInUi)
        {
            ApiConnection = apiConnection;
            this.userConfig = userConfig;
            Application = application;
            AppRoles = appRoles;
            ActAppRole = appRole;
            AvailableAppServer = availableAppServer;
            AddMode = addMode;
            DisplayMessageInUi = displayMessageInUi;
        }

        public void AppServerToAppRole(List<NetworkObject> nwObjects)
        {
        foreach(var appServer in nwObjects)
        {
            if(!ActAppRole.NetworkObjects.Contains(appServer) && !AppServerToAdd.Contains(appServer))
            {
                AppServerToAdd.Add(appServer);
            }
        }
        }

        public async Task Save()
        {
            foreach(var appServer in AppServerToDelete)
            {
                ActAppRole.NetworkObjects.Remove(appServer);
            }
            foreach(var appServer in AppServerToAdd)
            {
                ActAppRole.NetworkObjects.Add(appServer);
            }
            if(AddMode)
            {
                await AddAppRoleToDb();
            }
            else
            {
                await UpdateAppRoleInDb();
            }
            Close();
        }

        public async Task AddAppRoleToDb()
        {
            try
            {
                var Variables = new
                {
                    name = ActAppRole.Name,
                    appId = Application.Id,
                    comment = ActAppRole.Comment
                };
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<NewReturning>(FWO.Api.Client.Queries.ModellingQueries.newAppRole, Variables)).ReturnIds;
                if (returnIds != null)
                {
                    ActAppRole.Id = returnIds[0].NewId;
                    foreach(var appServer in ActAppRole.NetworkObjects)
                    {
                        var Vars = new
                        {
                            appServerId = appServer.Id,
                            appRoleId = ActAppRole.Id
                        };
                        await ApiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.ModellingQueries.addAppServerToAppRole, Vars);
                    }
                    AppRoles.Add(ActAppRole);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("add_app_role"), "", true);
            }
        }

        public async Task UpdateAppRoleInDb()
        {
            try
            {
                var Variables = new
                {
                    id = ActAppRole.Id,
                    name = ActAppRole.Name,
                    appId = Application.Id,
                    comment = ActAppRole.Comment
                };
                await ApiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.ModellingQueries.updateAppRole, Variables);
                foreach(var appServer in AppServerToDelete)
                {
                    var Vars = new
                    {
                        appServerId = appServer.Id,
                        appRoleId = ActAppRole.Id
                    };
                    await ApiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.ModellingQueries.removeAppServerFromAppRole, Vars);
                }
                foreach(var appServer in AppServerToAdd)
                {
                    var Vars = new
                    {
                        appServerId = appServer.Id,
                        appRoleId = ActAppRole.Id
                    };
                    await ApiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.ModellingQueries.addAppServerToAppRole, Vars);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("edit_app_role"), "", true);
            }
        }

        public void Close()
        {
            AppServerToAdd = new List<NetworkObject>();
            AppServerToDelete = new List<NetworkObject>();
        }
    }
}

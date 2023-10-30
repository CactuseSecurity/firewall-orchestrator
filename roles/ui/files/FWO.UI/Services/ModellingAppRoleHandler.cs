using FWO.Config.Api;
using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;


namespace FWO.Ui.Services
{
    public class ModellingAppRoleHandler
    {
        public FwoOwner Application { get; set; } = new();
        public List<ModellingAppRole> AppRoles { get; set; } = new();
        public ModellingAppRole ActAppRole { get; set; } = new();
        public List<ModellingAppServer> AvailableAppServers { get; set; } = new();
        public bool AddMode { get; set; } = false;

        public ModellingAppServerHandler AppServerHandler;
        public List<ModellingAppServer> AppServerToAdd { get; set; } = new();
        public List<ModellingAppServer> AppServerToDelete { get; set; } = new();
        public bool AddAppServerMode = false;
        public bool EditAppServerMode = false;
        public bool DeleteAppServerMode = false;
        public string deleteMessage = "";
        private ModellingAppServer actAppServer = new();

        private readonly ApiConnection ApiConnection;
        private readonly UserConfig userConfig;
        private Action<Exception?, string, string, bool> DisplayMessageInUi { get; set; } = DefaultInit.DoNothing;


        public ModellingAppRoleHandler(ApiConnection apiConnection, UserConfig userConfig, FwoOwner application, 
            List<ModellingAppRole> appRoles, ModellingAppRole appRole, List<ModellingAppServer> availableAppServers,
            bool addMode, Action<Exception?, string, string, bool> displayMessageInUi)
        {
            ApiConnection = apiConnection;
            this.userConfig = userConfig;
            Application = application;
            AppRoles = appRoles;
            ActAppRole = appRole;
            AvailableAppServers = availableAppServers;
            AddMode = addMode;
            DisplayMessageInUi = displayMessageInUi;
        }

        public void AppServerToAppRole(List<ModellingAppServer> appServers)
        {
            foreach(var appServer in appServers)
            {
                if(!appServer.IsDeleted && ActAppRole.AppServers.FirstOrDefault(w => w.Content.Id == appServer.Id) == null && !AppServerToAdd.Contains(appServer))
                {
                    AppServerToAdd.Add(appServer);
                }
            }
        }

        public async Task CreateAppServer()
        {
            AddAppServerMode = true;
            await HandleAppServer(new ModellingAppServer(){ ImportSource = GlobalConfig.kManual });
        }

        public async Task EditAppServer(ModellingAppServer appServer)
        {
            AddAppServerMode = false;
            await HandleAppServer(appServer);
        }

        public async Task HandleAppServer(ModellingAppServer appServer)
        {
            AppServerHandler = new ModellingAppServerHandler(ApiConnection, userConfig, Application, appServer, AvailableAppServers, AddAppServerMode, DisplayMessageInUi);
            EditAppServerMode = true;
        }

        public void RequestDeleteAppServer(ModellingAppServer appServer)
        {
            actAppServer = appServer;
            deleteMessage = userConfig.GetText("U9003") + appServer.Name + "?";
            DeleteAppServerMode = true;
        }

        public async Task DeleteAppServer()
        {
            try
            {
                DeleteAppServerMode = await ModellingAppServerHandler.DeleteAppServer(actAppServer, AvailableAppServers, ApiConnection);
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("delete_app_server"), "", true);
            }
        }

        public async Task Save()
        {
            foreach(var appServer in AppServerToDelete)
            {
                ActAppRole.AppServers.Remove(ActAppRole.AppServers.FirstOrDefault(x => x.Content.Id == appServer.Id));
            }
            foreach(var appServer in AppServerToAdd)
            {
                ActAppRole.AppServers.Add(new ModellingAppServerWrapper(){ Content = appServer });
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
                ReturnId[]? returnIds = (await ApiConnection.SendQueryAsync<NewReturning>(ModellingQueries.newAppRole, Variables)).ReturnIds;
                if (returnIds != null)
                {
                    ActAppRole.Id = returnIds[0].NewId;
                    foreach(var appServer in ActAppRole.AppServers)
                    {
                        var Vars = new
                        {
                            appServerId = appServer.Content.Id,
                            appRoleId = ActAppRole.Id
                        };
                        await ApiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addAppServerToAppRole, Vars);
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
                await ApiConnection.SendQueryAsync<ReturnId>(ModellingQueries.updateAppRole, Variables);
                foreach(var appServer in AppServerToDelete)
                {
                    var Vars = new
                    {
                        appServerId = appServer.Id,
                        appRoleId = ActAppRole.Id
                    };
                    await ApiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeAppServerFromAppRole, Vars);
                }
                foreach(var appServer in AppServerToAdd)
                {
                    var Vars = new
                    {
                        appServerId = appServer.Id,
                        appRoleId = ActAppRole.Id
                    };
                    await ApiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addAppServerToAppRole, Vars);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("edit_app_role"), "", true);
            }
        }

        public void Close()
        {
            AppServerToAdd = new List<ModellingAppServer>();
            AppServerToDelete = new List<ModellingAppServer>();
        }
    }
}

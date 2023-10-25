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
        public List<NetworkObject> AppServer { get; set; } = new();
        public bool AddMode { get; set; } = false;

        public List<NetworkObject> IpsToAdd { get; set; } = new();
        public List<NetworkObject> IpsToDelete { get; set; } = new();

        private readonly ApiConnection ApiConnection;
        private readonly UserConfig userConfig;
        private Action<Exception?, string, string, bool> DisplayMessageInUi { get; set; } = DefaultInit.DoNothing;



        private readonly ApiConnection apiConnection;


        public ModellingAppRoleHandler(ApiConnection apiConnection)
        {
            this.apiConnection = apiConnection;
        }
        public ModellingAppRoleHandler(ApiConnection apiConnection, UserConfig userConfig, FwoOwner application, 
            List<AppRole> appRoles, AppRole appRole,
            bool addMode, Action<Exception?, string, string, bool> displayMessageInUi)
        {
            ApiConnection = apiConnection;
            this.userConfig = userConfig;
            Application = application;
            AppRoles = appRoles;
            ActAppRole = appRole;
            AddMode = addMode;
            DisplayMessageInUi = displayMessageInUi;
        }
        public async Task Init()
        {
            try
            {
                AppServer = await ApiConnection.SendQueryAsync<List<NetworkObject>>(FWO.Api.Client.Queries.ModellingQueries.getAppServer, new { appId = Application.Id });
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("fetch_data"), "", true);
            }
        }

        public void AppServerToAppRole(List<NetworkObject> nwObjects)
        {
        foreach(var nwobj in nwObjects)
        {
            if(!ActAppRole.NetworkObjects.Contains(nwobj) && !IpsToAdd.Contains(nwobj))
            {
                IpsToAdd.Add(nwobj);
            }
        }
        }

        public async Task Save()
        {
            foreach(var nwobj in IpsToDelete)
            {
                ActAppRole.NetworkObjects.Remove(nwobj);
            }
            foreach(var nwobj in IpsToAdd)
            {
                ActAppRole.NetworkObjects.Add(nwobj);
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
                    foreach(var nwobj in ActAppRole.NetworkObjects)
                    {
                        var Vars = new
                        {
                            appServerId = nwobj.Id,
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
                foreach(var nwobj in IpsToDelete)
                {
                    var Vars = new
                    {
                        appServerId = nwobj.Id,
                        appRoleId = ActAppRole.Id
                    };
                    await ApiConnection.SendQueryAsync<ReturnId>(FWO.Api.Client.Queries.ModellingQueries.removeAppServerFromAppRole, Vars);
                }
                foreach(var nwobj in IpsToAdd)
                {
                    var Vars = new
                    {
                        appServerId = nwobj.Id,
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
            IpsToAdd = new List<NetworkObject>();
            IpsToDelete = new List<NetworkObject>();
        }
    }
}

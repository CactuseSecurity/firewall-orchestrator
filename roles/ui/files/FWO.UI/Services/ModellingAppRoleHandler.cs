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
        private string origId = "";

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
            AvailableAppServers = availableAppServers;
            AddMode = addMode;
            DisplayMessageInUi = displayMessageInUi;
            ActAppRole = appRole;
            if(!AddMode)
            {
                ActAppRole.Area = new () { Name = ReconstructArea(appRole) };
            }
            origId = ActAppRole.IdString;
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

        public void CreateAppServer()
        {
            AddAppServerMode = true;
            HandleAppServer(new ModellingAppServer(){ ImportSource = GlobalConfig.kManual });
        }

        public void EditAppServer(ModellingAppServer appServer)
        {
            AddAppServerMode = false;
            HandleAppServer(appServer);
        }

        public void HandleAppServer(ModellingAppServer appServer)
        {
            try
            {
                AppServerHandler = new ModellingAppServerHandler(ApiConnection, userConfig, Application, appServer, AvailableAppServers, AddAppServerMode, DisplayMessageInUi);
                EditAppServerMode = true;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("edit_app_role"), "", true);
            }
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

        public async Task<bool> Save()
        {
            try
            {
                if (ActAppRole.Sanitize())
                {
                    DisplayMessageInUi(null, userConfig.GetText("save_app_role"), userConfig.GetText("U0001"), true);
                }
                if(CheckAppRole())
                {
                    foreach(var appServer in AppServerToDelete)
                    {
                        ActAppRole.AppServers.Remove(ActAppRole.AppServers.FirstOrDefault(x => x.Content.Id == appServer.Id) ?? throw new Exception("Did not find app server."));
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
                    return true;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("edit_app_role"), "", true);
            }
            return false;
        }

        private bool CheckAppRole()
        {
            if(ActAppRole.IdString.Length <= ModellingAppRole.FixedPartLength || ActAppRole.Name == "")
            {
                DisplayMessageInUi(null, userConfig.GetText("edit_app_role"), userConfig.GetText("E5102"), true);
                return false;
            }
            if((AddMode || ActAppRole.IdString != origId) && IdStringAlreadyUsed(ActAppRole.IdString))
            {
                // popup for correction?
                DisplayMessageInUi(null, userConfig.GetText("edit_app_role"), userConfig.GetText("E9003"), true);
                return false;
            }
            return true;
        }

        private bool IdStringAlreadyUsed(string appRoleIdString)
        {
            return AppRoles.FirstOrDefault(x => x.IdString == appRoleIdString) != null;
        }

        public string ProposeFreeAppRoleNumber(ModellingNetworkArea area)
        {
            int maxNumbers = 99999;
            string idFix = GetFixedAppRolePart(area);
            ModellingAppRole? newestAR = AppRoles.Where(x => x.IdStringFixedPart == idFix).MaxBy(x => x.Id);
            if(newestAR != null)
            {
                if(Int32.TryParse(newestAR.IdStringFreePart, out int aRNumber))
                {
                    aRNumber++;
                    while(aRNumber <= maxNumbers)
                    {
                        if(!IdStringAlreadyUsed(idFix + aRNumber.ToString("D5")))
                        {
                            return aRNumber.ToString("D5");
                        }
                        aRNumber++;
                    }
                }
                aRNumber = 1;
                while(aRNumber <= maxNumbers)
                {
                    if(!IdStringAlreadyUsed(idFix + aRNumber.ToString("D5")))
                    {
                        return aRNumber.ToString("D5");
                    }
                    aRNumber++;
                }
            }
            return "00001";
        }

        public string GetFixedAppRolePart(ModellingNetworkArea area)
        {
            if(area.Name.Length >= ModellingAppRole.FixedPartLength)
            {
                return area.Name.Substring(0, ModellingAppRole.FixedPartLength).Remove(0, 2).Insert(0, "AR");
            }
            return area.Name;
        }

        public static string ReconstructArea(ModellingAppRole appRole)
        {
            if(appRole.IdString.Length >= ModellingAppRole.FixedPartLength)
            {
                return appRole.IdString.Substring(0, ModellingAppRole.FixedPartLength).Remove(0, 2).Insert(0, "NA");
            }
            return "";
        }

        private async Task AddAppRoleToDb()
        {
            try
            {
                var Variables = new
                {
                    name = ActAppRole.Name,
                    idString = ActAppRole.IdString,
                    appId = Application.Id,
                    comment = ActAppRole.Comment,
                    creator = userConfig.User.Name
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

        private async Task UpdateAppRoleInDb()
        {
            try
            {
                var Variables = new
                {
                    id = ActAppRole.Id,
                    name = ActAppRole.Name,
                    idString = ActAppRole.IdString,
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

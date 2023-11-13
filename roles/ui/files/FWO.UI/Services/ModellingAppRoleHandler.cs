using FWO.Config.Api;
using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Ui.Display;

namespace FWO.Ui.Services
{
    public class ModellingAppRoleHandler : ModellingHandlerBase
    {
        public List<ModellingAppRole> AppRoles { get; set; } = new();
        public ModellingAppRole ActAppRole { get; set; } = new();
        public List<ModellingAppServer> AvailableAppServers { get; set; } = new();
        public List<KeyValuePair<int, long>> AvailableNwElems { get; set; } = new();

        public ModellingAppServerHandler AppServerHandler;
        public List<ModellingAppServer> AppServerToAdd { get; set; } = new();
        public List<ModellingAppServer> AppServerToDelete { get; set; } = new();
        public bool AddAppServerMode = false;
        public bool EditAppServerMode = false;
        public bool DeleteAppServerMode = false;
        public bool ReactivateAppServerMode = false;
        public string Message = "";
        private ModellingAppServer actAppServer = new();
        private string origId = "";


        public ModellingAppRoleHandler(ApiConnection apiConnection, UserConfig userConfig, FwoOwner application, 
            List<ModellingAppRole> appRoles, ModellingAppRole appRole, List<ModellingAppServer> availableAppServers,
            List<KeyValuePair<int, long>> availableNwElems, bool addMode, Action<Exception?, string, string, bool> displayMessageInUi)
            : base (apiConnection, userConfig, application, addMode, displayMessageInUi)
        {
            AppRoles = appRoles;
            AvailableAppServers = availableAppServers;
            AvailableNwElems = availableNwElems;
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
                AppServerHandler = new ModellingAppServerHandler(apiConnection, userConfig, Application, appServer, AvailableAppServers, AddAppServerMode, DisplayMessageInUi);
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
            Message = userConfig.GetText("U9003") + appServer.Name + "?";
            DeleteAppServerMode = true;
        }

        public async Task DeleteAppServer()
        {
            try
            {
                DeleteAppServerMode = await DeleteAppServer(actAppServer, AvailableAppServers);
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("delete_app_server"), "", true);
            }
        }

        public void RequestReactivateAppServer(ModellingAppServer appServer)
        {
            actAppServer = appServer;
            Message = userConfig.GetText("U9005") + appServer.Name + "?";
            ReactivateAppServerMode = true;
        }

        public async Task ReactivateAppServer()
        {
            try
            {
                ReactivateAppServerMode = await ReactivateAppServer(actAppServer);
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
                if(int.TryParse(newestAR.IdStringFreePart, out int aRNumber))
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
            // Todo: parametrize in settings
            if(ModellingAppRole.FixedPartLength >= 2 && area.Name.Length >= ModellingAppRole.FixedPartLength)
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
                ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<NewReturning>(ModellingQueries.newAppRole, Variables)).ReturnIds;
                if (returnIds != null)
                {
                    ActAppRole.Id = returnIds[0].NewId;
                    await LogChange(ModellingTypes.ChangeType.Insert, ModellingTypes.ObjectType.AppRole, ActAppRole.Id,
                        $"New App Role: {ActAppRole.Display()}", Application.Id);
                    foreach(var appServer in ActAppRole.AppServers)
                    {
                        var Vars = new
                        {
                            appServerId = appServer.Content.Id,
                            appRoleId = ActAppRole.Id
                        };
                        await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addAppServerToAppRole, Vars);
                        await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ObjectType.AppRole, ActAppRole.Id,
                            $"Added App Server {appServer.Content.Display()} to App Role: {ActAppRole.Display()}", Application.Id);
                    }
                    AppRoles.Add(ActAppRole);
                    AvailableNwElems.Add(new KeyValuePair<int, long>((int)ModellingTypes.ObjectType.AppRole, ActAppRole.Id));
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
                await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.updateAppRole, Variables);
                await LogChange(ModellingTypes.ChangeType.Update, ModellingTypes.ObjectType.AppRole, ActAppRole.Id,
                    $"Updated App Role: {ActAppRole.Display()}", Application.Id);
                foreach(var appServer in AppServerToDelete)
                {
                    var Vars = new
                    {
                        appServerId = appServer.Id,
                        appRoleId = ActAppRole.Id
                    };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeAppServerFromAppRole, Vars);
                    await LogChange(ModellingTypes.ChangeType.Unassign, ModellingTypes.ObjectType.AppRole, ActAppRole.Id,
                        $"Removed App Server {appServer.Display()} from App Role: {ActAppRole.Display()}", Application.Id);
                }
                foreach(var appServer in AppServerToAdd)
                {
                    var Vars = new
                    {
                        appServerId = appServer.Id,
                        appRoleId = ActAppRole.Id
                    };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addAppServerToAppRole, Vars);
                    await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ObjectType.AppRole, ActAppRole.Id,
                        $"Added App Server {appServer.Display()} to App Role: {ActAppRole.Display()}", Application.Id);
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

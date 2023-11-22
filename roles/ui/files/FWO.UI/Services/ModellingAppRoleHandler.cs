using FWO.Config.Api;
using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using System.Text.Json;

namespace FWO.Ui.Services
{
    public class ModellingAppRoleHandler : ModellingHandlerBase
    {
        public List<ModellingAppRole> AppRoles { get; set; } = new();
        public ModellingAppRole ActAppRole { get; set; } = new();
        public List<ModellingAppServer> AvailableAppServers { get; set; } = new();
        public List<KeyValuePair<int, long>> AvailableNwElems { get; set; } = new();

        public List<ModellingAppServer> AppServerToAdd { get; set; } = new();
        public List<ModellingAppServer> AppServerToDelete { get; set; } = new();
        public string Message = "";
        private readonly string origId = "";
        public ModellingNamingConvention NamingConvention = new();


        public ModellingAppRoleHandler(ApiConnection apiConnection, UserConfig userConfig, FwoOwner application, 
            List<ModellingAppRole> appRoles, ModellingAppRole appRole, List<ModellingAppServer> availableAppServers,
            List<KeyValuePair<int, long>> availableNwElems, bool addMode, Action<Exception?, string, string, bool> displayMessageInUi)
            : base (apiConnection, userConfig, application, addMode, displayMessageInUi)
        {
            NamingConvention = JsonSerializer.Deserialize<ModellingNamingConvention>(userConfig.ModNamingConvention) ?? new();
            AppRoles = appRoles;
            foreach(var aR in AppRoles)
            {
                aR.SetFixedPartLength(NamingConvention.FixedPartLength);
            }
            AvailableAppServers = availableAppServers;
            AvailableNwElems = availableNwElems;
            ActAppRole = appRole;
            ActAppRole.SetFixedPartLength(NamingConvention.FixedPartLength);
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

        public async Task<bool> Save()
        {
            try
            {
                if (ActAppRole.Sanitize())
                {
                    DisplayMessageInUi(null, userConfig.GetText("save_app_role"), userConfig.GetText("U0001"), true);
                }
                if(await CheckAppRole())
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

        private async Task<bool> CheckAppRole()
        {
            if(ActAppRole.IdString.Length <= NamingConvention.FixedPartLength || ActAppRole.Name == "")
            {
                DisplayMessageInUi(null, userConfig.GetText("edit_app_role"), userConfig.GetText("E5102"), true);
                return false;
            }
            if((AddMode || ActAppRole.IdString != origId) && await IdStringAlreadyUsed(ActAppRole.IdString))
            {
                // popup for correction?
                DisplayMessageInUi(null, userConfig.GetText("edit_app_role"), userConfig.GetText("E9003"), true);
                return false;
            }
            return true;
        }

        private async Task<bool> IdStringAlreadyUsed(string appRoleIdString)
        {
            List<ModellingAppRole>? existAR = await apiConnection.SendQueryAsync<List<ModellingAppRole>>(ModellingQueries.getNewestAppRoles, new { pattern = appRoleIdString });
            if(existAR != null)
            {
                foreach(var aR in existAR)
                {
                    if(aR.Id  != ActAppRole.Id)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public async Task<string> ProposeFreeAppRoleNumber(ModellingNetworkArea area)
        {
            int maxNumbers = 10^NamingConvention.FreePartLength - 1;
            string idFix = GetFixedAppRolePart(area);
            ModellingAppRole? newestAR = (await apiConnection.SendQueryAsync<List<ModellingAppRole>>(ModellingQueries.getNewestAppRoles, new { pattern = idFix + "%" }))[0];
            if(newestAR != null)
            {
                newestAR.SetFixedPartLength(NamingConvention.FixedPartLength);
                if(int.TryParse(newestAR.IdStringFreePart, out int aRNumber))
                {
                    aRNumber++;
                    while(aRNumber <= maxNumbers)
                    {
                        if(!await IdStringAlreadyUsed(idFix + aRNumber.ToString($"D{NamingConvention.FreePartLength}")))
                        {
                            return aRNumber.ToString($"D{NamingConvention.FreePartLength}");
                        }
                        aRNumber++;
                    }
                }
                aRNumber = 1;
                while(aRNumber <= maxNumbers)
                {
                    if(!await IdStringAlreadyUsed(idFix + aRNumber.ToString($"D{NamingConvention.FreePartLength}")))
                    {
                        return aRNumber.ToString($"D{NamingConvention.FreePartLength}");
                    }
                    aRNumber++;
                }
            }
            return 1.ToString($"D{NamingConvention.FreePartLength}");
        }

        public string GetFixedAppRolePart(ModellingNetworkArea area)
        {
            if(area.Name.Length >= NamingConvention.FixedPartLength)
            {
                return area.Name.Substring(0, NamingConvention.FixedPartLength).Remove(0, NamingConvention.NetworkAreaPattern.Length).Insert(0, NamingConvention.AppRolePattern);
            }
            return area.Name;
        }

        public string ReconstructArea(ModellingAppRole appRole)
        {
            if(appRole.IdString.Length >= NamingConvention.FixedPartLength)
            {
                return appRole.IdString.Substring(0, NamingConvention.FixedPartLength).Remove(0, NamingConvention.AppRolePattern.Length).Insert(0, NamingConvention.NetworkAreaPattern);
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
                            nwObjectId = appServer.Content.Id,
                            nwGroupId = ActAppRole.Id
                        };
                        await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addNwObjectToNwGroup, Vars);
                        await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ObjectType.AppRole, ActAppRole.Id,
                            $"Added App Server {appServer.Content.Display()} to App Role: {ActAppRole.Display()}", Application.Id);
                    }
                    ActAppRole.Creator = userConfig.User.Name;
                    ActAppRole.CreationDate = DateTime.Now;
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
                        nwObjectId = appServer.Id,
                        nwGroupId = ActAppRole.Id
                    };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeNwObjectFromNwGroup, Vars);
                    await LogChange(ModellingTypes.ChangeType.Unassign, ModellingTypes.ObjectType.AppRole, ActAppRole.Id,
                        $"Removed App Server {appServer.Display()} from App Role: {ActAppRole.Display()}", Application.Id);
                }
                foreach(var appServer in AppServerToAdd)
                {
                    var Vars = new
                    {
                        nwObjectId = appServer.Id,
                        nwGroupId = ActAppRole.Id
                    };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addNwObjectToNwGroup, Vars);
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

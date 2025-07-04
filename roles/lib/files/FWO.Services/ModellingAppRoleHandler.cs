using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Basics;
using FWO.Config.Api;
using NetTools;
using System.Net;
using System.Text.Json;

namespace FWO.Services
{
    public class ModellingAppRoleHandler : ModellingHandlerBase
    {
        public List<ModellingAppRole> AppRoles { get; set; } = [];
        public ModellingAppRole ActAppRole { get; set; } = new();
        public List<ModellingAppServer> AppServersInArea { get; set; } = [];
        public List<ModellingAppServer> AppServerToAdd { get; set; } = [];
        public List<ModellingAppServer> AppServerToDelete { get; set; } = [];
        public ModellingNamingConvention NamingConvention = new();

        private ModellingManagedIdString OrigId = new();

        public ModellingAppRoleHandler(ApiConnection apiConnection, UserConfig userConfig, FwoOwner application,
            List<ModellingAppRole> appRoles, ModellingAppRole appRole, List<ModellingAppServer> availableAppServers,
            List<KeyValuePair<int, long>> availableNwElems, bool addMode, Action<Exception?, string, string, bool> displayMessageInUi, bool isOwner = true, bool readOnly = false)
            : base(apiConnection, userConfig, application, addMode, displayMessageInUi, readOnly, isOwner)
        {
            AppRoles = appRoles;
            AvailableAppServers = availableAppServers;
            AvailableNwElems = availableNwElems;
            ActAppRole = appRole;
            ApplyNamingConvention(application.ExtAppId ?? "");
        }

        private void ApplyNamingConvention(string extAppId)
        {
            NamingConvention = JsonSerializer.Deserialize<ModellingNamingConvention>(userConfig.ModNamingConvention) ?? new();
            foreach (ModellingAppRole aR in AppRoles)
            {
                aR.ManagedIdString.NamingConvention = NamingConvention;
            }
            ActAppRole.ManagedIdString.NamingConvention = NamingConvention;
            if (AddMode)
            {
                ActAppRole.ManagedIdString.SetAppPartFromExtId(extAppId);
            }
            else
            {
                ActAppRole.Area = new() { IdString = ModellingManagedIdString.ConvertAppRoleToArea(ActAppRole.IdString, NamingConvention) };
            }
        }

        public async Task InitAppRole(ModellingNetworkArea? newArea)
        {
            ActAppRole.Area = newArea;
            if (newArea != null)
            {
                if (newArea.IdString.Length >= NamingConvention.FixedPartLength && AddMode)
                {
                    ActAppRole.ManagedIdString.ConvertAreaToAppRoleFixedPart(newArea.IdString);
                    ActAppRole.ManagedIdString.FreePart = await ProposeFreeAppRoleNumber(ActAppRole.ManagedIdString);
                }
            }
            OrigId = new(ActAppRole.ManagedIdString);
            await SelectAppServersFromArea(newArea);
        }

        public void AppServerToAppRole(List<ModellingAppServer> appServers)
        {
            foreach (ModellingAppServer appServer in appServers)
            {
                if (!appServer.IsDeleted && ActAppRole.AppServers.FirstOrDefault(w => w.Content.Id == appServer.Id) == null && !AppServerToAdd.Contains(appServer))
                {
                    AppServerToAdd.Add(appServer);
                }
            }
        }

        public async Task<ModellingAppRole> GetDummyAppRole()
        {
            List<ModellingAppRole> dummyAppRole = await apiConnection.SendQueryAsync<List<ModellingAppRole>>(ModellingQueries.getDummyAppRole);
            if (dummyAppRole.Count > 0)
            {
                return dummyAppRole.First();
            }
            ActAppRole.Name = GlobalConst.kDummyAppRole;
            ActAppRole.IdString = GlobalConst.kDummyAppRole;
            await AddAppRoleToDb(null);
            return ActAppRole;
        }

        public async Task<bool> Save()
        {
            try
            {
                if (ActAppRole.Sanitize())
                {
                    DisplayMessageInUi(null, userConfig.GetText("save_app_role"), userConfig.GetText("U0001"), true);
                }
                if (await CheckAppRole())
                {
                    foreach (ModellingAppServer appServer in AppServerToDelete)
                    {
                        ActAppRole.AppServers.Remove(ActAppRole.AppServers.FirstOrDefault(x => x.Content.Id == appServer.Id) ?? throw new KeyNotFoundException("Did not find app server."));
                    }
                    foreach (ModellingAppServer appServer in AppServerToAdd)
                    {
                        ActAppRole.AppServers.Add(new ModellingAppServerWrapper() { Content = appServer });
                    }
                    if (AddMode)
                    {
                        await AddAppRoleToDb(Application.Id);
                    }
                    else
                    {
                        await UpdateAppRoleInDb();
                    }
                    CleanUp();
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
            if (ActAppRole.IdString.Length <= NamingConvention.FixedPartLength || ActAppRole.Name == "")
            {
                DisplayMessageInUi(null, userConfig.GetText("edit_app_role"), userConfig.GetText("E5102"), true);
                return false;
            }
            if (( AddMode || ActAppRole.IdString != OrigId.Whole ) && await IdStringAlreadyUsed(ActAppRole.IdString))
            {
                // popup for correction?
                DisplayMessageInUi(null, userConfig.GetText("edit_app_role"), ActAppRole.IdString.ToString() + ": " + userConfig.GetText("E9003"), true);
                ActAppRole.ManagedIdString = new(OrigId);
                return false;
            }
            return true;
        }

        private async Task<bool> IdStringAlreadyUsed(string appRoleIdString)
        {
            List<ModellingAppRole>? existAR = await apiConnection.SendQueryAsync<List<ModellingAppRole>>(ModellingQueries.getNewestAppRoles, new { pattern = appRoleIdString });
            if (existAR != null)
            {
                foreach (ModellingAppRole aR in existAR)
                {
                    if (aR.Id != ActAppRole.Id)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public async Task<string> ProposeFreeAppRoleNumber(ModellingManagedIdString idFixString)
        {
            int proposedNumber = 1;
            List<ModellingAppRole>? newestARs = await apiConnection.SendQueryAsync<List<ModellingAppRole>>(ModellingQueries.getNewestAppRoles, new { pattern = idFixString.CombinedFixPart + "%" });
            if (newestARs != null && newestARs.Count > 0)
            {
                newestARs[0].ManagedIdString.NamingConvention = NamingConvention;
                if (int.TryParse(newestARs[0].ManagedIdString.FreePart, out int newestARNumber))
                {
                    proposedNumber = await SearchFrom(idFixString, newestARNumber + 1);
                    if (proposedNumber == 0)
                    {
                        proposedNumber = await SearchFrom(idFixString, 1);
                    }
                }
            }
            return ProposedString(proposedNumber);
        }

        private async Task<int> SearchFrom(ModellingManagedIdString idFixString, int aRNumber)
        {
            double maxNumbers = Math.Pow(10, NamingConvention.FreePartLength) - 1;
            while (aRNumber <= maxNumbers)
            {
                if (!await IdStringAlreadyUsed(idFixString.CombinedFixPart + idFixString.Separator + ProposedString(aRNumber)))
                {
                    return aRNumber;
                }
                aRNumber++;
            }
            return 0;
        }

        private string ProposedString(int aRNumber)
        {
            return aRNumber.ToString($"D{NamingConvention.FreePartLength}");
        }

        private async Task AddAppRoleToDb(int? appId)
        {
            try
            {
                var Variables = new
                {
                    name = ActAppRole.Name,
                    idString = ActAppRole.IdString,
                    appId = appId,
                    comment = ActAppRole.Comment,
                    creator = userConfig.User.Name
                };
                ReturnId[]? returnIds = ( await apiConnection.SendQueryAsync<ReturnIdWrapper>(ModellingQueries.newAppRole, Variables) ).ReturnIds;
                if (returnIds != null)
                {
                    ActAppRole.Id = returnIds[0].NewIdLong;
                    await LogChange(ModellingTypes.ChangeType.Insert, ModellingTypes.ModObjectType.AppRole, ActAppRole.Id,
                        $"New App Role: {ActAppRole.Display()}", Application.Id);
                    foreach (ModellingAppServerWrapper appServer in ActAppRole.AppServers)
                    {
                        var Vars = new
                        {
                            nwObjectId = appServer.Content.Id,
                            nwGroupId = ActAppRole.Id
                        };
                        await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addNwObjectToNwGroup, Vars);
                        await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ModObjectType.AppRole, ActAppRole.Id,
                            $"Added App Server {appServer.Content.Display()} to App Role: {ActAppRole.Display()}", Application.Id);
                    }
                    ActAppRole.Creator = userConfig.User.Name;
                    ActAppRole.CreationDate = DateTime.Now;
                    AppRoles.Add(ActAppRole);
                    AvailableNwElems.Add(new KeyValuePair<int, long>((int)ModellingTypes.ModObjectType.AppRole, ActAppRole.Id));
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
                await LogChange(ModellingTypes.ChangeType.Update, ModellingTypes.ModObjectType.AppRole, ActAppRole.Id,
                    $"Updated App Role: {ActAppRole.Display()}", Application.Id);
                foreach (ModellingAppServer appServer in AppServerToDelete)
                {
                    var Vars = new
                    {
                        nwObjectId = appServer.Id,
                        nwGroupId = ActAppRole.Id
                    };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeNwObjectFromNwGroup, Vars);
                    await LogChange(ModellingTypes.ChangeType.Unassign, ModellingTypes.ModObjectType.AppRole, ActAppRole.Id,
                        $"Removed App Server {appServer.Display()} from App Role: {ActAppRole.Display()}", Application.Id);
                }
                foreach (ModellingAppServer appServer in AppServerToAdd)
                {
                    var Vars = new
                    {
                        nwObjectId = appServer.Id,
                        nwGroupId = ActAppRole.Id
                    };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addNwObjectToNwGroup, Vars);
                    await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ModObjectType.AppRole, ActAppRole.Id,
                        $"Added App Server {appServer.Display()} to App Role: {ActAppRole.Display()}", Application.Id);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("edit_app_role"), "", true);
            }
        }

        public void CleanUp()
        {
            AppServerToAdd = [];
            AppServerToDelete = [];
        }

        public async Task SelectAppServersFromArea(ModellingNetworkArea? area)
        {
            AppServersInArea = [];
            if (area != null)
            {
                foreach (ModellingAppServer? server in AvailableAppServers.Where(x => !x.IsDeleted))
                {
                    if (IsInArea(server, area))
                    {
                        server.InUse = await CheckAppServerInUse(server);
                        server.TooltipText = userConfig.GetText("C9002");
                        AppServersInArea.Add(server);
                    }
                }
            }
        }

        public void CountMembers(ModellingNetworkArea area)
        {
            area.MemberCount = 0;
            foreach (ModellingAppServer? server in AvailableAppServers.Where(x => !x.IsDeleted))
            {
                if (IsInArea(server, area))
                {
                    area.MemberCount++;
                }
            }
        }

        private static bool IsInArea(ModellingAppServer server, ModellingNetworkArea area)
        {
            try
            {
                foreach (NetworkDataWrapper areaIpData in area.IpData)
                {
                    IPAddress serverIpStart = IPAddress.Parse(server.Ip.StripOffNetmask());
                    IPAddress serverIpEnd = IPAddress.Parse(server.IpEnd.StripOffNetmask());
                    if(areaIpData.Content.Ip == null || areaIpData.Content.IpEnd == null)
                    {
                        return false;
                    }
                    IPAddress subnetIpStart = IPAddress.Parse(areaIpData.Content.Ip.StripOffNetmask());
                    IPAddress subnetIpEnd = IPAddress.Parse(areaIpData.Content.IpEnd.StripOffNetmask());

                    IPAddressRange ipRangeServer = new(serverIpStart, serverIpEnd);
                    IPAddressRange ipRangeSubnet = new(subnetIpStart, subnetIpEnd);

                    if (serverIpStart.AddressFamily != subnetIpStart.AddressFamily)
                    {
                        return false;
                    }

                    if (IpOperations.RangeOverlapExists(ipRangeServer, ipRangeSubnet))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}

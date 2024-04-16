using FWO.Config.Api;
using FWO.GlobalConstants;
using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using System.Text.Json;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Text.RegularExpressions;

namespace FWO.Ui.Services
{
    public class ModellingAppRoleHandler : ModellingHandlerBase
    {
        public List<ModellingAppRole> AppRoles { get; set; } = new();
        public ModellingAppRole ActAppRole { get; set; } = new();
        public List<ModellingAppServer> AvailableAppServers { get; set; } = new();
        public List<ModellingAppServer> AppServersInArea { get; set; } = new();
        public List<KeyValuePair<int, long>> AvailableNwElems { get; set; } = new();
        public List<ModellingAppServer> AppServerToAdd { get; set; } = new();
        public List<ModellingAppServer> AppServerToDelete { get; set; } = new();
        public ModellingNamingConvention NamingConvention = new();

        private ModellingManagedIdString OrigId = new();
        public bool ReadOnly = false;


        public ModellingAppRoleHandler(ApiConnection apiConnection, UserConfig userConfig, FwoOwner application, 
            List<ModellingAppRole> appRoles, ModellingAppRole appRole, List<ModellingAppServer> availableAppServers,
            List<KeyValuePair<int, long>> availableNwElems, bool addMode, Action<Exception?, string, string, bool> displayMessageInUi, bool isOwner = true, bool readOnly = false)
            : base (apiConnection, userConfig, application, addMode, displayMessageInUi, isOwner)
        {
            AppRoles = appRoles;
            AvailableAppServers = availableAppServers;
            AvailableNwElems = availableNwElems;
            ActAppRole = appRole;
            ReadOnly = readOnly;
            ApplyNamingConvention(application.ExtAppId);
        }

        private void ApplyNamingConvention(string extAppId)
        {
            NamingConvention = JsonSerializer.Deserialize<ModellingNamingConvention>(userConfig.ModNamingConvention) ?? new();
            foreach(var aR in AppRoles)
            {
                aR.ManagedIdString.NamingConvention = NamingConvention;
            }
            ActAppRole.ManagedIdString.NamingConvention = NamingConvention;
            if(AddMode)
            {
                ActAppRole.ManagedIdString.SetAppPartFromExtId(extAppId);
            }
            else
            {
                ActAppRole.Area = new () { IdString = ModellingManagedIdString.ConvertAppRoleToArea(ActAppRole.IdString, NamingConvention) };
            }
        }

        public async Task InitAppRole(ModellingNetworkArea? newArea)
        {
            ActAppRole.Area = newArea;
            if(newArea != null)
            {
                if(newArea.IdString.Length >= NamingConvention.FixedPartLength && AddMode)
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
            foreach(var appServer in appServers)
            {
                if(!appServer.IsDeleted && ActAppRole.AppServers.FirstOrDefault(w => w.Content.Id == appServer.Id) == null && !AppServerToAdd.Contains(appServer))
                {
                    AppServerToAdd.Add(appServer);
                }
            }
        }

        public async Task<ModellingAppRole> GetDummyAppRole()
        {
            List<ModellingAppRole> dummyAppRole = await apiConnection.SendQueryAsync<List<ModellingAppRole>>(ModellingQueries.getDummyAppRole);
            if(dummyAppRole.Count > 0)
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
                        await AddAppRoleToDb(Application.Id);
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
            if((AddMode || ActAppRole.IdString != OrigId.Whole) && await IdStringAlreadyUsed(ActAppRole.IdString))
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

        public async Task<string> ProposeFreeAppRoleNumber(ModellingManagedIdString idFixString)
        {
            int proposedNumber = 1;
            List<ModellingAppRole>? newestARs = await apiConnection.SendQueryAsync<List<ModellingAppRole>>(ModellingQueries.getNewestAppRoles, new { pattern = idFixString.CombinedFixPart + "%" });
            if(newestARs != null && newestARs.Count > 0)
            {
                newestARs[0].ManagedIdString.NamingConvention = NamingConvention;
                if(int.TryParse(newestARs[0].ManagedIdString.FreePart, out int newestARNumber))
                {
                    proposedNumber = await SearchFrom(idFixString, newestARNumber + 1);
                    if(proposedNumber == 0)
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
            while(aRNumber <= maxNumbers)
            {
                if(!await IdStringAlreadyUsed(idFixString.CombinedFixPart + idFixString.Separator + ProposedString(aRNumber)))
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
                ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<NewReturning>(ModellingQueries.newAppRole, Variables)).ReturnIds;
                if (returnIds != null)
                {
                    ActAppRole.Id = returnIds[0].NewId;
                    await LogChange(ModellingTypes.ChangeType.Insert, ModellingTypes.ModObjectType.AppRole, ActAppRole.Id,
                        $"New App Role: {ActAppRole.Display()}", Application.Id);
                    foreach(var appServer in ActAppRole.AppServers)
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
                foreach(var appServer in AppServerToDelete)
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
                foreach(var appServer in AppServerToAdd)
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

        public void Close()
        {
            AppServerToAdd = new ();
            AppServerToDelete = new ();
        }

        public async Task SelectAppServersFromArea(ModellingNetworkArea? area)
        {
            AppServersInArea = new ();
            if(area != null)
            {
                foreach(var server in AvailableAppServers.Where(x => !x.IsDeleted))
                {
                    if(IsInArea(server, area))
                    {
                        server.InUse = await CheckAppServerInUse(server);
                        server.TooltipText = userConfig.GetText("C9002");
                        AppServersInArea.Add(server);
                    }
                }
            }
        }

        private static string StripOffNetmask(string ip)
        {
            Match match = Regex.Match(ip, @"^([\d\.\:]+)\/");
            if (match.Success)
            {
                string matchedString = match.Value;
                return matchedString.Remove( matchedString.Length - 1 );
            }
            return ip;
        }

        private static bool IsInArea(ModellingAppServer server, ModellingNetworkArea area)
        {
            try
            {
                foreach(var subnet in area.Subnets)
                {
                    if(IsInSubnet(IPAddress.Parse(StripOffNetmask(server.Ip)), subnet.Content.Ip ?? throw new Exception()))
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

        private static bool IsInSubnet(IPAddress address, string subnetMask)
        {
            var slashIdx = subnetMask.IndexOf("/");
            var maskAddress = IPAddress.Parse(slashIdx == -1 ? subnetMask : subnetMask.Substring(0, slashIdx));
            if (maskAddress.AddressFamily != address.AddressFamily)
            {
                return false;
            }

            int maskLength = slashIdx == -1 ? (maskAddress.AddressFamily == AddressFamily.InterNetwork ? 31 : 127) : int.Parse(subnetMask.Substring(slashIdx + 1));
            if (maskLength == 0)
            {
                return true;
            }

            if (maskAddress.AddressFamily == AddressFamily.InterNetwork)
            {
                var maskAddressBits = BitConverter.ToUInt32(maskAddress.GetAddressBytes().Reverse().ToArray(), 0);
                var ipAddressBits = BitConverter.ToUInt32(address.GetAddressBytes().Reverse().ToArray(), 0);
                uint mask = uint.MaxValue << (32 - maskLength);
                return (maskAddressBits & mask) == (ipAddressBits & mask);
            }

            if (maskAddress.AddressFamily == AddressFamily.InterNetworkV6)
            {
                var maskAddressBits = new BitArray(maskAddress.GetAddressBytes().Reverse().ToArray());
                var ipAddressBits = new BitArray(address.GetAddressBytes().Reverse().ToArray());
                var ipAddressLength = ipAddressBits.Length;

                if (maskAddressBits.Length != ipAddressBits.Length)
                {
                    throw new ArgumentException("Length of IP Address and Subnet Mask do not match.");
                }

                for (var i = ipAddressLength - 1; i >= ipAddressLength - maskLength; i--)
                {
                    if (ipAddressBits[i] != maskAddressBits[i])
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }
    }
}

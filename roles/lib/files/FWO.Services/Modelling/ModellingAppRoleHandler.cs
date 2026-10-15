using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Logging;
using FWO.Basics;
using FWO.Config.Api;
using NetTools;
using System.Net;

namespace FWO.Services.Modelling
{
    public class ModellingAppRoleHandler : ModellingHandlerBase
    {
        public List<ModellingAppRole> AppRoles { get; set; } = [];
        public ModellingAppRole ActAppRole { get; set; } = new();
        public List<ModellingAppServer> AppServersInArea { get; set; } = [];
        public List<ModellingAppServer> AppServerToAdd { get; set; } = [];
        public List<ModellingAppServer> AppServerToDelete { get; set; } = [];
        public ModellingNamingConvention NamingConvention = new();

        /// <summary>
        /// Indicates whether the naming convention allows to convert area identifiers into app role identifiers.
        /// </summary>
        public bool AreaConversionValid { get; private set; } = true;

        private ModellingManagedIdString OrigId = new();

        // the app role shown elsewhere (e.g. in the library): only updated when the edited copy has been saved
        private readonly ModellingAppRole? OrigAppRole;

        public ModellingAppRoleHandler(ApiConnection apiConnection, UserConfig userConfig, FwoOwner application,
            List<ModellingAppRole> appRoles, ModellingAppRole appRole, List<ModellingAppServer> availableAppServers,
            List<KeyValuePair<int, long>> availableNwElems, bool addMode, Action<Exception?, string, string, bool> displayMessageInUi, bool isOwner = true, bool readOnly = false)
            : base(apiConnection, userConfig, application, addMode, displayMessageInUi, readOnly, isOwner)
        {
            AppRoles = appRoles;
            AvailableAppServers = availableAppServers;
            AvailableNwElems = availableNwElems;
            if (addMode)
            {
                ActAppRole = appRole;
            }
            else
            {
                // edit a copy, so that cancelled changes do not show up elsewhere
                OrigAppRole = appRole;
                ActAppRole = new(appRole) { AppServers = [.. appRole.AppServers] };
            }
            ApplyNamingConvention(application.ExtAppId ?? "");
        }

        private void ApplyNamingConvention(string extAppId)
        {
            NamingConvention = ModellingNamingConvention.FromJson(userConfig.ModNamingConvention);
            AreaConversionValid = NamingConvention.IsAreaConversionValid();
            foreach (ModellingAppRole aR in AppRoles)
            {
                aR.ManagedIdString.NamingConvention = NamingConvention;
            }
            ActAppRole.ManagedIdString.NamingConvention = NamingConvention;
            if (AddMode)
            {
                ActAppRole.ManagedIdString.SetAppPartFromExtId(extAppId);
            }
            else if (AreaConversionValid)
            {
                ActAppRole.Area = new() { IdString = ModellingManagedIdString.ConvertAppRoleToArea(ActAppRole.IdString, NamingConvention) };
            }
            if (!AreaConversionValid)
            {
                // reporting once per dialog: neither the proposed id nor the derived area would be usable
                Log.WriteWarning("Apply Naming Convention", $"Area conversion is not possible: fixed part length {NamingConvention.FixedPartLength}, " +
                    $"network area pattern '{NamingConvention.NetworkAreaPattern}', app role pattern '{NamingConvention.AppRolePattern}'.");
                DisplayMessageInUi(null, userConfig.GetText(DialogTitleKey()), userConfig.GetText(NamingConventionErrorKey()), true);
            }
        }

        /// <summary>
        /// Determines the message describing why area identifiers cannot be converted into app role identifiers.
        /// </summary>
        /// <returns>the key of the message to display</returns>
        private string NamingConventionErrorKey()
        {
            return NamingConvention.IsFixedPartLengthValid() ? "E5602" : "E5601";
        }

        /// <summary>
        /// Determines the title of the dialog the message belongs to, so that both carry the same wording.
        /// </summary>
        /// <returns>the key of the title to display</returns>
        private string DialogTitleKey()
        {
            if (ReadOnly)
            {
                return "app_role";
            }
            return AddMode ? "add_app_role" : "edit_app_role";
        }

        /// <summary>
        /// Initializes the app role for the selected network area and proposes an identifier in add mode.
        /// An invalid naming convention is reported once when the handler is created, so it is only skipped here.
        /// </summary>
        /// <param name="newArea">the selected network area, may be null</param>
        public async Task InitAppRole(ModellingNetworkArea? newArea)
        {
            ActAppRole.Area = newArea;
            if (newArea != null && newArea.IdString.Length >= NamingConvention.FixedPartLength && AddMode && AreaConversionValid)
            {
                ActAppRole.ManagedIdString.ConvertAreaToAppRoleFixedPart(newArea.IdString);
                ActAppRole.ManagedIdString.FreePart = await ProposeFreeAppRoleNumber(ActAppRole.ManagedIdString);
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

        /// <summary>
        /// Persists the app role. Users who may only try out the dialog (IsOwner not set) are rejected.
        /// If writing fails, the changes not yet written stay queued, so that saving can be retried.
        /// </summary>
        /// <returns>True if the app role has been saved.</returns>
        public async Task<bool> Save()
        {
            if (!IsOwner)
            {
                DisplayMessageInUi(null, userConfig.GetText("change_app_role_forbidden"), userConfig.GetText("E9030"), true);
                return false;
            }
            try
            {
                if (ActAppRole.Sanitize())
                {
                    DisplayMessageInUi(null, userConfig.GetText("save_app_role"), userConfig.GetText("U0001"), true);
                }
                if (await CheckAppRole())
                {
                    bool saved = AddMode ? await AddAppRoleToDb(Application.Id) : await UpdateAppRoleInDb();
                    if (saved)
                    {
                        CleanUp();
                    }
                    return saved;
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
            if ((AddMode || ActAppRole.IdString != OrigId.Whole) && await IdStringAlreadyUsed(ActAppRole.IdString))
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

        /// <summary>
        /// Creates the app role and assigns its app servers in the database.
        /// </summary>
        /// <param name="appId">the owning application, null for the dummy app role</param>
        /// <returns>True if the app role has been created. Saving again would create it a second time, so it is then
        /// registered with the app servers actually assigned, even if assigning the others failed (error displayed).</returns>
        private async Task<bool> AddAppRoleToDb(int? appId)
        {
            ReturnId[]? returnIds;
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
                returnIds = (await apiConnection.SendQueryAsync<ReturnIdWrapper>(ModellingQueries.newAppRole, Variables)).ReturnIds;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("add_app_role"), "", true);
                return false;
            }
            if (returnIds == null || returnIds.Length == 0)
            {
                return false;
            }
            ActAppRole.Id = returnIds[0].NewIdLong;
            await LogChange(ModellingTypes.ChangeType.Insert, ModellingTypes.ModObjectType.AppRole, ActAppRole.Id,
                $"New App Role: {ActAppRole.Display()}", Application.Id);
            QueueAllAppServersForAssignment();
            await WriteAppServerChanges("add_app_role");
            ActAppRole.Creator = userConfig.User.Name;
            ActAppRole.CreationDate = DateTime.Now;
            AppRoles.Add(ActAppRole);
            AvailableNwElems.Add(new KeyValuePair<int, long>((int)ModellingTypes.ModObjectType.AppRole, ActAppRole.Id));
            return true;
        }

        /// <summary>
        /// A new app role has no app servers assigned in the database yet, so all of its app servers are queued for adding.
        /// </summary>
        private void QueueAllAppServersForAssignment()
        {
            List<ModellingAppServer> selectedAppServers = [.. ModellingAppServerWrapper.Resolve(ActAppRole.AppServers)
                .Where(appServer => !AppServerToDelete.Exists(toDelete => toDelete.Id == appServer.Id))];
            AppServerToAdd = [.. selectedAppServers, .. AppServerToAdd];
            AppServerToDelete = [];
            ActAppRole.AppServers = [];
        }

        /// <summary>
        /// Updates the edited app role and its app server assignments in the database.
        /// Whatever has been written is transferred to the app role shown elsewhere, even if a later step fails.
        /// </summary>
        /// <returns>True if all changes have been written without error.</returns>
        private async Task<bool> UpdateAppRoleInDb()
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
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("edit_app_role"), "", true);
                return false;
            }
            OrigId = new(ActAppRole.ManagedIdString);
            await LogChange(ModellingTypes.ChangeType.Update, ModellingTypes.ModObjectType.AppRole, ActAppRole.Id,
                $"Updated App Role: {ActAppRole.Display()}", Application.Id);
            bool allWritten = await WriteAppServerChanges("edit_app_role");
            ApplySavedChangesToOrigAppRole();
            return allWritten;
        }

        /// <summary>
        /// Writes the queued app server changes. Each written change is moved from its queue into the app role,
        /// so that after a failure the app role matches the database and only the missing changes are written again.
        /// </summary>
        /// <param name="titleKey">text key of the title of a failure message</param>
        /// <returns>True if all queued changes have been written without error.</returns>
        private async Task<bool> WriteAppServerChanges(string titleKey)
        {
            try
            {
                foreach (ModellingAppServer appServer in AppServerToDelete.ToList())
                {
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeNwObjectFromNwGroup, new { nwObjectId = appServer.Id, nwGroupId = ActAppRole.Id });
                    ActAppRole.AppServers.RemoveAll(wrapper => wrapper.Content.Id == appServer.Id);
                    AppServerToDelete.Remove(appServer);
                    await LogChange(ModellingTypes.ChangeType.Unassign, ModellingTypes.ModObjectType.AppRole, ActAppRole.Id,
                        $"Removed App Server {appServer.Display()} from App Role: {ActAppRole.Display()}", Application.Id);
                }
                foreach (ModellingAppServer appServer in AppServerToAdd.ToList())
                {
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addNwObjectToNwGroup, new { nwObjectId = appServer.Id, nwGroupId = ActAppRole.Id });
                    ActAppRole.AppServers.Add(new ModellingAppServerWrapper() { Content = appServer });
                    AppServerToAdd.Remove(appServer);
                    await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ModObjectType.AppRole, ActAppRole.Id,
                        $"Added App Server {appServer.Display()} to App Role: {ActAppRole.Display()}", Application.Id);
                }
                return true;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText(titleKey), "", true);
                return false;
            }
        }

        /// <summary>
        /// Transfers the saved changes of the edited copy to the app role shown elsewhere.
        /// </summary>
        private void ApplySavedChangesToOrigAppRole()
        {
            if (OrigAppRole == null)
            {
                return;
            }
            OrigAppRole.Name = ActAppRole.Name;
            OrigAppRole.ManagedIdString = new(ActAppRole.ManagedIdString);
            OrigAppRole.Comment = ActAppRole.Comment;
            // a copy: after a failure the dialog stays open and keeps changing its own list
            OrigAppRole.AppServers = [.. ActAppRole.AppServers];
            OrigAppRole.Area = ActAppRole.Area;
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
                    if (areaIpData.Content.Ip == null || areaIpData.Content.IpEnd == null)
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

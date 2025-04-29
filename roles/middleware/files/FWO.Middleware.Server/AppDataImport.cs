using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Data.Modelling;
using FWO.Logging;
using FWO.Services;
using Novell.Directory.Ldap;
using System.Data;
using System.Text.Json;

namespace FWO.Middleware.Server
{
	/// <summary>
	/// Class handling the App Data Import
	/// </summary>
	public class AppDataImport : DataImportBase
	{
		private List<ModellingImportAppData> importedApps = [];
		private List<FwoOwner> existingApps = [];
		private List<ModellingAppServer> existingAppServers = [];

		private Ldap internalLdap = new();
		private Ldap ownerGroupLdap = new();

		private List<Ldap> connectedLdaps = [];
		private string modellerRoleDn = "";
		private string requesterRoleDn = "";
		private string implementerRoleDn = "";
		private string reviewerRoleDn = "";
		private string? ownerGroupLdapPath = "";
		private List<GroupGetReturnParameters> allGroups = [];
		private List<GroupGetReturnParameters> allInternalGroups = [];
		private ModellingNamingConvention NamingConvention = new();
		private UserConfig userConfig = new();
	

		/// <summary>
		/// Constructor for App Data Import
		/// </summary>
		public AppDataImport(ApiConnection apiConnection, GlobalConfig globalConfig) : base(apiConnection, globalConfig)
		{ }

		/// <summary>
		/// Run the App Data Import
		/// </summary>
		public async Task<bool> Run()
		{
			try
			{
				NamingConvention = JsonSerializer.Deserialize<ModellingNamingConvention>(globalConfig.ModNamingConvention) ?? new();
				List<string> importfilePathAndNames = JsonSerializer.Deserialize<List<string>>(globalConfig.ImportAppDataPath) ?? throw new JsonException("Config Data could not be deserialized.");
				userConfig = new(globalConfig);
				userConfig.User.Name = Roles.MiddlewareServer;
                userConfig.AutoReplaceAppServer = globalConfig.AutoReplaceAppServer;
				await InitLdap();
				foreach (var importfilePathAndName in importfilePathAndNames)
				{
					if (!RunImportScript(importfilePathAndName + ".py"))
					{
						Log.WriteInfo("Import App Data", $"Script {importfilePathAndName}.py failed but trying to import from existing file.");
					}
					await ImportSingleSource(importfilePathAndName + ".json");
				}
			}
			catch (Exception exc)
			{
				Log.WriteError("Import App Data", $"Import could not be processed.", exc);
				return false;
			}
			return true;
		}

		private async Task InitLdap()
		{
			connectedLdaps = await apiConnection.SendQueryAsync<List<Ldap>>(AuthQueries.getLdapConnections);
			internalLdap = connectedLdaps.FirstOrDefault(x => x.IsInternal() && x.HasGroupHandling()) ?? throw new KeyNotFoundException("No internal Ldap with group handling found.");
			ownerGroupLdap = connectedLdaps.FirstOrDefault(x => x.Id == globalConfig.OwnerLdapId) ?? throw new KeyNotFoundException("Ldap with group handling not found.");
			modellerRoleDn = $"cn=modeller,{internalLdap.RoleSearchPath}";
			requesterRoleDn = $"cn=requester,{internalLdap.RoleSearchPath}";
			implementerRoleDn = $"cn=implementer,{internalLdap.RoleSearchPath}";
			reviewerRoleDn = $"cn=reviewer,{internalLdap.RoleSearchPath}";
			allInternalGroups = await internalLdap.GetAllInternalGroups();
            ownerGroupLdapPath = ownerGroupLdap.GroupWritePath;
			if (globalConfig.OwnerLdapId == GlobalConst.kLdapInternalId)
            {
                allGroups = allInternalGroups;  // TODO: check if ref is ok here
            }
            else
            {
                allGroups = await ownerGroupLdap.GetAllGroupObjects(globalConfig.OwnerLdapGroupNames.
                    Replace(GlobalConst.kAppIdPlaceholder, "*").
                    Replace(GlobalConst.kFullAppIdPlaceholder, "*").
                    Replace(GlobalConst.kAppPrefixPlaceholder, "*"));
            }
		}

		private async Task<bool> ImportSingleSource(string importfileName)
		{
			try
			{
				ReadFile(importfileName);
				ModellingImportOwnerData? importedOwnerData = JsonSerializer.Deserialize<ModellingImportOwnerData>(importFile) ?? throw new JsonException("File could not be parsed.");
				if (importedOwnerData != null && importedOwnerData.Owners != null)
				{
					importedApps = importedOwnerData.Owners;
					await ImportApps(importfileName);
				}
			}
			catch (Exception exc)
			{
				Log.WriteError("Import App Data", $"File {importfileName} could not be processed.", exc);
				return false;
			}
			return true;
		}

		private async Task ImportApps(string importfileName)
		{
			int successCounter = 0;
			int failCounter = 0;
			int deleteCounter = 0;
			int deleteFailCounter = 0;

			if (!(globalConfig.OwnerLdapGroupNames.Contains(GlobalConst.kAppIdPlaceholder) ||
                globalConfig.OwnerLdapGroupNames.Contains(GlobalConst.kFullAppIdPlaceholder)))
            {
                Log.WriteWarning("Import App Data", $"Owner group pattern does not contain any of the placeholders {GlobalConst.kAppIdPlaceholder} or {GlobalConst.kFullAppIdPlaceholder}.");
			}
			else
			{
				existingApps = await apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwners);
				foreach (var incomingApp in importedApps)
				{
					if (await SaveApp(incomingApp))
					{
						++successCounter;
					}
					else
					{
						++failCounter;
					}
				}
				string? importSource = importedApps.FirstOrDefault()?.ImportSource;
				if(importSource != null)
				{
					foreach (var existingApp in existingApps.Where(x => x.ImportSource == importSource && x.Active))
					{
						if (importedApps.FirstOrDefault(x => x.ExtAppId == existingApp.ExtAppId) == null)
						{
							if (await DeactivateApp(existingApp))
							{
								++deleteCounter;
							}
							else
							{
								++deleteFailCounter;
							}
						}
					}
				}
				Log.WriteInfo("Import App Data", $"Imported from {importfileName}: {successCounter} apps, {failCounter} failed. Deactivated {deleteCounter} apps, {deleteFailCounter} failed.");
			}
		}

		private async Task<bool> SaveApp(ModellingImportAppData incomingApp)
		{
			try
			{
				string userGroupDn;
				FwoOwner? existingApp = existingApps.FirstOrDefault(x => x.ExtAppId == incomingApp.ExtAppId);

				if (existingApp == null)
				{
					userGroupDn = await NewApp(incomingApp);
				}
				else
				{
					userGroupDn = await UpdateApp(incomingApp, existingApp);
				}
				// in order to store email addresses of users in the group in UiUser for email notification:
				await AddAllGroupMembersToUiUser(userGroupDn);
			}
			catch (Exception exc)
			{
				Log.WriteError("Import App Data", $"App {incomingApp.Name} could not be processed.", exc);
				return false;
			}
			return true;
		}

		private async Task<string> NewApp(ModellingImportAppData incomingApp)
		{
			string userGroupDn;
			userGroupDn = globalConfig.ManageOwnerLdapGroups ? await CreateUserGroup(incomingApp) : GetGroupDn(incomingApp.ExtAppId);

			var variables = new
			{
				name = incomingApp.Name,
				dn = incomingApp.MainUser ?? "",
				groupDn = userGroupDn,
				appIdExternal = incomingApp.ExtAppId,
				criticality = incomingApp.Criticality,
				importSource = incomingApp.ImportSource,
				commSvcPossible = false
			};
			ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<ReturnIdWrapper>(OwnerQueries.newOwner, variables)).ReturnIds;
			if (returnIds != null)
			{
				if(incomingApp.MainUser != null && incomingApp.MainUser != "")
				{
					await UpdateRoles(incomingApp.MainUser);
				}
				int appId = returnIds[0].NewId;
				foreach (var appServer in incomingApp.AppServers)
				{
					await NewAppServer(appServer, appId, incomingApp.ImportSource);
				}
			}
			return userGroupDn;
		}

		private async Task<string> UpdateApp(ModellingImportAppData incomingApp, FwoOwner existingApp)
		{
			string userGroupDn = GetGroupDn(incomingApp.ExtAppId);
			if (globalConfig.ManageOwnerLdapGroups)
			{
				if (string.IsNullOrEmpty(existingApp.GroupDn) && allGroups.FirstOrDefault(x => x.GroupDn == userGroupDn) == null)
				{
					string newDn = await CreateUserGroup(incomingApp);
					if(newDn != userGroupDn) // may this happen?
					{
						Log.WriteInfo("Import App Data", $"New UserGroup DN {newDn} differs from settings value {userGroupDn}.");
						userGroupDn = newDn;
					}
				}
				else
				{
					await UpdateUserGroup(incomingApp, userGroupDn);
				}
			}

			var Variables = new
			{
				id = existingApp.Id,
				name = incomingApp.Name,
				dn = incomingApp.MainUser ?? "",
				groupDn = userGroupDn,
			    appIdExternal = string.IsNullOrEmpty(incomingApp.ExtAppId) ? null : incomingApp.ExtAppId,
				criticality = incomingApp.Criticality,
				commSvcPossible = existingApp.CommSvcPossible
			};
			await apiConnection.SendQueryAsync<ReturnIdWrapper>(OwnerQueries.updateOwner, Variables);
			if(incomingApp.MainUser != null && incomingApp.MainUser != "")
			{
				await UpdateRoles(incomingApp.MainUser);
			}
			await ImportAppServers(incomingApp, existingApp.Id);
			return userGroupDn;
		}

		private async Task<bool> DeactivateApp(FwoOwner app)
		{
			try
			{
				await apiConnection.SendQueryAsync<ReturnIdWrapper>(OwnerQueries.deactivateOwner, new { id = app.Id });
			}
			catch (Exception exc)
			{
				Log.WriteError("Import App Data", $"Outdated App {app.Name} could not be deactivated.", exc);
				return false;
			}
			return true;
		}

		private string GetGroupName(string extAppIdString)
		{
            // hard-coded GlobalConst.kAppIdSeparator could be moved to settings

            if (globalConfig.OwnerLdapGroupNames.Contains(GlobalConst.kFullAppIdPlaceholder))
            {
    			return globalConfig.OwnerLdapGroupNames.Replace(GlobalConst.kFullAppIdPlaceholder, extAppIdString);
            }
            
            if (globalConfig.OwnerLdapGroupNames.Contains(GlobalConst.kAppPrefixPlaceholder) && 
                globalConfig.OwnerLdapGroupNames.Contains(GlobalConst.kAppIdPlaceholder))
            {
				string[] parts = extAppIdString.Split(GlobalConst.kAppIdSeparator);
				string appPrefix = parts.Length > 0 ? parts[0] : "";
				string appId = parts.Length > 1 ? parts[1] : "";
    			return globalConfig.OwnerLdapGroupNames.Replace(GlobalConst.kAppPrefixPlaceholder, appPrefix).Replace(GlobalConst.kAppIdPlaceholder, appId);
            }
            Log.WriteInfo("Import App Data", $"Could not find ayn placeholders in group name pattern \"{globalConfig.OwnerLdapGroupNames}\" " +
                $"({GlobalConst.kFullAppIdPlaceholder}, {GlobalConst.kAppPrefixPlaceholder}, {GlobalConst.kAppIdPlaceholder} ");
            return globalConfig.OwnerLdapGroupNames;
		}

		private string GetGroupDn(string extAppIdString)
		{
            if (ownerGroupLdapPath == null)
            {
				throw new ArgumentNullException(nameof(ownerGroupLdapPath));
            }
			return $"cn={GetGroupName(extAppIdString)},{ownerGroupLdapPath}";
		}


		/// <summary>
		/// for each user of a remote ldap group create a user in uiuser
		/// this is necessary in order to get details like email address for users
		/// which have never logged in but who need to be notified via email
		/// </summary>
		private async Task AddAllGroupMembersToUiUser(string userGroupDn)
		{
			foreach (Ldap ldap in connectedLdaps)
			{
				foreach (string memberDn in await ldap.GetGroupMembers(userGroupDn))
				{
					UiUser? uiUser = await ConvertLdapToUiUser(apiConnection, memberDn);
					if(uiUser != null)
					{
						await UiUserHandler.UpsertUiUser(apiConnection, uiUser, false);
					}
				}
			}
		}

		private async Task<UiUser?> ConvertLdapToUiUser(ApiConnection apiConnection, string userDn)
		{
			// add the modelling user to local uiuser table for later ref to email address
			// find the user in all connected ldaps
			foreach (Ldap ldap in connectedLdaps)
			{
				if (!string.IsNullOrEmpty(ldap.UserSearchPath) && userDn.ToLower().Contains(ldap.UserSearchPath!.ToLower()))
				{
					LdapEntry? ldapUser = await ldap.GetUserDetailsFromLdap(userDn);
					
					if (ldapUser != null)
					{
						// add data from ldap entry to uiUser
						return new()
						{
							LdapConnection = new UiLdapConnection(){ Id = ldap.Id },
							Dn = ldapUser.Dn,
							Name = ldap.GetName(ldapUser),
							Firstname = ldap.GetFirstName(ldapUser),
							Lastname = ldap.GetLastName(ldapUser),
							Email = ldap.GetEmail(ldapUser),
							Tenant = await DeriveTenantFromLdap(ldap, ldapUser)							
						};
					}
				}
			}
			return null;
		}

		private async Task<Tenant> DeriveTenantFromLdap(Ldap ldap, LdapEntry ldapUser)
		{
			// try to derive the the user's tenant from the ldap settings
			Tenant tenant = new()
			{
				Id = GlobalConst.kTenant0Id  // default: tenant0 (id=1)
			};

			string tenantName = "";

			// can we derive the users tenant purely from its ldap?
			if (!string.IsNullOrEmpty(ldap.GlobalTenantName) || ldap.TenantLevel > 0)
			{
				if (ldap.TenantLevel > 0)
				{
					// getting tenant via tenant level setting from distinguished name
					tenantName = ldap.GetTenantName(ldapUser);
				}
				else
				{
					if (!string.IsNullOrEmpty(ldap.GlobalTenantName))
					{
						tenantName = ldap.GlobalTenantName ?? "";
					}
				}

				var variables = new { tenant_name = tenantName };
				Tenant[] tenants = await apiConnection.SendQueryAsync<Tenant[]>(AuthQueries.getTenantId, variables, "getTenantId");
				if (tenants.Length == 1)
				{
					tenant.Id = tenants[0].Id;
				}
			}
			return tenant;
		}

		private async Task<string> CreateUserGroup(ModellingImportAppData incomingApp)
		{
			string groupDn = "";
			if (incomingApp.Modellers != null && incomingApp.Modellers.Count > 0
				|| incomingApp.ModellerGroups != null && incomingApp.ModellerGroups.Count > 0)
			{
				string groupName = GetGroupName(incomingApp.ExtAppId);
				groupDn = await internalLdap.AddGroup(groupName, true);
				if (incomingApp.Modellers != null)
				{
					foreach (var modeller in incomingApp.Modellers)
					{
						// add user to internal group:
						await internalLdap.AddUserToEntry(modeller, groupDn);
					}
				}
				if (incomingApp.ModellerGroups != null)
				{
					foreach (var modellerGrp in incomingApp.ModellerGroups)
					{
						await internalLdap.AddUserToEntry(modellerGrp, groupDn);
					}
				}
				await internalLdap.AddUserToEntry(groupDn, modellerRoleDn);
				await internalLdap.AddUserToEntry(groupDn, requesterRoleDn);
				await internalLdap.AddUserToEntry(groupDn, implementerRoleDn);
				await internalLdap.AddUserToEntry(groupDn, reviewerRoleDn);
			}
			return groupDn;
		}

		private async Task<string> UpdateUserGroup(ModellingImportAppData incomingApp, string groupDn)
		{
			List<string> existingMembers = (allGroups.FirstOrDefault(x => x.GroupDn == groupDn) ?? throw new KeyNotFoundException($"Group with DN '{groupDn}' could not be found.")).Members;
			if (incomingApp.Modellers != null)
			{
				foreach (var modeller in incomingApp.Modellers)
				{
					if (existingMembers.FirstOrDefault(x => x.Equals(modeller, StringComparison.OrdinalIgnoreCase)) == null)
					{
                        await internalLdap.AddUserToEntry(modeller, groupDn);
					}
				}
			}
			if (incomingApp.ModellerGroups != null)
			{
				foreach (var modellerGrp in incomingApp.ModellerGroups)
				{
					if (existingMembers.FirstOrDefault(x => x.Equals(modellerGrp, StringComparison.OrdinalIgnoreCase)) == null)
					{
						await internalLdap.AddUserToEntry(modellerGrp, groupDn);
					}
				}
			}
			foreach (var member in existingMembers)
			{
				if ((incomingApp.Modellers == null || incomingApp.Modellers.FirstOrDefault(x => x.Equals(member, StringComparison.OrdinalIgnoreCase)) == null)
					&& (incomingApp.ModellerGroups == null || incomingApp.ModellerGroups.FirstOrDefault(x => x.Equals(member, StringComparison.OrdinalIgnoreCase)) == null))
				{
					await internalLdap.RemoveUserFromEntry(member, groupDn);
				}
			}
			await UpdateRoles(groupDn);
			return groupDn;
		}

		private async Task UpdateRoles(string dn)
		{
			List<string> roles = await internalLdap.GetRoles([dn]);
			if(!roles.Contains(Roles.Modeller))
			{
				await internalLdap.AddUserToEntry(dn, modellerRoleDn);
			}
			if(!roles.Contains(Roles.Requester))
			{
				await internalLdap.AddUserToEntry(dn, requesterRoleDn);
			}
			if(!roles.Contains(Roles.Implementer))
			{
				await internalLdap.AddUserToEntry(dn, implementerRoleDn);
			}
			if(!roles.Contains(Roles.Reviewer))
			{
				await internalLdap.AddUserToEntry(dn, reviewerRoleDn);
			}
		}

		private async Task ImportAppServers(ModellingImportAppData incomingApp, int applId)
		{
			int successCounter = 0;
			int failCounter = 0;
			int deleteCounter = 0;
			int deleteFailCounter = 0;

			var Variables = new
			{
				importSource = incomingApp.ImportSource,
				appId = applId
			};
			existingAppServers = await apiConnection.SendQueryAsync<List<ModellingAppServer>>(ModellingQueries.getAppServersBySource, Variables);
			foreach (var incomingAppServer in incomingApp.AppServers)
			{
				if (await SaveAppServer(incomingAppServer, applId, incomingApp.ImportSource))
				{
					++successCounter;
				}
				else
				{
					++failCounter;
				}
			}
			foreach (var existingAppServer in existingAppServers.Where(e => !e.IsDeleted).ToList())
			{
				if (incomingApp.AppServers.FirstOrDefault(x => x.Ip.IpAsCidr() == existingAppServer.Ip.IpAsCidr() && x.IpEnd.IpAsCidr() == existingAppServer.IpEnd.IpAsCidr()) == null)
				{
					if (await MarkDeletedAppServer(existingAppServer))
					{
						++deleteCounter;
					}
					else
					{
						++deleteFailCounter;
					}
				}
			}
			Log.WriteDebug($"Import App Server Data for App {incomingApp.Name}", $"Imported {successCounter} app servers, {failCounter} failed. {deleteCounter} app servers marked as deleted, {deleteFailCounter} failed.");
		}

		private async Task<bool> SaveAppServer(ModellingImportAppServer incomingAppServer, int appID, string impSource)
		{
			try
			{
				if(incomingAppServer.IpEnd == "")
				{
					incomingAppServer.IpEnd = incomingAppServer.Ip;
				}
				if(globalConfig.DnsLookup)
				{
					incomingAppServer.Name = await BuildAppServerName(incomingAppServer);
				}
				ModellingAppServer? existingAppServer = existingAppServers.FirstOrDefault(x => x.Ip.IpAsCidr() == incomingAppServer.Ip.IpAsCidr() && x.IpEnd.IpAsCidr() == incomingAppServer.IpEnd.IpAsCidr());
				if (existingAppServer == null)
				{
					return await NewAppServer(incomingAppServer, appID, impSource);
				}

				if (existingAppServer.IsDeleted)
				{
					if (!await ReactivateAppServer(existingAppServer))
					{	
						return false;
					}
				}
				else
				{
					// in case there are still active appservers from other sources (resulting e.g. from older revisions)
					await AppServerHelper.DeactivateOtherSources(apiConnection, userConfig, existingAppServer);
				}
				if (!existingAppServer.Name.Equals(incomingAppServer.Name))
				{
					if (!await UpdateAppServerName(existingAppServer, incomingAppServer.Name))
					{	
						return false;
					}
				}
				if (existingAppServer.CustomType == null)
				{
					if (!await UpdateAppServerType(existingAppServer))
					{	
						return false;
					}
				}
				return true;
			}
			catch (Exception exc)
			{
				Log.WriteError("Import App Server Data", $"App Server {incomingAppServer.Name} could not be processed.", exc);
				return false;
			}
		}

		private async Task<string> BuildAppServerName(ModellingImportAppServer appServer)
		{
			try
			{
				return await AppServerHelper.ConstructAppServerNameFromDns(appServer.ToModellingAppServer(), NamingConvention, globalConfig.OverwriteExistingNames, true);
			}
			catch (Exception exc)
			{
				Log.WriteError("Import App Server Data", $"App Server name {appServer.Name} could not be set according to naming conventions.", exc);
			}
			return appServer.Name;
		}

		private async Task<bool> NewAppServer(ModellingImportAppServer incomingAppServer, int appID, string impSource)
		{
			try
			{
				var Variables = new
				{
					name = incomingAppServer.Name,
					appId = appID,
					ip = incomingAppServer.Ip.IpAsCidr(),
					ipEnd = incomingAppServer.IpEnd != "" ? incomingAppServer.IpEnd.IpAsCidr() : incomingAppServer.Ip.IpAsCidr(),
					importSource = impSource,
					customType = 0
				};
				ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<ReturnIdWrapper>(ModellingQueries.newAppServer, Variables)).ReturnIds;
				if(returnIds != null && returnIds.Length > 0)
				{
					ModellingAppServer newModAppServer = new(incomingAppServer.ToModellingAppServer()){ Id = returnIds[0].NewIdLong, ImportSource = impSource, AppId = appID};
					await ModellingHandlerBase.LogChange(ModellingTypes.ChangeType.Insert, ModellingTypes.ModObjectType.AppServer, newModAppServer.Id,
                        $"New App Server: {newModAppServer.Display()}", apiConnection, userConfig, newModAppServer.AppId, DefaultInit.DoNothing, null, newModAppServer.ImportSource);
					await AppServerHelper.DeactivateOtherSources(apiConnection, userConfig, newModAppServer);
				}
			}
			catch (Exception exc)
			{
				Log.WriteError("Import App Server Data", $"App Server {incomingAppServer.Name} could not be processed.", exc);
				return false;
			}
			return true;
		}

		private async Task<bool> ReactivateAppServer(ModellingAppServer appServer)
		{
			try
			{
				var Variables = new
				{
					id = appServer.Id,
					deleted = false
				};
				await apiConnection.SendQueryAsync<ReturnIdWrapper>(ModellingQueries.setAppServerDeletedState, Variables);
				await ModellingHandlerBase.LogChange(ModellingTypes.ChangeType.Reactivate, ModellingTypes.ModObjectType.AppServer, appServer.Id,
                    $"Reactivate App Server: {appServer.Display()}", apiConnection, userConfig, appServer.AppId, DefaultInit.DoNothing, null, appServer.ImportSource);
				await AppServerHelper.DeactivateOtherSources(apiConnection, userConfig, appServer);
			}
			catch (Exception exc)
			{
				Log.WriteError("Import App Server Data", $"App Server {appServer.Name} could not be reactivated.", exc);
				return false;
			}
			return true;
		}

		private async Task<bool> UpdateAppServerType(ModellingAppServer appServer)
		{
			try
			{
				var Variables = new
				{
					id = appServer.Id,
					customType = 0
				};
				await apiConnection.SendQueryAsync<ReturnIdWrapper>(ModellingQueries.setAppServerType, Variables);
				await ModellingHandlerBase.LogChange(ModellingTypes.ChangeType.Update, ModellingTypes.ModObjectType.AppServer, appServer.Id,
                    $"Update App Server Type: {appServer.Display()}", apiConnection, userConfig, appServer.AppId, DefaultInit.DoNothing, null, appServer.ImportSource);
			}
			catch (Exception exc)
			{
				Log.WriteError("Import App Server Data", $"Type of App Server {appServer.Name} could not be set.", exc);
				return false;
			}
			return true;
		}

		private async Task<bool> UpdateAppServerName(ModellingAppServer appServer, string newName)
		{
			if (appServer.Name != newName)
			{
				try
				{
					var Variables = new
					{
						newName,
						id = appServer.Id,
					};
					await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.setAppServerName, Variables);
					await ModellingHandlerBase.LogChange(ModellingTypes.ChangeType.Update, ModellingTypes.ModObjectType.AppServer, appServer.Id,
                    	$"Update App Server Name: {appServer.Display()}", apiConnection, userConfig, appServer.AppId, DefaultInit.DoNothing, null, appServer.ImportSource);
					Log.WriteWarning("Import App Server Data", $"Name of App Server changed from {appServer.Name} changed to {newName}");
				}
				catch (Exception exc)
				{
					Log.WriteError("Import App Server Data", $"Name of App Server {appServer.Name} could not be set to {newName}.", exc);
					return false;
				}
			}
			return true;
		}

		private async Task<bool> MarkDeletedAppServer(ModellingAppServer appServer)
		{
			try
			{
				var Variables = new
				{
					id = appServer.Id,
					deleted = true
				};
				await apiConnection.SendQueryAsync<ReturnIdWrapper>(ModellingQueries.setAppServerDeletedState, Variables);
				await ModellingHandlerBase.LogChange(ModellingTypes.ChangeType.Update, ModellingTypes.ModObjectType.AppServer, appServer.Id,
                    $"Deactivate App Server: {appServer.Display()}", apiConnection, userConfig, appServer.AppId, DefaultInit.DoNothing, null, appServer.ImportSource);
				await AppServerHelper.ReactivateOtherSource(apiConnection, userConfig, appServer);
			}
			catch (Exception exc)
			{
				Log.WriteError("Import AppServer Data", $"Outdated AppServer {appServer.Name} could not be marked as deleted.", exc);
				return false;
			}
			return true;
		}
	}
}

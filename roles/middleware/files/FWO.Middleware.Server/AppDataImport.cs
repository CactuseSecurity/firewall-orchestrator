using FWO.Logging;
using NetTools;
using FWO.Api.Client;
using FWO.GlobalConstants;
using FWO.Api.Data;
using FWO.Config.Api;
using System.Text.Json;
using FWO.Middleware.RequestParameters;
using FWO.Api.Client.Queries;
using Novell.Directory.Ldap;
using System.Data;
using Microsoft.IdentityModel.Tokens;

namespace FWO.Middleware.Server
{
	/// <summary>
	/// Class handling the App Data Import
	/// </summary>
	public class AppDataImport : DataImportBase
	{
		private List<ModellingImportAppData> importedApps = new();
		private List<FwoOwner> existingApps = new();
		private List<ModellingAppServer> existingAppServers = new();

		private Ldap internalLdap = new();

		private List<Ldap> connectedLdaps = new();
		private string modellerRoleDn = "";
		private string requesterRoleDn = "";
		private string implementerRoleDn = "";
		private string reviewerRoleDn = "";
		List<GroupGetReturnParameters> allGroups = new();


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
				List<string> importfilePathAndNames = JsonSerializer.Deserialize<List<string>>(globalConfig.ImportAppDataPath) ?? throw new Exception("Config Data could not be deserialized.");
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
			internalLdap = connectedLdaps.FirstOrDefault(x => x.IsInternal() && x.HasGroupHandling()) ?? throw new Exception("No internal Ldap with group handling found.");
			modellerRoleDn = $"cn=modeller,{internalLdap.RoleSearchPath}";
			requesterRoleDn = $"cn=requester,{internalLdap.RoleSearchPath}";
			implementerRoleDn = $"cn=implementer,{internalLdap.RoleSearchPath}";
			reviewerRoleDn = $"cn=reviewer,{internalLdap.RoleSearchPath}";
			allGroups = internalLdap.GetAllInternalGroups();
		}

		private async Task<bool> ImportSingleSource(string importfileName)
		{
			try
			{
				ReadFile(importfileName);
				ModellingImportOwnerData? importedOwnerData = JsonSerializer.Deserialize<ModellingImportOwnerData>(importFile) ?? throw new Exception("File could not be parsed.");
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

			existingApps = await apiConnection.SendQueryAsync<List<FwoOwner>>(Api.Client.Queries.OwnerQueries.getOwners);
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
				foreach (var existingApp in existingApps.Where(x => x.ImportSource == incomingApp.ImportSource && x.Active))
				{
					if (importedApps.FirstOrDefault(x => x.Name == existingApp.Name) == null)
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
			if (true)
			{
				userGroupDn = CreateUserGroup(incomingApp);
			}
			else
			{
				// alternatively: simply use an existing usergroup from external LDAP
				// TODO: needs to be implemented
				// userGroupDn = incomingApp.Name + "external-ldap-path";
			}

			var Variables = new
			{
				name = incomingApp.Name,
				dn = incomingApp.MainUser ?? "",
				groupDn = userGroupDn,
				appIdExternal = incomingApp.ExtAppId,
				criticality = incomingApp.Criticality,
				importSource = incomingApp.ImportSource,
				commSvcPossible = false
			};
			ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<NewReturning>(OwnerQueries.newOwner, Variables)).ReturnIds;
			if (returnIds != null)
			{
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
			string userGroupDn = existingApp.GroupDn;
			if (existingApp.GroupDn == null || existingApp.GroupDn == "")
			{
				GroupGetReturnParameters? groupWithSameName = allGroups.FirstOrDefault(x => new DistName(x.GroupDn).Group == GroupName(incomingApp.ExtAppId));
				if (groupWithSameName != null)
				{
					if (userGroupDn == "")
					{
						userGroupDn = groupWithSameName.GroupDn;
					}
					UpdateUserGroup(incomingApp, groupWithSameName.GroupDn);
				}
				else
				{
					userGroupDn = CreateUserGroup(incomingApp);
				}
			}
			else
			{
				UpdateUserGroup(incomingApp, userGroupDn);
			}

			var Variables = new
			{
				id = existingApp.Id,
				name = incomingApp.Name,
				dn = incomingApp.MainUser ?? "",
				groupDn = userGroupDn,
				appIdExternal = incomingApp.ExtAppId,
				criticality = incomingApp.Criticality,
				commSvcPossible = existingApp.CommSvcPossible
			};
			await apiConnection.SendQueryAsync<NewReturning>(OwnerQueries.updateOwner, Variables);
			await ImportAppServers(incomingApp, existingApp.Id);
			return userGroupDn;
		}

		private async Task<bool> DeactivateApp(FwoOwner app)
		{
			try
			{
				await apiConnection.SendQueryAsync<NewReturning>(Api.Client.Queries.OwnerQueries.deactivateOwner, new { id = app.Id });
			}
			catch (Exception exc)
			{
				Log.WriteError("Import App Data", $"Outdated App {app.Name} could not be deactivated.", exc);
				return false;
			}
			return true;
		}

		private static string GroupName(string appName)
		{
			return GlobalConst.kModellerGroup + appName;
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
				foreach (string memberDn in ldap.GetGroupMembers(userGroupDn))
				{
					await UiUserHandler.UpsertUiUser(apiConnection, await ConvertLdapToUiUser(apiConnection, memberDn), false);
				}
			}
		}

		private async Task<UiUser> ConvertLdapToUiUser(ApiConnection apiConnection, string userDn)
		{
			// add the modelling user to local uiuser table for later ref to email address
			UiUser uiUser = new();

			// find the user in all connected ldaps
			foreach (Ldap ldap in connectedLdaps)
			{
				if (!ldap.UserSearchPath.IsNullOrEmpty() && userDn.ToLower().Contains(ldap.UserSearchPath.ToLower()))
				{
					LdapEntry ldapUser = ldap.GetUserDetailsFromLdap(userDn);
					
					if (ldapUser != null)
					{
						// add data from ldap entry to uiUser
						uiUser = new()
						{
							LdapConnection = new UiLdapConnection(),
							Dn = ldapUser.Dn,
							Name = ldap.GetName(ldapUser),
							Firstname = ldap.GetFirstName(ldapUser),
							Lastname = ldap.GetLastName(ldapUser),
							Email = ldap.GetEmail(ldapUser),
							Tenant = await DeriveTenantFromLdap(ldap, ldapUser)							
						};
						uiUser.LdapConnection.Id = ldap.Id;
						return uiUser;			
					}
				}
			}
			return uiUser;

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
			if (!ldap.GlobalTenantName.IsNullOrEmpty() || ldap.TenantLevel > 0)
			{
				if (ldap.TenantLevel > 0)
				{
					// getting tenant via tenant level setting from distinguished name
					tenantName = ldap.GetTenantName(ldapUser);
				}
				else
				{
					if (!ldap.GlobalTenantName.IsNullOrEmpty())
					{
						tenantName = ldap.GlobalTenantName;
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

		private string CreateUserGroup(ModellingImportAppData incomingApp)
		{
			string groupDn = "";
			if (incomingApp.Modellers != null && incomingApp.Modellers.Count > 0
				|| incomingApp.ModellerGroups != null && incomingApp.ModellerGroups.Count > 0)
			{
				string groupName = GroupName(incomingApp.ExtAppId);
				groupDn = internalLdap.AddGroup(groupName, true);
				if (incomingApp.Modellers != null)
				{
					foreach (var modeller in incomingApp.Modellers)
					{
						// add user to internal group:
						internalLdap.AddUserToEntry(modeller, groupDn);
					}
				}
				if (incomingApp.ModellerGroups != null)
				{
					foreach (var modellerGrp in incomingApp.ModellerGroups)
					{
						internalLdap.AddUserToEntry(modellerGrp, groupDn);
					}
				}
				internalLdap.AddUserToEntry(groupDn, modellerRoleDn);
				internalLdap.AddUserToEntry(groupDn, requesterRoleDn);
				internalLdap.AddUserToEntry(groupDn, implementerRoleDn);
				internalLdap.AddUserToEntry(groupDn, reviewerRoleDn);
			}
			return groupDn;
		}

		private string UpdateUserGroup(ModellingImportAppData incomingApp, string groupDn)
		{
			List<string> existingMembers = (allGroups.FirstOrDefault(x => x.GroupDn == groupDn) ?? throw new Exception("Group could not be found.")).Members;
			if (incomingApp.Modellers != null)
			{
				foreach (var modeller in incomingApp.Modellers)
				{
					if (existingMembers.FirstOrDefault(x => x.ToLower() == modeller.ToLower()) == null)
					{
						internalLdap.AddUserToEntry(modeller, groupDn);
					}
				}
			}
			if (incomingApp.ModellerGroups != null)
			{
				foreach (var modellerGrp in incomingApp.ModellerGroups)
				{
					if (existingMembers.FirstOrDefault(x => x.ToLower() == modellerGrp.ToLower()) == null)
					{
						internalLdap.AddUserToEntry(modellerGrp, groupDn);
					}
				}
			}
			foreach (var member in existingMembers)
			{
				if ((incomingApp.Modellers == null || incomingApp.Modellers.FirstOrDefault(x => x.ToLower() == member.ToLower()) == null)
					&& (incomingApp.ModellerGroups == null || incomingApp.ModellerGroups.FirstOrDefault(x => x.ToLower() == member.ToLower()) == null))
				{
					internalLdap.RemoveUserFromEntry(member, groupDn);
				}
			}
			return groupDn;
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
			existingAppServers = await apiConnection.SendQueryAsync<List<ModellingAppServer>>(Api.Client.Queries.ModellingQueries.getImportedAppServers, Variables);
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
			foreach (var existingAppServer in existingAppServers)
			{
				if (incomingApp.AppServers.FirstOrDefault(x => IpAsCidr(x.Ip) == IpAsCidr(existingAppServer.Ip)) == null)
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
				ModellingAppServer? existingAppServer = existingAppServers.FirstOrDefault(x => IpAsCidr(x.Ip) == IpAsCidr(incomingAppServer.Ip));
				if (existingAppServer == null)
				{
					return await NewAppServer(incomingAppServer, appID, impSource);
				}
				else
				{
					if (existingAppServer.IsDeleted)
					{
						return await ReactivateAppServer(existingAppServer);
					}
					if (existingAppServer.CustomType == null)
					{
						return await UpdateAppServerType(existingAppServer);
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

		private async Task<bool> NewAppServer(ModellingImportAppServer incomingAppServer, int appID, string impSource)
		{
			try
			{
				var Variables = new
				{
					name = incomingAppServer.Name,
					appId = appID,
					ip = IpAsCidr(incomingAppServer.Ip),
					ipEnd = incomingAppServer.IpEnd != "" ? IpAsCidr(incomingAppServer.IpEnd) : IpAsCidr(incomingAppServer.Ip),
					importSource = impSource,
					customType = 0
				};
				await apiConnection.SendQueryAsync<NewReturning>(Api.Client.Queries.ModellingQueries.newAppServer, Variables);
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
				await apiConnection.SendQueryAsync<NewReturning>(Api.Client.Queries.ModellingQueries.setAppServerDeletedState, Variables);
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
				await apiConnection.SendQueryAsync<NewReturning>(Api.Client.Queries.ModellingQueries.setAppServerType, Variables);
			}
			catch (Exception exc)
			{
				Log.WriteError("Import App Server Data", $"Type of App Server {appServer.Name} could not be set.", exc);
				return false;
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
				await apiConnection.SendQueryAsync<NewReturning>(Api.Client.Queries.ModellingQueries.setAppServerDeletedState, Variables);
			}
			catch (Exception exc)
			{
				Log.WriteError("Import AppServer Data", $"Outdated AppServer {appServer.Name} could not be marked as deleted.", exc);
				return false;
			}
			return true;
		}

		private static string IpAsCidr(string ip)
		{
			return IPAddressRange.Parse(ip).ToCidrString();
		}
	}
}

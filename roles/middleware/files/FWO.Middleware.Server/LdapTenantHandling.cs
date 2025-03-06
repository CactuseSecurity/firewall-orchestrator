using FWO.Data;
using FWO.Data.Middleware;
using FWO.Encryption;
using FWO.Logging;
using Novell.Directory.Ldap;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

namespace FWO.Middleware.Server
{
	/// <summary>
	/// Class handling the ldap transactions
	/// </summary>
	public partial class Ldap : LdapConnectionBase
	{

		/// <summary>
		/// Add new tenant
		/// </summary>
		/// <returns>true if tenant added</returns>
		public bool AddTenant(string tenantName)
		{
			Log.WriteInfo("Add Tenant", $"Trying to add Tenant: \"{tenantName}\"");
			bool tenantAdded = false;
			try
			{
                using LdapConnection connection = Connect();
                // Authenticate as write user
                TryBind(connection, WriteUser, WriteUserPwd);

				LdapAttributeSet attributeSet = new ()
				{
					new LdapAttribute("objectclass", "organizationalUnit")
				};

                LdapEntry newEntry = new (TenantNameToDn(tenantName), attributeSet);

                try
                {
                    //Add the entry to the directory
                    connection.Add(newEntry);
                    tenantAdded = true;
                    Log.WriteDebug("Add tenant", $"Tenant {tenantName} added in {Address}:{Port}");
                }
                catch (Exception exception)
                {
                    Log.WriteInfo("Add Tenant", $"couldn't add tenant to LDAP {Address}:{Port}: {exception}");
                }
            }
			catch (Exception exception)
			{
				Log.WriteError($"Non-LDAP exception {Address}:{Port}", "Unexpected error while trying to add tenant", exception);
			}
			return tenantAdded;
		}

		/// <summary>
		/// Delete tenant
		/// </summary>
		/// <returns>true if tenant deleted</returns>
		public bool DeleteTenant(string tenantName)
		{
			Log.WriteDebug("Delete Tenant", $"Trying to delete Tenant: \"{tenantName}\" from Ldap");
			bool tenantDeleted = false;
			try
			{
                using LdapConnection connection = Connect();
                // Authenticate as write user
                TryBind(connection, WriteUser, WriteUserPwd);

                try
                {
					string tenantDn = TenantNameToDn(tenantName);
                    //Delete the entry in the directory
                    connection.Delete(tenantDn);
                    tenantDeleted = true;
                    Log.WriteDebug("Delete Tenant", $"tenant {tenantDn} deleted in {Address}:{Port}");
                }
                catch (Exception exception)
                {
                    Log.WriteInfo("Delete Tenant", $"couldn't delete tenant in LDAP {Address}:{Port}: {exception}");
                }
            }
			catch (Exception exception)
			{
				Log.WriteError($"Non-LDAP exception {Address}:{Port}", "Unexpected error while trying to delete tenant", exception);
			}
			return tenantDeleted;
		}

		private string TenantNameToDn(string tenantName)
		{
			return $"ou={tenantName},{UserSearchPath}";
		}

    }
}

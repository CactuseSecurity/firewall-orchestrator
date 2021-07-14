using System.Collections.Generic;
using System.Threading.Tasks;

namespace FWO.Middleware.Client
{
    public class MiddlewareClient
    {
        readonly RequestSender requestSender;

        public MiddlewareClient(string middlewareServerUri)
        {
            requestSender = new RequestSender(middlewareServerUri);
        }

        public async Task<MiddlewareServerResponse> AuthenticateUser(string Username, string Password)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "Username", Username },
                { "Password", Password }
            };

            return await requestSender.SendRequest(parameters, "AuthenticateUser");
        }

        public async Task<MiddlewareServerResponse> CreateInitialJWT()
        {

            Dictionary<string, object> parameters = new Dictionary<string, object> { };

            return await requestSender.SendRequest(parameters, "CreateInitialJWT");
        }

        public async Task<MiddlewareServerResponse> ChangePassword(string UserDn, string oldPassword, string newPassword, string jwt)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "UserDn", UserDn },
                { "oldPassword", oldPassword },
                { "newPassword", newPassword }
            };

            return await requestSender.SendRequest(parameters, "ChangePassword", jwt);
        }

        public async Task<MiddlewareServerResponse> GetAllRoles(string jwt)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {};

            return await requestSender.SendRequest(parameters, "GetAllRoles", jwt);
        }

        public async Task<MiddlewareServerResponse> GetGroups(string Ldap, string SearchPattern, string jwt)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "Ldap", Ldap },
                { "SearchPattern", SearchPattern }
            };

            return await requestSender.SendRequest(parameters, "GetGroups", jwt);
        }

        public async Task<MiddlewareServerResponse> GetInternalGroups(string jwt)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {};

            return await requestSender.SendRequest(parameters, "GetInternalGroups", jwt);
        }

        public async Task<MiddlewareServerResponse> GetUsers(string Ldap, string SearchPattern, string jwt)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "Ldap", Ldap },
                { "SearchPattern", SearchPattern }
            };

            return await requestSender.SendRequest(parameters, "GetUsers", jwt);
        }

        public async Task<MiddlewareServerResponse> AddUser(string Ldap, string Username, string Password, string Email, string jwt)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "Ldap", Ldap },
                { "Username", Username },
                { "Password", Password },
                { "Email", Email }
            };

            return await requestSender.SendRequest(parameters, "AddUser", jwt);
        }

        public async Task<MiddlewareServerResponse> UpdateUser(string Ldap, string Username, string Email, string jwt)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "Ldap", Ldap },
                { "Username", Username },
                { "Email", Email }
            };

            return await requestSender.SendRequest(parameters, "UpdateUser", jwt);
        }

        public async Task<MiddlewareServerResponse> SetPassword(string Ldap, string Username, string Password, string jwt)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "Ldap", Ldap },
                { "Username", Username },
                { "Password", Password }
            };

            return await requestSender.SendRequest(parameters, "SetPassword", jwt);
        }

        public async Task<MiddlewareServerResponse> DeleteUser(string Ldap, string Username, string jwt)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "Ldap", Ldap },
                { "Username", Username }
            };

            return await requestSender.SendRequest(parameters, "DeleteUser", jwt);
        }

        public async Task<MiddlewareServerResponse> AddGroup(string GroupName, string jwt)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "GroupName", GroupName }
            };

            return await requestSender.SendRequest(parameters, "AddGroup", jwt);
        }

        public async Task<MiddlewareServerResponse> UpdateGroup(string OldName, string NewName, string jwt)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "OldName", OldName },
                { "NewName", NewName }
            };

            return await requestSender.SendRequest(parameters, "UpdateGroup", jwt);
        }

        public async Task<MiddlewareServerResponse> DeleteGroup(string GroupName, string jwt)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "GroupName", GroupName }
            };

            return await requestSender.SendRequest(parameters, "DeleteGroup", jwt);
        }

        public async Task<MiddlewareServerResponse> AddUserToRole(string Username, string Role, string jwt)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "Username", Username },
                { "Role", Role }
            };

            return await requestSender.SendRequest(parameters, "AddUserToRole", jwt);
        }

        public async Task<MiddlewareServerResponse> RemoveUserFromRole(string Username, string Role, string jwt)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "Username", Username },
                { "Role", Role }
            };

            return await requestSender.SendRequest(parameters, "RemoveUserFromRole", jwt);
        }

        public async Task<MiddlewareServerResponse> AddUserToGroup(string Username, string Group, string jwt)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "Username", Username },
                { "Group", Group }
            };

            return await requestSender.SendRequest(parameters, "AddUserToGroup", jwt);
        }

        public async Task<MiddlewareServerResponse> RemoveUserFromGroup(string Username, string Group, string jwt)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "Username", Username },
                { "Group", Group }
            };

            return await requestSender.SendRequest(parameters, "RemoveUserFromGroup", jwt);
        }

        public async Task<MiddlewareServerResponse> RemoveUserFromAllEntries(string Username, string jwt)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "Username", Username }
            };

            return await requestSender.SendRequest(parameters, "RemoveUserFromAllEntries", jwt);
        }

        public async Task<MiddlewareServerResponse> AddTenant(string TenantName, string jwt)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "TenantName", TenantName }
            };

            return await requestSender.SendRequest(parameters, "AddTenant", jwt);
        }

        public async Task<MiddlewareServerResponse> DeleteTenant(string TenantName, string jwt)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "TenantName", TenantName }
            };

            return await requestSender.SendRequest(parameters, "DeleteTenant", jwt);
        }
    }
}


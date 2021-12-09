using RestSharp;
using FWO.Middleware.RequestParameters;
using RestSharp.Authenticators;
using RestSharp.Serializers.SystemTextJson;
using System.Text.Json;

namespace FWO.Middleware.Client
{
    public class MiddlewareClient
    {
        readonly RestClient restClient;

        public MiddlewareClient(string middlewareServerUri)
        {
            restClient = new RestClient(middlewareServerUri + "api/");
            restClient.RemoteCertificateValidationCallback += (_, _, _, _) => true;

            JsonSerializerOptions options = new JsonSerializerOptions();
            options.PropertyNameCaseInsensitive = true;
            SystemTextJsonSerializer serializer = new SystemTextJsonSerializer(options);
            restClient.UseSerializer(() => serializer);
        }

        public void SetAuthenticationToken(string jwt)
        {
            restClient.Authenticator = new JwtAuthenticator(jwt);
        }

        public async Task<IRestResponse<string>> AuthenticateUser(AuthenticationTokenGetParameters parameters)
        {
            IRestRequest request = new RestRequest("AuthenticationToken/Get", Method.POST, DataFormat.Json);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<string>(request);
        }

        public async Task<IRestResponse<string>> CreateInitialJWT()
        {
            IRestRequest request = new RestRequest("AuthenticationToken/Get", Method.POST, DataFormat.Json);
            request.AddJsonBody(new object());
            return await restClient.ExecuteAsync<string>(request);
        }

        public async Task<IRestResponse<List<LdapGetUpdateParameters>>> GetLdaps()
        {
            IRestRequest request = new RestRequest("AuthenticationServer", Method.GET, DataFormat.Json);
            request.AddJsonBody(new object());
            return await restClient.ExecuteAsync<List<LdapGetUpdateParameters>>(request);
        }

        public async Task<IRestResponse<int>> AddLdap(LdapAddParameters parameters)
        {
            IRestRequest request = new RestRequest("AuthenticationServer", Method.POST, DataFormat.Json);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<int>(request);
        }

        public async Task<IRestResponse<int>> UpdateLdap(LdapGetUpdateParameters parameters)
        {
            IRestRequest request = new RestRequest("AuthenticationServer", Method.PUT, DataFormat.Json);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<int>(request);
        }

        public async Task<IRestResponse<int>> DeleteLdap(LdapDeleteParameters parameters)
        {
            IRestRequest request = new RestRequest("AuthenticationServer", Method.DELETE, DataFormat.Json);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<int>(request);
        }

        public async Task<IRestResponse<string>> ChangePassword(UserChangePasswordParameters parameters)
        {
            IRestRequest request = new RestRequest("User/EditPassword", Method.PATCH, DataFormat.Json);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<string>(request);
        }

        public async Task<IRestResponse<KeyValuePair<string, List<KeyValuePair<string, string>>>[]>> GetAllRoles()
        {
            IRestRequest request = new RestRequest("Role", Method.GET, DataFormat.Json);
            return await restClient.ExecuteAsync<KeyValuePair<string, List<KeyValuePair<string, string>>>[]>(request);
        }

        public async Task<IRestResponse<List<string>>> GetGroups(GroupGetParameters parameters)
        {
            IRestRequest request = new RestRequest("Group/Get", Method.POST, DataFormat.Json);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<List<string>>(request);
        }

        public async Task<IRestResponse<List<KeyValuePair<string, List<string>>>>> GetInternalGroups()
        {
            IRestRequest request = new RestRequest("Group", Method.GET, DataFormat.Json);
            return await restClient.ExecuteAsync<List<KeyValuePair<string, List<string>>>>(request);
        }

        public async Task<IRestResponse<List<UserGetReturnParameters>>> GetUsers()
        {
            IRestRequest request = new RestRequest("User", Method.GET, DataFormat.Json);
            request.AddJsonBody(new object());
            return await restClient.ExecuteAsync<List<UserGetReturnParameters>>(request);
        }

        public async Task<IRestResponse<List<LdapUserGetReturnParameters>>> GetLdapUsers(LdapUserGetParameters parameters)
        {
            IRestRequest request = new RestRequest("User/Get", Method.POST, DataFormat.Json);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<List<LdapUserGetReturnParameters>>(request);
        }

        public async Task<IRestResponse<int>> AddUser(UserAddParameters parameters)
        {
            IRestRequest request = new RestRequest("User", Method.POST, DataFormat.Json);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<int>(request);
        }

        public async Task<IRestResponse<bool>> UpdateUser(UserEditParameters parameters)
        {
            IRestRequest request = new RestRequest("User", Method.PUT, DataFormat.Json);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<bool>(request);
        }

        public async Task<IRestResponse<string>> SetPassword(UserResetPasswordParameters parameters)
        {
            IRestRequest request = new RestRequest("User/ResetPassword", Method.PATCH, DataFormat.Json);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<string>(request);
        }

        public async Task<IRestResponse<bool>> DeleteUser(UserDeleteParameters parameters)
        {
            IRestRequest request = new RestRequest("User", Method.DELETE, DataFormat.Json);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<bool>(request);
        }

        public async Task<IRestResponse<string>> AddGroup(GroupAddDeleteParameters parameters)
        {
            IRestRequest request = new RestRequest("Group", Method.POST, DataFormat.Json);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<string>(request);
        }

        public async Task<IRestResponse<string>> UpdateGroup(GroupEditParameters parameters)
        {
            IRestRequest request = new RestRequest("Group", Method.PUT, DataFormat.Json);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<string>(request);
        }

        public async Task<IRestResponse<bool>> DeleteGroup(GroupAddDeleteParameters parameters)
        {
            IRestRequest request = new RestRequest("Group", Method.DELETE, DataFormat.Json);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<bool>(request);
        }

        public async Task<IRestResponse<bool>> AddUserToRole(RoleAddDeleteUserParameters parameters)
        {
            IRestRequest request = new RestRequest("Role/User", Method.POST, DataFormat.Json);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<bool>(request);
        }

        public async Task<IRestResponse<bool>> RemoveUserFromRole(RoleAddDeleteUserParameters parameters)
        {
            IRestRequest request = new RestRequest("Role/User", Method.DELETE, DataFormat.Json);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<bool>(request);
        }

        public async Task<IRestResponse<bool>> AddUserToGroup(GroupAddDeleteUserParameters parameters)
        {
            IRestRequest request = new RestRequest("Group/User", Method.POST, DataFormat.Json);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<bool>(request);
        }

        public async Task<IRestResponse<bool>> RemoveUserFromGroup(GroupAddDeleteUserParameters parameters)
        {
            IRestRequest request = new RestRequest("Group/User", Method.DELETE, DataFormat.Json);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<bool>(request);
        }

        public async Task<IRestResponse<bool>> RemoveUserFromAllEntries(UserDeleteAllEntriesParameters parameters)
        {
            IRestRequest request = new RestRequest("User/AllGroupsAndRoles", Method.DELETE, DataFormat.Json);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<bool>(request);
        }

        public async Task<IRestResponse<List<TenantGetReturnParameters>>> GetTenants()
        {
            IRestRequest request = new RestRequest("Tenant", Method.GET, DataFormat.Json);
            request.AddJsonBody(new object());
            return await restClient.ExecuteAsync<List<TenantGetReturnParameters>>(request);
        }

        public async Task<IRestResponse<int>> AddTenant(TenantAddParameters parameters)
        {
            IRestRequest request = new RestRequest("Tenant", Method.POST, DataFormat.Json);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<int>(request);
        }

        public async Task<IRestResponse<bool>> UpdateTenant(TenantEditParameters parameters)
        {
            IRestRequest request = new RestRequest("Tenant", Method.PUT, DataFormat.Json);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<bool>(request);
        }

        public async Task<IRestResponse<bool>> DeleteTenant(TenantDeleteParameters parameters)
        {
            IRestRequest request = new RestRequest("Tenant", Method.DELETE, DataFormat.Json);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<bool>(request);
        }
    }
}

﻿using RestSharp;
using FWO.Middleware.RequestParameters;
using RestSharp.Authenticators;
using RestSharp.Serializers.SystemTextJson;
using System.Text.Json;
using RestSharp.Serializers.NewtonsoftJson;
using Newtonsoft.Json;
using RestSharp.Serializers;
using System.Runtime.CompilerServices;
using System.Data;

namespace FWO.Middleware.Client
{
    public class MiddlewareClient : IDisposable
    {
        private bool disposed = false;
        private RestClient restClient;
        readonly string middlewareServerUri;

        public MiddlewareClient(string middlewareServerUri)
        {
            this.middlewareServerUri = middlewareServerUri;
            restClient = CreateRestClient(authenticator: null);
        }

        private RestClient CreateRestClient(IAuthenticator? authenticator)
        {
            RestClientOptions restClientOptions = new RestClientOptions();
            restClientOptions.RemoteCertificateValidationCallback += (_, _, _, _) => true;
            restClientOptions.BaseUrl = new Uri(middlewareServerUri + "api/");
            restClientOptions.Authenticator = authenticator;
            return new RestClient(restClientOptions, null, ConfigureRestClientSerialization);
        }

        private void ConfigureRestClientSerialization(SerializerConfig config)
        {
            JsonNetSerializer serializer = new JsonNetSerializer(); // Case insensivitive is enabled by default
            config.UseSerializer(() => serializer);
        }

        public void SetAuthenticationToken(string jwt)
        {
            restClient = CreateRestClient(new JwtAuthenticator(jwt));
        }

        public async Task<RestResponse<string>> AuthenticateUser(AuthenticationTokenGetParameters parameters)
        {
            RestRequest request = new RestRequest("AuthenticationToken/Get", Method.Post);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<string>(request);
        }

        public async Task<RestResponse<string>> CreateInitialJWT()
        {
            RestRequest request = new RestRequest("AuthenticationToken/Get", Method.Post);
            request.AddJsonBody(new object());
            return await restClient.ExecuteAsync<string>(request);
        }

        public async Task<RestResponse<int>> TestConnection(LdapGetUpdateParameters parameters)
        {
            RestRequest request = new RestRequest("AuthenticationServer/TestConnection", Method.Get);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<int>(request);
        }

        public async Task<RestResponse<List<LdapGetUpdateParameters>>> GetLdaps()
        {
            RestRequest request = new RestRequest("AuthenticationServer", Method.Get);
            request.AddJsonBody(new object());
            return await restClient.ExecuteAsync<List<LdapGetUpdateParameters>>(request);
        }

        public async Task<RestResponse<int>> AddLdap(LdapAddParameters parameters)
        {
            RestRequest request = new RestRequest("AuthenticationServer", Method.Post);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<int>(request);
        }

        public async Task<RestResponse<int>> UpdateLdap(LdapGetUpdateParameters parameters)
        {
            RestRequest request = new RestRequest("AuthenticationServer", Method.Put);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<int>(request);
        }

        public async Task<RestResponse<int>> DeleteLdap(LdapDeleteParameters parameters)
        {
            RestRequest request = new RestRequest("AuthenticationServer", Method.Delete);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<int>(request);
        }

        public async Task<RestResponse<string>> ChangePassword(UserChangePasswordParameters parameters)
        {
            RestRequest request = new RestRequest("User/EditPassword", Method.Patch);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<string>(request);
        }

        public async Task<RestResponse<List<RoleGetReturnParameters>>> GetAllRoles()
        {
            RestRequest request = new RestRequest("Role", Method.Get);
            return await restClient.ExecuteAsync<List<RoleGetReturnParameters>>(request);
        }

        public async Task<RestResponse<List<string>>> GetGroups(GroupGetParameters parameters)
        {
            RestRequest request = new RestRequest("Group/Get", Method.Post);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<List<string>>(request);
        }

        public async Task<RestResponse<List<GroupGetReturnParameters>>> GetInternalGroups()
        {
            RestRequest request = new RestRequest("Group", Method.Get);
            return await restClient.ExecuteAsync<List<GroupGetReturnParameters>>(request);
        }

        public async Task<RestResponse<List<UserGetReturnParameters>>> GetUsers()
        {
            RestRequest request = new RestRequest("User", Method.Get);
            request.AddJsonBody(new object());
            return await restClient.ExecuteAsync<List<UserGetReturnParameters>>(request);
        }

        public async Task<RestResponse<List<LdapUserGetReturnParameters>>> GetLdapUsers(LdapUserGetParameters parameters)
        {
            RestRequest request = new RestRequest("User/Get", Method.Post);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<List<LdapUserGetReturnParameters>>(request);
        }

        public async Task<RestResponse<int>> AddUser(UserAddParameters parameters)
        {
            RestRequest request = new RestRequest("User", Method.Post);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<int>(request);
        }

        public async Task<RestResponse<bool>> UpdateUser(UserEditParameters parameters)
        {
            RestRequest request = new RestRequest("User", Method.Put);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<bool>(request);
        }

        public async Task<RestResponse<string>> SetPassword(UserResetPasswordParameters parameters)
        {
            RestRequest request = new RestRequest("User/ResetPassword", Method.Patch);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<string>(request);
        }

        public async Task<RestResponse<bool>> DeleteUser(UserDeleteParameters parameters)
        {
            RestRequest request = new RestRequest("User", Method.Delete);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<bool>(request);
        }

        public async Task<RestResponse<string>> AddGroup(GroupAddDeleteParameters parameters)
        {
            RestRequest request = new RestRequest("Group", Method.Post);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<string>(request);
        }

        public async Task<RestResponse<string>> UpdateGroup(GroupEditParameters parameters)
        {
            RestRequest request = new RestRequest("Group", Method.Put);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<string>(request);
        }

        public async Task<RestResponse<bool>> DeleteGroup(GroupAddDeleteParameters parameters)
        {
            RestRequest request = new RestRequest("Group", Method.Delete);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<bool>(request);
        }

        public async Task<RestResponse<bool>> AddUserToRole(RoleAddDeleteUserParameters parameters)
        {
            RestRequest request = new RestRequest("Role/User", Method.Post);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<bool>(request);
        }

        public async Task<RestResponse<bool>> RemoveUserFromRole(RoleAddDeleteUserParameters parameters)
        {
            RestRequest request = new RestRequest("Role/User", Method.Delete);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<bool>(request);
        }

        public async Task<RestResponse<bool>> AddUserToGroup(GroupAddDeleteUserParameters parameters)
        {
            RestRequest request = new RestRequest("Group/User", Method.Post);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<bool>(request);
        }

        public async Task<RestResponse<bool>> RemoveUserFromGroup(GroupAddDeleteUserParameters parameters)
        {
            RestRequest request = new RestRequest("Group/User", Method.Delete);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<bool>(request);
        }

        public async Task<RestResponse<bool>> RemoveUserFromAllEntries(UserDeleteAllEntriesParameters parameters)
        {
            RestRequest request = new RestRequest("User/AllGroupsAndRoles", Method.Delete);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<bool>(request);
        }

        public async Task<RestResponse<List<TenantGetReturnParameters>>> GetTenants()
        {
            RestRequest request = new RestRequest("Tenant", Method.Get);
            request.AddJsonBody(new object());
            return await restClient.ExecuteAsync<List<TenantGetReturnParameters>>(request);
        }

        public async Task<RestResponse<int>> AddTenant(TenantAddParameters parameters)
        {
            RestRequest request = new RestRequest("Tenant", Method.Post);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<int>(request);
        }

        public async Task<RestResponse<bool>> UpdateTenant(TenantEditParameters parameters)
        {
            RestRequest request = new RestRequest("Tenant", Method.Put);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<bool>(request);
        }

        public async Task<RestResponse<bool>> DeleteTenant(TenantDeleteParameters parameters)
        {
            RestRequest request = new RestRequest("Tenant", Method.Delete);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<bool>(request);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;
            if (disposing)
            {
                restClient.Dispose();
                disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~ MiddlewareClient()
        {
            Dispose(false);
        }
    }
}

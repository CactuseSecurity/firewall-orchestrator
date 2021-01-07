using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Linq;
using System.Net;

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

        public async Task<MiddlewareServerResponse> GetAllRoles(string jwt)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {};

            return await requestSender.SendRequest(parameters, "GetAllRoles", jwt);
        }

        public async Task<MiddlewareServerResponse> GetUsers(string Ldap, string jwt)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "Ldap", Ldap }
            };

            return await requestSender.SendRequest(parameters, "GetUsers", jwt);
        }

        public async Task<MiddlewareServerResponse> AddUser(string Username, string Password, string Email, string jwt)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "Username", Username },
                { "Password", Password },
                { "Email", Email }
            };

            return await requestSender.SendRequest(parameters, "AddUser", jwt);
        }

        public async Task<MiddlewareServerResponse> UpdateUser(string Username, string Email, string jwt)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "Username", Username },
                { "Email", Email }
            };

            return await requestSender.SendRequest(parameters, "UpdateUser", jwt);
        }

        public async Task<MiddlewareServerResponse> DeleteUser(string Username, string jwt)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "Username", Username }
            };

            return await requestSender.SendRequest(parameters, "DeleteUser", jwt);
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
    }
}


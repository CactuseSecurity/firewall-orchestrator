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

        public async Task<MiddlewareServerResponse> GetAllRoles()
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {};

            return await requestSender.SendRequest(parameters, "GetAllRoles");
        }

        public async Task<MiddlewareServerResponse> GetUsers(string Ldap)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "Ldap", Ldap }
            };

            return await requestSender.SendRequest(parameters, "GetUsers");
        }

        public async Task<MiddlewareServerResponse> AddUser(string Username)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "Username", Username }
            };

            return await requestSender.SendRequest(parameters, "AddUser");
        }

        public async Task<MiddlewareServerResponse> DeleteUser(string Username)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "Username", Username }
            };

            return await requestSender.SendRequest(parameters, "DeleteUser");
        }

        public async Task<MiddlewareServerResponse> AddUserToRole(string Username, string Role)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "Username", Username },
                { "Role", Role }
            };

            return await requestSender.SendRequest(parameters, "AddUserToRole");
        }

        public async Task<MiddlewareServerResponse> RemoveUserFromRole(string Username, string Role)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "Username", Username },
                { "Role", Role }
            };

            return await requestSender.SendRequest(parameters, "RemoveUserFromRole");
        }
    }
}


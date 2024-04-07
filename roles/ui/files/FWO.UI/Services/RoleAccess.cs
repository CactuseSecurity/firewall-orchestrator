using System.Net;
using FWO.GlobalConstants;
using FWO.Api.Data;
using FWO.Middleware.Client;
using FWO.Middleware.RequestParameters;
using RestSharp;

namespace FWO.Ui.Services
{
    public class RoleAccess
    {
        static public async Task<List<Role>> GetRolesFromInternalLdap(MiddlewareClient middlewareClient)
        {
            List<Role> roles = new List<Role>();
            RestResponse<List<RoleGetReturnParameters>> middlewareServerResponse = await middlewareClient.GetAllRoles();
            if (middlewareServerResponse.StatusCode == HttpStatusCode.OK && middlewareServerResponse.Data != null)
            {
                foreach (var ldapRole in middlewareServerResponse.Data)
                {
                    Role role = new Role() { Dn = ldapRole.Role, Name = (new DistName(ldapRole.Role)).Role };
                    foreach (var roleAttr in ldapRole.Attributes)
                    {
                        if (roleAttr.Key == "description")
                        {
                            role.Description = roleAttr.Value;
                        }
                        else if (roleAttr.Key == "user")
                        {
                            UiUser newUser = new UiUser() { Dn = roleAttr.Value, Name = (new DistName(roleAttr.Value)).UserName };
                            role.Users.Add(newUser);
                        }
                    }
                    roles.Add(role);
                }
            }
            return roles;
        }

        static public async Task<List<UiUser>> GetRoleMembers(MiddlewareClient middlewareClient, string roleName)
        {
            List<UiUser> users = new List<UiUser>();
            List<Role> roles = await GetRolesFromInternalLdap(middlewareClient);
            Role? role = roles.FirstOrDefault(x => x.Name == roleName);
            if(role != null)
            {
                users = role.Users;
            }
            return users;
        }
    }
}

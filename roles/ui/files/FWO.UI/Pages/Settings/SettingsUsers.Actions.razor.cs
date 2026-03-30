using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Logging;
using FWO.Middleware.Client;
using FWO.Services;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Components;
using RestSharp;

namespace FWO.Ui.Pages.Settings
{
    public partial class SettingsUsers
    {
    private void SetAvailableTenants(UiLdapConnection? newLdap)
    {
        try
        {
            selectedLdap = newLdap;
            availableTenants = [];
            if(selectedLdap != null)
            {
                if (selectedLdap.TenantId != null && selectedLdap.TenantId != 0)
                {
                    Tenant? ten = tenants.FirstOrDefault(x => x.Id == selectedLdap.TenantId);
                    if (ten != null) availableTenants.Add(ten);
                }
                else if (selectedLdap.TenantLevel > 0)
                {
                    availableTenants = tenants;
                }
            }
            if(selectedTenant != null && !availableTenants.Contains(selectedTenant) && availableTenants.Count > 0)
            {
                selectedTenant = availableTenants.First();
            }
        }
        catch (Exception exception)
        {
            DisplayMessageInUi(exception, userConfig.GetText("get_tenant_data"), "", false);
        }
    }

    private void AddUser()
    {
        try
        {
            AddMode = true;
            newUser = new UiUser(){ Email = "" };
            selectedLdap = writableLdaps.FirstOrDefault();
            SetAvailableTenants(selectedLdap);
            selectedTenant = availableTenants.FirstOrDefault();
            selectedGroup = null;
            newUser.Groups = [];
            selectedRole = null;
            newUser.Roles = [];
            Edit(newUser);
        }
        catch (Exception exception)
        {
            DisplayMessageInUi(exception, userConfig.GetText("add_user"), "", false);
        }
    }

    private void Clone(UiUser user)
    {
        try
        {
            AddMode = true;
            newUser = new UiUser(user);
            newUser.Password = "";
            newUser.Firstname = user.Firstname;
            newUser.Lastname = user.Lastname;
            newUser.Email = user.Email;
            selectedLdap = (newUser.LdapConnection != null ? newUser.LdapConnection : writableLdaps.FirstOrDefault());
            SetAvailableTenants(selectedLdap);
            selectedTenant = newUser.Tenant;
            selectedGroup = (newUser.Groups != null ? groups.FirstOrDefault(x => x.Name == newUser.Groups.FirstOrDefault()) : null);
            newUser.Groups = new List<string>();
            selectedRole = (newUser.Roles != null ? roles.FirstOrDefault(x => x.Name == newUser.Roles.FirstOrDefault()) : null);
            newUser.Roles = new List<string>();
            Edit(newUser);
        }
        catch (Exception exception)
        {
            DisplayMessageInUi(exception, userConfig.GetText("add_user"), "", false);
        }
    }

    private void Edit(UiUser user)
    {
        actUser = new UiUser(user);
        if(actUser.LdapConnection == null)
        {
            actUser.LdapConnection = new UiLdapConnection();
        }
        if (actUser.Tenant != null && actUser.Tenant.Id == GlobalConst.kTenant0Id && actUser.LdapConnection.GlobalTenantName != null && actUser.LdapConnection.GlobalTenantName != "")
        {
            actUser.Tenant.Name = actUser.LdapConnection.GlobalTenantName;
        }
        EditMode = true;
    }

    private void SynchronizeUserData(UiUser user)
    {
        if(selectedLdap != null)
        {
            user.LdapConnection = selectedLdap;
            user.Tenant = (selectedLdap.TenantLevel > 0 ? selectedTenant : null);
            // todo: further dn specification maybe in ldapconnection?
            user.Dn = (selectedLdap.Type == (int)LdapType.ActiveDirectory ? "cn=": "uid=") + user.Name +
                (selectedLdap.TenantLevel > 0 ? ",ou=" + (selectedTenant?.Id == GlobalConst.kTenant0Id && selectedLdap.GlobalTenantName != null && selectedLdap.GlobalTenantName != "" ? selectedLdap.GlobalTenantName : selectedTenant?.Name) : "") + "," + selectedLdap.UserSearchPath;
        }
        user.Groups = [];
        if(selectedGroup != null)
        {
            user.Groups.Add(selectedGroup.Name);
        }
        user.PasswordMustBeChanged = true;
        user.Roles = [];
        if(selectedRole != null)
        {
            user.Roles.Add(selectedRole.Name);
            if (selectedRole.Name == Roles.Auditor)
            {
                user.PasswordMustBeChanged = false;
            }
        }
    }

    private async Task Save()
    {
        try
        {
            if (actUser.Sanitize())
            {
                DisplayMessageInUi(null, userConfig.GetText("add_user"), userConfig.GetText("U0001"), true);
            }
            if (AddMode)
            {
                SynchronizeUserData(actUser);

                if (CheckValues())
                {
                    // insert new user to ldap
                    UserAddParameters parameters = new UserAddParameters
                    {
                        Email = actUser.Email,
                        Firstname = actUser.Firstname,
                        Lastname = actUser.Lastname,
                        LdapId = actUser.LdapConnection.Id,
                        Password = actUser.Password,
                        UserDn = actUser.Dn,
                        TenantId = (actUser.Tenant != null ? actUser.Tenant.Id : 0),
                        PwChangeRequired = actUser.PasswordMustBeChanged
                    };
                    RestResponse<int> middlewareServerResponse = await middlewareClient.AddUser(parameters);
                    if (middlewareServerResponse.StatusCode != HttpStatusCode.OK || middlewareServerResponse.Data == 0)
                    {
                        DisplayMessageInUi(null, userConfig.GetText("add_user"), userConfig.GetText("E5213"), true);
                    }
                    else
                    {
                        actUser.DbId = middlewareServerResponse.Data;
                        uiUsers.Add(actUser);
                        await AddUserToGroupsInLdap(actUser);
                        await AddUserToRolesInLdap(actUser);
                        AddMode = false;
                        EditMode = false;

                        Log.WriteAudit(
                            Title: $"Users Settings",
                            Text: $"Added User: {actUser.Name} (DN: {actUser.Dn})",
                            UserName: userConfig.User.Name,
                            UserDN: userConfig.User.Dn);
                    }
                }
            }
            else
            {
                // Update existing user in ldap --> currently only email; TODO: add first and last name
                UserEditParameters parameters = new UserEditParameters {Email = actUser.Email, LdapId = actUser.LdapConnection.Id, UserId = actUser.DbId };
                RestResponse<bool> middlewareServerResponse = await middlewareClient.UpdateUser(parameters);
                if (middlewareServerResponse.StatusCode != HttpStatusCode.OK || middlewareServerResponse.Data == false)
                {
                    DisplayMessageInUi(null, userConfig.GetText("update_user"), userConfig.GetText("E5214"), true);
                }
                else
                {
                    uiUsers[uiUsers.FindIndex(x => x.DbId == actUser.DbId)].Email = actUser.Email;
                    EditMode = false;

                    Log.WriteAudit(
                        Title: $"Users Settings",
                        Text: $"Edited User: {actUser.Name} (DN: {actUser.Dn})",
                        UserName: userConfig.User.Name,
                        UserDN: userConfig.User.Dn);
                }
            }



            AnalyseSampleUsers();
        }
        catch (Exception exception)
        {
            DisplayMessageInUi(exception, userConfig.GetText("save_user"), "", true);
        }
    }

    private bool CheckValues()
    {
        string errorMsg;
        if (actUser.Name == null || actUser.Name == "" || actUser.Password == null || actUser.Password == "" || actUser.Roles.Count == 0)
        {
            DisplayMessageInUi(null, userConfig.GetText("add_user"), userConfig.GetText("E5211"), true);
            return false;
        }
        if (!PasswordPolicy.CheckPolicy(actUser.Password, globalConfig, userConfig, out errorMsg))
        {
            DisplayMessageInUi(null, userConfig.GetText("add_user"), errorMsg, true);
            return false;
        }
        if (actUser.LdapConnection.TenantLevel > 0 && (actUser.Tenant == null || !tenants.Exists(x => x.Name == actUser.Tenant.Name)))
        {
            DisplayMessageInUi(null, userConfig.GetText("add_user"), userConfig.GetText("E5212"), true);
            return false;
        }
        if (uiUsers.Exists(x => x.Dn == actUser.Dn))
        {
            DisplayMessageInUi(null, userConfig.GetText("add_user"), userConfig.GetText("E5210"), true);
            return false;
        }
        return true;
    }

    private async Task AddUserToGroupsInLdap(UiUser user)
    {
        try
        {
            foreach(string groupName in user.Groups)
            {
                UserGroup? group = groups.FirstOrDefault(x => x.Name == groupName);
                if (group != null)
                {
                    GroupAddDeleteUserParameters addUserParameters = new GroupAddDeleteUserParameters { GroupDn = group.Dn, UserDn = user.Dn };
                    RestResponse<bool> middlewareServerResponse = await middlewareClient.AddUserToGroup(addUserParameters);
                    if ((middlewareServerResponse.StatusCode != HttpStatusCode.OK) || (middlewareServerResponse.Data == false))
                    {
                        DisplayMessageInUi(null, userConfig.GetText("assign_user_to_group"), userConfig.GetText("E5242"), true);
                    }
                }
            }
        }
        catch (System.Exception exception)
        {
            DisplayMessageInUi(exception, userConfig.GetText("assign_user_to_group"), "", true);
        }
    }

    private async Task AddUserToRolesInLdap(UiUser user)
    {
        try
        {
            foreach(string roleName in user.Roles)
            {
                Role? role = roles.FirstOrDefault(x => x.Name == roleName);
                if (role != null)
                {
                    RoleAddDeleteUserParameters parameters = new RoleAddDeleteUserParameters { Role = role.Dn, UserDn = user.Dn };
                    RestResponse<bool> middlewareServerResponse = await middlewareClient.AddUserToRole(parameters);
                    if ((middlewareServerResponse.StatusCode != HttpStatusCode.OK) || (middlewareServerResponse.Data == false))
                    {
                        DisplayMessageInUi(null, userConfig.GetText("assign_user_group_to_role"), userConfig.GetText("E5255"), true);
                    }
                }
            }
        }
        catch (System.Exception exception)
        {
            DisplayMessageInUi(exception, userConfig.GetText("assign_user_group_to_role"), "", true);
        }
    }

    private void RequestDelete(UiUser user)
    {
        try
        {
            actUser = user;
            if (actUser.Dn == userConfig.User.Dn)
            {
                DisplayMessageInUi(null, userConfig.GetText("delete_user"), userConfig.GetText("E5215"), true);
            }
            else
            {
                deleteMessage = userConfig.GetText("U5201") + actUser.Name + "?";
                if (!actUser.IsInternal())
                {
                    deleteMessage += userConfig.GetText("U5202");
                }
                DeleteMode = true;
            }
        }
        catch (Exception exception)
        {
            DisplayMessageInUi(exception, userConfig.GetText("delete_user"), "", true);
        }
    }

    private async Task Delete()
    {
        try
        {
            // delete user from Ldap
            UserDeleteParameters parameters = new UserDeleteParameters
            {
                LdapId = actUser.LdapConnection.Id,
                UserId = actUser.DbId
            };
            RestResponse<bool> middlewareServerResponse = await middlewareClient.DeleteUser(parameters);
            if (middlewareServerResponse.StatusCode != HttpStatusCode.OK || middlewareServerResponse.Data == false)
            {
                DisplayMessageInUi(null, userConfig.GetText("delete_user"), userConfig.GetText("E5216"), true);
            }
            else
            {
                uiUsers.Remove(actUser);
                DeleteMode = false;

                Log.WriteAudit(
                    Title: $"Users Settings",
                    Text: $"Deleted User: {actUser.Name} (DN: {actUser.Dn})",
                    UserName: userConfig.User.Name,
                    UserDN: userConfig.User.Dn);
            }
        }
        catch (Exception exception)
        {
            DisplayMessageInUi(exception, userConfig.GetText("delete_user"), "", true);
        }
        StateHasChanged();
    }

    private async Task CleanUpUser(UiUser user)
    {
        try
        {
            var Variables = new { id = user.DbId };
            int delId = (await apiConnection.SendQueryAsync<ReturnId>(AuthQueries.deleteUser, Variables)).DeletedId;
            if (delId == user.DbId)
            {
                uiUsers.Remove(user);
            }
        }
        catch (Exception exception)
        {
            DisplayMessageInUi(exception, userConfig.GetText("delete_user_local"), "", false);
        }
    }

    private void RequestResetPassword(UiUser user)
    {
        if(!(new DistName(user.Dn)).IsInternal())
        {
            DisplayMessageInUi(null, userConfig.GetText("reset_password"), userConfig.GetText("E5217"), true);
        }
        else
        {
            actUser = user;
            actUser.Password = "";
            ResetPasswordMode = true;
        }
    }

    private async Task ResetPassword()
    {
        try
        {
            string errorMsg = "";
            if (actUser.Password == null || actUser.Password == "")
            {
                DisplayMessageInUi(null, userConfig.GetText("reset_password"), userConfig.GetText("E5218"), true);
            }
            else if (!PasswordPolicy.CheckPolicy(actUser.Password, globalConfig, userConfig, out errorMsg))
            {
                DisplayMessageInUi(null, userConfig.GetText("reset_password"), errorMsg, true);
            }
            else
            {
                UserResetPasswordParameters parameters = new UserResetPasswordParameters
                {
                    LdapId = actUser.LdapConnection.Id,
                    NewPassword = actUser.Password,
                    UserId = actUser.DbId
                };
                RestResponse<string> middlewareServerResponse = await middlewareClient.SetPassword(parameters);
                if (middlewareServerResponse.StatusCode != HttpStatusCode.OK)
                {
                    DisplayMessageInUi(null, userConfig.GetText("reset_password"), userConfig.GetText("E5219"), true);
                }
                else
                {
                    if (middlewareServerResponse.Data != null)
                    {
                        errorMsg = middlewareServerResponse.Data;
                    }
                    if(errorMsg != "")
                    {
                        DisplayMessageInUi(null, userConfig.GetText("reset_password"), errorMsg, true);
                    }

                    Log.WriteAudit(
                        Title: $"Users Settings",
                        Text: $"Password reset User: {actUser.Name} (DN: {actUser.Dn})",
                        UserName: userConfig.User.Name,
                        UserDN: userConfig.User.Dn);
                }
                ResetPasswordMode = false;
            }
        }
        catch (Exception exception)
        {
            DisplayMessageInUi(exception, userConfig.GetText("reset_password"), "", true);
        }
    }

    private void RequestRemoveSampleData()
    {
        if (sampleUsers.Exists(user => user.DbId == userConfig.User.DbId))
        {
            sampleRemoveMessage = userConfig.GetText("E5220");
            sampleRemoveAllowed = false;
        }
        else
        {
            sampleRemoveMessage = userConfig.GetText("U5203");
            sampleRemoveAllowed = true;
        }
        SampleRemoveMode = true;
    }

    private async Task RemoveSampleData()
    {
        showSampleRemoveButton = false;
        SampleRemoveMode = false;
        workInProgress = true;
        foreach (var user in sampleUsers)
        {
            try
            {
                UserDeleteAllEntriesParameters parameters = new UserDeleteAllEntriesParameters { UserId = user.DbId };
                RestResponse<bool> middlewareServerResponse = await middlewareClient.RemoveUserFromAllEntries(parameters);
                if (middlewareServerResponse.StatusCode != HttpStatusCode.OK || middlewareServerResponse.Data == false)
                {
                    DisplayMessageInUi(null, userConfig.GetText("remove_sample_data"), userConfig.GetText("E5221"), true);
                    showSampleRemoveButton = true;
                }
                else
                {
                    actUser = user;
                    await Delete();
                }
            }
            catch (System.Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("remove_sample_data"), "", false);
            }
        }
        workInProgress = false;
        StateHasChanged();
    }

    private void Cancel()
    {
        AddMode = false;
        EditMode = false;
        DeleteMode = false;
        SampleRemoveMode = false;
        ResetPasswordMode = false;
    }    }
}

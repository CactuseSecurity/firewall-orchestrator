using System.Text.Json.Serialization; 
using Newtonsoft.Json;
using FWO.Middleware.RequestParameters;

namespace FWO.Api.Data
{
    public class UiUser
    {
        [JsonProperty("uiuser_username"), JsonPropertyName("uiuser_username")]
        public string Name { get; set; } = "";

        [JsonProperty("uiuser_id"), JsonPropertyName("uiuser_id")]
        public int DbId { get; set; }

        [JsonProperty("uuid"), JsonPropertyName("uuid")]
        public string Dn { get; set; } = "";

        public string Password { get; set; } = "";

        [JsonProperty("uiuser_email"), JsonPropertyName("uiuser_email")]
        public string? Email { get; set; }

        [JsonProperty("uiuser_first_name"), JsonPropertyName("uiuser_first_name")]
        public string? Firstname { get; set; }

        [JsonProperty("uiuser_last_name"), JsonPropertyName("uiuser_last_name")]
        public string? Lastname { get; set; }

        [JsonProperty("tenant"), JsonPropertyName("tenant")]
        public Tenant? Tenant { get; set;}

        [JsonProperty("uiuser_language"), JsonPropertyName("uiuser_language")]
        public string? Language { get; set; }

        [JsonProperty("uiuser_last_login"), JsonPropertyName("uiuser_last_login")]
        public DateTime? LastLogin { get; set; }

        [JsonProperty("uiuser_last_password_change"), JsonPropertyName("uiuser_last_password_change")]
        public DateTime? LastPasswordChange { get; set; }

        [JsonProperty("uiuser_password_must_be_changed"), JsonPropertyName("uiuser_password_must_be_changed")]
        public bool PasswordMustBeChanged { get; set; }

        [JsonProperty("ldap_connection"), JsonPropertyName("ldap_connection")]
        public UiLdapConnection LdapConnection { get; set;} = new ();

        public string Jwt { get; set; } = "";
        public List<string> Roles { get; set; } = [];
        public List<string> Groups { get; set; } = [];
        public List<int> Ownerships { get; set; } = [];


        public UiUser()
        {
            Tenant = new ();
            LdapConnection = new ();
        }
        
        public UiUser(UiUser user)
        {
            Name = user.Name;
            DbId = user.DbId;
            Dn = user.Dn;
            if (user.Tenant != null)
            {
                Tenant = new Tenant(user.Tenant);
            }
            Password = user.Password;
            Firstname = user.Firstname;
            Lastname = user.Lastname;
            Email = user.Email;
            Language = user.Language;
            Groups = user.Groups;
            Roles = user.Roles;
            Ownerships = user.Ownerships;
            if (user.LdapConnection != null)
            {
                LdapConnection = new UiLdapConnection(user.LdapConnection);
            }
        }

        public UiUser(UserGetReturnParameters userGetReturnParameters)
        {
            Name = userGetReturnParameters.Name;
            DbId = userGetReturnParameters.UserId;
            Dn = userGetReturnParameters.UserDn;
            Email = userGetReturnParameters.Email;
            Firstname = userGetReturnParameters.Firstname;
            Lastname = userGetReturnParameters.Lastname;
            if (userGetReturnParameters.TenantId != 0)
            {
                Tenant = new (){Id = userGetReturnParameters.TenantId};
            }
            Language = userGetReturnParameters.Language;
            LastLogin = userGetReturnParameters.LastLogin;
            LastPasswordChange = userGetReturnParameters.LastPasswordChange;
            PasswordMustBeChanged = userGetReturnParameters.PwChangeRequired;
            LdapConnection = new (){Id = userGetReturnParameters.LdapId};
        }

        public bool IsInternal()
        {
            return new DistName(Dn).IsInternal();
        }

        public bool Sanitize()
        {
            bool shortened = false;
            Name = Sanitizer.SanitizeLdapNameMand(Name, ref shortened);
            Email = Sanitizer.SanitizeOpt(Email, ref shortened);
            Firstname = Sanitizer.SanitizeOpt(Firstname, ref shortened);
            Lastname = Sanitizer.SanitizeOpt(Lastname, ref shortened);
            Password = Sanitizer.SanitizePasswMand(Password, ref shortened);
            return shortened;
        }

        public UserGetReturnParameters ToApiParams()
        {
            return new UserGetReturnParameters
            {
                Name = this.Name,
                UserId = this.DbId,
                UserDn = this.Dn,
                Email = this.Email,
                Firstname = this.Firstname,
                Lastname = this.Lastname,
                TenantId = this.Tenant != null ? this.Tenant.Id : 0,
                Language = this.Language,
                LastLogin = this.LastLogin,
                LastPasswordChange = this.LastPasswordChange,
                PwChangeRequired = this.PasswordMustBeChanged,
                LdapId = this.LdapConnection.Id
            };
        }

        public string RoleList()
        {
            return string.Join(", ", Roles);
        }

        public string GroupList()
        {
            return string.Join(", ", Groups);
        }
    }
}

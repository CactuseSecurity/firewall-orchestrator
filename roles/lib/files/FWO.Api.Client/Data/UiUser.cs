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
        public UiLdapConnection LdapConnection { get; set;} = new UiLdapConnection();

        public string DefaultRole { get; set; } = "";

        public List<string>? Roles { get; set; }

        public string Jwt { get; set; } = "";

        public List<string>? Groups { get; set; }

        public UiUser()
        {
            Tenant = new Tenant();
            LdapConnection = new UiLdapConnection();
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
            Email = user.Email;
            Language = user.Language;
            if (user.Groups != null)
            {
                Groups = user.Groups;
            }
            if (user.Roles != null)
            {
                Roles = user.Roles;
            }
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
            if (userGetReturnParameters.TenantId != 0)
            {
                Tenant = new Tenant(){Id = userGetReturnParameters.TenantId};
            }
            Language = userGetReturnParameters.Language;
            LastLogin = userGetReturnParameters.LastLogin;
            LastPasswordChange = userGetReturnParameters.LastPasswordChange;
            PasswordMustBeChanged = userGetReturnParameters.PwChangeRequired;
            LdapConnection = new UiLdapConnection(){Id = userGetReturnParameters.LdapId};
        }

        public bool isInternal()
        {
            return new DistName(Dn).IsInternal();
        }

        public bool Sanitize()
        {
            bool shortened = false;
            Name = Sanitizer.SanitizeLdapNameMand(Name, ref shortened);
            Email = Sanitizer.SanitizeOpt(Email, ref shortened);
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
                TenantId = (this.Tenant != null ? this.Tenant.Id : 0),
                Language = this.Language,
                LastLogin = this.LastLogin,
                LastPasswordChange = this.LastPasswordChange,
                PwChangeRequired = this.PasswordMustBeChanged,
                LdapId = this.LdapConnection.Id
            };
        }
    }
}

using System.Text.Json.Serialization;
using FWO.Middleware.RequestParameters;

namespace FWO.Api.Data
{
    public class UiUser
    {
        [JsonPropertyName("uiuser_username")]
        public string Name { get; set; } = "";

        [JsonPropertyName("uiuser_id")]
        public int DbId { get; set; }

        [JsonPropertyName("uuid")]
        public string Dn { get; set; } = "";

        public string Password { get; set; } = "";

        [JsonPropertyName("uiuser_email")]
        public string? Email { get; set; }

        [JsonPropertyName("tenant")]
        public Tenant? Tenant { get; set;}

        [JsonPropertyName("uiuser_language")]
        public string? Language { get; set; }

        [JsonPropertyName("uiuser_last_login")]
        public DateTime? LastLogin { get; set; }

        [JsonPropertyName("uiuser_last_password_change")]
        public DateTime? LastPasswordChange { get; set; }

        [JsonPropertyName("uiuser_password_must_be_changed")]
        public bool PasswordMustBeChanged { get; set; }

        [JsonPropertyName("ldap_connection")]
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

        public void setNamesFromDn()
        {
            DistName distname = new DistName(Dn);
            Name = distname.UserName;
            Tenant = new Tenant();
            Tenant.Name = distname.getTenant();
        }

        public bool isInternal()
        {
            return new DistName(Dn).IsInternal();
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

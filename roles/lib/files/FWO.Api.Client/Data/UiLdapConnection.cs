using System.Text.Json.Serialization; 
using Newtonsoft.Json;
using FWO.Middleware.RequestParameters;

namespace FWO.Api.Data
{
    public class UiLdapConnection : LdapConnectionBase
    {
        private string name = "";
        
        [JsonProperty("ldap_name"), JsonPropertyName("ldap_name")]
        public string Name 
        { 
            get
            {
                // for compatibility: take hostname if not filled
                return ((name != null && name != "") ? name : Host());
            }
            set
            {
                name = value;
            } 
        }

        public string TenantIdAsString
        {
            get => TenantId?.ToString()?? "null";
            set => TenantId = value == "null" ? null : int.Parse(value);
        }

        public UiLdapConnection()
        {}

        public UiLdapConnection(LdapGetUpdateParameters ldapGetUpdateParameters) : base(ldapGetUpdateParameters)
        {
            Name = (ldapGetUpdateParameters.Name != null ? ldapGetUpdateParameters.Name : "");
        }

        public UiLdapConnection(UiLdapConnection ldapConnection)
        {
            Id = ldapConnection.Id;
            Name = ldapConnection.Name;
            Address = ldapConnection.Address;
            Port = ldapConnection.Port;
            Type = ldapConnection.Type;
            PatternLength = ldapConnection.PatternLength;
            SearchUser = ldapConnection.SearchUser;
            Tls = ldapConnection.Tls;
            TenantLevel = ldapConnection.TenantLevel;
            SearchUserPwd = ldapConnection.SearchUserPwd;
            UserSearchPath = ldapConnection.UserSearchPath;
            RoleSearchPath = ldapConnection.RoleSearchPath;
            GroupSearchPath = ldapConnection.GroupSearchPath;
            WriteUser = ldapConnection.WriteUser;
            WriteUserPwd = ldapConnection.WriteUserPwd;
            TenantId = ldapConnection.TenantId;
            GlobalTenantName = ldapConnection.GlobalTenantName;
        }

        public void Sanitize()
        {
            Name = Sanitizer.SanitizeMand(Name);
            Address = Sanitizer.SanitizeMand(Address);
            SearchUser = Sanitizer.SanitizeLdapPathOpt(SearchUser);
            UserSearchPath = Sanitizer.SanitizeLdapPathOpt(UserSearchPath);
            RoleSearchPath = Sanitizer.SanitizeLdapPathOpt(RoleSearchPath);
            GroupSearchPath = Sanitizer.SanitizeLdapPathOpt(GroupSearchPath);
            WriteUser = Sanitizer.SanitizeLdapPathOpt(WriteUser);
            GlobalTenantName = Sanitizer.SanitizeOpt(GlobalTenantName);
            SearchUserPwd = Sanitizer.SanitizePasswOpt(SearchUserPwd);
            WriteUserPwd = Sanitizer.SanitizePasswOpt(WriteUserPwd);
        }

        public LdapGetUpdateParameters ToApiParams()
        {
            return new LdapGetUpdateParameters
            {
                Id = this.Id,
                Name = this.Name,
                Address = this.Address,
                Port = this.Port,
                Type = this.Type,
                PatternLength = this.PatternLength,
                SearchUser = this.SearchUser,
                Tls = this.Tls,
                TenantLevel = this.TenantLevel,
                SearchUserPwd = this.SearchUserPwd,
                SearchpathForUsers = this.UserSearchPath,
                SearchpathForRoles = this.RoleSearchPath,
                SearchpathForGroups = this.GroupSearchPath,
                WriteUser = this.WriteUser,
                WriteUserPwd = this.WriteUserPwd,
                TenantId = this.TenantId,
                GlobalTenantName = this.GlobalTenantName
            };
        }
    }
}

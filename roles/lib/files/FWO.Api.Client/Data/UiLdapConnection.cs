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
            Active = ldapConnection.Active;
        }

        public bool Sanitize()
        {
            bool shortened = false;
            Name = Sanitizer.SanitizeMand(Name, ref shortened);
            Address = Sanitizer.SanitizeMand(Address, ref shortened);
            SearchUser = Sanitizer.SanitizeLdapPathOpt(SearchUser, ref shortened);
            UserSearchPath = Sanitizer.SanitizeLdapPathOpt(UserSearchPath, ref shortened);
            RoleSearchPath = Sanitizer.SanitizeLdapPathOpt(RoleSearchPath, ref shortened);
            GroupSearchPath = Sanitizer.SanitizeLdapPathOpt(GroupSearchPath, ref shortened);
            WriteUser = Sanitizer.SanitizeLdapPathOpt(WriteUser, ref shortened);
            GlobalTenantName = Sanitizer.SanitizeOpt(GlobalTenantName, ref shortened);
            SearchUserPwd = Sanitizer.SanitizePasswOpt(SearchUserPwd, ref shortened);
            WriteUserPwd = Sanitizer.SanitizePasswOpt(WriteUserPwd, ref shortened);
            return shortened;
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
                GlobalTenantName = this.GlobalTenantName,
                Active = this.Active
            };
        }
    }
}

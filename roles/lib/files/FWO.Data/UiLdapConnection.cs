using FWO.Data.Middleware;
using Newtonsoft.Json;
using System.Text.Json.Serialization; 

namespace FWO.Data
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
                return (name != null && name != "") ? name : Host();
            }
            set
            {
                name = value;
            } 
        }

        public UiLdapConnection()
        {}

        public UiLdapConnection(LdapGetUpdateParameters ldapGetUpdateParameters) : base(ldapGetUpdateParameters)
        {
            Name = ldapGetUpdateParameters.Name ?? "";
        }

        public UiLdapConnection(UiLdapConnection ldapConnection) : base(ldapConnection)
        {
            Name = ldapConnection.Name;
        }

        public override bool Sanitize()
        {
            bool shortened = base.Sanitize();
            Name = Sanitizer.SanitizeMand(Name, ref shortened);
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
                WritepathForGroups = this.GroupWritePath,
                WriteUser = this.WriteUser,
                WriteUserPwd = this.WriteUserPwd,
                TenantId = this.TenantId,
                GlobalTenantName = this.GlobalTenantName,
                Active = this.Active
            };
        }
    }
}

using System.Text.Json.Serialization;

namespace FWO.Api.Data
{
    public class UiLdapConnection : LdapConnectionBase
    {
        private string name = "";
        
        [JsonPropertyName("ldap_name")]
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
        }
    }
}

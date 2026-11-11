using FWO.Basics;
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

        /// <summary>
        /// The servers as one text for display and input, separated by commas.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
        public string AddressText
        {
            get => string.Join(", ", Addresses);
            set => Addresses = ParseAddresses(value);
        }

        public UiLdapConnection()
        { }

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
            Name = Name.SanitizeMand(ref shortened);
            return shortened;
        }

        public LdapGetUpdateParameters ToApiParams()
        {
            return new LdapGetUpdateParameters
            {
                Id = this.Id,
                Name = this.Name,
                Addresses = [.. this.Addresses],
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

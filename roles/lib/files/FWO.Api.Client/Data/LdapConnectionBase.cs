using System.Text.Json.Serialization;
using FWO.Middleware.RequestParameters;

namespace FWO.Api.Data
{
    public enum LdapType
    {
        Default = 0,
        ActiveDirectory = 1,
        OpenLdap = 2
    }

    public class LdapConnectionBase
    {
        [JsonPropertyName("ldap_connection_id")]
        public int Id { get; set; }

        [JsonPropertyName("ldap_server")]
        public string Address { get; set; } = "";

        [JsonPropertyName("ldap_port")]
        public int Port { get; set; }

        [JsonPropertyName("ldap_type")]
        public int Type { get; set; }

        [JsonPropertyName("ldap_pattern_length")]
        public int PatternLength { get; set; }

        [JsonPropertyName("ldap_search_user")]
        public string? SearchUser { get; set; }

        [JsonPropertyName("ldap_tls")]
        public bool Tls { get; set; }

        [JsonPropertyName("ldap_tenant_level")]
        public int TenantLevel { get; set; }

        [JsonPropertyName("ldap_search_user_pwd")]
        public string? SearchUserPwd { get; set; }

        [JsonPropertyName("ldap_searchpath_for_users")]
        public string? UserSearchPath { get; set; }

        [JsonPropertyName("ldap_searchpath_for_roles")]
        public string? RoleSearchPath { get; set; }

        [JsonPropertyName("ldap_searchpath_for_groups")]
        public string? GroupSearchPath { get; set; }

        [JsonPropertyName("ldap_write_user")]
        public string? WriteUser { get; set; }

        [JsonPropertyName("ldap_write_user_pwd")]
        public string? WriteUserPwd { get; set; }

        [JsonPropertyName("tenant_id")]
        public int? TenantId { get; set; }

        [JsonPropertyName("ldap_global_tenant_name")]
        public string? GlobalTenantName { get; set; }

        public LdapConnectionBase()
        {}

        public LdapConnectionBase(LdapGetUpdateParameters ldapGetUpdateParameters)
        {
            Id = ldapGetUpdateParameters.Id;
            Address = ldapGetUpdateParameters.Address;
            Port = ldapGetUpdateParameters.Port;
            Type = ldapGetUpdateParameters.Type;
            PatternLength = ldapGetUpdateParameters.PatternLength;
            SearchUser = ldapGetUpdateParameters.SearchUser;
            Tls = ldapGetUpdateParameters.Tls;
            TenantLevel = ldapGetUpdateParameters.TenantLevel;
            SearchUserPwd = ldapGetUpdateParameters.SearchUserPwd;
            UserSearchPath = ldapGetUpdateParameters.SearchpathForUsers;
            RoleSearchPath = ldapGetUpdateParameters.SearchpathForRoles;
            GroupSearchPath = ldapGetUpdateParameters.SearchpathForGroups;
            WriteUser = ldapGetUpdateParameters.WriteUser;
            WriteUserPwd = ldapGetUpdateParameters.WriteUserPwd;
            TenantId = ldapGetUpdateParameters.TenantId;
            GlobalTenantName = ldapGetUpdateParameters.GlobalTenantName;
        }

        public string Host()
        {
            return (Address != "" ? Address + ":" + Port : "");
        }
        
        public bool IsWritable()
        {
            return (WriteUser != null && WriteUser != "");
        }

        public bool HasGroupHandling()
        {
            return (GroupSearchPath != null && GroupSearchPath != "");
        }

        public bool HasRoleHandling()
        {
            return (RoleSearchPath != null && RoleSearchPath != "");
        }

        public bool IsInternal()
        {
            return ((new DistName(UserSearchPath)).IsInternal());
        }
    }
}

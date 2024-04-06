using System.Text.Json.Serialization; 
using Newtonsoft.Json; 
using System.Reflection.Metadata.Ecma335;

using FWO.Middleware.RequestParameters;
using FWO.Encryption;

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
        [JsonProperty("ldap_connection_id"), JsonPropertyName("ldap_connection_id")]
        public int Id { get; set; }

        [JsonProperty("ldap_server"), JsonPropertyName("ldap_server")]
        public string Address { get; set; } = "";

        [JsonProperty("ldap_port"), JsonPropertyName("ldap_port")]
        public int Port { get; set; }

        [JsonProperty("ldap_type"), JsonPropertyName("ldap_type")]
        public int Type { get; set; }

        [JsonProperty("ldap_pattern_length"), JsonPropertyName("ldap_pattern_length")]
        public int PatternLength { get; set; }

        [JsonProperty("ldap_search_user"), JsonPropertyName("ldap_search_user")]
        public string? SearchUser { get; set; }

        [JsonProperty("ldap_tls"), JsonPropertyName("ldap_tls")]
        public bool Tls { get; set; }

        [JsonProperty("ldap_tenant_level"), JsonPropertyName("ldap_tenant_level")]
        public int TenantLevel { get; set; }

        [JsonProperty("ldap_search_user_pwd"), JsonPropertyName("ldap_search_user_pwd")]
        public string? SearchUserPwd { get; set; }

        [JsonProperty("ldap_searchpath_for_users"), JsonPropertyName("ldap_searchpath_for_users")]
        public string? UserSearchPath { get; set; }

        [JsonProperty("ldap_searchpath_for_roles"), JsonPropertyName("ldap_searchpath_for_roles")]
        public string? RoleSearchPath { get; set; }

        [JsonProperty("ldap_searchpath_for_groups"), JsonPropertyName("ldap_searchpath_for_groups")]
        public string? GroupSearchPath { get; set; }

        [JsonProperty("ldap_write_user"), JsonPropertyName("ldap_write_user")]
        public string? WriteUser { get; set; }

        [JsonProperty("ldap_write_user_pwd"), JsonPropertyName("ldap_write_user_pwd")]
        public string? WriteUserPwd { get; set; }

        [JsonProperty("tenant_id"), JsonPropertyName("tenant_id")]
        public int? TenantId { get; set; }

        [JsonProperty("ldap_global_tenant_name"), JsonPropertyName("ldap_global_tenant_name")]
        public string? GlobalTenantName { get; set; }

        [JsonProperty("active"), JsonPropertyName("active")]
        public bool Active { get; set; } = true;

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
            if (ldapGetUpdateParameters.SearchUserPwd != null)
            {
                try 
                {
                    SearchUserPwd = AesEnc.Decrypt(ldapGetUpdateParameters.SearchUserPwd, AesEnc.GetMainKey());
                }
                catch
                {
                    // assuming we found an unencrypted password
                    SearchUserPwd = ldapGetUpdateParameters.SearchUserPwd;
                }
            }
            UserSearchPath = ldapGetUpdateParameters.SearchpathForUsers;
            RoleSearchPath = ldapGetUpdateParameters.SearchpathForRoles;
            GroupSearchPath = ldapGetUpdateParameters.SearchpathForGroups;
            WriteUser = ldapGetUpdateParameters.WriteUser;

            if (ldapGetUpdateParameters.WriteUserPwd != null)
            {
                try 
                {
                    WriteUserPwd = AesEnc.Decrypt(ldapGetUpdateParameters.WriteUserPwd, AesEnc.GetMainKey());
                }
                catch
                {
                    // assuming we found an unencrypted password
                    WriteUserPwd = ldapGetUpdateParameters.WriteUserPwd;
                }
            }

            TenantId = ldapGetUpdateParameters.TenantId;
            GlobalTenantName = ldapGetUpdateParameters.GlobalTenantName;
            Active = ldapGetUpdateParameters.Active;
        }

        public string Host()
        {
            return Address != "" ? Address + ":" + Port : "";
        }
        
        public bool IsWritable()
        {
            return WriteUser != null && WriteUser != "";
        }

        public bool HasGroupHandling()
        {
            return GroupSearchPath != null && GroupSearchPath != "";
        }

        public bool HasRoleHandling()
        {
            return RoleSearchPath != null && RoleSearchPath != "";
        }

        public bool IsInternal()
        {
            return new DistName(UserSearchPath).IsInternal();
        }
    }
}

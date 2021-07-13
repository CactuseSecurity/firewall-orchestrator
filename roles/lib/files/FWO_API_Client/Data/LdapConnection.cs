using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Api.Data
{
    public enum LdapType
    {
        Default = 0,
        ActiveDirectory = 1,
        OpenLdap = 2
    }

    public class UiLdapConnection
    {
        [JsonPropertyName("ldap_connection_id")]
        public int Id { get; set; }

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

        [JsonPropertyName("ldap_server")]
        public string Address { get; set; }

        [JsonPropertyName("ldap_port")]
        public int Port { get; set; }

        [JsonPropertyName("ldap_type")]
        public int Type { get; set; }

        [JsonPropertyName("ldap_pattern_length")]
        public int PatternLength { get; set; }

        [JsonPropertyName("ldap_search_user")]
        public string SearchUser { get; set; }

        [JsonPropertyName("ldap_tls")]
        public bool Tls { get; set; }

        [JsonPropertyName("ldap_tenant_level")]
        public int TenantLevel { get; set; }

        [JsonPropertyName("ldap_search_user_pwd")]
        public string SearchUserPwd { get; set; }

        [JsonPropertyName("ldap_searchpath_for_users")]
        public string UserSearchPath { get; set; }

        [JsonPropertyName("ldap_searchpath_for_roles")]
        public string RoleSearchPath { get; set; }

        [JsonPropertyName("ldap_searchpath_for_groups")]
        public string GroupSearchPath { get; set; }

        [JsonPropertyName("ldap_write_user")]
        public string WriteUser { get; set; }

        [JsonPropertyName("ldap_write_user_pwd")]
        public string WriteUserPwd { get; set; }

        [JsonPropertyName("tenant_id")]
        public int? TenantId { get; set; }

        public string TenantIdAsString
        {
            get => TenantId?.ToString()?? "null";
            set => TenantId = value == "null" ? null :(int?)int.Parse(value);
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

        public string Host()
        {
            return ((Address != null && Address != "") ? Address + ":" + Port : "");
        }
        
        public bool IsWritable()
        {
            return (WriteUser != null && WriteUser != "");
        }

        public bool IsInternal()
        {
            return ((new DistName(UserSearchPath)).IsInternal());
        }
    }
}

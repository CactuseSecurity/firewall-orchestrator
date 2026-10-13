using FWO.Basics;
using FWO.Data.Middleware;
using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data
{
    public enum LdapType
    {
        Default = 0,
        ActiveDirectory = 1,
        OpenLdap = 2
    }

    public class LdapConnectionBase
    {
        /// <summary>Maximum number of servers of one LDAP connection.</summary>
        public const int kMaxServers = 10;
        /// <summary>Maximum length of a server name (the maximum length of a DNS name).</summary>
        public const int kMaxServerLength = 253;
        private static readonly char[] kAddressSeparators = [',', ';', ' ', '\t', '\n', '\r'];

        [JsonProperty("ldap_connection_id"), JsonPropertyName("ldap_connection_id")]
        public int Id { get; set; }

        /// <summary>
        /// Servers of the directory (names or ip addresses), tried in this order. Several servers are replicas of one
        /// directory and share all other settings of the connection, including the port.
        /// </summary>
        [JsonProperty("ldap_server"), JsonPropertyName("ldap_server")]
        public List<string> Addresses { get; set; } = [];

        [JsonProperty("ldap_port"), JsonPropertyName("ldap_port")]
        public int Port { get; set; }

        [JsonProperty("ldap_type"), JsonPropertyName("ldap_type")]
        public int Type { get; set; }

        [JsonProperty("ldap_pattern_length"), JsonPropertyName("ldap_pattern_length")]
        public int PatternLength { get; set; }

        [JsonProperty("ldap_search_user"), JsonPropertyName("ldap_search_user")]
        public string SearchUser { get; set; } = "";

        [JsonProperty("ldap_tls"), JsonPropertyName("ldap_tls")]
        public bool Tls { get; set; }

        [JsonProperty("ldap_tenant_level"), JsonPropertyName("ldap_tenant_level")]
        public int TenantLevel { get; set; }

        [JsonProperty("ldap_search_user_pwd"), JsonPropertyName("ldap_search_user_pwd")]
        public string SearchUserPwd { get; set; } = "";

        [JsonProperty("ldap_searchpath_for_users"), JsonPropertyName("ldap_searchpath_for_users")]
        public string? UserSearchPath { get; set; }

        [JsonProperty("ldap_searchpath_for_roles"), JsonPropertyName("ldap_searchpath_for_roles")]
        public string? RoleSearchPath { get; set; }

        [JsonProperty("ldap_searchpath_for_groups"), JsonPropertyName("ldap_searchpath_for_groups")]
        public string? GroupSearchPath { get; set; }

        [JsonProperty("ldap_writepath_for_groups"), JsonPropertyName("ldap_writepath_for_groups")]
        public string? GroupWritePath { get; set; }

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
        { }

        public LdapConnectionBase(LdapConnectionBase ldapConnection)
        {
            Id = ldapConnection.Id;
            Addresses = [.. ldapConnection.Addresses];
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
            GroupWritePath = ldapConnection.GroupWritePath;
            WriteUser = ldapConnection.WriteUser;
            WriteUserPwd = ldapConnection.WriteUserPwd;
            TenantId = ldapConnection.TenantId;
            GlobalTenantName = ldapConnection.GlobalTenantName;
            Active = ldapConnection.Active;
        }

        public LdapConnectionBase(LdapGetUpdateParameters ldapGetUpdateParameters)
        {
            Id = ldapGetUpdateParameters.Id;
            Addresses = [.. ldapGetUpdateParameters.Addresses];
            Port = ldapGetUpdateParameters.Port;
            Type = ldapGetUpdateParameters.Type;
            PatternLength = ldapGetUpdateParameters.PatternLength;
            SearchUser = ldapGetUpdateParameters.SearchUser ?? "";
            Tls = ldapGetUpdateParameters.Tls;
            TenantLevel = ldapGetUpdateParameters.TenantLevel;
            SearchUserPwd = ldapGetUpdateParameters.SearchUserPwd ?? "";
            UserSearchPath = ldapGetUpdateParameters.SearchpathForUsers;
            RoleSearchPath = ldapGetUpdateParameters.SearchpathForRoles;
            GroupSearchPath = ldapGetUpdateParameters.SearchpathForGroups;
            GroupWritePath = ldapGetUpdateParameters.WritepathForGroups;
            WriteUser = ldapGetUpdateParameters.WriteUser;
            WriteUserPwd = ldapGetUpdateParameters.WriteUserPwd;
            TenantId = ldapGetUpdateParameters.TenantId;
            GlobalTenantName = ldapGetUpdateParameters.GlobalTenantName;
            Active = ldapGetUpdateParameters.Active;
        }

        public virtual bool Sanitize()
        {
            bool shortened = false;
            Addresses = SanitizeAddresses(Addresses, ref shortened);
            SearchUser = SearchUser.SanitizeLdapPathOpt(ref shortened) ?? "";
            UserSearchPath = UserSearchPath.SanitizeLdapPathOpt(ref shortened);
            RoleSearchPath = RoleSearchPath.SanitizeLdapPathOpt(ref shortened);
            GroupSearchPath = GroupSearchPath.SanitizeLdapPathOpt(ref shortened);
            GroupWritePath = GroupWritePath.SanitizeLdapPathOpt(ref shortened);
            WriteUser = WriteUser.SanitizeLdapPathOpt(ref shortened);
            GlobalTenantName = GlobalTenantName.SanitizeOpt(ref shortened);
            SearchUserPwd = SearchUserPwd.SanitizePasswOpt(ref shortened) ?? "";
            WriteUserPwd = WriteUserPwd.SanitizePasswOpt(ref shortened);
            return shortened;
        }

        /// <summary>
        /// Lists the servers of the connection with their port, e.g. for display and logs.
        /// </summary>
        /// <returns>The servers as "address:port", separated by commas; empty if no server is set.</returns>
        public string Host()
        {
            return FormatHosts(Addresses, Port);
        }

        /// <summary>
        /// Lists servers with their port.
        /// </summary>
        /// <param name="addresses">Names or ip addresses of the servers.</param>
        /// <param name="port">Port shared by the servers.</param>
        /// <returns>The servers as "address:port", separated by commas; empty if no server is given.</returns>
        public static string FormatHosts(IEnumerable<string> addresses, int port)
        {
            return string.Join(", ", addresses.Select(address => $"{address}:{port}"));
        }

        /// <summary>
        /// Splits a text listing servers, separated by commas, semicolons or blanks, into the server list.
        /// </summary>
        /// <param name="addressText">Text as entered, e.g. "ldap1.example.com, ldap2.example.com".</param>
        /// <returns>The servers in the given order, without empty entries and duplicates.</returns>
        public static List<string> ParseAddresses(string? addressText)
        {
            return (addressText ?? "")
                .Split(kAddressSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<string> SanitizeAddresses(List<string>? addresses, ref bool shortened)
        {
            List<string> sanitizedAddresses = [];
            foreach (string address in addresses ?? [])
            {
                string sanitizedAddress = address.SanitizeMand(ref shortened);
                if (sanitizedAddress != "" && !sanitizedAddresses.Contains(sanitizedAddress, StringComparer.OrdinalIgnoreCase))
                {
                    sanitizedAddresses.Add(sanitizedAddress);
                }
            }
            return sanitizedAddresses;
        }

        public bool IsWritable()
        {
            return WriteUser != null && WriteUser != "";
        }

        public bool HasGroupHandling()
        {
            return (GroupSearchPath != null && GroupSearchPath != "")
                || (GroupWritePath != null && GroupWritePath != "");
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

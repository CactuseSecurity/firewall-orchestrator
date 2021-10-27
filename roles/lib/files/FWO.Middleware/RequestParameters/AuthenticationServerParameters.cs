using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FWO.Middleware.RequestParameters
{
    public class LdapAddParameters
    {
        [JsonPropertyName("address")]
        public string Address { get; set; }

        [JsonPropertyName("port")]
        public string Port { get; set; } = "636";

        [JsonPropertyName("searchUser")]
        public string SearchUser { get; set; }

        [JsonPropertyName("tls")]
        public string Tls { get; set; }

        [JsonPropertyName("tenantLevel")]
        public string TenantLevel { get; set; }

        [JsonPropertyName("searchUserPwd")]
        public string SearchUserPwd { get; set; }

        [JsonPropertyName("searchpathForUsers")]
        public string SearchpathForUsers { get; set; }

        [JsonPropertyName("searchpathForRoles")]
        public string SearchpathForRoles { get; set; }

        [JsonPropertyName("writeUser")]
        public string WriteUser { get; set; }

        [JsonPropertyName("writeUserPwd")]
        public string WriteUserPwd { get; set; }

        [JsonPropertyName("tenantId")]
        public string TenantId { get; set; }
    }
}

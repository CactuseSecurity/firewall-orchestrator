using System.Text.Json.Serialization; 
using Newtonsoft.Json;


namespace FWO.Middleware.Server
{
    /// <summary>
    /// Structure for imported app data 
    /// </summary>
    public class ModellingImportAppData
    {
        /// <summary>
        /// App Name
        /// </summary>
        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        /// <summary>
        /// External Id of App
        /// </summary>
        [JsonProperty("app_id_external"), JsonPropertyName("app_id_external")]
        public string ExtAppId { get; set; } = "";

        /// <summary>
        /// List of allowed modellers (Dn)
        /// </summary>
        [JsonProperty("modellers"), JsonPropertyName("modellers")]
        public List<string> Modellers { get; set; } = new();

        /// <summary>
        /// List of Ldap Groups of allowed modellers (Dn)
        /// </summary>
        [JsonProperty("modeller_groups"), JsonPropertyName("modeller_groups")]
        public List<string> ModellerGroups { get; set; } = new();

        /// <summary>
        /// Criticality of App
        /// </summary>
        [JsonProperty("criticality"), JsonPropertyName("criticality")]
        public string? Criticality { get; set; }

        /// <summary>
        /// Source of App import
        /// </summary>
        [JsonProperty("import_source"), JsonPropertyName("import_source")]
        public string ImportSource { get; set; } = "";

        /// <summary>
        /// App Servers of App
        /// </summary>
        [JsonProperty("app_servers"), JsonPropertyName("app_servers")]
        public List<ModellingImportAppServer> AppServers { get; set; } = new();
    }
    
    /// <summary>
    /// Structure for imported app server 
    /// </summary>
    public class ModellingImportAppServer
    {
        /// <summary>
        /// App Server Name
        /// </summary>
        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        /// <summary>
        /// App Server Subnet
        /// </summary>
        [JsonProperty("subnet"), JsonPropertyName("subnet")]
        public string Subnet { get; set; } = "";

        /// <summary>
        /// App Server Ip
        /// </summary>
        [JsonProperty("ip"), JsonPropertyName("ip")]
        public string Ip { get; set; } = "";
    }
}

using System.Text.Json.Serialization;
using FWO.Data.Modelling;
using Newtonsoft.Json;


namespace FWO.Middleware.Server
{
    /// <summary>
    /// Structure for imported owner data 
    /// </summary>
    public class ModellingImportOwnerData
    {
        /// <summary>
        /// List of all Owners
        /// </summary>
        [JsonProperty("owners"), JsonPropertyName("owners")]
        public List<ModellingImportAppData>? Owners { get; set; }
    }

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
        /// Main User (Dn)
        /// </summary>
        [JsonProperty("main_user"), JsonPropertyName("main_user")]
        public string? MainUser { get; set; } = "";

        /// <summary>
        /// List of allowed modellers (Dn)
        /// </summary>
        [JsonProperty("modellers"), JsonPropertyName("modellers")]
        public List<string>? Modellers { get; set; } = [];

        /// <summary>
        /// List of Ldap Groups of allowed modellers (Dn): (currently handled same as modellers)
        /// </summary>
        [JsonProperty("modeller_groups"), JsonPropertyName("modeller_groups")]
        public List<string>? ModellerGroups { get; set; } = [];

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
        /// Recertification interval
        /// </summary>
        [JsonProperty("recert_period_days"), JsonPropertyName("recert_period_days")]
        public int? RecertInterval { get; set; }

        /// <summary>
        /// First Recertification interval (currently not regarded)
        /// </summary>
        [JsonProperty("days_until_first_recert"), JsonPropertyName("days_until_first_recert")]
        public int? FirstRecertInterval { get; set; }

        /// <summary>
        /// Recertification active
        /// </summary>
        [JsonProperty("recert_active"), JsonPropertyName("recert_active")]
        public bool RecertActive { get; set; } = false;

        /// <summary>
        /// App Servers of App
        /// </summary>
        [JsonProperty("app_servers"), JsonPropertyName("app_servers")]
        public List<ModellingImportAppServer> AppServers { get; set; } = [];
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
        /// App Server Ip
        /// </summary>
        [JsonProperty("ip"), JsonPropertyName("ip")]
        public string Ip { get; set; } = "";

        /// <summary>
        /// App Server IpEnd
        /// </summary>
        [JsonProperty("ip_end"), JsonPropertyName("ip_end")]
        public string IpEnd { get; set; } = "";

        /// <summary>
        /// Conversion to ModellingAppServer
        /// </summary>
        public ModellingAppServer ToModellingAppServer()
        {
            return new ModellingAppServer()
            {
                Name = Name,
                Ip = Ip,
                IpEnd = IpEnd
            };
        }
    }
}

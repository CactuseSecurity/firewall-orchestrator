using Newtonsoft.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.DeviceAutoDiscovery
{
    public class FmApiStatus
    {
        [JsonProperty("code"), JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonProperty("message"), JsonPropertyName("message")]
        public string Message { get; set; } = "";
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////////

    public class FmApiTopLevelHelper
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("status"), JsonPropertyName("status")]
        public FmApiStatus Status { get; set; } = new();

        [JsonProperty("result"), JsonPropertyName("result")]
        public List<FmApiDataHelper> Result { get; set; } = [];
    }

    public class FmApiDataHelper
    {
        [JsonProperty("data"), JsonPropertyName("data")]
        public List<Adom> AdomList { get; set; } = [];
    }

    public class Adom

    {
        [JsonProperty("oid"), JsonPropertyName("oid")]
        public int Oid { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("uuid"), JsonPropertyName("uuid")]
        public string Uid { get; set; } = "";

        [JsonProperty("devices"), JsonPropertyName("devices")]
        public List<FortiGate> DeviceList { get; set; } = [];

        // public List<Package> Packages = new List<Package>();
        // public List<Assignment> Assignments { get; set; } = [];
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////////

    public class FmApiTopLevelHelperDev
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("status"), JsonPropertyName("status")]
        public FmApiStatus Status { get; set; } = new();

        [JsonProperty("result"), JsonPropertyName("result")]
        public List<FmApiDataHelperDev> Result { get; set; } = [];
    }

    public class FmApiDataHelperDev
    {
        [JsonProperty("data"), JsonPropertyName("data")]
        public List<FortiGate> FortiGates { get; set; } = [];
    }
    public class FortiGate
    {
        [JsonProperty("oid"), JsonPropertyName("oid")]
        public int Oid { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("hostname"), JsonPropertyName("hostname")]
        public string Hostname { get; set; } = "";

        [JsonProperty("mgt_vdom"), JsonPropertyName("mgt_vdom")]
        public string MgtVdom { get; set; } = "";

        [JsonProperty("vdom"), JsonPropertyName("vdom")]
        public List<Vdom> VdomList { get; set; } = [];
    }
    public class Vdom
    {
        [JsonProperty("oid"), JsonPropertyName("oid")]
        public int Oid { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";
    }

    public class SessionAuthInfo
    {
        [JsonProperty("session"), JsonPropertyName("session")]
        public string SessionId { get; set; } = "";
    }
}

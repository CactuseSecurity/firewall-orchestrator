using RestSharp;
using System.Text.Json;
using FWO.Api.Data;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using FWO.Logging;
using RestSharp.Serializers.NewtonsoftJson;
using RestSharp.Serializers;

namespace FWO.Rest.Client
{
    public class CheckPointClient
    {
        readonly RestClient restClient;

        public CheckPointClient(Management manager)
        {
            RestClientOptions restClientOptions = new();
            restClientOptions.RemoteCertificateValidationCallback += (_, _, _, _) => true;
            restClientOptions.BaseUrl = new Uri("https://" + manager.Hostname + ":" + manager.Port + "/web_api/");
            restClient = new RestClient(restClientOptions, null, ConfigureRestClientSerialization);
        }

        private void ConfigureRestClientSerialization(SerializerConfig config)
        {
            JsonNetSerializer serializer = new(); // Case insensivitive is enabled by default
            config.UseSerializer(() => serializer);
        }

        public async Task<RestResponse<CpSessionAuthInfo>> AuthenticateUser(string? user, string? pwd, string? domain)
        {
            if (user == null || user == "")
            {
                Log.WriteWarning("Autodiscovery", $"GetDomains got empty user string, aborting");
                return new RestResponse<CpSessionAuthInfo>(new RestRequest());
            }
            pwd ??= "";
            domain ??= "";
            Dictionary<string, string> body = new()
            {
                { "user", user },
                { "password", pwd }
            };
            if (domain != "")
                body.Add("domain", domain);
            RestRequest request = new("login", Method.Post);
            request.AddJsonBody(body);
            request.AddHeader("Content-Type", "application/json");
            return await restClient.ExecuteAsync<CpSessionAuthInfo>(request);
        }

        public async Task<RestResponse<CpSessionAuthInfo>> DeAuthenticateUser(string session)
        {
            RestRequest request = new("logout", Method.Post);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("X-chkp-sid", session);
            request.AddJsonBody(new { });
            return await restClient.ExecuteAsync<CpSessionAuthInfo>(request);
        }

        public async Task<RestResponse<CpDomainHelper>> GetDomains(string session)
        {
            RestRequest request = new("show-domains", Method.Post);
            request.AddHeader("X-chkp-sid", session);
            request.AddHeader("Content-Type", "application/json");
            Dictionary<string, string> body = new()
            {
                { "details-level", "full" }
            };
            request.AddJsonBody(body);
            return await restClient.ExecuteAsync<CpDomainHelper>(request);
        }

        public async Task<List<CpDevice>> GetGateways(string session, string ManagementType)
        // session id pins this session to a specific domain (if domain was given during login) 
        {
            RestRequest request = new("show-gateways-and-servers", Method.Post);
            request.AddHeader("X-chkp-sid", session);
            request.AddHeader("Content-Type", "application/json");
            Dictionary<string, string> body = new()
            {
                { "details-level", "full" }
            };
            request.AddJsonBody(body);
            Log.WriteDebug("Autodiscovery", $"using CP REST API call 'show-gateways-and-servers'");
            List<string> gwTypes = ["simple-gateway", "simple-cluster", "CpmiVsNetobj", "CpmiVsClusterNetobj", "CpmiGatewayPlain", "CpmiGatewayCluster", "CpmiVsxClusterNetobj", "CpmiVsxNetobj"];

            // getting all gateways of this management 
            RestResponse<CpDeviceHelper> devices = await restClient.ExecuteAsync<CpDeviceHelper>(request);
            if (devices.Data != null)
            {
                foreach (CpDevice dev in devices.Data.DeviceList)
                {
                    if (gwTypes.Contains(dev.CpDevType))
                    {
                        if (!dev.Policy.AccessPolicyInstalled)
                            Log.WriteWarning("Autodiscovery", $"found gateway '{dev.Name}' without access policy");
                    }
                }
                return devices.Data.DeviceList;
            }
            return [];
        }

    }

    public class CpSessionAuthInfo
    {
        [JsonProperty("sid"), JsonPropertyName("sid")]
        public string SessionId { get; set; } = "";
    }

    public class CpApiStatus
    {
        [JsonProperty("code"), JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonProperty("message"), JsonPropertyName("message")]
        public string Message { get; set; } = "";
    }

    public class CpDomainHelper
    {
        [JsonProperty("objects"), JsonPropertyName("objects")]
        public List<Domain> DomainList { get; set; } = [];

        [JsonProperty("total"), JsonPropertyName("total")]
        public int Total { get; set; }
    }

    public class Domain
    {
        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("uid"), JsonPropertyName("uid")]
        public string Uid { get; set; } = "";

        [JsonProperty("domain-type"), JsonPropertyName("domain-type")]
        public string DomainType { get; set; } = "";
    }

    public class CpPackagesHelper
    {
        [JsonProperty("packages"), JsonPropertyName("packages")]
        public List<CpPackage> PackageList { get; set; } = [];
    }

    public class CpDeviceHelper
    {
        [JsonProperty("objects"), JsonPropertyName("objects")]
        public List<CpDevice> DeviceList { get; set; } = [];
    }

    public class CpDevice
    {
        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("uid"), JsonPropertyName("uid")]
        public string Uid { get; set; } = "";

        [JsonProperty("type"), JsonPropertyName("type")]
        public string CpDevType { get; set; } = "";

        [JsonProperty("domain"), JsonPropertyName("domain")]
        public Domain Domain { get; set; } = new Domain();

        [JsonProperty("policy"), JsonPropertyName("policy")]
        public CpPolicy Policy { get; set; } = new CpPolicy();

        public CpPackage Package { get; set; } = new CpPackage();

        public string LocalLayerName { get; set; } = "";
        public string GlobalLayerName { get; set; } = "";

        public List<CpAccessLayer> Layers { get; set; } = [];

    }

    public class DevObjectsHelper
    {
        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";
    }

    public class CpPolicy
    {
        [JsonProperty("access-policy-installed"), JsonPropertyName("access-policy-installed")]
        public bool AccessPolicyInstalled { get; set; }

        [JsonProperty("access-policy-name"), JsonPropertyName("access-policy-name")]
        public string AccessPolicyName { get; set; } = "";
    }

    public class CpPackage
    {
        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("uid"), JsonPropertyName("uid")]
        public string Uid { get; set; } = "";

        [JsonProperty("domain"), JsonPropertyName("domain")]
        public Domain Domain { get; set; } = new Domain();

        [JsonProperty("access-layers"), JsonPropertyName("access-layers")]
        public List<CpAccessLayer> CpAccessLayers { get; set; } = [];
    }

    public class CpAccessLayer
    {
        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("uid"), JsonPropertyName("uid")]
        public string Uid { get; set; } = "";

        [JsonProperty("type"), JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonProperty("parent-layer"), JsonPropertyName("parent-layer")]
        public string ParentLayer { get; set; } = "";

        [JsonProperty("domain"), JsonPropertyName("domain")]
        public Domain Domain { get; set; } = new Domain();

        [JsonProperty("firewall"), JsonPropertyName("firewall")]
        public bool IsFirewallEnabled { get; set; }

        [JsonProperty("shared"), JsonPropertyName("shared")]
        public bool IsShared { get; set; }

        [JsonProperty("applications-and-url-filtering"), JsonPropertyName("applications-and-url-filtering")]
        public bool IsApplicationsAndUrlFilteringEnabled { get; set; }

        [JsonProperty("content-awareness"), JsonPropertyName("content-awareness")]
        public bool IsContentAwarenessEnabled { get; set; }

        [JsonProperty("mobile-access"), JsonPropertyName("mobile-access")]
        public bool IsMobileAccessEnabled { get; set; }

        [JsonProperty("implicit-cleanup-action"), JsonPropertyName("implicit-cleanup-action")]
        public string ImplicitCleanupAction { get; set; } = "drop";

        public string LayerType { get; set; } = "";
    }
}

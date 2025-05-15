using RestSharp;
using System.Text.Json;
using System.Net;
using FWO.Data;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using FWO.Logging;
using RestSharp.Serializers.NewtonsoftJson;
using RestSharp.Serializers;
using System.Reflection.Metadata;
using MimeKit;

namespace FWO.Rest.Client
{
    public class CheckPointClient
    {
        readonly RestClient restClient;
        readonly string CPSidHeaderKey = "X-chkp-sid";

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
            request.AddHeader(CPSidHeaderKey, session);
            request.AddJsonBody(new { });
            return await restClient.ExecuteAsync<CpSessionAuthInfo>(request);
        }

        public async Task<List<Domain>> GetDomains(string session)
        {
            RestRequest request = new("show-domains", Method.Post);
            request.AddHeader(CPSidHeaderKey, session);
            request.AddHeader("Content-Type", "application/json");
            Dictionary<string, string> body = new()
            {
                { "details-level", "full" }
            };
            request.AddJsonBody(body);
            RestResponse<CpDomainHelper> domainResponse = await restClient.ExecuteAsync<CpDomainHelper>(request);

            if (domainResponse.StatusCode == HttpStatusCode.OK && domainResponse.IsSuccessful && domainResponse.Data?.DomainList != null)
            {
                List<Domain> domainList = domainResponse.Data.DomainList;
                if (domainList.Count == 0)
                {
                    Log.WriteDebug("Autodiscovery", $"found no domains - assuming this is a standard management, adding dummy domain with empty name");
                    domainList.Add(new Domain { Name = "" });
                }
                return domainList;
            }
            return [];
        }

        public async Task<List<CpDevice>> GetAllCpDevices(string session)
        // session id pins this session to a specific domain (if domain was given during login) 
        {
            RestRequest request = new("show-gateways-and-servers", Method.Post);
            request.AddHeader(CPSidHeaderKey, session);
            request.AddHeader("Content-Type", "application/json");
            Dictionary<string, string> body = new()
            {
                { "details-level", "full" }
            };
            request.AddJsonBody(body);
            Log.WriteDebug("Autodiscovery", $"using CP REST API call 'show-gateways-and-servers'");

            // getting all devices of this management 
            RestResponse<CpDeviceHelper> devices = await restClient.ExecuteAsync<CpDeviceHelper>(request);
            if (devices.Data != null)
            {
                return devices.Data.DeviceList;
            }
            return [];

        }

        public async Task<List<CpDevice>> GetManagers(string session, string ManagementType)
        // session id pins this session to a specific domain (if domain was given during login) 
        {
            List<string> gwTypes = ["simple-gateway", "simple-cluster", "CpmiVsNetobj", "CpmiVsClusterNetobj", "CpmiGatewayPlain", "CpmiGatewayCluster", "CpmiVsxClusterNetobj", "CpmiVsxNetobj"];

            List<CpDevice> devices = await GetAllCpDevices(session);
            if (devices != null)
            {
                foreach (CpDevice dev in devices)
                {
                    if (gwTypes.Contains(dev.CpDevType) && !dev.Policy.AccessPolicyInstalled)
                    {
                        Log.WriteWarning("Autodiscovery", $"found gateway '{dev.Name}' without access policy");
                    }
                }
                return devices;
            }
            return [];
        }

        public async Task<List<CpDevice>> GetGateways(string session)
        // session id pins this session to a specific domain (if domain was given during login) 
        {
            List<string> gwTypes = ["simple-gateway", "simple-cluster", "CpmiVsNetobj", "CpmiVsClusterNetobj", "CpmiGatewayPlain", "CpmiGatewayCluster", "CpmiVsxClusterNetobj", "CpmiVsxNetobj"];

            List<CpDevice> devices = await GetAllCpDevices(session);
            if (devices != null)
            {
                foreach (CpDevice dev in devices)
                {
                    if (gwTypes.Contains(dev.CpDevType) && !dev.Policy.AccessPolicyInstalled)
                    {
                        Log.WriteWarning("Autodiscovery", $"found gateway '{dev.Name}' without access policy");
                    }
                }
                return devices;
            }
            return [];
        }

        public async Task<string> GetGlobalDomainUid(string session)
        {
            RestRequest request = new("show-global-domain", Method.Post);
            request.AddHeader("X-chkp-sid", session);
            request.AddHeader("Content-Type", "application/json");
            Dictionary<string, string> body = new()
            {
                { "details-level", "full" },
                { "name", "Global" }
            };
            request.AddJsonBody(body);
            Log.WriteDebug("Autodiscovery", $"using CP REST API call 'show-global-domain'");

            // getting name and uid of the global domain 
            RestResponse<CpNameUidHelper> globalDomain = await restClient.ExecuteAsync<CpNameUidHelper>(request);
            if (globalDomain.Data != null)
            {
                return globalDomain.Data.Uid;
            }
            return "";
        }

        public async Task<string> GetMdsUid(Management management)
        {
            RestResponse<CpSessionAuthInfo> response = await AuthenticateUser(management.ImportCredential.ImportUser, management.ImportCredential.Secret, "System Data");
            if (response.StatusCode != HttpStatusCode.OK || !response.IsSuccessful)
            {
                Log.WriteError("Autodiscovery", $"failed to authenticate user '{management.ImportCredential.ImportUser}'");
                return "";
            }
            string session = response.Data?.SessionId ?? "";
            if (session == "")
            {
                Log.WriteError("Autodiscovery", $"failed to authenticate user '{management.ImportCredential.ImportUser}'");
                return "";
            }

            RestRequest request = new("show-mdss", Method.Post);
            request.AddHeader("X-chkp-sid", session);
            request.AddHeader("Content-Type", "application/json");
            Dictionary<string, string> body = [];
            request.AddJsonBody(body);
            Log.WriteDebug("Autodiscovery", $"using CP REST API call 'show-mdss'");

            // getting name and uid of the global domain 
            RestResponse<MdsHelper> mdsObjects = await restClient.ExecuteAsync<MdsHelper>(request);
            if (mdsObjects.Data?.Mds != null)
            {
                if (mdsObjects.Data.Mds.Count == 0)
                {
                    Log.WriteDebug("Autodiscovery", $"found no MDS - assuming this is a standard management, adding dummy domain with empty name");
                    mdsObjects.Data.Mds.Add(new CpNameUidHelper { Name = "", Uid = "" });
                    return "";
                }
                else
                {
                    return mdsObjects.Data.Mds.First().Uid;
                }
            }
            return "";
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

    public class CpNameUidHelper
    {
        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("uid"), JsonPropertyName("uid")]
        public string Uid { get; set; } = "";
    }

    public class MdsHelper
    {
        [JsonProperty("objects"), JsonPropertyName("objects")]
        public List<CpNameUidHelper> Mds { get; set; } = [];
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

        [JsonProperty("ipv4-address"), JsonPropertyName("ipv4-address")]
        public string ManagementIp { get; set; } = "";

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

using RestSharp;
using System.Text.Json;
using FWO.GlobalConstants;
using FWO.Api.Data;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using FWO.Logging;
using RestSharp.Serializers.NewtonsoftJson;
using System.Text.Encodings.Web;
using System.Text;
using RestSharp.Serializers;

namespace FWO.Rest.Client
{
    public class CheckPointClient
    {
        readonly RestClient restClient;

        public CheckPointClient(Management manager)
        {
            RestClientOptions restClientOptions = new RestClientOptions();
            restClientOptions.RemoteCertificateValidationCallback += (_, _, _, _) => true;
            restClientOptions.BaseUrl = new Uri("https://" + manager.Hostname + ":" + manager.Port + "/web_api/");
            restClient = new RestClient(restClientOptions, null, ConfigureRestClientSerialization);
        }

        private void ConfigureRestClientSerialization(SerializerConfig config)
        {
            JsonNetSerializer serializer = new JsonNetSerializer(); // Case insensivitive is enabled by default
            config.UseSerializer(() => serializer);
        } 

        public async Task<RestResponse<CpSessionAuthInfo>> AuthenticateUser(string? user, string? pwd, string? domain)
        {
            if (user == null || user == "")
            {
                Log.WriteWarning("Autodiscovery", $"GetDomains got empty user string, aborting");
                return new RestResponse<CpSessionAuthInfo>(new RestRequest());
            }
            if (pwd == null)
                pwd = "";
            if (domain == null)
                domain = "";
            Dictionary<string, string> body = new Dictionary<string, string>();
            body.Add("user", user);
            body.Add("password", pwd);
            if (domain != "")
                body.Add("domain", domain);
            RestRequest request = new RestRequest("login", Method.Post);
            request.AddJsonBody(body);
            request.AddHeader("Content-Type", "application/json");
            return await restClient.ExecuteAsync<CpSessionAuthInfo>(request);
        }

        public async Task<RestResponse<CpSessionAuthInfo>> DeAuthenticateUser(string session)
        {
            RestRequest request = new RestRequest("logout", Method.Post);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("X-chkp-sid", session);
            request.AddJsonBody(new { });
            return await restClient.ExecuteAsync<CpSessionAuthInfo>(request);
        }

        public async Task<RestResponse<CpDomainHelper>> GetDomains(string session)
        {
            RestRequest request = new RestRequest("show-domains", Method.Post);
            request.AddHeader("X-chkp-sid", session);
            request.AddHeader("Content-Type", "application/json");
            Dictionary<string, string> body = new Dictionary<string, string>();
            body.Add("details-level", "full");
            request.AddJsonBody(body);
            return await restClient.ExecuteAsync<CpDomainHelper>(request);
        }

        private static bool containsDomainLayer(List<CpAccessLayer> layers)
        {
            foreach (CpAccessLayer layer in layers)
            {
                if (layer.ParentLayer != "")
                    return true;
            }
            return false;
        }

        public async Task<List<CpDevice>> GetGateways(string session, string ManagementType)
        // session id pins this session to a specific domain (if domain was given during login) 
        {
            RestRequest request = new RestRequest("show-gateways-and-servers", Method.Post);
            request.AddHeader("X-chkp-sid", session);
            request.AddHeader("Content-Type", "application/json");
            Dictionary<string, string> body = new Dictionary<string, string>();
            body.Add("details-level", "full");
            request.AddJsonBody(body);
            Log.WriteDebug("Autodiscovery", $"using CP REST API call 'show-gateways-and-servers'");
            List<string> gwTypes = ["simple-gateway", "simple-cluster", "CpmiVsClusterNetobj", "CpmiGatewayPlain", "CpmiGatewayCluster", "CpmiVsxClusterNetobj", "CpmiVsxNetobj"];

            // getting all gateways of this management 
            RestResponse<CpDeviceHelper> devices = await restClient.ExecuteAsync<CpDeviceHelper>(request);
            if(devices.Data != null)
            {
                foreach (CpDevice dev in devices.Data.DeviceList)
                {
                    if (gwTypes.Contains(dev.CpDevType))
                    {
                        if (dev.Policy.AccessPolicyInstalled)   // get package info
                        {
                            Log.WriteDebug("Autodiscovery", $"found gateway '{dev.Name}' with access policy '{dev.Policy.AccessPolicyName}'");
                            RestRequest requestPackage = new RestRequest("show-package", Method.Post);
                            requestPackage.AddHeader("X-chkp-sid", session);
                            requestPackage.AddHeader("Content-Type", "application/json");
                            Dictionary<string, string> packageBody = new Dictionary<string, string>();
                            packageBody.Add("name", dev.Policy.AccessPolicyName);
                            packageBody.Add("details-level", "full");
                            requestPackage.AddJsonBody(packageBody);
                            RestResponse<CpPackage> package = await restClient.ExecuteAsync<CpPackage>(requestPackage);
                            if (dev != null && package != null && package.Data != null)
                            {
                                dev.Package = package.Data;
                                Log.WriteDebug("Autodiscovery", $"for gateway '{dev.Name}' we found a package '{dev?.Package?.Name}' with {dev?.Package?.CpAccessLayers.Count} layers");

                                extractLayerNames(dev!.Package, dev.Name, ManagementType, out string localLayerName, out string globalLayerName);
                                dev.LocalLayerName = localLayerName;
                                dev.GlobalLayerName = globalLayerName;
                            }
                        }
                        else
                            Log.WriteWarning("Autodiscovery", $"found gateway '{dev.Name}' without access policy");
                    }
                }
                return devices.Data.DeviceList;
            }
            return new List<CpDevice>();
        }

        private void extractLayerNames(CpPackage package, string devName, string managementType, out string localLayerName, out string globalLayerName)
        {
            localLayerName = "";
            globalLayerName = "";
            // getting rid of unneccessary layers (eg. url filtering, application, ...)
            List<CpAccessLayer> relevantLayers = new List<CpAccessLayer>();
            if (package.CpAccessLayers.Count == 1) // default: pick the first layer found (if any)
                relevantLayers.Add(package.CpAccessLayers[0]);
            else if (package.CpAccessLayers.Count > 1)
            {
                Log.WriteWarning("Autodiscovery", $"for gateway '{devName}'/ package '{package.Name}' we found multiple ({package.CpAccessLayers.Count}) layers");
                // for now: pick the layer which the most "firewall-ish" - TODO: deal with layer chaining
                foreach (CpAccessLayer layer in package.CpAccessLayers)
                {
                    if (layer.IsFirewallEnabled && !layer.IsApplicationsAndUrlFilteringEnabled && !layer.IsContentAwarenessEnabled && !layer.IsMobileAccessEnabled)
                        relevantLayers.Add(layer);
                }
            }

            foreach (CpAccessLayer layer in relevantLayers)
            {
                if (layer.Type != "access-layer") // only dealing with access layers, ignore the rest
                    continue;

                if (layer.ParentLayer != "")      // this is a domain layer
                {
                    localLayerName = layer.Name;
                    layer.LayerType = "domain-layer";
                    Log.WriteDebug("Autodiscovery", $"found domain layer with link to parent layer '{layer.ParentLayer}'");
                }
                else if (managementType == "stand-alone")
                {
                    localLayerName = layer.Name;
                    layer.LayerType = "local-layer";
                    Log.WriteDebug("Autodiscovery", $"found stand-alone layer '{layer.Name}'");
                }
                else if (containsDomainLayer(package.CpAccessLayers))
                {   // this must the be global layer
                    layer.LayerType = "global-layer";
                    globalLayerName = layer.Name;
                    Log.WriteDebug("Autodiscovery", $"found global layer '{layer.Name}'");
                }
                else
                { // in domain context, but no global layer exists
                    layer.LayerType = "stand-alone-layer";
                    localLayerName = layer.Name;
                    Log.WriteDebug("Autodiscovery", $"found stand-alone layer in domain context '{layer.Name}'");
                }
                // TODO: this will contstantly overwrite local layer name if more than one exists, the last one wins!
            }

            package.CpAccessLayers = relevantLayers;
            if (relevantLayers.Count == 0)
                Log.WriteWarning("Autodiscovery", $"found gateway '{devName}' without access layers");
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
        public List<Domain> DomainList { get; set; } = new List<Domain>();

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

        // public List<Assignment> Assignments = new List<Assignment>();
    }

    public class CpDeviceHelper
    {
        [JsonProperty("objects"), JsonPropertyName("objects")]
        public List<CpDevice> DeviceList { get; set; } = new List<CpDevice>();
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
        public List<CpAccessLayer> CpAccessLayers { get; set; } = new List<CpAccessLayer>();
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

using RestSharp;
using RestSharp.Serializers.SystemTextJson;
using System.Text.Json;
using FWO.Api.Data;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;
using Newtonsoft.Json;
using FWO.Logging;
using RestSharp.Serializers.NewtonsoftJson;

namespace FWO.Rest.Client
{
    public class CheckPointClient
    {
        readonly RestClient restClient;

        public CheckPointClient(Management manager)
        {
            RestClientOptions restClientOptions = new RestClientOptions();
            restClientOptions.RemoteCertificateValidationCallback += (_, _, _, _) => true;
            // restClientOptions.Encoding = Encoding.Latin1;
            restClientOptions.BaseUrl = new Uri("https://" + manager.Hostname + ":" + manager.Port + "/web_api/");

            restClient = new RestClient(restClientOptions);
            // restClient.AddDefaultHeader("Content-Type", "application/json");

            // string xsid = getter.login(args.user, apiuser_pwd, args.hostname, args.port, args.domain, ssl_verification, proxy_string, debug=args.debug)

            // api_versions = getter.api_call(base_url, 'show-api-versions', {}, xsid, ssl_verification, proxy_string)
            // api_version = api_versions["current-version"]
            // api_supported = api_versions["supported-versions"]
            // v_url = getter.set_api_url(base_url,args.version,api_supported,args.hostname)


            JsonNetSerializer serializer = new JsonNetSerializer(); // Case insensivitive is enabled by default
            restClient.UseDefaultSerializers();
            restClient.UseSerializer(() => serializer);
        }

        // protected class JsonDefaultHeader
        // {
        //     [JsonProperty("Content-Type"), JsonPropertyName("ContentType")]
        //     const string ContentType = "application/json";
        // }
        public async Task<RestResponse<CpSessionAuthInfo>> AuthenticateUser(string user, string pwd, string? domain)
        {
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

        public async Task<List<CpDevice>> GetGateways(string session)
        {
            RestRequest request = new RestRequest("show-gateways-and-servers", Method.Post);
            request.AddHeader("X-chkp-sid", session);
            request.AddHeader("Content-Type", "application/json");
            Dictionary<string, string> body = new Dictionary<string, string>();
            body.Add("details-level", "full");
            request.AddJsonBody(body);
            Log.WriteDebug("Autodiscovery", $"using CP REST API call 'show-gateways-and-servers'");
            RestResponse<CpDeviceHelper> devices = await restClient.ExecuteAsync<CpDeviceHelper>(request);
            List<String> gwTypes = new List<string> { "simple-gateway", "simple-cluster", "CpmiVsClusterNetobj", "CpmiGatewayPlain", "CpmiGatewayCluster", "CpmiVsxClusterNetobj" };
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
                        dev.Package = package.Data;
                        Log.WriteDebug("Autodiscovery", $"for gateway '{dev.Name}' we found a package '{dev.Package.Name}' with {dev.Package.CpAccessLayers.Count} layers");

                        // now getting rid of unneccessary layes (eg. url filtering, application, ...)
                        List<CpAccessLayer> relevantLayers = new List<CpAccessLayer>();
                        if (dev.Package.CpAccessLayers.Count == 1) // default: pick the first layer found (if any)
                            relevantLayers.Add(dev.Package.CpAccessLayers[0]);
                        else if (dev.Package.CpAccessLayers.Count > 1)
                        {
                            Log.WriteWarning("Autodiscovery", $"for gateway '{dev.Name}'/ package '{dev.Package.Name}' we found multiple ({dev.Package.CpAccessLayers.Count}) layers");
                            // for now: pick the layer which the most "firewall-ish" - TODO: deal with layer chaining
                            foreach (CpAccessLayer layer in dev.Package.CpAccessLayers)
                            {
                                if (layer.IsFirewallEnabled && !layer.IsApplicationsAndUrlFilteringEnabled && !layer.IsContentAwarenessEnabled && !layer.IsMobileAccessEnabled)
                                    relevantLayers.Add(layer);
                            }
                        }
                        dev.Package.CpAccessLayers = relevantLayers;
                        if (relevantLayers.Count == 0)
                            Log.WriteWarning("Autodiscovery", $"found gateway '{dev.Name}' without access layers");
                    }
                    else
                        Log.WriteWarning("Autodiscovery", $"found gateway '{dev.Name}' without access policy");
                }
            }
            return devices.Data.DeviceList;
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
        public List<Domain> DomainList { get; set; }

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

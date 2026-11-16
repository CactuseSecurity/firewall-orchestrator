using FWO.Api.Client;
using FWO.Logging;
using MailKit.Security;
using Newtonsoft.Json;
using RestSharp;
using System.Net;

namespace FWO.DeviceAutoDiscovery
{
    /// <summary>
    /// Azure Resource Manager rejected the api-version of a call, an older one may still work.
    /// </summary>
    public class AzureApiVersionNotSupportedException(string message) : Exception(message)
    { }

    /// <summary>
    /// Client for the Azure Resource Manager REST API used by the autodiscovery of Azure managements.
    /// </summary>
    /// <remarks>
    /// The certificates of the public Azure endpoints are always checked, independent of the setting for firewall
    /// connections which exists for appliances with self-signed certificates.
    /// </remarks>
    public class AzureClient : RestApiClient
    {
        /// <summary>
        /// Base url of Azure Resource Manager; the bearer token is only ever sent to this host.
        /// </summary>
        public const string kManagementUrl = "https://management.azure.com";
        private const string kLoginUrl = "https://login.microsoftonline.com";
        private const string kManagementScope = "https://management.azure.com/.default";
        private const string kAutodiscovery = "Autodiscovery";
        private const int kMaxPages = 100;

        // api versions in order of preference; older ones are used if a cloud does not support the newer ones yet
        private static readonly List<string> ApiVersions = ["2026-01-01", "2024-05-01"];
        // error codes of Azure Resource Manager for an api-version unknown to the resource provider
        private static readonly List<string> UnsupportedApiVersionErrorCodes = ["InvalidApiVersionParameter", "NoRegisteredProviderFound"];

        /// <summary>
        /// The api version determined by the first successful list call, used for all further calls.
        /// </summary>
        public string? ApiVersion { get; private set; }

        /// <summary>
        /// Creates a client for Azure Resource Manager.
        /// </summary>
        public AzureClient() : base(kManagementUrl + "/", checkCertificates: true)
        { }

        /// <summary>
        /// Gets an Azure Resource Manager access token with the OAuth 2.0 client credentials flow.
        /// </summary>
        /// <param name="tenantId">The Entra ID tenant id.</param>
        /// <param name="clientId">The service principal application (client) id.</param>
        /// <param name="clientSecret">The service principal secret.</param>
        /// <returns>The bearer access token.</returns>
        public virtual async Task<string> GetAccessToken(string tenantId, string clientId, string clientSecret)
        {
            RestRequest request = new($"{kLoginUrl}/{Uri.EscapeDataString(tenantId)}/oauth2/v2.0/token", Method.Post);
            request.AddParameter("grant_type", "client_credentials");
            request.AddParameter("client_id", clientId);
            request.AddParameter("client_secret", clientSecret);
            request.AddParameter("scope", kManagementScope);
            RestResponse response = await restClient.ExecuteAsync(request);

            string? accessToken = null;
            if (response.StatusCode == HttpStatusCode.OK && !string.IsNullOrEmpty(response.Content))
            {
                accessToken = JsonConvert.DeserializeObject<AzureTokenResponse>(response.Content)?.AccessToken;
            }
            if (string.IsNullOrEmpty(accessToken))
            {
                // never log the request as it contains the client secret
                Log.WriteWarning(kAutodiscovery, $"Azure login failed for client id {clientId}: {(int)response.StatusCode} {response.Content}");
                throw new AuthenticationException($"Azure login failed for client id {clientId}");
            }
            return accessToken;
        }

        /// <summary>
        /// Lists all Azure Firewalls of a subscription.
        /// </summary>
        /// <param name="subscriptionId">The Azure subscription id.</param>
        /// <param name="accessToken">The bearer access token.</param>
        /// <returns>The Azure Firewalls of all pages.</returns>
        public async Task<List<AzureFirewall>> GetAzureFirewalls(string subscriptionId, string accessToken)
        {
            string path = $"subscriptions/{Uri.EscapeDataString(subscriptionId)}/providers/Microsoft.Network/azureFirewalls";
            return await ListItems<AzureFirewall>(path, accessToken);
        }

        /// <summary>
        /// Gets all items of a list call with the newest api version the cloud supports.
        /// </summary>
        /// <param name="path">The resource path below the management url.</param>
        /// <param name="accessToken">The bearer access token.</param>
        /// <returns>The items of all pages.</returns>
        public async Task<List<T>> ListItems<T>(string path, string accessToken)
        {
            List<string> apiVersions = ApiVersion != null ? [ApiVersion] : ApiVersions;
            foreach (string apiVersion in apiVersions)
            {
                try
                {
                    List<T> items = await GetAllPages<T>($"{kManagementUrl}/{path}?api-version={apiVersion}", accessToken);
                    ApiVersion = apiVersion;
                    return items;
                }
                catch (AzureApiVersionNotSupportedException)
                {
                    Log.WriteInfo(kAutodiscovery, $"Azure api version {apiVersion} not supported for '{path}', trying an older one");
                }
            }
            throw new InvalidOperationException($"Azure api versions {string.Join(", ", apiVersions)} are not supported for '{path}'");
        }

        private async Task<List<T>> GetAllPages<T>(string url, string accessToken)
        {
            List<T> items = [];
            string? nextUrl = url;
            int pageCount = 0;
            while (nextUrl != null)
            {
                if (pageCount >= kMaxPages)
                {
                    throw new InvalidOperationException($"Azure api call to '{url}' exceeded the maximum of {kMaxPages} pages");
                }
                pageCount++;
                // the bearer token must only ever be sent to Azure Resource Manager
                if (!nextUrl.StartsWith(kManagementUrl + "/", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Azure api call refused for unexpected url '{nextUrl}'");
                }
                string content = await GetPage(nextUrl, accessToken);
                AzureListPage<T> page = JsonConvert.DeserializeObject<AzureListPage<T>>(content) ?? new();
                items.AddRange(page.Value);
                nextUrl = string.IsNullOrEmpty(page.NextLink) ? null : page.NextLink;
            }
            return items;
        }

        /// <summary>
        /// Gets the raw content of a single page of a list call.
        /// </summary>
        /// <param name="url">The absolute url of the page.</param>
        /// <param name="accessToken">The bearer access token.</param>
        /// <returns>The json content of the page.</returns>
        protected virtual async Task<string> GetPage(string url, string accessToken)
        {
            RestRequest request = new(url, Method.Get);
            request.AddHeader("Authorization", $"Bearer {accessToken}");
            RestResponse response = await restClient.ExecuteAsync(request);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                return response.Content ?? "";
            }
            if (response.StatusCode == HttpStatusCode.BadRequest && IsUnsupportedApiVersionError(response.Content))
            {
                throw new AzureApiVersionNotSupportedException($"api-version not supported for url '{url}': {response.Content}");
            }
            throw new InvalidOperationException($"Azure api call to '{url}' failed: {(int)response.StatusCode} {response.Content}");
        }

        /// <summary>
        /// Checks whether an Azure Resource Manager error response rejects the api-version.
        /// </summary>
        /// <param name="content">The json content of the error response.</param>
        /// <returns>True if the error code reports an unsupported api-version.</returns>
        public static bool IsUnsupportedApiVersionError(string? content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return false;
            }
            try
            {
                string? errorCode = JsonConvert.DeserializeObject<AzureCloudError>(content)?.Error?.Code;
                return errorCode != null && UnsupportedApiVersionErrorCodes.Contains(errorCode);
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// A page of an Azure Resource Manager list call.
    /// </summary>
    public class AzureListPage<T>
    {
        [JsonProperty("value")]
        public List<T> Value { get; set; } = [];

        [JsonProperty("nextLink")]
        public string? NextLink { get; set; }
    }

    /// <summary>
    /// An Azure Firewall resource.
    /// </summary>
    public class AzureFirewall
    {
        [JsonProperty("id")]
        public string Id { get; set; } = "";

        [JsonProperty("name")]
        public string Name { get; set; } = "";

        [JsonProperty("properties")]
        public AzureFirewallProperties Properties { get; set; } = new();
    }

    /// <summary>
    /// Properties of an Azure Firewall resource.
    /// </summary>
    public class AzureFirewallProperties
    {
        /// <summary>
        /// The firewall policy enforced on the firewall; missing for firewalls with classic rules.
        /// </summary>
        [JsonProperty("firewallPolicy")]
        public AzureSubResource? FirewallPolicy { get; set; }
    }

    /// <summary>
    /// Reference to another Azure resource.
    /// </summary>
    public class AzureSubResource
    {
        [JsonProperty("id")]
        public string Id { get; set; } = "";
    }

    internal class AzureTokenResponse
    {
        [JsonProperty("access_token")]
        public string? AccessToken { get; set; }
    }

    internal class AzureCloudError
    {
        [JsonProperty("error")]
        public AzureCloudErrorDetails? Error { get; set; }
    }

    internal class AzureCloudErrorDetails
    {
        [JsonProperty("code")]
        public string? Code { get; set; }
    }
}

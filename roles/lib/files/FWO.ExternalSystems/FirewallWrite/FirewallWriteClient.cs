using FWO.Api.Client;
using FWO.Basics.Exceptions;
using FWO.Data;
using FWO.Encryption;
using FWO.Logging;
using RestSharp;
using System.Text.Json.Nodes;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// REST session with one firewall management for the firewall write library. Vendor clients add login and logout,
    /// authentication, the evaluation of responses and the runtime placeholders that need data read from the firewall.
    /// </summary>
    public abstract class FirewallWriteClient : RestApiClient
    {
        private const string kLogCategory = "Firewall Write";
        private const int kMaxLoggedResponseLength = 2000;

        protected Management Management { get; }

        /// <summary>Creates a client for a management.</summary>
        /// <param name="baseUrl">Base url of the vendor API.</param>
        /// <param name="management">Management with export credentials.</param>
        /// <param name="checkCertificates">False accepts any server certificate.</param>
        protected FirewallWriteClient(string baseUrl, Management management, bool checkCertificates)
            : base(baseUrl, checkCertificates: checkCertificates)
        {
            Management = management;
        }

        /// <summary>Opens the session, if the vendor API needs one.</summary>
        public abstract Task Login();

        /// <summary>Discards the uncommitted changes of the session after an error, if the vendor API supports it.</summary>
        public virtual Task Discard() => Task.CompletedTask;

        /// <summary>Closes the session.</summary>
        public virtual Task Logout() => Task.CompletedTask;

        /// <summary>
        /// Sends one rendered call. Runtime placeholders are resolved first, then the call is checked for leftover
        /// placeholders and valid JSON before it is sent.
        /// </summary>
        public virtual async Task<FirewallStepResult> Execute(RenderedFirewallRequest request)
        {
            await ResolveRuntimeValues(request);
            FirewallTemplateRenderer.EnsureResolved(request);
            RestResponse response = await SendRendered(request);
            return ToResult(request, response, IsSuccess(response));
        }

        /// <summary>
        /// Resolves the runtime placeholders the vendor supports. Fails for runtime placeholders the vendor cannot resolve.
        /// </summary>
        protected virtual Task ResolveRuntimeValues(RenderedFirewallRequest request)
        {
            string? unsupported = FirewallWritePlaceholders.RuntimePlaceholders.FirstOrDefault(placeholder => FirewallTemplateRenderer.Contains(request, placeholder));
            if (unsupported != null)
            {
                throw new ConfigException($"Template error in {request.Operation}. Placeholder {unsupported} is not supported for this device type.");
            }
            return Task.CompletedTask;
        }

        /// <summary>Builds and sends a rendered call with authentication.</summary>
        protected async Task<RestResponse> SendRendered(RenderedFirewallRequest request)
        {
            RestRequest restRequest = new(request.Path, ToMethod(request.Method));
            string? body = PrepareBody(request);
            if (body != null)
            {
                restRequest.AddStringBody(body, ContentType.Json);
            }
            Log.WriteDebug(kLogCategory, $"{request.Operation}: {request.Method} {request.Path}");
            return await SendAuthenticated(restRequest);
        }

        /// <summary>Adds authentication to a request and sends it.</summary>
        protected async Task<RestResponse> SendAuthenticated(RestRequest request)
        {
            request.AddHeader("Accept", "application/json");
            AddAuthentication(request);
            return await Send(request);
        }

        /// <summary>Returns the body to send; vendors add session data here.</summary>
        protected virtual string? PrepareBody(RenderedFirewallRequest request) => request.Body;

        /// <summary>Adds the vendor's authentication header to a request.</summary>
        protected abstract void AddAuthentication(RestRequest request);

        /// <summary>Whether a response reports success.</summary>
        protected virtual bool IsSuccess(RestResponse response) => response.IsSuccessful;

        /// <summary>Sends a request. Unit tests override this to simulate the firewall.</summary>
        protected virtual Task<RestResponse> Send(RestRequest request) => restClient.ExecuteAsync(request);

        /// <summary>Returns user and decrypted secret of the management's export credential.</summary>
        /// <exception cref="ProcessingFailedException">Thrown if the export credential is incomplete.</exception>
        protected (string User, string Secret) ExportCredentials()
        {
            Credential credential = Management.ExportCredential;
            if (credential == null || string.IsNullOrWhiteSpace(credential.Secret))
            {
                throw new ProcessingFailedException($"Export credentials missing for management '{Management.Name}'.");
            }
            string secret = AesEnc.TryDecrypt(credential.Secret, true, kLogCategory,
                $"Could not decrypt secret in export credential '{credential.Name}'.", true);
            return (credential.ImportUser ?? "", secret);
        }

        /// <summary>Returns "https://host:port" of the management.</summary>
        /// <exception cref="ProcessingFailedException">Thrown if the management has no host name.</exception>
        protected static string HostUrl(Management management, string pathPrefix = "")
        {
            if (string.IsNullOrWhiteSpace(management.Hostname))
            {
                throw new ProcessingFailedException($"Management '{management.Name}' has no host name.");
            }
            string host = management.Hostname.Trim();
            if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                host = new Uri(host).Host;
            }
            int port = management.Port > 0 ? management.Port : 443;
            return $"https://{host}:{port}{pathPrefix}";
        }

        /// <summary>Parses a JSON response body; null for empty or invalid content.</summary>
        protected static JsonNode? ParseJson(string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }
            try
            {
                return JsonNode.Parse(content);
            }
            catch (System.Text.Json.JsonException)
            {
                return null;
            }
        }

        /// <summary>Converts a response to a step result; the logged content is shortened.</summary>
        protected static FirewallStepResult ToResult(RenderedFirewallRequest request, RestResponse response, bool success)
        {
            string content = response.Content ?? response.ErrorMessage ?? "";
            if (content.Length > kMaxLoggedResponseLength)
            {
                content = content[..kMaxLoggedResponseLength];
            }
            if (!success)
            {
                Log.WriteWarning(kLogCategory, $"{request.Operation} on '{request.Step.TargetName}' failed: {(int)response.StatusCode} {content}");
            }
            return new FirewallStepResult(request.Operation, request.Step.TargetName, success, (int)response.StatusCode, content);
        }

        private static Method ToMethod(string method) => method switch
        {
            "GET" => Method.Get,
            "POST" => Method.Post,
            "PUT" => Method.Put,
            "PATCH" => Method.Patch,
            "DELETE" => Method.Delete,
            _ => throw new ConfigException($"Unsupported HTTP method '{method}'.")
        };
    }
}

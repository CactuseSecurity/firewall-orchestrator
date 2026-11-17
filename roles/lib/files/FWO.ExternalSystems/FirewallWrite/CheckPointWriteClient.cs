using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Logging;
using RestSharp;
using System.Text.Json.Nodes;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// Check Point management API session: login (into the domain of an MDS management), session id header, discard on
    /// error and logout. Object creation that fails only because of a duplicate address warning is retried with
    /// ignore-warnings, and an object that already exists as requested counts as created, like the existing Check Point
    /// ticket handling does.
    /// </summary>
    public class CheckPointWriteClient : FirewallWriteClient
    {
        private const string kSessionHeader = "X-chkp-sid";
        private const string kIgnoreWarnings = "ignore-warnings";
        private const string kShowAccessLayer = "show-access-layer";

        private static readonly List<FirewallWriteOperation> RetryableOperations =
            [FirewallWriteOperation.HostCreate, FirewallWriteOperation.NetworkCreate, FirewallWriteOperation.AddressRangeCreate];
        private static readonly List<string> DuplicateAddressWarnings =
        [
            "multiple IP", "same IP address", "same IPv4 address", "same IPv6 address", "More than one object has the same",
            "More than one network has the same", "More than one host has the same", "More than one address range has the same"
        ];

        /// <summary>Command reading the object of a create operation, to check whether it is already present as requested.</summary>
        private static readonly Dictionary<FirewallWriteOperation, string> ShowCommands = new()
        {
            [FirewallWriteOperation.HostCreate] = "show-host",
            [FirewallWriteOperation.Ipv6HostCreate] = "show-host",
            [FirewallWriteOperation.NetworkCreate] = "show-network",
            [FirewallWriteOperation.Ipv6NetworkCreate] = "show-network",
            [FirewallWriteOperation.AddressRangeCreate] = "show-address-range",
            [FirewallWriteOperation.Ipv6AddressRangeCreate] = "show-address-range",
            [FirewallWriteOperation.GroupCreate] = "show-group"
        };

        private const string kDuplicateNameMessage = "More than one object named";
        private const string kInternalErrorPrefix = "generic_internal";
        private static readonly List<string> CheckPointAuthenticationCodes = ["login", "wrong_session", "session_expired", "unauthorized", "permission"];

        private const string kShowTask = "show-task";
        private const string kTaskId = "task-id";
        private const string kTaskStatusInProgress = "in progress";
        private const string kTaskStatusSucceeded = "succeeded";
        private const int kDefaultPublishWaitSeconds = 300;
        private static readonly TimeSpan PublishPollInterval = TimeSpan.FromSeconds(1);

        private readonly int publishWaitSeconds;
        private string? sessionId;

        /// <summary>Creates a client for the web_api of a Check Point management.</summary>
        /// <param name="management">Management with export credentials.</param>
        /// <param name="checkCertificates">False accepts any server certificate.</param>
        /// <param name="responseTimeout">Seconds a call may take; also the time a publish may take to finish. Null keeps the default.</param>
        public CheckPointWriteClient(Management management, bool checkCertificates, int? responseTimeout = null)
            : base(HostUrl(management, "/web_api/"), management, checkCertificates, responseTimeout)
        {
            publishWaitSeconds = responseTimeout is > 0 ? responseTimeout.Value : kDefaultPublishWaitSeconds;
        }

        /// <inheritdoc/>
        public override async Task Login()
        {
            (string user, string password) = ExportCredentials();
            JsonObject body = new() { ["user"] = user, ["password"] = password };
            string domain = !string.IsNullOrWhiteSpace(Management.DomainUid) ? Management.DomainUid : Management.ConfigPath ?? "";
            if (!string.IsNullOrWhiteSpace(domain))
            {
                body["domain"] = domain;
            }

            RestRequest request = new("login", Method.Post);
            request.AddStringBody(body.ToJsonString(), ContentType.Json);
            RestResponse response = await Send(request);
            sessionId = ParseJson(response.Content)?["sid"]?.GetValue<string>();
            if (!response.IsSuccessful || string.IsNullOrWhiteSpace(sessionId))
            {
                throw LoginFailure(response, "Check Point");
            }
        }

        /// <inheritdoc/>
        public override async Task Discard() => await SessionCall("discard");

        /// <inheritdoc/>
        public override async Task Logout()
        {
            await SessionCall("logout");
            sessionId = null;
        }

        /// <inheritdoc/>
        public override async Task<FirewallStepResult> Execute(RenderedFirewallRequest request)
        {
            FirewallStepResult result = await base.Execute(request);
            if (result.Success && request.Operation == FirewallWriteOperation.Publish)
            {
                return await AwaitPublishTask(result);
            }
            if (result.Success || !RetryableOperations.Contains(request.Operation) || !IsDuplicateAddressWarning(result.Message))
            {
                return result;
            }

            JsonNode? body = ParseJson(request.Body);
            if (body is not JsonObject bodyObject || bodyObject.ContainsKey(kIgnoreWarnings))
            {
                return result;
            }
            Log.WriteInfo("Firewall Write", $"Retrying {request.Operation} on '{request.Step.TargetName}' with {kIgnoreWarnings}.");
            bodyObject[kIgnoreWarnings] = true;
            request.Body = bodyObject.ToJsonString();
            RestResponse retryResponse = await SendRendered(request);
            return ToResult(request, retryResponse, IsSuccess(retryResponse));
        }

        /// <inheritdoc/>
        protected override void AddAuthentication(RestRequest request)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                throw new FirewallWriteException(FirewallWriteErrorKind.Authentication, "Check Point session missing.");
            }
            request.AddHeader(kSessionHeader, sessionId);
        }

        /// <summary>
        /// Maps the "code" of a Check Point management API error, e.g. "generic_err_object_not_found" or
        /// "generic_err_object_locked"; other codes fall back to the HTTP status.
        /// </summary>
        protected override (FirewallWriteErrorKind? Kind, string? NativeCode) ClassifyVendorError(JsonNode? content)
        {
            string? code = content?["code"]?.ToString();
            string message = content?.ToJsonString() ?? "";
            FirewallWriteErrorKind? kind = code switch
            {
                null => null,
                _ when code.Contains("not_found", StringComparison.OrdinalIgnoreCase) => FirewallWriteErrorKind.NotFound,
                _ when code.Contains("locked", StringComparison.OrdinalIgnoreCase) => FirewallWriteErrorKind.Conflict,
                // validation errors of the policy on publish do not go away by retrying
                _ when code.Contains("publish_failed", StringComparison.OrdinalIgnoreCase) => FirewallWriteErrorKind.Rejected,
                _ when code.Contains("too_many_requests", StringComparison.OrdinalIgnoreCase) => FirewallWriteErrorKind.RateLimited,
                _ when CheckPointAuthenticationCodes.Any(part => code.Contains(part, StringComparison.OrdinalIgnoreCase)) => FirewallWriteErrorKind.Authentication,
                _ when code.Contains("not_unique", StringComparison.OrdinalIgnoreCase)
                    || message.Contains(kDuplicateNameMessage, StringComparison.OrdinalIgnoreCase) => FirewallWriteErrorKind.AlreadyExists,
                _ when code.StartsWith(kInternalErrorPrefix, StringComparison.OrdinalIgnoreCase) => FirewallWriteErrorKind.FirewallError,
                _ => null
            };
            return (kind, code);
        }

        /// <summary>
        /// Reads the object of the same name and compares it with the requested one, see <see cref="CheckPointObjectComparison"/>:
        /// hosts, networks and address ranges must have the requested addresses, a group exactly the requested members.
        /// Other operations are never treated as present.
        /// </summary>
        protected override async Task<bool> IsDesiredStatePresent(RenderedFirewallRequest request)
        {
            if (!ShowCommands.TryGetValue(request.Operation, out string? showCommand))
            {
                return false;
            }
            JsonNode? requested = ParseJson(request.Body);
            string? name = requested?["name"] is JsonValue nameValue && nameValue.TryGetValue(out string? text) ? text : null;
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }
            RestRequest showRequest = new(showCommand, Method.Post);
            showRequest.AddStringBody(new JsonObject { ["name"] = name }.ToJsonString(), ContentType.Json);
            RestResponse response = await SendAuthenticated(showRequest);
            JsonNode? existing = response.IsSuccessful ? ParseJson(response.Content) : null;
            if (existing == null)
            {
                return false;
            }
            return request.Operation switch
            {
                FirewallWriteOperation.HostCreate or FirewallWriteOperation.Ipv6HostCreate => CheckPointObjectComparison.HostMatches(requested, existing),
                FirewallWriteOperation.NetworkCreate or FirewallWriteOperation.Ipv6NetworkCreate => CheckPointObjectComparison.NetworkMatches(requested, existing),
                FirewallWriteOperation.AddressRangeCreate or FirewallWriteOperation.Ipv6AddressRangeCreate => CheckPointObjectComparison.AddressRangeMatches(requested, existing),
                FirewallWriteOperation.GroupCreate => CheckPointObjectComparison.GroupMatches(requested, existing),
                _ => false
            };
        }

        /// <summary>
        /// A rule uid is only valid within its layer, so "not found" may also mean the layer is wrong. The rule only counts
        /// as deleted if the layer of the delete call exists.
        /// </summary>
        protected override async Task<bool> IsRuleAlreadyDeleted(RenderedFirewallRequest request)
        {
            JsonNode? body = ParseJson(request.Body);
            string? layer = body?["layer"] is JsonValue layerValue && layerValue.TryGetValue(out string? text) ? text : null;
            if (string.IsNullOrWhiteSpace(layer))
            {
                return false;
            }
            JsonObject layerReference = Guid.TryParse(layer, out _) ? new JsonObject { ["uid"] = layer } : new JsonObject { ["name"] = layer };
            RestRequest showRequest = new(kShowAccessLayer, Method.Post);
            showRequest.AddStringBody(layerReference.ToJsonString(), ContentType.Json);
            RestResponse response = await SendAuthenticated(showRequest);
            return response.IsSuccessful;
        }

        /// <summary>
        /// A Check Point publish only starts a task; its answer does not tell whether the changes were published. Waits
        /// until the task is finished, at most the response timeout: only "succeeded" counts as published. A failed
        /// task or one that is still running at the end fails the publish, so that the session is discarded instead of
        /// being left behind unpublished. An answer without task id is kept as it is.
        /// </summary>
        private async Task<FirewallStepResult> AwaitPublishTask(FirewallStepResult published)
        {
            string? taskId = ParseJson(published.Message)?[kTaskId] is JsonValue idValue && idValue.TryGetValue(out string? id) ? id : null;
            if (string.IsNullOrWhiteSpace(taskId))
            {
                Log.WriteWarning("Firewall Write", $"Check Point publish on '{Management.Name}' returned no task id, its result cannot be checked.");
                return published;
            }
            int maxPolls = Math.Max(1, (int)Math.Ceiling(publishWaitSeconds / PublishPollInterval.TotalSeconds));
            for (int poll = 0; poll < maxPolls; poll++)
            {
                RestResponse response = await ShowTask(taskId);
                JsonNode? task = ParseJson(response.Content)?["tasks"]?[0];
                string? status = task?["status"]?.ToString();
                if (!response.IsSuccessful || status == null)
                {
                    return PublishFailure(published, FirewallWriteErrors.NativeMessage(response), ClassifyFailure(response, published.Operation).Kind);
                }
                if (status != kTaskStatusInProgress)
                {
                    return status == kTaskStatusSucceeded
                        ? published with { Message = $"{published.Message} Publish task {taskId}: {status}." }
                        : PublishFailure(published, task!.ToJsonString(), FirewallWriteErrorKind.Rejected);
                }
                await WaitBeforeNextPoll();
            }
            return PublishFailure(published, $"Publish task {taskId} did not finish within {publishWaitSeconds} seconds.", FirewallWriteErrorKind.Unreachable);
        }

        /// <summary>Reads the state of an asynchronous Check Point task, e.g. of a publish, with show-task.</summary>
        private async Task<RestResponse> ShowTask(string taskId)
        {
            RestRequest request = new(kShowTask, Method.Post);
            request.AddStringBody(new JsonObject { [kTaskId] = taskId }.ToJsonString(), ContentType.Json);
            return await SendAuthenticated(request);
        }

        /// <summary>The failed result of a publish whose task did not succeed, keeping the calls made so far and the firewall's message.</summary>
        private FirewallStepResult PublishFailure(FirewallStepResult published, string nativeMessage, FirewallWriteErrorKind kind)
        {
            string message = $"Check Point publish on '{Management.Name}' failed ({kind}): {nativeMessage}";
            Log.WriteWarning("Firewall Write", message);
            FirewallWriteError error = new(kind, nativeMessage, published.StatusCode, null, published.Operation);
            return published with { Success = false, Message = message, Error = error };
        }

        /// <summary>Waits between two reads of a running publish task. Unit tests override this to run without delay.</summary>
        protected virtual Task WaitBeforeNextPoll() => Task.Delay(PublishPollInterval);

        private static bool IsDuplicateAddressWarning(string content) =>
            DuplicateAddressWarnings.Any(warning => content.Contains(warning, StringComparison.OrdinalIgnoreCase));

        /// <summary>Sends a session call; failures are only logged, because they run during cleanup.</summary>
        private async Task SessionCall(string command)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return;
            }
            try
            {
                RestRequest request = new(command, Method.Post);
                request.AddStringBody("{}", ContentType.Json);
                RestResponse response = await SendAuthenticated(request);
                if (!response.IsSuccessful)
                {
                    Log.WriteWarning("Firewall Write", $"Check Point {command} failed: {(int)response.StatusCode} {response.Content}");
                }
            }
            catch (Exception exception)
            {
                Log.WriteWarning("Firewall Write", $"Check Point {command} threw an exception: {exception.Message}");
            }
        }
    }
}

using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics.Exceptions;
using FWO.Config.Api;
using FWO.Data;
using FWO.Logging;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Workflow;
using FWO.ExternalSystems;
using FWO.ExternalSystems.FirewallWrite;
using FWO.ExternalSystems.Tufin.SecureChange;
using RestSharp;
using System.Net;
using System.Text.Json;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Class handling the sending of external requests
    /// </summary>
    public partial class ExternalRequestSender : IDisposable
    {
        /// <summary>
        /// Api Connection
        /// </summary>
        protected readonly ApiConnection apiConnection;

        /// <summary>
        /// Global Config
        /// </summary>
        protected GlobalConfig globalConfig;

        private readonly UserConfig userConfig;
        private readonly SCClient? InjScClient;
        private readonly Func<WfImplTask, IReadOnlyList<int>?, Task<ModifyFirewallResult>> modifyFirewall;
        ExternalTicketSystem? ExtTicketSystem;
        private bool disposed = false;


        // todo: map to internal states to use "lowest_end_state" setting ?
        private static readonly List<string> openRequestStates =
        [
            ExtStates.ExtReqInitialized.ToString(),
            ExtStates.ExtReqFailed.ToString(),
            ExtStates.ExtReqRequested.ToString(),
            ExtStates.ExtReqInProgress.ToString()
        ];

        private const string LogMessageTitle = "External Request Sender";


        /// <summary>
        /// Constructor for External Request Sender
        /// </summary>
        /// <param name="apiConnection">Api connection</param>
        /// <param name="globalConfig">Global config</param>
        /// <param name="injScClient">SecureChange client replacing the real one (unit testing only)</param>
        /// <param name="injFirewallWrite">Firewall write replacing the write library (unit testing only)</param>
        public ExternalRequestSender(ApiConnection apiConnection, GlobalConfig globalConfig, SCClient? injScClient = null,
            Func<WfImplTask, IReadOnlyList<int>?, Task<ModifyFirewallResult>>? injFirewallWrite = null)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
            userConfig = UserConfig.ForGlobalSettings(globalConfig, apiConnection);
            InjScClient = injScClient;
            modifyFirewall = injFirewallWrite ?? ((implTask, installOnGatewayIds) => new FirewallWriter(new ApiFirewallWriteDataSource(apiConnection,
                new GuardicoreRulesetFormat(globalConfig.AppLabelValueFormat, globalConfig.ModComSvcSectionPrefix)),
                placementProvider: new RulePositionPlacementProvider()).ModifyFirewall(implTask, installOnGatewayIds));
        }

        /// <summary>
        /// Run the External Request Sender
        /// </summary>
        /// <param name="cancellationToken">Stops before the next request; requests already locked are released.</param>
        public async Task<List<string>> Run(CancellationToken cancellationToken = default)
        {
            List<string> FailedRequests = [];
            cancellationToken.ThrowIfCancellationRequested();
            ExternalRequestDataHelper openRequests = await apiConnection.SendQueryAsync<ExternalRequestDataHelper>(ExtRequestQueries.getAndLockOpenRequests, new { states = openRequestStates });
            try
            {
                foreach (ExternalRequest request in openRequests.ExternalRequests)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await HandleRequest(request, FailedRequests);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // unprocessed requests are still locked and would otherwise be blocked
                await ReleaseRemainingLocks(openRequests.ExternalRequests);
                throw;
            }
            await ReleaseRemainingLocks(openRequests.ExternalRequests);
            return FailedRequests;
        }

        private async Task HandleRequest(ExternalRequest request, List<string> FailedRequests)
        {
            try
            {
                if (IsFirewallWriteRequest(request))
                {
                    await HandleFirewallWriteRequest(request);
                    await ReleaseLock(request);
                    return;
                }
                ExtTicketSystem = ReadTicketSystem(request);
                if (ExtTicketSystem == null || !ExtTicketSystem.IsTufinSecureChange())
                {
                    await DiscardUnsupportedRequest(request);
                    await ReleaseLock(request);
                    return;
                }
                if (request.ExtRequestState == ExtStates.ExtReqInitialized.ToString() ||
                    request.ExtRequestState == ExtStates.ExtReqFailed.ToString()) // try again
                {
                    if (request.WaitCycles > 0)
                    {
                        await CountDownWaitCycle(request);
                    }
                    else
                    {
                        await SendRequest(request);
                    }
                }
                else
                {
                    await RefreshState(request);
                }
                await ReleaseLock(request);
            }
            catch (Exception exception)
            {
                Log.WriteError(LogMessageTitle, "Runs into exception: ", exception);
                FailedRequests.Add(RequestInfo(request));
            }
        }

        /// <summary>The external ticket system of a request; null if it cannot be read.</summary>
        private static ExternalTicketSystem? ReadTicketSystem(ExternalRequest request)
        {
            try
            {
                return JsonSerializer.Deserialize<ExternalTicketSystem>(request.ExtTicketSystem);
            }
            catch (JsonException exception)
            {
                Log.WriteWarning(LogMessageTitle, $"{RequestInfo(request)}: the external ticket system cannot be read: {exception.Message}");
                return null;
            }
        }

        /// <summary>
        /// Discards a request that is neither written to the firewall nor handed to Tufin SecureChange, e.g. one of the
        /// former Check Point ticket path created before the upgrade. It would fail in every cycle and, as the only active
        /// request of its ticket, block the ticket; discarding it ends it with the reason, the ticket can be implemented
        /// again by its actions.
        /// </summary>
        private async Task DiscardUnsupportedRequest(ExternalRequest request)
        {
            request.LastMessage = "This kind of external request is not supported anymore (only Tufin SecureChange and the direct firewall write); " +
                "the request is discarded. Start the implementation of the ticket again.";
            request.ExtRequestState = ExtStates.ExtReqDiscarded.ToString();
            Log.WriteWarning(LogMessageTitle, $"{RequestInfo(request)}: {request.LastMessage}");
            await UpdateRequestCreation(request);
            // finishing the request ends its block of the ticket (one active request per ticket)
            await apiConnection.SendQueryAsync<ReturnId>(ExtRequestQueries.updateExtRequestFinal,
                new { id = request.Id, extRequestState = request.ExtRequestState, finishDate = DateTime.Now });
        }

        /// <summary>Releases the lock of a request taken for this run.</summary>
        private async Task ReleaseLock(ExternalRequest request)
        {
            if ((await apiConnection.SendQueryAsync<ReturnId>(ExtRequestQueries.updateExternalRequestLock, new { id = request.Id, locked = false })).UpdatedIdLong == request.Id)
            {
                request.Locked = false;
            }
        }

        private static string RequestInfo(ExternalRequest request)
        {
            return $"Request Id: {request.Id}, Internal TicketId: {request.TicketId}, TaskNo: {request.TaskNumber}";
        }

        private async Task ReleaseRemainingLocks(List<ExternalRequest> requests)
        {
            try
            {
                foreach (ExternalRequest? request in requests.Where(r => r.Locked))
                {
                    await apiConnection.SendQueryAsync<ReturnId>(ExtRequestQueries.updateExternalRequestLock, new { id = request.Id, locked = false });
                }
            }
            catch (Exception exception)
            {
                Log.WriteError(LogMessageTitle, "Release Lock runs into exception: ", exception);
            }
        }

        private async Task SendRequest(ExternalRequest request)
        {
            ExternalTicket? ticket = null;

            try
            {
                ticket = await ConstructTicket(request);

                if (ExtTicketSystem == null)
                {
                    throw new InvalidOperationException("No external ticket system loaded.");
                }

                await SendDefaultRequest(request, ticket);
            }
            catch (ProcessingFailedException)
            {
                throw;
            }
            catch (Exception)
            {
                if (!await HandleTimeOut(request, ticket))
                {
                    throw;
                }
            }
        }

        private async Task SendDefaultRequest(ExternalRequest request, ExternalTicket ticket)
        {
            Log.WriteInfo(LogMessageTitle, $"Sending {RequestInfo(request)}");
            request.Attempts++;
            await ResolveAuthorization(request, ticket.TicketSystem, ticket.OnManagement);

            RestResponse<int> ticketIdResponse = await ticket.CreateExternalTicket();
            request.LastMessage = ticketIdResponse.Content;

            if (ticketIdResponse.StatusCode == HttpStatusCode.OK || ticketIdResponse.StatusCode == HttpStatusCode.Created)
            {
                string? externalTicketId = ExtractExternalTicketId(ticketIdResponse);
                if (!string.IsNullOrWhiteSpace(externalTicketId))
                {
                    request.ExtTicketId = externalTicketId;
                }

                request.ExtRequestState = ExtStates.ExtReqRequested.ToString();
                await UpdateRequestCreation(request);

                try
                {
                    using ExternalRequestHandler extReqHandler = new(userConfig, apiConnection);
                    await extReqHandler.HandleStateChange(request);
                }
                catch (Exception exception)
                {
                    Log.WriteError(LogMessageTitle, $"{RequestInfo(request)} was created externally, but ticket sync failed.", exception);
                }

                Log.WriteDebug(LogMessageTitle, $"{RequestInfo(request)}. Success Message: " + ticketIdResponse.Content);
            }
            else
            {
                Log.WriteError(LogMessageTitle, $"{RequestInfo(request)}. Error Message: " + ticketIdResponse.StatusDescription + ", " + ticketIdResponse.Content);

                if (AnalyseForRejected(ticketIdResponse))
                {
                    await RejectRequest(request);
                }
                else
                {
                    request.ExtRequestState = ExtStates.ExtReqFailed.ToString();
                    await UpdateRequestCreation(request);
                }

                throw new ProcessingFailedException(
                    $"External request failed for {RequestInfo(request)} with status {(int)ticketIdResponse.StatusCode} " +
                    $"{ticketIdResponse.StatusCode}: {ticketIdResponse.Content}");
            }
        }

        /// <summary>
        /// Builds the Authorization header string of the ticket system right before the call. A failure is reported as
        /// a failed attempt (not as ProcessingFailedException), so the request is retried and finally rejected after the
        /// configured number of attempts instead of being retried in every cycle.
        /// </summary>
        /// <param name="request">The external request, its last message is set on failure.</param>
        /// <param name="ticketSystem">The ticket system stored with the request.</param>
        /// <param name="management">Management of the request, whose settings give the credential currently configured.</param>
        private async Task ResolveAuthorization(ExternalRequest request, ExternalTicketSystem ticketSystem, Management? management)
        {
            try
            {
                await ExternalTicketAuthorization.ResolveHeader(ticketSystem, apiConnection, await LoadConfiguredSystem(management));
            }
            catch (Exception exception)
            {
                request.LastMessage = exception.Message;
                Log.WriteError(LogMessageTitle, $"{RequestInfo(request)}: no authorization for the external ticket system.", exception);
                throw new InvalidOperationException(exception.Message, exception);
            }
        }

        private async Task<ExternalTicket> ConstructTicket(ExternalRequest request)
        {
            if (ExtTicketSystem == null)
            {
                throw new InvalidOperationException("No external ticket system loaded.");
            }

            ExternalTicket ticket = ExternalTicketFactory.Create(ExtTicketSystem, userConfig.GetExternalCertificateChecks(), InjScClient);
            ticket.TicketText = request.ExtRequestContent;
            ticket.TicketSystem = ExtTicketSystem;
            ticket.ExtQueryVariables = request.ExtQueryVariables;
            ticket.OnManagement = await LoadManagementForRequest(request);

            return ticket;
        }

        private static int? GetManagementId(string extQueryVariables)
        {
            if (string.IsNullOrWhiteSpace(extQueryVariables))
            {
                return null;
            }

            Dictionary<string, List<int>>? extQueryVars =
                JsonSerializer.Deserialize<Dictionary<string, List<int>>>(extQueryVariables);

            return extQueryVars != null
                && extQueryVars.TryGetValue(ExternalVarKeys.ManagementId, out List<int>? ids)
                && ids.Count > 0
                ? ids[0]
                : null;
        }

        private async Task<Management?> LoadManagementForRequest(ExternalRequest request)
        {
            int? managementId = GetManagementId(request.ExtQueryVariables);
            if (managementId == null)
            {
                return null;
            }

            var variables = new { id = managementId.Value };
            List<Management> managements = await apiConnection.SendQueryAsync<List<Management>>(DeviceQueries.getManagementById, variables);

            return managements.FirstOrDefault();
        }

        private async Task RejectRequest(ExternalRequest request)
        {
            request.ExtRequestState = ExtStates.ExtReqRejected.ToString();
            await UpdateRequestCreation(request);
            using ExternalRequestHandler extReqHandler = new(userConfig, apiConnection);
            await extReqHandler.HandleStateChange(request);
        }

        private async Task<bool> HandleTimeOut(ExternalRequest request, ExternalTicket? ticket)
        {
            if (ticket != null && request.Attempts > 0)
            {
                try
                {
                    if (request.Attempts >= ticket.TicketSystem.MaxAttempts)
                    {
                        await RejectRequest(request);
                    }
                    else
                    {
                        request.ExtRequestState = ExtStates.ExtReqFailed.ToString();
                        request.WaitCycles = request.Attempts * ticket.TicketSystem.CyclesBetweenAttempts;
                        await UpdateRequestCreation(request);
                        return true;
                    }
                }
                catch (Exception exception)
                {
                    Log.WriteError(LogMessageTitle, "Timeout handling failed: ", exception);
                }
            }
            return false;
        }

        private static bool AnalyseForRejected(RestResponse<int>? ticketIdResponse)
        {
            return ticketIdResponse != null && ticketIdResponse.Content != null &&
                (
                ticketIdResponse.Content.Contains("Check Point rule change tasks are not yet supported.") ||
                (ticketIdResponse.Content.Contains("GENERAL_ERROR") && !TryAgain(ticketIdResponse)) ||
                ticketIdResponse.Content.Contains("ILLEGAL_ARGUMENT_ERROR") ||
                ticketIdResponse.Content.Contains("FIELD_VALIDATION_ERROR") ||
                ticketIdResponse.Content.Contains("WEB_APPLICATION_ERROR") ||
                ticketIdResponse.Content.Contains("implementation failure"));
        }

        private static bool TryAgain(RestResponse<int> ticketIdResponse)
        {
            return ticketIdResponse.Content != null &&
                ticketIdResponse.Content.Contains("Unable to rollback against JDBC Connection");
        }

        private async Task RefreshState(ExternalRequest request)
        {
            (request.ExtRequestState, request.LastMessage) = await PollState(request);
            await UpdateRequestProcess(request);

            if (request.ExtRequestState == ExtStates.ExtReqDone.ToString() || request.ExtRequestState == ExtStates.ExtReqRejected.ToString())
            {
                using ExternalRequestHandler extReqHandler = new(userConfig, apiConnection);
                await extReqHandler.HandleStateChange(request);
            }
        }

        /// <summary>
        /// The Tufin SecureChange system as currently configured in the device specific settings of a management, so that
        /// a changed credential applies to open requests as well. Null without management or if the settings cannot be
        /// read; the credential stored with the request applies then.
        /// </summary>
        private async Task<ExternalTicketSystem?> LoadConfiguredSystem(Management? management)
        {
            if (management == null)
            {
                return null;
            }
            try
            {
                GlobalDeviceSpecificSettings? settings = await (ManagementSettingsLoader ?? LoadManagementSettings)(management);
                return settings == null ? null : ExternalTicketSystem.FromSecureChangeSettings(settings);
            }
            catch (Exception exception)
            {
                Log.WriteWarning(LogMessageTitle, $"Settings of management {management.Id} not readable, the credential stored with the request applies: {exception.Message}");
                return null;
            }
        }

        /// <summary>
        /// Loads the device specific settings in effect for a management (settable for unit testing only).
        /// </summary>
        internal Func<Management, Task<GlobalDeviceSpecificSettings?>>? ManagementSettingsLoader { get; init; }

        /// <summary>Loads the device specific settings in effect for a management; null if they cannot be resolved.</summary>
        private async Task<GlobalDeviceSpecificSettings?> LoadManagementSettings(Management management)
        {
            List<DeviceSpecificScopePath> paths = [DeviceSpecificScopePath.ForManagement(management)];
            List<GlobalDeviceSpecificSettings> settings = await new DeviceSpecificSettingsManager(apiConnection).LoadEffectiveSettingsAsync(paths);
            return settings.FirstOrDefault();
        }

        private async Task<(string, string?)> PollState(ExternalRequest request)
        {
            try
            {
                if (ExtTicketSystem == null)
                {
                    throw new InvalidOperationException("No external ticket system loaded.");
                }

                ExternalTicket ticket = ExternalTicketFactory.Create(
                    ExtTicketSystem,
                    userConfig.GetExternalCertificateChecks(),
                    InjScClient
                );

                ticket.TicketId = request.ExtTicketId;
                await ExternalTicketAuthorization.ResolveHeader(ExtTicketSystem, apiConnection,
                    await LoadConfiguredSystem(await LoadManagementForRequest(request)));

                return await ticket.GetNewState(request.ExtRequestState);
            }
            catch (Exception exc)
            {
                request.LastMessage = exc.Message;
                await UpdateRequestProcess(request);
                throw;
            }
        }

        private async Task UpdateRequestCreation(ExternalRequest request)
        {
            request.LastCreationResponse = request.LastMessage;

            var Variables = new
            {
                id = request.Id,
                extRequestState = request.ExtRequestState,
                extTicketId = request.ExtTicketId,
                creationResponse = request.LastCreationResponse,
                waitCycles = request.WaitCycles,
                attempts = request.Attempts
            };

            await apiConnection.SendQueryAsync<ReturnId>(ExtRequestQueries.updateExtRequestCreation, Variables);
        }

        private async Task UpdateRequestProcess(ExternalRequest request)
        {
            try
            {
                request.LastProcessingResponse = request.LastMessage;

                var Variables = new
                {
                    id = request.Id,
                    extRequestState = request.ExtRequestState,
                    processingResponse = request.LastProcessingResponse
                };

                await apiConnection.SendQueryAsync<ReturnId>(ExtRequestQueries.updateExtRequestProcess, Variables);
            }
            catch (Exception exception)
            {
                Log.WriteError(LogMessageTitle, "UpdateRequestProcess failed: ", exception);
            }
        }

        private async Task CountDownWaitCycle(ExternalRequest request)
        {
            var Variables = new
            {
                id = request.Id,
                waitCycles = --request.WaitCycles
            };
            await apiConnection.SendQueryAsync<ReturnId>(ExtRequestQueries.updateExternalRequestWaitCycles, Variables);
        }

        /// <summary>
        /// Extracts the created external ticket id from the response body or Location header.
        /// </summary>
        private static string? ExtractExternalTicketId(RestResponse<int> response)
        {
            string? ticketIdFromBody = ExtractExternalTicketIdFromBody(response.Content);
            if (!string.IsNullOrWhiteSpace(ticketIdFromBody))
            {
                return ticketIdFromBody;
            }

            string? locationHeader = response.Headers?
                .FirstOrDefault(h => h.Name.Equals("location", StringComparison.OrdinalIgnoreCase))?
                .Value?.ToString();

            if (string.IsNullOrWhiteSpace(locationHeader))
            {
                return null;
            }

            if (Uri.TryCreate(locationHeader, UriKind.Absolute, out Uri? locationUri))
            {
                return locationUri.Segments.LastOrDefault()?.TrimEnd('/');
            }

            return locationHeader.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        }

        /// <summary>
        /// Extracts the created external ticket id from ticket.id or id in the JSON response body.
        /// </summary>
        private static string? ExtractExternalTicketIdFromBody(string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(content);

                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                if (document.RootElement.TryGetProperty("ticket", out JsonElement ticket) && ticket.ValueKind == JsonValueKind.Object && ticket.TryGetProperty("id", out JsonElement ticketId))
                {
                    return ticketId.ToString();
                }

                if (document.RootElement.TryGetProperty("id", out JsonElement id))
                {
                    return id.ToString();
                }
            }
            catch (JsonException)
            {
                return null;
            }

            return null;
        }

        /// <summary>
        /// Dispose method to clean up resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose method
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    userConfig?.Dispose();
                }
                disposed = true;
            }
        }
    }
}

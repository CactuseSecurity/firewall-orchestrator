using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Basics.Exceptions;
using FWO.Config.Api;
using FWO.Data;
using FWO.Logging;
using FWO.Services;
using FWO.ExternalSystems;
using FWO.ExternalSystems.Tufin.SecureChange;
using RestSharp;
using System.Net;
using System.Text.Json;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Class handling the sending of external requests
    /// </summary>
    public class ExternalRequestSender : IDisposable
    {
        private static readonly TimeSpan OverdueRequestThreshold = TimeSpan.FromHours(24);
        private static readonly TimeSpan DefaultLockLeaseDuration = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan MinimumLockLeaseDuration = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan LockLeaseSafetyBuffer = TimeSpan.FromMinutes(2);

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
        private readonly string lockOwner = $"{Environment.MachineName}:{Guid.NewGuid():N}";
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
        public ExternalRequestSender(ApiConnection apiConnection, GlobalConfig globalConfig, SCClient? injScClient = null)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
            userConfig = new(globalConfig, apiConnection, new() { Language = GlobalConst.kEnglish });
            InjScClient = injScClient;
        }

        /// <summary>
        /// Run the External Request Sender
        /// </summary>
        public async Task<List<string>> Run()
        {
            List<string> FailedRequests = [];
            List<ExternalRequest> openRequests = await apiConnection.SendQueryAsync<List<ExternalRequest>>(ExtRequestQueries.getOpenRequests,
                new { states = openRequestStates, currentTime = DateTime.UtcNow });
            foreach (ExternalRequest request in openRequests)
            {
                await HandleRequest(request, FailedRequests);
            }
            return FailedRequests;
        }

        private async Task HandleRequest(ExternalRequest request, List<string> FailedRequests)
        {
            bool requestFailed = false;
            try
            {
                if (!await TryLockRequest(request))
                {
                    return;
                }
                await SetOverdueAlertIfNeeded(request);
                ExtTicketSystem = JsonSerializer.Deserialize<ExternalTicketSystem>(request.ExtTicketSystem) ?? throw new JsonException("No Ticket System");
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
            }
            catch (Exception exception)
            {
                requestFailed = true;
                Log.WriteError(LogMessageTitle, "Runs into exception: ", exception);
                FailedRequests.Add(RequestInfo(request));
            }
            finally
            {
                if (request.Locked)
                {
                    try
                    {
                        await UnlockRequest(request);
                    }
                    catch (Exception exception)
                    {
                        Log.WriteError(LogMessageTitle, "Release Lock runs into exception: ", exception);
                        if (!requestFailed)
                        {
                            FailedRequests.Add(RequestInfo(request));
                        }
                    }
                }
            }
        }

        private static string RequestInfo(ExternalRequest request)
        {
            return $"Request Id: {request.Id}, Internal TicketId: {request.TicketId}, TaskNo: {request.TaskNumber}";
        }

        private async Task SendRequest(ExternalRequest request)
        {
            ExternalTicket? ticket = ConstructTicket(request);
            try
            {
                Log.WriteInfo(LogMessageTitle, $"Sending {RequestInfo(request)}");
                await RenewLockRequest(request);
                request.Attempts++;
                RestResponse<int> ticketIdResponse = await ticket.CreateExternalTicket();
                request.LastMessage = ticketIdResponse.Content;
                if (ticketIdResponse.StatusCode == HttpStatusCode.OK || ticketIdResponse.StatusCode == HttpStatusCode.Created)
                {
                    var locationHeader = ticketIdResponse.Headers?.FirstOrDefault(h => h.Name.Equals("location", StringComparison.OrdinalIgnoreCase))?.Value?.ToString();
                    if (!string.IsNullOrEmpty(locationHeader))
                    {
                        Uri locationUri = new(locationHeader);
                        request.ExtTicketId = locationUri.Segments[^1];
                    }
                    request.ExtRequestState = ExtStates.ExtReqRequested.ToString();
                    await UpdateRequestCreation(request);
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
                    throw new ProcessingFailedException("RestResponse: HttpStatusCode not OK");
                }
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

        private ExternalTicket ConstructTicket(ExternalRequest request)
        {
            ExternalTicket ticket;

            if (ExtTicketSystem?.Type == ExternalTicketSystemType.TufinSecureChange)
            {
                ticket = new SCTicket(ExtTicketSystem, InjScClient)
                {
                    TicketSystem = ExtTicketSystem,
                    TicketText = request.ExtRequestContent
                };
            }
            else
            {
                throw new NotSupportedException("Ticket system not supported yet");
            }
            return ticket;
        }

        private async Task RejectRequest(ExternalRequest request)
        {
            request.ExtRequestState = ExtStates.ExtReqRejected.ToString();
            await RenewLockRequest(request);
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
                ((ticketIdResponse.Content.Contains("GENERAL_ERROR") && !TryAgain(ticketIdResponse)) ||
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
            await RenewLockRequest(request);
            using ExternalRequestHandler extReqHandler = new(userConfig, apiConnection);
            await extReqHandler.HandleStateChange(request);
        }

        private async Task<(string, string?)> PollState(ExternalRequest request)
        {
            try
            {
                ExternalTicket ticket;
                if (ExtTicketSystem?.Type == ExternalTicketSystemType.TufinSecureChange)
                {
                    ticket = new SCTicket(ExtTicketSystem, InjScClient)
                    {
                        TicketId = request.ExtTicketId
                    };
                }
                else
                {
                    throw new NotSupportedException("Ticket system not supported yet");
                }
                await RenewLockRequest(request);
                return await ticket.GetNewState(request.ExtRequestState);
            }
            catch (Exception exc)
            {
                request.LastMessage = exc.Message;
                try
                {
                    await UpdateRequestProcess(request);
                }
                catch (Exception updateException)
                {
                    throw new AggregateException(exc, updateException);
                }
                throw;
            }
        }

        private async Task UpdateRequestCreation(ExternalRequest request)
        {
            var Variables = new
            {
                id = request.Id,
                extRequestState = request.ExtRequestState,
                extTicketId = request.ExtTicketId,
                creationResponse = request.LastMessage,
                waitCycles = request.WaitCycles,
                attempts = request.Attempts
            };
            ReturnId result = await apiConnection.SendQueryAsync<ReturnId>(ExtRequestQueries.updateExtRequestCreation, Variables);
            if (result.UpdatedIdLong != request.Id)
            {
                throw new InvalidOperationException($"External request creation update failed for request {request.Id}.");
            }
        }

        private async Task UpdateRequestProcess(ExternalRequest request)
        {
            var Variables = new
            {
                id = request.Id,
                extRequestState = request.ExtRequestState,
                processingResponse = request.LastMessage
            };
            ReturnId result = await apiConnection.SendQueryAsync<ReturnId>(ExtRequestQueries.updateExtRequestProcess, Variables);
            if (result.UpdatedIdLong != request.Id)
            {
                throw new InvalidOperationException($"External request process update failed for request {request.Id}.");
            }
        }

        private async Task CountDownWaitCycle(ExternalRequest request)
        {
            var Variables = new
            {
                id = request.Id,
                waitCycles = --request.WaitCycles
            };
            ReturnId result = await apiConnection.SendQueryAsync<ReturnId>(ExtRequestQueries.updateExternalRequestWaitCycles, Variables);
            if (result.UpdatedIdLong != request.Id)
            {
                throw new InvalidOperationException($"External request wait cycle update failed for request {request.Id}.");
            }
        }

        private async Task<bool> TryLockRequest(ExternalRequest request)
        {
            DateTime lockAcquiredAt = DateTime.UtcNow;
            DateTime lockExpiresAt = GetLockExpiration(request, lockAcquiredAt);
            ReturnId result = await apiConnection.SendQueryAsync<ReturnId>(ExtRequestQueries.tryLockExternalRequest,
                new { id = request.Id, states = openRequestStates, lockOwner, lockAcquiredAt, lockExpiresAt, currentTime = lockAcquiredAt });
            request.Locked = result.AffectedRows == 1;
            if (request.Locked)
            {
                SetLockMetadata(request, lockAcquiredAt, lockExpiresAt);
            }
            return request.Locked;
        }

        private async Task RenewLockRequest(ExternalRequest request)
        {
            if (!request.Locked)
            {
                return;
            }

            DateTime lockAcquiredAt = DateTime.UtcNow;
            DateTime lockExpiresAt = GetLockExpiration(request, lockAcquiredAt);
            ReturnId result = await apiConnection.SendQueryAsync<ReturnId>(ExtRequestQueries.renewExternalRequestLock,
                new { id = request.Id, lockOwner, lockAcquiredAt, lockExpiresAt });
            if (result.AffectedRows != 1)
            {
                throw new InvalidOperationException($"External request lock renewal failed for request {request.Id}.");
            }
            SetLockMetadata(request, lockAcquiredAt, lockExpiresAt);
        }

        private async Task UnlockRequest(ExternalRequest request)
        {
            ReturnId result = await apiConnection.SendQueryAsync<ReturnId>(ExtRequestQueries.updateExternalRequestLock,
                new { id = request.Id, lockOwner, locked = false });
            if (result.AffectedRows != 1)
            {
                throw new InvalidOperationException($"External request lock release failed for request {request.Id}.");
            }
            request.Locked = false;
            request.LockOwner = null;
            request.LockAcquiredAt = null;
            request.LockExpiresAt = null;
        }

        private void SetLockMetadata(ExternalRequest request, DateTime lockAcquiredAt, DateTime lockExpiresAt)
        {
            request.LockOwner = lockOwner;
            request.LockAcquiredAt = lockAcquiredAt;
            request.LockExpiresAt = lockExpiresAt;
        }

        private static DateTime GetLockExpiration(ExternalRequest request, DateTime lockAcquiredAt)
        {
            return lockAcquiredAt + GetLockLeaseDuration(request);
        }

        private static TimeSpan GetLockLeaseDuration(ExternalRequest request)
        {
            try
            {
                ExternalTicketSystem? ticketSystem = JsonSerializer.Deserialize<ExternalTicketSystem>(request.ExtTicketSystem);
                if (ticketSystem?.ResponseTimeout > 0)
                {
                    TimeSpan systemLeaseDuration = TimeSpan.FromSeconds(ticketSystem.ResponseTimeout) + LockLeaseSafetyBuffer;
                    return systemLeaseDuration > MinimumLockLeaseDuration ? systemLeaseDuration : MinimumLockLeaseDuration;
                }
            }
            catch (JsonException exception)
            {
                Log.WriteWarning(LogMessageTitle,
                    $"{RequestInfo(request)} has invalid external ticket system configuration. Using default lock lease duration. Details: {exception.Message}");
            }
            return DefaultLockLeaseDuration;
        }

        private async Task SetOverdueAlertIfNeeded(ExternalRequest request)
        {
            if (request.CreationDate <= DateTime.MinValue.AddYears(1))
            {
                return;
            }

            if (DateTime.UtcNow - request.CreationDate < OverdueRequestThreshold)
            {
                return;
            }

            string title = "External request overdue";
            string description = $"{RequestInfo(request)} has been open since {request.CreationDate:O} in state {request.ExtRequestState}.";

            await AlertHelper.SetAlert(apiConnection, title, description, GlobalConst.kExternalRequest, AlertCode.ExternalRequest,
                new AlertHelper.AdditionalAlertData
                {
                    CompareDesc = true,
                    CompareTitle = true,
                    JsonData = new
                    {
                        request.Id,
                        request.TicketId,
                        request.TaskNumber,
                        request.ExtRequestState,
                        request.CreationDate
                    }
                });
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

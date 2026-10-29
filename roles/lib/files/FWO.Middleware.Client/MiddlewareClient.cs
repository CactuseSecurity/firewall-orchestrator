using FWO.Api.Client;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Logging;
using RestSharp;

namespace FWO.Middleware.Client
{
    public class MiddlewareClient : RestApiClient, IDisposable
    {
        /// <summary>
        /// Response header of the middleware list endpoints telling whether further items follow the returned page.
        /// </summary>
        public const string kHasMoreHeader = "X-Has-More";

        // pages read at most for one list, so a server that keeps answering with further pages cannot hold the client
        internal const int kMaxPagesPerList = 1000;

        private bool disposed = false;

        /// <summary>
        /// Creates a client for the middleware REST API.
        /// </summary>
        /// <remarks>
        /// Certificate checking is on: this connection carries the user's password on
        /// its way to being exchanged for a JWT, so accepting any server certificate
        /// would hand those credentials to anything answering on the port. Platform
        /// validation is enough because the installer adds the internal CA to the
        /// operating system trust store on every FWO host (internalCA/install-trust.yml),
        /// and a customer-managed certificate is validated against its own issuer.
        /// </remarks>
        /// <param name="middlewareServerUri">Base uri of the middleware server.</param>
        public MiddlewareClient(string middlewareServerUri) : base(middlewareServerUri + "api/", checkCertificates: true)
        { }

        public async Task<RestResponse<TokenPair>> AuthenticateUser(AuthenticationTokenGetParameters parameters)
        {
            RestRequest request = new("AuthenticationToken/GetTokenPair", Method.Post);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<TokenPair>(request);
        }

        public async Task<RestResponse<TokenPair>> CreateInitialJWT(CancellationToken cancellationToken = default)
        {
            RestRequest request = new("AuthenticationToken/GetTokenPair", Method.Post);
            request.AddJsonBody(new object());
            return await restClient.ExecuteAsync<TokenPair>(request, cancellationToken);
        }

        public async Task<RestResponse<int>> TestConnection(LdapGetUpdateParameters parameters)
        {
            RestRequest request = new("AuthenticationServer/TestConnection", Method.Post);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<int>(request);
        }

        public async Task<RestResponse<List<LdapGetUpdateParameters>>> GetLdaps()
        {
            RestRequest request = new("AuthenticationServer", Method.Get);
            request.AddJsonBody(new object());
            return await restClient.ExecuteAsync<List<LdapGetUpdateParameters>>(request);
        }

        public async Task<RestResponse<int>> AddLdap(LdapAddParameters parameters)
        {
            RestRequest request = new("AuthenticationServer", Method.Post);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<int>(request);
        }

        public async Task<RestResponse<int>> UpdateLdap(LdapGetUpdateParameters parameters)
        {
            RestRequest request = new("AuthenticationServer", Method.Put);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<int>(request);
        }

        public async Task<RestResponse<int>> DeleteLdap(LdapDeleteParameters parameters)
        {
            RestRequest request = new("AuthenticationServer", Method.Delete);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<int>(request);
        }

        public virtual async Task<RestResponse<string>> ChangePassword(UserChangePasswordParameters parameters)
        {
            RestRequest request = new("User/EditPassword", Method.Patch);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<string>(request);
        }

        public async Task<RestResponse<List<RoleGetReturnParameters>>> GetAllRoles()
        {
            RestRequest request = new("Role", Method.Get);
            return await restClient.ExecuteAsync<List<RoleGetReturnParameters>>(request);
        }

        public async Task<RestResponse<List<string>>> GetGroups(GroupGetParameters parameters)
        {
            return await ExecutePagedPostAsync<string>("Group/Get", parameters);
        }

        public async Task<RestResponse<List<string>>> GetGroupMembers(GroupMemberGetParameters parameters)
        {
            return await ExecutePagedPostAsync<string>("Group/Members", parameters);
        }

        public async Task<RestResponse<List<string>>> ResolveGroupMembers(GroupResolveParameters parameters)
        {
            return await ExecutePagedPostAsync<string>("Group/Resolve", parameters);
        }

        /// <summary>
        /// Resolves explicitly requested Flow network and service groups through the middleware API.
        /// </summary>
        public virtual async Task<RestResponse<FlowGroupResolutionResult>> ResolveFlowGroupMembers(FlowGroupResolutionParameters parameters)
        {
            RestRequest request = new("flow/resolveGroupMembers", Method.Post);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<FlowGroupResolutionResult>(request);
        }

        public async Task<RestResponse<List<string>>> GetGroupMemberships(GroupMembershipGetParameters parameters)
        {
            return await ExecutePagedPostAsync<string>("Group/Memberships", parameters);
        }

        public async Task<RestResponse<List<GroupGetReturnParameters>>> GetInternalGroups()
        {
            return await ExecuteAllPagesAsync<GroupGetReturnParameters>(offset => BuildPagedGetRequest("Group", offset, addEmptyBody: false));
        }

        public async Task<RestResponse<List<UserGetReturnParameters>>> GetUsers()
        {
            return await ExecuteAllPagesAsync<UserGetReturnParameters>(offset => BuildPagedGetRequest("User", offset, addEmptyBody: true));
        }

        public async Task<RestResponse<List<LdapUserGetReturnParameters>>> GetLdapUsers(LdapUserGetParameters parameters)
        {
            return await ExecutePagedPostAsync<LdapUserGetReturnParameters>("User/Get", parameters);
        }

        public async Task<RestResponse<int>> AddUser(UserAddParameters parameters)
        {
            RestRequest request = new("User", Method.Post);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<int>(request);
        }

        public async Task<RestResponse<bool>> UpdateUser(UserEditParameters parameters)
        {
            RestRequest request = new("User", Method.Put);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<bool>(request);
        }

        public async Task<RestResponse<string>> SetPassword(UserResetPasswordParameters parameters)
        {
            RestRequest request = new("User/ResetPassword", Method.Patch);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<string>(request);
        }

        public async Task<RestResponse<bool>> DeleteUser(UserDeleteParameters parameters)
        {
            RestRequest request = new("User", Method.Delete);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<bool>(request);
        }

        public async Task<RestResponse<string>> AddGroup(GroupAddDeleteParameters parameters)
        {
            RestRequest request = new("Group", Method.Post);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<string>(request);
        }

        public async Task<RestResponse<string>> UpdateGroup(GroupEditParameters parameters)
        {
            RestRequest request = new("Group", Method.Put);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<string>(request);
        }

        public async Task<RestResponse<bool>> DeleteGroup(GroupAddDeleteParameters parameters)
        {
            RestRequest request = new("Group", Method.Delete);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<bool>(request);
        }

        public async Task<RestResponse<bool>> AddUserToRole(RoleAddDeleteUserParameters parameters)
        {
            RestRequest request = new("Role/User", Method.Post);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<bool>(request);
        }

        public async Task<RestResponse<bool>> RemoveUserFromRole(RoleAddDeleteUserParameters parameters)
        {
            RestRequest request = new("Role/User", Method.Delete);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<bool>(request);
        }

        public async Task<RestResponse<bool>> AddUserToGroup(GroupAddDeleteUserParameters parameters)
        {
            RestRequest request = new("Group/User", Method.Post);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<bool>(request);
        }

        public async Task<RestResponse<bool>> RemoveUserFromGroup(GroupAddDeleteUserParameters parameters)
        {
            RestRequest request = new("Group/User", Method.Delete);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<bool>(request);
        }

        public async Task<RestResponse<bool>> RemoveUserFromAllEntries(UserDeleteAllEntriesParameters parameters)
        {
            RestRequest request = new("User/AllGroupsAndRoles", Method.Delete);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<bool>(request);
        }

        public async Task<RestResponse<List<TenantGetReturnParameters>>> GetTenants()
        {
            return await ExecuteAllPagesAsync<TenantGetReturnParameters>(offset => BuildPagedGetRequest("Tenant", offset, addEmptyBody: true));
        }

        public async Task<RestResponse<int>> AddTenant(TenantAddParameters parameters)
        {
            RestRequest request = new("Tenant", Method.Post);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<int>(request);
        }

        public async Task<RestResponse<bool>> UpdateTenant(TenantEditParameters parameters)
        {
            RestRequest request = new("Tenant", Method.Put);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<bool>(request);
        }

        public async Task<RestResponse<bool>> DeleteTenant(TenantDeleteParameters parameters)
        {
            RestRequest request = new("Tenant", Method.Delete);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<bool>(request);
        }

        public async Task<RestResponse<bool>> AddExternalRequest(ExternalRequestAddParameters parameters)
        {
            RestRequest request = new("ExternalRequest", Method.Post);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<bool>(request);
        }

        public async Task<RestResponse<bool>> PatchExternalRequestState(ExternalRequestPatchStateParameters parameters)
        {
            RestRequest request = new("ExternalRequest/PatchState", Method.Patch);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<bool>(request);
        }

        public async Task<RestResponse<WorkflowActionResult>> ExecuteWorkflowActions(WorkflowActionParameters parameters)
        {
            RestRequest request = new("Workflow/Actions", Method.Post);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<WorkflowActionResult>(request);
        }

        /// <summary>
        /// Resolves the target managements of an object task with the rights of the middleware server.
        /// </summary>
        /// <param name="parameters">Management of the request task and kind of the ordered objects.</param>
        /// <returns>The REST response containing the target managements.</returns>
        public virtual async Task<RestResponse<ImplTaskTargetResult>> GetImplTaskTargets(ImplTaskTargetParameters parameters)
        {
            RestRequest request = new("workflow/getImplTaskTargets", Method.Post);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<ImplTaskTargetResult>(request);
        }

        /// <summary>
        /// Processes the configured immediate notification for an interface request.
        /// </summary>
        /// <param name="connectionId">Database ID of the requested modelling connection.</param>
        /// <returns>The REST response containing the result of processing the configured notifications.</returns>
        public virtual async Task<RestResponse<NotificationDeliveryResult>> SendInterfaceRequestNotification(int connectionId)
        {
            RestRequest request = new("Notification/interface-request", Method.Post);
            request.AddJsonBody(new InterfaceRequestNotificationParameters { ConnectionId = connectionId });
            return await restClient.ExecuteAsync<NotificationDeliveryResult>(request);
        }

        /// <summary>
        /// Processes configured immediate notifications for applications using a decommissioned interface.
        /// </summary>
        /// <param name="connectionId">Database ID of the decommissioned modelling connection.</param>
        /// <param name="replacementConnectionId">Optional replacement connection ID.</param>
        /// <param name="reason">User-supplied decommission reason.</param>
        /// <returns>The REST response containing the processing result.</returns>
        public virtual async Task<RestResponse<NotificationDeliveryResult>> SendInterfaceDecommissionNotification(
            int connectionId, int? replacementConnectionId, string reason)
        {
            RestRequest request = new("Notification/interface-decommission", Method.Post);
            request.AddJsonBody(new InterfaceDecommissionNotificationParameters
            {
                ConnectionId = connectionId,
                ReplacementConnectionId = replacementConnectionId,
                Reason = reason
            });
            return await restClient.ExecuteAsync<NotificationDeliveryResult>(request);
        }

        public async Task<RestResponse<string>> GetReport(ReportGetParameters parameters)
        {
            RestRequest request = new("Report", Method.Post);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<string>(request);
        }

        public async Task<RestResponse<string>> GetComplianceReport(ComplianceReportParameters parameters)
        {
            RestRequest request = new("Compliance/Report", Method.Post);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<string>(request);
        }

        public async Task<RestResponse<string>> ImportMatrix(ImportMatrixParameters parameters)
        {
            RestRequest request = new("Compliance/ImportMatrix", Method.Post);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<string>(request);
        }
        public async Task<RestResponse<List<SchedulerJobInfo>>> GetSchedulerJobs()
        {
            RestRequest request = new("Scheduler", Method.Get);
            return await restClient.ExecuteAsync<List<SchedulerJobInfo>>(request);
        }

        public async Task<RestResponse<bool>> RunSchedulerJob(SchedulerJobTriggerParameters parameters)
        {
            RestRequest request = new("Scheduler/Run", Method.Post);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<bool>(request);
        }

        public async Task<RestResponse<bool>> RunComplianceCheck()
        {
            RestRequest request = new("Compliance/ComplianceCheck");
            return await restClient.ExecuteAsync<bool>(request);
        }

        public async Task<RestResponse<ComplianceCheckStartResult>> StartComplianceCheck()
        {
            RestRequest request = new("Compliance/ComplianceCheck/Start", Method.Post);
            return await restClient.ExecuteAsync<ComplianceCheckStartResult>(request);
        }

        public async Task<RestResponse<ComplianceCheckJobStatus>> GetComplianceCheckStatus(string jobId)
        {
            RestRequest request = new($"Compliance/ComplianceCheck/Status/{jobId}", Method.Get);
            return await restClient.ExecuteAsync<ComplianceCheckJobStatus>(request);
        }

        /// <summary>
        /// Get a new token pair
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public virtual async Task<RestResponse<TokenPair>> RefreshToken(RefreshTokenRequest parameters)
        {
            RestRequest request = new("AuthenticationToken/Refresh", Method.Post);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<TokenPair>(request);
        }

        /// <summary>
        /// Revoke a refresh token
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public virtual async Task<RestResponse> RevokeRefreshToken(RefreshTokenRequest parameters)
        {
            RestRequest request = new("AuthenticationToken/Revoke", Method.Post);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync(request);
        }

        /// <summary>
        /// Checks whether the session of the authenticated caller is still live, i.e. whether its refresh token has
        /// neither expired nor been revoked.
        /// </summary>
        /// <param name="parameters">Refresh token of the session.</param>
        /// <returns>True if the session is live, false if it has ended.</returns>
        public virtual async Task<RestResponse<bool>> CheckSession(SessionCheckParameters parameters)
        {
            RestRequest request = new("AuthenticationToken/CheckSession", Method.Post);
            request.AddJsonBody(parameters);
            return await restClient.ExecuteAsync<bool>(request);
        }

        /// <summary>
        /// Posts a paged list request. Without a limit and offset of the caller every page is read, so the caller
        /// receives the complete list as before the middleware paged its lists; otherwise only the requested page.
        /// </summary>
        /// <param name="resource">Resource of the list endpoint.</param>
        /// <param name="parameters">Request body carrying the paging keys.</param>
        /// <returns>The response holding the items of every read page.</returns>
        private async Task<RestResponse<List<T>>> ExecutePagedPostAsync<T>(string resource, PagingParameters parameters)
        {
            if (parameters.Limit != null || parameters.Offset != null)
            {
                RestRequest request = new(resource, Method.Post);
                request.AddJsonBody(parameters);
                return await restClient.ExecuteAsync<List<T>>(request);
            }

            try
            {
                return await ExecuteAllPagesAsync<T>(offset =>
                {
                    parameters.Offset = offset == 0 ? null : offset;
                    RestRequest request = new(resource, Method.Post);
                    request.AddJsonBody(parameters);
                    return request;
                });
            }
            finally
            {
                parameters.Offset = null;
            }
        }

        /// <summary>
        /// Builds the request of one page of a paged list read by GET.
        /// </summary>
        /// <param name="resource">Resource of the list endpoint.</param>
        /// <param name="offset">Number of items to skip.</param>
        /// <param name="addEmptyBody">True to send an empty JSON body as the endpoint expects.</param>
        private static RestRequest BuildPagedGetRequest(string resource, int offset, bool addEmptyBody)
        {
            RestRequest request = new(resource, Method.Get);
            if (offset > 0)
            {
                request.AddQueryParameter("offset", offset.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            if (addEmptyBody)
            {
                request.AddJsonBody(new object());
            }
            return request;
        }

        /// <summary>
        /// Reads the pages of a list one after the other while the <see cref="kHasMoreHeader"/> response header
        /// announces further items, at most <see cref="kMaxPagesPerList"/> pages.
        /// </summary>
        /// <param name="buildPageRequest">Builds the request of the page starting at the given offset.</param>
        /// <returns>The response of the last read page holding the items of every read page, the first
        /// unsuccessful response, or an error response without items if further pages follow the last readable one.</returns>
        private async Task<RestResponse<List<T>>> ExecuteAllPagesAsync<T>(Func<int, RestRequest> buildPageRequest)
        {
            List<T> allItems = [];
            int offset = 0;
            RestResponse<List<T>> response;
            int pageCount = 0;
            do
            {
                response = await restClient.ExecuteAsync<List<T>>(buildPageRequest(offset));
                if (!response.IsSuccessful || response.Data == null)
                {
                    return response;
                }
                allItems.AddRange(response.Data);
                offset += response.Data.Count;
                pageCount++;
            }
            while (HasMorePages(response) && response.Data.Count > 0 && pageCount < kMaxPagesPerList);

            if (pageCount >= kMaxPagesPerList && HasMorePages(response) && response.Data.Count > 0)
            {
                return MarkAsIncomplete(response, offset);
            }
            response.Data = allItems;
            return response;
        }

        /// <summary>
        /// Turns the response of the last readable page into an error, so a caller cannot take the read items for
        /// the complete list.
        /// </summary>
        /// <param name="response">Response of the last read page, announcing further items.</param>
        /// <param name="readItemCount">Number of items read before the page limit was reached.</param>
        /// <returns>The response without items, marked as failed.</returns>
        private static RestResponse<List<T>> MarkAsIncomplete<T>(RestResponse<List<T>> response, int readItemCount)
        {
            string resource = response.Request?.Resource ?? "";
            string message = $"List {resource} has more than {readItemCount} items in {kMaxPagesPerList} pages; it is not returned incomplete.";
            Log.WriteWarning("Middleware Client", message);
            response.Data = null;
            response.ResponseStatus = ResponseStatus.Error;
            response.ErrorMessage = message;
            return response;
        }

        /// <summary>
        /// Tells whether the response announces further items through the <see cref="kHasMoreHeader"/> header.
        /// </summary>
        internal static bool HasMorePages(RestResponse response)
        {
            HeaderParameter? hasMoreHeader = response.Headers?.FirstOrDefault(header =>
                string.Equals(header.Name, kHasMoreHeader, StringComparison.OrdinalIgnoreCase));
            return string.Equals(hasMoreHeader?.Value?.ToString(), "true", StringComparison.OrdinalIgnoreCase);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;
            if (disposing)
            {
                restClient.Dispose();
                disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~MiddlewareClient()
        {
            Dispose(false);
        }
    }
}

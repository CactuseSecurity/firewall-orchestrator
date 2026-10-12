using FWO.Data.Middleware;
using FWO.Middleware.Client;
using RestSharp;
using System.Net;

namespace FWO.Test.Mocks
{
    /// <summary>
    /// Mock implementation of MiddlewareClient for testing purposes
    /// </summary>
    public class MockMiddlewareClient : MiddlewareClient
    {
        public TokenPair? NextRefreshTokenResponse { get; set; }
        public bool ShouldRefreshSucceed { get; set; } = true;
        /// <summary>
        /// Status the middleware answers a failed refresh with. Unauthorized is its verdict
        /// on the token; a 5xx says it could not carry the request out, which the caller has
        /// to treat differently because it says nothing about the token.
        /// </summary>
        public HttpStatusCode RefreshFailureStatusCode { get; set; } = HttpStatusCode.Unauthorized;
        /// <summary>
        /// When set, the refresh call gets no HTTP answer at all, as if the middleware could
        /// not be reached.
        /// </summary>
        public bool SimulateRefreshTransportFailure { get; set; }
        public bool ReturnRefreshData { get; set; } = true;
        public bool ShouldRevokeSucceed { get; set; } = true;
        public int RefreshTokenCallCount { get; private set; }
        public int RevokeRefreshTokenCallCount { get; private set; }
        public int ChangePasswordCallCount { get; private set; }
        public RefreshTokenRequest? LastRefreshRequest { get; private set; }
        public RefreshTokenRequest? LastRevokeRequest { get; private set; }
        public UserChangePasswordParameters? LastChangePasswordRequest { get; private set; }
        public Exception? ChangePasswordException { get; set; }
        public RestResponse<string> ChangePasswordResponse { get; set; } = new(new RestRequest())
        {
            StatusCode = HttpStatusCode.OK,
            Data = "",
            ResponseStatus = ResponseStatus.Completed,
            IsSuccessStatusCode = true
        };

        public MockMiddlewareClient() : base("http://localhost/")
        {
        }

        public override async Task<RestResponse<TokenPair>> RefreshToken(RefreshTokenRequest parameters)
        {
            RefreshTokenCallCount++;
            LastRefreshRequest = parameters;

            await Task.CompletedTask;

            RestRequest request = new();

            if (ShouldRefreshSucceed && NextRefreshTokenResponse != null)
            {
                RestResponse<TokenPair> response = new(request)
                {
                    StatusCode = HttpStatusCode.OK,
                    Data = ReturnRefreshData ? NextRefreshTokenResponse : null,
                    ResponseStatus = ResponseStatus.Completed,
                    Content = System.Text.Json.JsonSerializer.Serialize(NextRefreshTokenResponse),
                    IsSuccessStatusCode = true
                };
                return response;
            }

            if (SimulateRefreshTransportFailure)
            {
                // No HTTP answer arrived, so there is no status code to report.
                return new RestResponse<TokenPair>(request)
                {
                    ErrorMessage = "The middleware could not be reached",
                    ResponseStatus = ResponseStatus.Error,
                    IsSuccessStatusCode = false
                };
            }

            // An answer did arrive, which is what ResponseStatus.Completed means; whether it
            // settles the session is decided by its status code.
            RestResponse<TokenPair> failResponse = new(request)
            {
                StatusCode = RefreshFailureStatusCode,
                ErrorMessage = "Refresh token failed",
                ResponseStatus = ResponseStatus.Completed,
                IsSuccessStatusCode = false
            };
            return failResponse;
        }

        public override async Task<RestResponse<string>> ChangePassword(UserChangePasswordParameters parameters)
        {
            ChangePasswordCallCount++;
            LastChangePasswordRequest = parameters;

            await Task.CompletedTask;

            if (ChangePasswordException != null)
            {
                throw ChangePasswordException;
            }

            return ChangePasswordResponse;
        }

        public override async Task<RestResponse> RevokeRefreshToken(RefreshTokenRequest parameters)
        {
            RevokeRefreshTokenCallCount++;
            LastRevokeRequest = parameters;

            await Task.CompletedTask;

            RestRequest request = new();

            if (ShouldRevokeSucceed)
            {
                return new RestResponse(request)
                {
                    StatusCode = HttpStatusCode.OK,
                    ResponseStatus = ResponseStatus.Completed,
                    IsSuccessStatusCode = true
                };
            }

            return new RestResponse(request)
            {
                StatusCode = HttpStatusCode.BadRequest,
                ErrorMessage = "Revoke token failed",
                ResponseStatus = ResponseStatus.Error,
                IsSuccessStatusCode = false
            };
        }

        public void Reset()
        {
            RefreshTokenCallCount = 0;
            RevokeRefreshTokenCallCount = 0;
            ChangePasswordCallCount = 0;
            LastRefreshRequest = null;
            LastRevokeRequest = null;
            LastChangePasswordRequest = null;
            ChangePasswordException = null;
            NextRefreshTokenResponse = null;
            ShouldRefreshSucceed = true;
            RefreshFailureStatusCode = HttpStatusCode.Unauthorized;
            SimulateRefreshTransportFailure = false;
            ReturnRefreshData = true;
            ShouldRevokeSucceed = true;
            ChangePasswordResponse = new(new RestRequest())
            {
                StatusCode = HttpStatusCode.OK,
                Data = "",
                ResponseStatus = ResponseStatus.Completed,
                IsSuccessStatusCode = true
            };
        }
    }
}

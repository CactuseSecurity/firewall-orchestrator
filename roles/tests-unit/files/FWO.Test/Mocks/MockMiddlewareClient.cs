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
        public bool ShouldRevokeSucceed { get; set; } = true;
        public int RefreshTokenCallCount { get; private set; }
        public int RevokeRefreshTokenCallCount { get; private set; }
        public RefreshTokenRequest? LastRefreshRequest { get; private set; }
        public RefreshTokenRequest? LastRevokeRequest { get; private set; }

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
                    Data = NextRefreshTokenResponse,
                    ResponseStatus = ResponseStatus.Completed,
                    Content = System.Text.Json.JsonSerializer.Serialize(NextRefreshTokenResponse),
                    IsSuccessStatusCode = true
                };
                return response;
            }

            RestResponse<TokenPair> failResponse = new(request)
            {
                StatusCode = HttpStatusCode.Unauthorized,
                ErrorMessage = "Refresh token failed",
                ResponseStatus = ResponseStatus.Error,
                IsSuccessStatusCode = false
            };
            return failResponse;
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
            LastRefreshRequest = null;
            LastRevokeRequest = null;
            NextRefreshTokenResponse = null;
            ShouldRefreshSucceed = true;
            ShouldRevokeSucceed = true;
        }
    }
}

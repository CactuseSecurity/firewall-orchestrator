using Microsoft.IdentityModel.JsonWebTokens;

namespace FWO.Middleware.Client
{
    public enum JwtValidationStatus
    {
        Success,
        Expired,
        Invalid
    }

    public sealed class JwtValidationResult
    {
        public JwtValidationStatus Status { get; init; }

        public JsonWebToken? Token { get; init; }

        public bool IsSuccess => Status == JwtValidationStatus.Success;
    }
}

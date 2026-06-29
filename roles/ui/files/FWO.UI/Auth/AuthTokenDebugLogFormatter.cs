using FWO.Data.Middleware;
using FWO.Middleware.Client;
using Microsoft.IdentityModel.Tokens;
using RestSharp;
using System.IdentityModel.Tokens.Jwt;

namespace FWO.Ui.Auth;

/// <summary>
/// Formats safe debug information for authentication tokens.
/// </summary>
public static class AuthTokenDebugLogFormatter
{
    /// <summary>
    /// Builds a debug log message for the access token returned by a successful login.
    /// </summary>
    public static string FormatLoginTokenDebugMessage(string username, RestResponse<TokenPair> authResponse)
    {
        TokenPair? tokenPair = TokenPairResponseParser.Parse(authResponse, "Login");
        return FormatLoginTokenDebugMessage(username, tokenPair?.AccessToken);
    }

    /// <summary>
    /// Builds a debug log message for an access token without logging the bearer credential itself.
    /// </summary>
    public static string FormatLoginTokenDebugMessage(string username, string? accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return $"User \"{username}\" logged in, but no access token was available for debug logging.";
        }

        JwtSecurityTokenHandler tokenHandler = new();
        if (!tokenHandler.CanReadToken(accessToken))
        {
            return $"User \"{username}\" received an unreadable access JWT.";
        }

        try
        {
            JwtSecurityToken token = tokenHandler.ReadJwtToken(accessToken);
            return $"User \"{username}\" received access JWT jti={token.Id}, expires={token.ValidTo.ToLocalTime():yyyy-MM-dd'T'HH:mm:sszzz}.";
        }
        catch (SecurityTokenException)
        {
            return $"User \"{username}\" received an unreadable access JWT.";
        }
        catch (ArgumentException)
        {
            return $"User \"{username}\" received an unreadable access JWT.";
        }
    }
}

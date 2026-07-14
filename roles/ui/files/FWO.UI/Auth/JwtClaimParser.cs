using System.Security.Claims;

namespace FWO.Ui.Auth
{
    public static class JwtClaimParser
    {
        public static List<string> ExtractStringClaimValues(IEnumerable<Claim> claims, string claimType)
        {
            return global::FWO.Basics.JwtClaimParser.ExtractStringClaimValues(claims, claimType);
        }

        public static List<int> ExtractIntClaimValues(IEnumerable<Claim> claims, string claimType)
        {
            return global::FWO.Basics.JwtClaimParser.ExtractIntClaimValues(claims, claimType);
        }
    }
}

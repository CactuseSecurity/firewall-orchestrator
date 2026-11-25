namespace FWO.Basics
{
    /// <summary>
    /// Names of claims in the JWTs of the middleware server, as they appear in the token. Services reading them by
    /// these names validate the tokens without mapping the inbound claims (JwtBearerOptions.MapInboundClaims = false).
    /// </summary>
    public static class JwtClaimTypes
    {
        /// <summary>Name of the user.</summary>
        public const string kName = "unique_name";

        /// <summary>Roles of the user, one claim per role.</summary>
        public const string kRole = "role";

        /// <summary>Directory identity (dn) of the user.</summary>
        public const string kUserDn = "x-hasura-uuid";
    }
}

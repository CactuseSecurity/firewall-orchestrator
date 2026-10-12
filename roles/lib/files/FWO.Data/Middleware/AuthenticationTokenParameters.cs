namespace FWO.Data.Middleware
{
    public class AuthenticationTokenGetParameters
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
    }

    public class AuthenticationTokenGetForUserParameters
    {
        public string AdminUsername { get; set; } = "";
        public string AdminPassword { get; set; } = "";
        public string TargetUserDn { get; set; } = "";
        public string TargetUserName { get; set; } = "";

        /// <summary>
        /// Optional target selection. Defaults to an empty object for existing callers.
        /// </summary>
        public AuthenticationTokenGetForUserOptions? Options { get; set; } = new();
    }

    /// <summary>
    /// Selects the directory of a delegated-token target when its name or DN is not unique.
    /// </summary>
    public class AuthenticationTokenGetForUserOptions
    {
        /// <summary>
        /// Positive LDAP connection ID of the target. Null searches all directories and succeeds only
        /// when exactly one directory contains the target.
        /// </summary>
        public int? TargetLdapId { get; set; }
    }
}

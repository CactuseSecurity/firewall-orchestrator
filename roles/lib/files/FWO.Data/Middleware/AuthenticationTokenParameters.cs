using FWO.Basics;

namespace FWO.Data.Middleware
{
    public class AuthenticationTokenGetParameters
    {
        public string? Username { get; set; }
        public string? Password { get; set; }

        /// <summary>
        /// Options of the login. Defaults to an empty object; null is treated like the empty object.
        /// </summary>
        public AuthenticationTokenGetOptions? Options { get; set; } = new();
    }

    /// <summary>
    /// Options of a login with user name and password.
    /// </summary>
    public class AuthenticationTokenGetOptions
    {
        /// <summary>
        /// Longest accepted client address.
        /// </summary>
        public const int kMaxClientAddressLength = ClientAddressFormat.kMaxLength;

        /// <summary>
        /// Description of <see cref="ClientAddress"/>, used for the API documentation and the validation feedback.
        /// </summary>
        public static readonly string kClientAddressDescription = "Optional. Address of the client a trusted frontend (this host or a host listed in "
            + "login_trusted_client_hosts) logs in for: exactly one IPv4 or IPv6 address in canonical notation without port, brackets or scope id, "
            + $"at most {kMaxClientAddressLength} characters. Null or omitted, and from any other caller, the address of the caller itself is used.";

        /// <summary>
        /// Address of the client the login is made for, described by <see cref="kClientAddressDescription"/>.
        /// </summary>
        public string? ClientAddress { get; set; }
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

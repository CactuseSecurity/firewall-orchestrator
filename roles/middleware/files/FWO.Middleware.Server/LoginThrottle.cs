using FWO.Config.File;
using FWO.Logging;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.RateLimiting;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Limits of the LDAP-backed login endpoints, read from the optional login_* keys of fworch.json.
    /// </summary>
    public sealed class LoginThrottleSettings
    {
        internal const int kDefaultMaxDirectories = 16;
        internal const int kDefaultClientAttemptsPerMinute = 30;
        internal const int kDefaultUserFailuresPerMinute = 10;
        private const string kLogCategory = "Login Throttle";

        /// <summary>Maximum number of active LDAP connections a single login may fan out to.</summary>
        public int MaxDirectories { get; init; } = kDefaultMaxDirectories;

        /// <summary>Credentialed login attempts per minute and client address; trusted clients are exempt.</summary>
        public int ClientAttemptsPerMinute { get; init; } = kDefaultClientAttemptsPerMinute;

        /// <summary>Failed logins per minute for one user name from one client address.</summary>
        public int UserFailuresPerMinute { get; init; } = kDefaultUserFailuresPerMinute;

        /// <summary>
        /// Client addresses exempt from the per-client limit: this host and the configured UI hosts, whose
        /// address is shared by all users logging in through the UI.
        /// </summary>
        public List<IPAddress> TrustedClientAddresses { get; init; } = [];

        /// <summary>
        /// Builds the settings from fworch.json, falling back to the defaults for missing or invalid values.
        /// </summary>
        public static LoginThrottleSettings FromConfigFile()
        {
            List<IPAddress> trustedAddresses = GetLocalAddresses();
            trustedAddresses.AddRange(ResolveHosts(ConfigFile.LoginTrustedClientHosts ?? []));
            return new LoginThrottleSettings
            {
                MaxDirectories = PositiveOrDefault(ConfigFile.LoginMaxDirectories, kDefaultMaxDirectories),
                ClientAttemptsPerMinute = PositiveOrDefault(ConfigFile.LoginClientAttemptsPerMinute, kDefaultClientAttemptsPerMinute),
                UserFailuresPerMinute = PositiveOrDefault(ConfigFile.LoginUserFailuresPerMinute, kDefaultUserFailuresPerMinute),
                TrustedClientAddresses = trustedAddresses.Distinct().ToList()
            };
        }

        /// <summary>
        /// Resolves host names or literal addresses; hosts that cannot be resolved are logged and skipped.
        /// </summary>
        internal static List<IPAddress> ResolveHosts(IEnumerable<string> hosts)
        {
            List<IPAddress> addresses = [];
            foreach (string host in hosts.Where(host => !string.IsNullOrWhiteSpace(host)))
            {
                try
                {
                    if (IPAddress.TryParse(host.Trim(), out IPAddress? address))
                    {
                        addresses.Add(address);
                    }
                    else
                    {
                        addresses.AddRange(Dns.GetHostAddresses(host.Trim()));
                    }
                }
                catch (Exception exception)
                {
                    Log.WriteWarning(kLogCategory, $"Trusted login client host \"{host}\" could not be resolved and is not exempt: {exception.Message}");
                }
            }
            return addresses.ConvertAll(LoginThrottle.Normalize);
        }

        private static List<IPAddress> GetLocalAddresses()
        {
            List<IPAddress> addresses = [IPAddress.Loopback, IPAddress.IPv6Loopback];
            try
            {
                addresses.AddRange(NetworkInterface.GetAllNetworkInterfaces()
                    .SelectMany(networkInterface => networkInterface.GetIPProperties().UnicastAddresses)
                    .Select(unicastAddress => unicastAddress.Address));
            }
            catch (NetworkInformationException exception)
            {
                Log.WriteWarning(kLogCategory, $"Local addresses could not be read, only loopback is exempt: {exception.Message}");
            }
            return addresses.ConvertAll(LoginThrottle.Normalize);
        }

        private static int PositiveOrDefault(int? value, int defaultValue)
        {
            return value is > 0 ? value.Value : defaultValue;
        }
    }

    /// <summary>
    /// Rejects credentialed login attempts before they cause LDAP work: per client address, and per user name and
    /// client address for failed attempts. Anonymous token requests cause no LDAP work and are never limited.
    /// </summary>
    public sealed class LoginThrottle : IDisposable
    {
        private readonly HashSet<IPAddress> trustedClients;
        private readonly PartitionedRateLimiter<string> clientAttempts;
        private readonly PartitionedRateLimiter<string> userFailures;

        /// <summary>Retry hint for rejected clients, matching the limiter window.</summary>
        public const int kRetryAfterSeconds = 60;

        /// <summary>
        /// Creates the throttle with one-minute windows.
        /// </summary>
        public LoginThrottle(LoginThrottleSettings settings) : this(settings, TimeSpan.FromSeconds(kRetryAfterSeconds))
        { }

        /// <summary>
        /// Creates the throttle with a custom window, used by tests.
        /// </summary>
        internal LoginThrottle(LoginThrottleSettings settings, TimeSpan window)
        {
            trustedClients = settings.TrustedClientAddresses.Select(Normalize).ToHashSet();
            clientAttempts = CreateLimiter(settings.ClientAttemptsPerMinute, window);
            userFailures = CreateLimiter(settings.UserFailuresPerMinute, window);
        }

        /// <summary>
        /// Consumes one attempt of the client and checks the failures of the user.
        /// </summary>
        /// <param name="userName">Login name of the attempt; null or empty for anonymous requests.</param>
        /// <param name="client">Client address as seen after forwarded-header processing.</param>
        /// <returns>True if the attempt may proceed to the directories.</returns>
        public bool TryBeginAttempt(string? userName, IPAddress? client)
        {
            if (string.IsNullOrWhiteSpace(userName))
            {
                return true;
            }
            if (userFailures.GetStatistics(UserKey(userName, client))?.CurrentAvailablePermits <= 0)
            {
                return false;
            }
            if (client != null && trustedClients.Contains(Normalize(client)))
            {
                return true;
            }
            using RateLimitLease lease = clientAttempts.AttemptAcquire(ClientKey(client));
            return lease.IsAcquired;
        }

        /// <summary>
        /// Records a failed login of the user from the client.
        /// </summary>
        public void RecordFailure(string? userName, IPAddress? client)
        {
            if (!string.IsNullOrWhiteSpace(userName))
            {
                using RateLimitLease lease = userFailures.AttemptAcquire(UserKey(userName, client));
            }
        }

        /// <summary>
        /// Maps IPv4-mapped IPv6 addresses to IPv4 so both notations share one partition.
        /// </summary>
        internal static IPAddress Normalize(IPAddress address)
        {
            return address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            clientAttempts.Dispose();
            userFailures.Dispose();
        }

        private static string ClientKey(IPAddress? client)
        {
            return client == null ? "unknown" : Normalize(client).ToString();
        }

        private static string UserKey(string userName, IPAddress? client)
        {
            return $"{userName.Trim().ToLowerInvariant()}|{ClientKey(client)}";
        }

        private static PartitionedRateLimiter<string> CreateLimiter(int permitLimit, TimeSpan window)
        {
            return PartitionedRateLimiter.Create<string, string>(key => RateLimitPartition.GetFixedWindowLimiter(key,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = window,
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
        }
    }
}

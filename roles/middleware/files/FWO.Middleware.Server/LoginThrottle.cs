using FWO.Basics;
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

        /// <summary>Failed logins per minute for one user name from one caller address.</summary>
        public int UserFailuresPerMinute { get; init; } = kDefaultUserFailuresPerMinute;

        /// <summary>
        /// Addresses of this host and the configured UI hosts. They are exempt from the per-client limit, and
        /// the client address they report with a login is accepted: the UI logs in for its users and reports
        /// their address, so that they are limited and logged by their own address.
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
            return addresses.ConvertAll(ClientAddressFormat.Normalize);
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
            return addresses.ConvertAll(ClientAddressFormat.Normalize);
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
        private readonly int userFailuresPerWindow;
        // attempts of a user name and client that are still running: each of them may still fail
        private readonly Dictionary<string, int> pendingAttempts = [];
        private readonly object attemptLock = new();

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
            trustedClients = settings.TrustedClientAddresses.Select(ClientAddressFormat.Normalize).ToHashSet();
            clientAttempts = CreateLimiter(settings.ClientAttemptsPerMinute, window);
            userFailures = CreateLimiter(settings.UserFailuresPerMinute, window);
            userFailuresPerWindow = settings.UserFailuresPerMinute;
        }

        /// <summary>
        /// Consumes one attempt of the client and checks the failures of the user, counting the attempts of the user
        /// that are still running as possible failures: otherwise concurrent attempts would all pass the check before
        /// the first failure is recorded.
        /// </summary>
        /// <remarks>
        /// The failures of the user stay keyed by the caller even if it reports another client: a caller on a
        /// trusted host could otherwise evade the failure limit by reporting a new client address with every guess.
        /// </remarks>
        /// <param name="userName">Login name of the attempt; null or empty for anonymous requests.</param>
        /// <param name="caller">Address of the caller as seen after forwarded-header processing.</param>
        /// <param name="reportedClient">Client the caller logs in for, see <see cref="ResolveClient"/>; null if it reported none.</param>
        /// <returns>The attempt, to be disposed when it ends, or null if the attempt must not proceed to the directories.</returns>
        public LoginAttempt? BeginAttempt(string? userName, IPAddress? caller, IPAddress? reportedClient = null)
        {
            if (string.IsNullOrWhiteSpace(userName))
            {
                return new LoginAttempt(this, null);
            }
            string userKey = UserKey(userName, caller);
            IPAddress? client = ResolveClient(caller, reportedClient);
            lock (attemptLock)
            {
                long availableFailures = userFailures.GetStatistics(userKey)?.CurrentAvailablePermits ?? userFailuresPerWindow;
                int pending = pendingAttempts.GetValueOrDefault(userKey);
                if (availableFailures - pending <= 0 || !TryConsumeClientAttempt(client))
                {
                    return null;
                }
                pendingAttempts[userKey] = pending + 1;
            }
            return new LoginAttempt(this, userKey);
        }

        /// <summary>
        /// Returns the client a login is counted and logged for. A trusted caller (this host or a configured UI
        /// host) logs in for its own clients and may report their address; the address reported by any other
        /// caller is ignored, as it would let that caller choose its logged address and its login limit.
        /// </summary>
        /// <param name="caller">Address of the caller as seen after forwarded-header processing.</param>
        /// <param name="reportedClient">Client address reported by the caller; null if it reported none.</param>
        /// <returns>The reported client of a trusted caller, otherwise the caller.</returns>
        public IPAddress? ResolveClient(IPAddress? caller, IPAddress? reportedClient)
        {
            if (reportedClient == null || !IsTrustedClient(caller))
            {
                return caller;
            }
            return ClientAddressFormat.Normalize(reportedClient);
        }

        /// <summary>
        /// Tells whether an address belongs to this host or a configured UI host.
        /// </summary>
        /// <param name="address">Address to check.</param>
        /// <returns>True if the address is trusted.</returns>
        public bool IsTrustedClient(IPAddress? address)
        {
            return address != null && trustedClients.Contains(ClientAddressFormat.Normalize(address));
        }

        /// <summary>
        /// Records a failed attempt of the user key.
        /// </summary>
        internal void RecordFailure(string userKey)
        {
            using RateLimitLease lease = userFailures.AttemptAcquire(userKey);
        }

        /// <summary>
        /// Ends a running attempt of the user key.
        /// </summary>
        internal void EndAttempt(string userKey)
        {
            lock (attemptLock)
            {
                int pending = pendingAttempts.GetValueOrDefault(userKey) - 1;
                if (pending > 0)
                {
                    pendingAttempts[userKey] = pending;
                }
                else
                {
                    pendingAttempts.Remove(userKey);
                }
            }
        }

        /// <summary>
        /// Consumes one attempt of the client address; trusted clients (UI and middleware hosts) are not limited.
        /// </summary>
        /// <param name="client">Client of the attempt, see <see cref="ResolveClient"/>.</param>
        /// <returns>True if the client may make another attempt.</returns>
        private bool TryConsumeClientAttempt(IPAddress? client)
        {
            if (IsTrustedClient(client))
            {
                return true;
            }
            using RateLimitLease lease = clientAttempts.AttemptAcquire(ClientKey(client));
            return lease.IsAcquired;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            clientAttempts.Dispose();
            userFailures.Dispose();
        }

        /// <summary>
        /// Returns the partition key of a client: its normalized address (see <see cref="ClientAddressFormat.Normalize"/>),
        /// so that both notations of an IPv4 address and an IPv6 address with and without scope id share one partition.
        /// </summary>
        /// <param name="client">Client address; null if it is not known.</param>
        /// <returns>The address text, or <see cref="ClientAddressFormat.kUnknown"/>.</returns>
        private static string ClientKey(IPAddress? client)
        {
            return ClientAddressFormat.Describe(client);
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

    /// <summary>
    /// A login attempt admitted by the <see cref="LoginThrottle"/>; it counts as a possible failure of its user until
    /// it is disposed. Anonymous attempts are not counted.
    /// </summary>
    public sealed class LoginAttempt : IDisposable
    {
        private readonly LoginThrottle throttle;
        private readonly string? userKey;
        private int ended;

        /// <summary>
        /// Creates an admitted attempt.
        /// </summary>
        /// <param name="throttle">The throttle that admitted the attempt.</param>
        /// <param name="userKey">Key of the user name and client; null for anonymous attempts, which are not counted.</param>
        internal LoginAttempt(LoginThrottle throttle, string? userKey)
        {
            this.throttle = throttle;
            this.userKey = userKey;
        }

        /// <summary>
        /// Records that the credentials of the attempt were invalid.
        /// </summary>
        public void RecordFailure()
        {
            if (userKey != null)
            {
                throttle.RecordFailure(userKey);
            }
        }

        /// <summary>
        /// Ends the attempt; a recorded failure stays counted.
        /// </summary>
        public void Dispose()
        {
            if (userKey != null && Interlocked.Exchange(ref ended, 1) == 0)
            {
                throttle.EndAttempt(userKey);
            }
        }
    }
}

using FWO.Logging;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Helper for deterministic LDAP login source selection.
    /// </summary>
    public static class AuthLdapSelection
    {
        /// <summary>
        /// Selects the first successful LDAP index based on configured order.
        /// </summary>
        /// <param name="loginSuccessByOrder">Login results in LDAP configuration order.</param>
        /// <returns>Index of preferred LDAP or -1 if none succeeded.</returns>
        public static int GetPreferredLdapIndex(IReadOnlyList<bool>? loginSuccessByOrder)
        {
            if (loginSuccessByOrder == null || loginSuccessByOrder.Count == 0)
            {
                return -1;
            }

            for (int index = 0; index < loginSuccessByOrder.Count; index++)
            {
                if (loginSuccessByOrder[index])
                {
                    return index;
                }
            }

            return -1;
        }
    }
    /// <summary>
    /// Signals that a login was refused because the directories are busy, slow, or misconfigured rather than
    /// because the credentials were wrong. Carries the HTTP status the endpoints answer with.
    /// </summary>
    public sealed class LoginCapacityException : Exception
    {
        /// <summary>Message code for a login refused while the middleware is at capacity.</summary>
        public const string kTooManyAttempts = "A0006 Too many login attempts. Please try again later.";

        /// <summary>Message code for a login that could not be completed by the directories in time.</summary>
        public const string kUnavailable = "A0007 Login is temporarily unavailable. Please try again later.";

        /// <summary>HTTP status code to answer with.</summary>
        public int StatusCode { get; }

        /// <summary>
        /// Creates the exception with its message code and HTTP status.
        /// </summary>
        public LoginCapacityException(string message, int statusCode) : base(message)
        {
            StatusCode = statusCode;
        }
    }

    /// <summary>
    /// Limits LDAP work created by authentication across concurrent requests.
    /// </summary>
    internal static class LdapAuthenticationGate
    {
        internal const int MaxConcurrentOperations = 4;
        internal const int MaxConcurrentRequests = 32;
        internal static readonly TimeSpan kTotalTimeout = TimeSpan.FromSeconds(10);
        private const string kLogCategory = "User Authentication";
        private static readonly SemaphoreSlim kSlots = new(MaxConcurrentOperations, MaxConcurrentOperations);
        private static readonly SemaphoreSlim kRequests = new(MaxConcurrentRequests, MaxConcurrentRequests);

        /// <summary>
        /// Maximum number of directories one login may fan out to, see <see cref="LoginThrottleSettings.MaxDirectories"/>.
        /// </summary>
        internal static int MaxDirectories { get; private set; } = LoginThrottleSettings.kDefaultMaxDirectories;

        /// <summary>
        /// Applies the configured directory cap; called once at startup.
        /// </summary>
        internal static void Configure(LoginThrottleSettings settings)
        {
            MaxDirectories = settings.MaxDirectories;
        }

        /// <summary>
        /// Runs directory attempts in configuration order with a shared concurrency limit and deadline.
        /// </summary>
        internal static Task<T[]> RunAsync<T>(IReadOnlyList<Func<CancellationToken, Task<T>>> attempts, CancellationToken cancellationToken)
        {
            return RunAsync(attempts, kTotalTimeout, cancellationToken);
        }

        /// <summary>
        /// Runs directory attempts with a custom deadline, used by tests.
        /// </summary>
        internal static async Task<T[]> RunAsync<T>(IReadOnlyList<Func<CancellationToken, Task<T>>> attempts, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (attempts.Count > MaxDirectories)
            {
                Log.WriteError(kLogCategory, $"Login needs {attempts.Count} active LDAP connections, but at most {MaxDirectories} are allowed. " +
                    "Deactivate unused connections or raise login_max_directories in fworch.json.");
                throw new LoginCapacityException(LoginCapacityException.kUnavailable, StatusCodes.Status503ServiceUnavailable);
            }

            if (!await kRequests.WaitAsync(0, cancellationToken))
            {
                Log.WriteWarning(kLogCategory, $"Login refused: {MaxConcurrentRequests} logins are already waiting for LDAP.");
                throw new LoginCapacityException(LoginCapacityException.kTooManyAttempts, StatusCodes.Status429TooManyRequests);
            }
            using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            try
            {
                deadline.CancelAfter(timeout);
                List<Task<T>> tasks = [];
                foreach (Func<CancellationToken, Task<T>> attempt in attempts)
                {
                    tasks.Add(RunOneAsync(attempt, deadline.Token));
                }
                try
                {
                    return await Task.WhenAll(tasks).WaitAsync(deadline.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw DeadlineExceeded(timeout);
                }
            }
            finally
            {
                kRequests.Release();
            }
        }

        /// <summary>
        /// Creates the exception for LDAP work that did not finish within its deadline, and logs it.
        /// </summary>
        internal static LoginCapacityException DeadlineExceeded(TimeSpan timeout)
        {
            Log.WriteWarning(kLogCategory, $"Login refused: the LDAP connections did not answer within {timeout.TotalSeconds} seconds.");
            return new LoginCapacityException(LoginCapacityException.kUnavailable, StatusCodes.Status503ServiceUnavailable);
        }

        /// <summary>
        /// Holds one slot until the LDAP attempt finishes or observes cancellation.
        /// </summary>
        private static async Task<T> RunOneAsync<T>(Func<CancellationToken, Task<T>> attempt, CancellationToken cancellationToken)
        {
            await kSlots.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return await attempt(cancellationToken);
            }
            finally
            {
                kSlots.Release();
            }
        }
    }
}

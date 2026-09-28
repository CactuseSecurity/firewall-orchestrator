using FWO.Basics.Exceptions;
using FWO.Logging;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Runs the first API query a service makes at startup, retrying while the API is still
    /// coming up and failing with an actionable message once the startup budget is spent.
    /// </summary>
    /// <remarks>
    /// The middleware cannot serve anything before this query succeeds, so it runs before the
    /// web server binds its port. Retrying forever there is what turned an unreachable API into
    /// the worst possible failure mode: a service systemd reports as running, whose port never
    /// opens, whose Apache reverse proxy therefore answers 503, and with no failed unit, no
    /// non-zero exit and no bounded wait anywhere for an operator to find. Since 9.5.0 that
    /// query also needs a client certificate and a pinned CA chain, so there are considerably
    /// more ways for it to fail permanently than "the API has not started yet".
    ///
    /// The budget is deliberately shorter than the systemd start rate limit window allows for
    /// (see fworch_systemd_start_limit_* in inventory/group_vars/all.yml), so the process still
    /// exits and is restarted rather than latching into a failed state an installer run would
    /// have to clear: a real installation legitimately spends minutes with the API down while
    /// the api role redeploys it, and this must recover from that on its own. What changes is
    /// that every cycle now says what it tried and what to check.
    /// </remarks>
    public static class ApiStartupProbe
    {
        private const string kLogCategory = "Api startup";

        /// <summary>How long the first query may keep failing before the service gives up.</summary>
        internal static readonly TimeSpan kStartupBudget = TimeSpan.FromSeconds(60);

        /// <summary>Delay after the first failure. Doubles per attempt, up to kMaxRetryDelay.</summary>
        internal static readonly TimeSpan kFirstRetryDelay = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Ceiling for the retry delay, so a service that is only waiting for a dependency
        /// notices it became available instead of sleeping through most of its own budget.
        /// </summary>
        internal static readonly TimeSpan kMaxRetryDelay = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Runs <paramref name="query"/> until it succeeds or the startup budget is spent.
        /// </summary>
        /// <typeparam name="QueryResultType">Result type of the first query.</typeparam>
        /// <param name="query">The first API query the service makes.</param>
        /// <param name="apiServerUri">The endpoint being addressed, named in every log message.</param>
        /// <param name="budget">Overrides the startup budget. Intended for tests.</param>
        /// <param name="delay">Overrides how a retry delay is awaited. Intended for tests.</param>
        /// <param name="utcNow">Overrides the clock the budget is measured with. Intended for tests.</param>
        /// <returns>The result of the first successful attempt.</returns>
        /// <exception cref="ApiUnavailableAtStartupException">No attempt succeeded within the budget.</exception>
        public static async Task<QueryResultType> RunFirstQueryAsync<QueryResultType>(
            Func<Task<QueryResultType>> query,
            string apiServerUri,
            TimeSpan? budget = null,
            Func<TimeSpan, Task>? delay = null,
            Func<DateTime>? utcNow = null)
        {
            TimeSpan totalBudget = budget ?? kStartupBudget;
            Func<TimeSpan, Task> awaitDelay = delay ?? (interval => Task.Delay(interval));
            Func<DateTime> clock = utcNow ?? (() => DateTime.UtcNow);

            DateTime startedAt = clock();
            TimeSpan retryDelay = kFirstRetryDelay;
            int attempts = 0;
            Exception lastFailure;

            while (true)
            {
                attempts++;
                try
                {
                    return await query();
                }
                catch (Exception exception)
                {
                    lastFailure = exception;
                }

                TimeSpan elapsed = clock() - startedAt;
                // Checked against the delay that would follow, not against the elapsed time
                // alone: sleeping past the budget and only then reporting it would make the
                // service take longer to fail than it was allowed to take to start.
                if (elapsed + retryDelay > totalBudget)
                {
                    throw new ApiUnavailableAtStartupException(
                        BuildFailureMessage(apiServerUri, attempts, elapsed), lastFailure);
                }

                // One line per attempt. The stack trace belongs to the failure that ends the
                // startup, and repeating it per attempt is what buried the cause before.
                Log.WriteWarning(kLogCategory,
                    $"The API at {apiServerUri} did not answer the first query " +
                    $"(attempt {attempts}: {lastFailure.Message}). " +
                    $"Retrying in {retryDelay.TotalSeconds:0.#}s.");
                await awaitDelay(retryDelay);
                TimeSpan doubledDelay = retryDelay * 2;
                retryDelay = doubledDelay > kMaxRetryDelay ? kMaxRetryDelay : doubledDelay;
            }
        }

        /// <summary>
        /// Builds the message an operator gets when the startup is given up on.
        /// </summary>
        /// <param name="apiServerUri">The endpoint that was addressed.</param>
        /// <param name="attempts">How many attempts were made.</param>
        /// <param name="elapsed">How long they took in total.</param>
        /// <returns>The failure message, naming the endpoint and what to check.</returns>
        internal static string BuildFailureMessage(string apiServerUri, int attempts, TimeSpan elapsed)
        {
            return $"The GraphQL API at {apiServerUri} did not answer the first query after {attempts} " +
                $"attempts over {elapsed.TotalSeconds:0} seconds, so this service is stopping instead of " +
                "waiting for it indefinitely. Its own web server was never started, which is why its " +
                "Apache reverse proxy answers 503. Check, in this order: that the API is running and " +
                "reachable at that address; that tls_client_certificate and tls_client_private_key in " +
                "the FWO config file exist and are readable by this service; that the API server " +
                "certificate is valid for the host name in api_uri and chains to tls_ca_certificate - a " +
                "name the certificate does not carry is rejected before its chain is even looked at; and " +
                "that the API vhost accepts this host's client certificate (SSLVerifyClient). " +
                "See documentation/certificates.md.";
        }
    }
}

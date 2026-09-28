namespace FWO.Test.Helpers
{
    /// <summary>
    /// Waits for a condition by polling instead of sleeping for a fixed window, so a loaded
    /// runner delays a test rather than failing it.
    /// </summary>
    internal static class PollingWait
    {
        /// <summary>
        /// How often a condition is re-evaluated while waiting.
        /// </summary>
        private static readonly TimeSpan kPollInterval = TimeSpan.FromMilliseconds(25);

        /// <summary>
        /// Polls a condition until it holds or the timeout elapses.
        /// </summary>
        /// <param name="condition">Condition that is expected to become true.</param>
        /// <param name="timeout">Upper bound for the wait.</param>
        /// <returns>True when the condition became true within the timeout.</returns>
        public static Task<bool> UntilAsync(Func<bool> condition, TimeSpan timeout)
        {
            return UntilAsync(() => Task.FromResult(condition()), timeout);
        }

        /// <summary>
        /// Polls an asynchronous condition until it holds or the timeout elapses.
        /// </summary>
        /// <param name="condition">Condition that is expected to become true.</param>
        /// <param name="timeout">Upper bound for the wait.</param>
        /// <returns>True when the condition became true within the timeout.</returns>
        public static async Task<bool> UntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
        {
            long deadline = Environment.TickCount64 + (long)timeout.TotalMilliseconds;
            while (Environment.TickCount64 < deadline)
            {
                if (await condition())
                {
                    return true;
                }
                await Task.Delay(kPollInterval);
            }
            return await condition();
        }
    }
}

namespace FWO.Test.Helpers
{
    /// <summary>
    /// Clock frozen at a fixed instant, so time-dependent code can be tested without
    /// depending on when the test runs. Uses the machine's local time zone like TimeProvider.System.
    /// </summary>
    internal sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset utcNow;

        /// <summary>
        /// Creates a clock that always returns the given instant.
        /// </summary>
        /// <param name="now">Instant returned as current time.</param>
        public FixedTimeProvider(DateTimeOffset now)
        {
            utcNow = now.ToUniversalTime();
        }

        /// <summary>
        /// Creates a clock that always returns the given local wall-clock time.
        /// </summary>
        /// <param name="localNow">Local time returned as current time.</param>
        public FixedTimeProvider(DateTime localNow)
            : this(new DateTimeOffset(DateTime.SpecifyKind(localNow, DateTimeKind.Local)))
        { }

        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}

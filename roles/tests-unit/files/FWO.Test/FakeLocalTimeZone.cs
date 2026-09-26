using System.Reflection;

namespace FWO.Test
{
    public class FakeLocalTimeZone : IDisposable
    {
        private readonly TimeZoneInfo _actualLocalTimeZoneInfo;

        /// <summary>
        /// Explicit rather than a property initializer, so the system zone is captured before
        /// the first instance replaces it and not lazily on first use of the property.
        /// </summary>
        static FakeLocalTimeZone()
        {
            SystemTimeZone = TimeZoneInfo.Local;
        }

        /// <summary>
        /// Gets the local time zone the process started with. Faking only replaces the managed
        /// cache, so the C library keeps using this zone throughout the test run.
        /// </summary>
        public static TimeZoneInfo SystemTimeZone { get; }

        /// <summary>
        /// Puts the system time zone back until the returned instance is disposed.
        /// </summary>
        /// <remarks>
        /// Required around X509Chain.Build on Linux: it hands the verification time to
        /// mktime() as local wall clock time together with the daylight saving flag of the
        /// managed local zone, and mktime() interprets both in the C library's zone. With
        /// Europe/Berlin faked on a UTC host that flag is set in summer, which glibc 2.35
        /// (Ubuntu 22.04) rejects, so building any chain throws a CryptographicException.
        /// </remarks>
        /// <returns>The scope restoring the previously active local time zone on disposal.</returns>
        public static FakeLocalTimeZone UseSystemTimeZone()
        {
            return new FakeLocalTimeZone(SystemTimeZone);
        }

        private static void SetLocalTimeZone(TimeZoneInfo timeZoneInfo)
        {
            var info = typeof(TimeZoneInfo).GetField("s_cachedData", BindingFlags.NonPublic | BindingFlags.Static);
            object? cachedData = info?.GetValue(null);

            var field = cachedData?.GetType().GetField("_localTimeZone", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Instance);
            field?.SetValue(cachedData, timeZoneInfo);
        }

        public FakeLocalTimeZone(TimeZoneInfo timeZoneInfo)
        {
            _actualLocalTimeZoneInfo = TimeZoneInfo.Local;
            SetLocalTimeZone(timeZoneInfo);
        }

        public void Dispose()
        {
            SetLocalTimeZone(_actualLocalTimeZoneInfo);
        }
    }
}

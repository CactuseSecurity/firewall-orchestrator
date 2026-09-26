using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace FWO.Test
{
    /// <summary>
    /// Puts the system time zone back around each test for tests building an X509Chain
    /// (see <see cref="FakeLocalTimeZone.UseSystemTimeZone"/>).
    /// </summary>
    /// <remarks>
    /// Applied to a fixture, NUnit runs it around SetUp and TearDown as well; applied to a
    /// test method, it only wraps the test body. NUnit runs AfterTest even when the wrapped
    /// code throws, so the previously active local time zone is always restored. The local
    /// time zone is process wide, so only apply this to non parallelizable fixtures or tests.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    internal sealed class UseSystemTimeZoneAttribute : TestActionAttribute
    {
        private FakeLocalTimeZone? systemTimeZone;

        /// <summary>
        /// Wraps every single test, also when applied to a fixture.
        /// </summary>
        public override ActionTargets Targets => ActionTargets.Test;

        /// <summary>
        /// Puts the system time zone back before the wrapped code runs.
        /// </summary>
        /// <param name="test">The test about to run.</param>
        public override void BeforeTest(ITest test)
        {
            systemTimeZone = FakeLocalTimeZone.UseSystemTimeZone();
        }

        /// <summary>
        /// Restores the previously active local time zone after the wrapped code ran.
        /// </summary>
        /// <param name="test">The test that has run.</param>
        public override void AfterTest(ITest test)
        {
            systemTimeZone?.Dispose();
            systemTimeZone = null;
        }
    }
}

using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace FWO.Test.Helpers
{
    /// <summary>
    /// Custom attribute to skip integration-style tests unless an explicit test environment opt-in is present.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
    public class RequiresIntegrationEnvironmentAttribute : NUnitAttribute, IApplyToTest
    {
        public void ApplyToTest(NUnit.Framework.Internal.Test test)
        {
            if (!IsIntegrationEnvironmentEnabled())
            {
                test.RunState = NUnit.Framework.Interfaces.RunState.Ignored;
                test.Properties.Set(NUnit.Framework.Internal.PropertyNames.SkipReason,
                    "Test requires FWO_RUN_INTEGRATION_TESTS=true.");
            }
        }

        private static bool IsIntegrationEnvironmentEnabled()
        {
            string? runIntegrationTests = Environment.GetEnvironmentVariable("FWO_RUN_INTEGRATION_TESTS");
            return runIntegrationTests != null &&
                runIntegrationTests.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
    }
}

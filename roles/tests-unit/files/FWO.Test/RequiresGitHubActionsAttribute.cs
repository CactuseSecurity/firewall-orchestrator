using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace FWO.Test
{
    /// <summary>
    /// Custom attribute to skip tests when not running in GitHub Actions
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
    public class RequiresGitHubActionsAttribute : NUnitAttribute, IApplyToTest
    {
        public void ApplyToTest(NUnit.Framework.Internal.Test test)
        {
            if (!IsRunningInGitHubActions())
            {
                test.RunState = NUnit.Framework.Interfaces.RunState.Ignored;
                test.Properties.Set(NUnit.Framework.Internal.PropertyNames.SkipReason,
                    "Test requires GitHub Actions environment (CI=true, SUDO_USER=runner)");
            }
        }

        private static bool IsRunningInGitHubActions()
        {
            string? sudoUser = Environment.GetEnvironmentVariable("SUDO_USER");
            string? ci = Environment.GetEnvironmentVariable("CI");

            if (string.IsNullOrEmpty(sudoUser) || string.IsNullOrEmpty(ci))
            {
                return false;
            }

            return sudoUser.Equals("runner", StringComparison.OrdinalIgnoreCase) &&
                ci.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
    }
}

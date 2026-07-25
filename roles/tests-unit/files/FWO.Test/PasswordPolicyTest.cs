using FWO.Basics;
using FWO.Config.Api;
using FWO.Ui.Services;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class PasswordPolicyTest
    {
        [TestCaseSource(nameof(InvalidPasswordCases))]
        public void CheckPolicy_ReturnsExpectedFailure(string password, Action<GlobalConfig> configureGlobalConfig, string expectedError)
        {
            GlobalConfig globalConfig = CreateGlobalConfig();
            configureGlobalConfig(globalConfig);
            SimulatedUserConfig userConfig = new();

            bool result = PasswordPolicy.CheckPolicy(password, globalConfig, userConfig, out string errorMsg);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(errorMsg, Is.EqualTo(expectedError));
            });
        }

        [Test]
        public void CheckPolicy_ReturnsTrueForValidPassword()
        {
            GlobalConfig globalConfig = CreateGlobalConfig();
            SimulatedUserConfig userConfig = new();

            bool result = PasswordPolicy.CheckPolicy("Abcdef1!", globalConfig, userConfig, out string errorMsg);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(errorMsg, Is.Empty);
            });
        }

        private static IEnumerable<TestCaseData> InvalidPasswordCases()
        {
            yield return new TestCaseData("Ab1!", new Action<GlobalConfig>(config => config.PwMinLength = 8), "E54118");
            yield return new TestCaseData("abcdef1!", new Action<GlobalConfig>(config =>
            {
                config.PwUpperCaseRequired = true;
            }), "E5412");
            yield return new TestCaseData("ABCDEF1!", new Action<GlobalConfig>(config =>
            {
                config.PwLowerCaseRequired = true;
            }), "E5413");
            yield return new TestCaseData("Abcdefgh!", new Action<GlobalConfig>(config =>
            {
                config.PwNumberRequired = true;
            }), "E5414");
            yield return new TestCaseData("Abcdef12", new Action<GlobalConfig>(config =>
            {
                config.PwSpecialCharactersRequired = true;
            }), "E5415");
        }

        private static SimulatedGlobalConfig CreateGlobalConfig()
        {
            return new SimulatedGlobalConfig
            {
                PwMinLength = 4,
                PwUpperCaseRequired = false,
                PwLowerCaseRequired = false,
                PwNumberRequired = false,
                PwSpecialCharactersRequired = false
            };
        }
    }
}

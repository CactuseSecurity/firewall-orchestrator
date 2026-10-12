using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class TestInitializerTest
    {
        /// <summary>
        /// Verifies that only ordinary unit-test runs receive the synthetic config file.
        /// </summary>
        /// <param name="configuredConfigPath">The explicitly configured FWO config path.</param>
        /// <param name="runIntegrationTests">The integration-test opt-in value.</param>
        /// <param name="expected">Whether the synthetic config should be created.</param>
        [TestCase(null, null, true)]
        [TestCase("", "false", true)]
        [TestCase("/tmp/fworch.json", null, false)]
        [TestCase(null, "true", false)]
        [TestCase(null, "TRUE", false)]
        public void ShouldCreateSyntheticConfigUsesOnlyUnitTestEnvironments(
            string? configuredConfigPath,
            string? runIntegrationTests,
            bool expected)
        {
            bool result = TestInitializer.ShouldCreateSyntheticConfig(configuredConfigPath, runIntegrationTests);

            Assert.That(result, Is.EqualTo(expected));
        }
    }
}

using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class TestInitializerTest
    {
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

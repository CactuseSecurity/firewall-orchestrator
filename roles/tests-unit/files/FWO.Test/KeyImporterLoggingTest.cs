using FWO.Config.File;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Verifies that a failed key import never writes the key material to the log.
    /// NonParallelizable: the log is captured by redirecting the process global console output.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    internal class KeyImporterLoggingTest
    {
        private const string kKeyMarker = "FWOSECRETKEYMATERIALMARKER";
        private const string kBrokenPrivateKey = "-----BEGIN PRIVATE KEY-----\n" + kKeyMarker + "\n-----END PRIVATE KEY-----";

        [Test]
        public void ExtractKeyFromPem_InvalidKey_DoesNotLogKeyMaterial()
        {
            TextWriter originalOut = Console.Out;
            using StringWriter capturedOut = new();
            Console.SetOut(capturedOut);
            try
            {
                Assert.That(() => KeyImporter.ExtractKeyFromPem(kBrokenPrivateKey, true), Throws.Exception);
            }
            finally
            {
                Console.SetOut(originalOut);
            }

            string log = capturedOut.ToString();
            Assert.Multiple(() =>
            {
                Assert.That(log, Does.Contain("private key"));
                Assert.That(log, Does.Not.Contain(kKeyMarker));
            });
        }
    }
}

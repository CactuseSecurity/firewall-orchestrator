using FWO.Logging;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    public class LockTest
    {
        private string lockFilePath = $"/var/fworch/lock/{Assembly.GetEntryAssembly()?.GetName().Name}_log.lock";

        [SetUp]
        public void SetUp()
        {
            if (File.Exists(lockFilePath))
            {
                File.Delete(lockFilePath);
            }

            // Implicitly call static constructor so backround lock process is started
            Log.WriteInfo("Startup", "Starting Lock Tests...");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(lockFilePath))
            {
                File.Delete(lockFilePath);
            }
        }

        [Test]
        [Parallelizable]
        public async Task LogLockUi()
        {
            // Request lock
            using (var writer = new StreamWriter(lockFilePath))
            {
                await writer.WriteLineAsync("REQUESTED");
            }

            await Task.Delay(1200);

            // Assure lock is granted after request
            using (var reader = new StreamReader(lockFilePath))
            {
                Assert.That((await reader.ReadToEndAsync()).Trim().EndsWith("GRANTED"));
            }

            // Assure write is NOT possible after lock was granted
            Task logWriter = Task.Run(() =>
            {
                Log.WriteDebug("TEST_TILE", "TEST_TEXT");
            });

            await Task.Delay(500);

            Assert.That(logWriter.IsCompleted, Is.False);

            // Release lock
            using (var writer = new StreamWriter(lockFilePath))
            {
                await writer.WriteLineAsync("RELEASED");
            }

            await Task.Delay(1200);

            // Assure write IS possible after lock was released
            Assert.That(logWriter.IsCompleted, Is.True);

            // Request lock
            using (var writer = new StreamWriter(lockFilePath))
            {
                await writer.WriteLineAsync("REQUESTED");
            }

            await Task.Delay(11_200);

            // If not release in time make sure that the lock will be forcefully released
            using (var reader = new StreamReader(lockFilePath))
            {
                Assert.That((await reader.ReadToEndAsync()).Trim().EndsWith("FORCEFULLY RELEASED"));
            }
        }
    }
}

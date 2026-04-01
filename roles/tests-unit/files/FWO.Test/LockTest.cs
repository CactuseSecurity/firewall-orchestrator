using FWO.Logging;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    [NonParallelizable]
    public class LockTest
    {
        private string lockFilePath = $"/var/fworch/lock/{Assembly.GetEntryAssembly()?.GetName().Name}_log.lock";
        private static Random random = new Random();
        private static readonly TimeSpan LockStateTimeout = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

        [SetUp]
        public async Task SetUp()
        {
            await ExecuteFileAction(() =>
            {
                if (File.Exists(lockFilePath))
                {
                    File.Delete(lockFilePath);
                }
                return Task.CompletedTask;
            });

            // Implicitly call static constructor so backround lock process is started
            Log.WriteInfo("Startup", "Starting Lock Tests...");
            Log.WriteInfo("Startup", $"LockFilePath is: {lockFilePath}");
        }

        [TearDown]
        public async Task TearDown()
        {
            await ExecuteFileAction(() =>
            {
                if (File.Exists(lockFilePath))
                {
                    File.Delete(lockFilePath);
                }
                return Task.CompletedTask;
            });
        }

        [Test]
        public async Task LogLock()
        {
            // Request lock
            await ExecuteFileAction(async () =>
            {
                using (var writer = new StreamWriter(lockFilePath))
                {
                    await writer.WriteLineAsync("REQUESTED");
                }
            });

            // Assure lock is granted after request
            await WaitForLockFileState("GRANTED");

            // Assure write is NOT possible after lock was granted
            Task logWriter = Task.Run(() =>
            {
                Log.WriteDebug("TEST_TITLE", "TEST_TEXT");
            });

            await Task.Delay(PollInterval * 5);
            Assert.That(logWriter.IsCompleted, Is.False);

            // Release lock
            await ExecuteFileAction(async () =>
            {
                using (var writer = new StreamWriter(lockFilePath))
                {
                    await writer.WriteLineAsync("RELEASED");
                }
            });

            // Assure write IS possible after lock was released
            await WaitForTaskCompletion(logWriter, "log writer did not complete after lock release");
            Assert.That(logWriter.IsCompletedSuccessfully, Is.True);

            // Request lock
            await ExecuteFileAction(async () =>
            {
                using (var writer = new StreamWriter(lockFilePath))
                {
                    await writer.WriteLineAsync("REQUESTED");
                }
            });

            // If not release in time make sure that the lock will be forcefully released
            await WaitForLockFileState("FORCEFULLY RELEASED");
        }

        private static async Task ExecuteFileAction(Func<Task> action)
        {
            bool success = false;
            int maxRetryAttempts = 50;
            int retryCount = 0;

            // Handle IO Exception like file blocking from another process by retrying with a random delay
            while (!success && retryCount < maxRetryAttempts)
            {
                try
                {
                    await action();
                    success = true;
                }
                catch (IOException)
                {
                    retryCount++;
                }
                await Task.Delay(random.Next(50, 100));
            }

            if (!success)
            {
                Assert.Fail($"Lock file access failed after {maxRetryAttempts} retries.");
            }
        }

        private async Task WaitForLockFileState(string expectedSuffix)
        {
            DateTime deadline = DateTime.UtcNow + LockStateTimeout;
            while (DateTime.UtcNow < deadline)
            {
                string? lockState = await TryReadLockFile();
                if (lockState != null && lockState.EndsWith(expectedSuffix))
                {
                    return;
                }
                await Task.Delay(PollInterval);
            }

            string? finalState = await TryReadLockFile();
            Assert.Fail($"Timed out waiting for lock file state '{expectedSuffix}'. Last state: '{finalState ?? "<unavailable>"}'.");
        }

        private async Task<string?> TryReadLockFile()
        {
            string? content = null;
            await ExecuteFileAction(async () =>
            {
                using StreamReader reader = new(lockFilePath);
                content = (await reader.ReadToEndAsync()).Trim();
            });
            return content;
        }

        private static async Task WaitForTaskCompletion(Task task, string timeoutMessage)
        {
            DateTime deadline = DateTime.UtcNow + LockStateTimeout;
            while (DateTime.UtcNow < deadline)
            {
                if (task.IsCompleted)
                {
                    await task;
                    return;
                }
                await Task.Delay(PollInterval);
            }

            Assert.Fail(timeoutMessage);
        }
    }
}

using FWO.Logging;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    public class LockTest
    {
        private string lockFilePath = $"/var/fworch/lock/{Assembly.GetEntryAssembly()?.GetName().Name}_log.lock";
        private static Random random = new Random();

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

			await Task.Delay(2000);

			// Assure lock is granted after request
			await ExecuteFileAction(async () =>
			{
				using (var reader = new StreamReader(lockFilePath))
				{
					Assert.That((await reader.ReadToEndAsync()).Trim().EndsWith("GRANTED"));
				}
			});

			// Assure write is NOT possible after lock was granted
			Task logWriter = Task.Run(() =>
			{
				Log.WriteDebug("TEST_TITLE", "TEST_TEXT");
			});

			await Task.Delay(500);

			Assert.That(logWriter.IsCompleted, Is.False);

			// Release lock
			await ExecuteFileAction(async () =>
			{
				using (var writer = new StreamWriter(lockFilePath))
				{
					await writer.WriteLineAsync("RELEASED");
				}
			});

			await Task.Delay(2000);

			// Assure write IS possible after lock was released
			Assert.That(logWriter.IsCompletedSuccessfully, Is.True);

			// Request lock
			await ExecuteFileAction(async () =>
			{
				using (var writer = new StreamWriter(lockFilePath))
				{
					await writer.WriteLineAsync("REQUESTED");
				}
			});

			await Task.Delay(12_000);

			// If not release in time make sure that the lock will be forcefully released
			await ExecuteFileAction(async () =>
			{
				using (var reader = new StreamReader(lockFilePath))
				{
					Assert.That((await reader.ReadToEndAsync()).Trim().EndsWith("FORCEFULLY RELEASED"));
				}
			});
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
	}
}

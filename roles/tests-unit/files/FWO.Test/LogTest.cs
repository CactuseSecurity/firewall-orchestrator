using FWO.Logging;
using NUnit.Framework;
using FWO.Test.Helpers;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    [NonParallelizable]
    internal class LogTest
    {
        [Test]
        public async Task WriteAudit_PreservesLdapDnCharacters()
        {
            const string userDn = "CN=Jane Doe,OU=Firewall Team,DC=example,DC=com";
            string writtenLog = await ConsoleOutput.CaptureAsync(() =>
            {
                Log.WriteAudit("LDAP", "Testing DN logging", "jane.doe", userDn, false);
                return Task.CompletedTask;
            });

            Assert.That(writtenLog, Does.Contain(userDn));
            Assert.That(writtenLog, Does.Contain("(DN: CN=Jane Doe,OU=Firewall Team,DC=example,DC=com)"));
        }

        [Test]
        public async Task WriteInfo_StripsControlCharactersButKeepsMeaningfulPunctuation()
        {
            const string logText = "User\tCN=Jane Doe,\u001BOU=Firewall Team,DC=example,DC=com\r\nnext\u0000";
            string writtenLog = await ConsoleOutput.CaptureAsync(() =>
            {
                Log.WriteInfo("LDAP", logText);
                return Task.CompletedTask;
            });

            writtenLog = writtenLog.TrimEnd('\r', '\n');
            Assert.That(writtenLog, Does.Contain("User CN=Jane Doe,OU=Firewall Team,DC=example,DC=com next"));
            Assert.That(writtenLog.Any(char.IsControl), Is.False);
        }

        [Test]
        public async Task WriteInfo_StripsInvisibleUnicodeFormatCharacters()
        {
            const string logText = "prefix\u200B\u200D\u202A\u2066CN=Jane Doe,OU=Firewall Team,DC=example,DC=com\u2069suffix";
            string writtenLog = await ConsoleOutput.CaptureAsync(() =>
            {
                Log.WriteInfo("LDAP", logText);
                return Task.CompletedTask;
            });

            writtenLog = writtenLog.TrimEnd('\r', '\n');
            Assert.That(writtenLog, Does.Contain("prefixCN=Jane Doe,OU=Firewall Team,DC=example,DC=comsuffix"));
            Assert.That(writtenLog.Any(ch => char.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.Format), Is.False);
        }

        [Test]
        public async Task WriteInfo_FailingConsoleWrite_DoesNotBlockLaterLogCalls()
        {
            string writtenLog = await ConsoleOutput.CaptureAsync(async capture =>
            {
                Console.SetOut(new ThrowingTextWriter());
                Assert.DoesNotThrow(() => Log.WriteInfo("Console", "write that fails"));

                Console.SetOut(capture.Writer);
                Task secondWrite = Task.Run(() => Log.WriteInfo("Console", "write after failure"));
                await secondWrite.WaitAsync(kSecondWriteTimeout);
            });

            Assert.That(writtenLog, Does.Contain("write after failure"));
            Assert.That(writtenLog, Does.Not.Contain("write that fails"));
        }

        /// <summary>
        /// Replaces a log semaphore left held by a failed test, so a regression fails only that
        /// test instead of blocking every later log call of the test process.
        /// </summary>
        [TearDown]
        public void ReleaseHeldLogSemaphore()
        {
            FieldInfo semaphoreField = typeof(Log).GetField(kSemaphoreFieldName, BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingFieldException(nameof(Log), kSemaphoreFieldName);
            if (semaphoreField.GetValue(null) is SemaphoreSlim { CurrentCount: 0 })
            {
                semaphoreField.SetValue(null, new SemaphoreSlim(1, 1));
            }
        }

        private const string kSemaphoreFieldName = "semaphore";
        private static readonly TimeSpan kSecondWriteTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Writer that fails every write, like stdout during a broken pipe.
        /// </summary>
        private sealed class ThrowingTextWriter : TextWriter
        {
            /// <summary>
            /// Encoding of the (never written) output.
            /// </summary>
            public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;

            /// <summary>
            /// Always throws.
            /// </summary>
            /// <param name="value">Ignored.</param>
            public override void Write(char value)
            {
                throw new IOException("Simulated broken pipe");
            }

            /// <summary>
            /// Always throws.
            /// </summary>
            /// <param name="value">Ignored.</param>
            public override void WriteLine(string? value)
            {
                throw new IOException("Simulated broken pipe");
            }
        }
    }
}

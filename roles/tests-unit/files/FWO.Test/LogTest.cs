using FWO.Logging;
using NUnit.Framework;
using FWO.Test.Helpers;

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
        public async Task WriteError_IncludesInnerExceptionDetails()
        {
            Exception innerException = new IOException("socket closed during TLS handshake");
            Exception outerException = new HttpRequestException("An error occurred while sending the request.", innerException);

            string writtenLog = await ConsoleOutput.CaptureAsync(() =>
            {
                Log.WriteError("Transport", "API request failed.", outerException);
                return Task.CompletedTask;
            });

            Assert.Multiple(() =>
            {
                Assert.That(writtenLog, Does.Contain(nameof(HttpRequestException)));
                Assert.That(writtenLog, Does.Contain(nameof(IOException)));
                Assert.That(writtenLog, Does.Contain("socket closed during TLS handshake"));
            });
        }
    }
}

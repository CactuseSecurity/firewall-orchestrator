using FWO.Logging;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [NonParallelizable]
    internal class LogTest
    {
        [Test]
        public void WriteAudit_PreservesLdapDnCharacters()
        {
            const string userDn = "CN=Jane Doe,OU=Firewall Team,DC=example,DC=com";
            using StringWriter logOutput = new();
            TextWriter originalConsoleOut = Console.Out;

            try
            {
                Console.SetOut(logOutput);

                Log.WriteAudit("LDAP", "Testing DN logging", "jane.doe", userDn, false);

                string writtenLog = logOutput.ToString();
                Assert.That(writtenLog, Does.Contain(userDn));
                Assert.That(writtenLog, Does.Contain("(DN: CN=Jane Doe,OU=Firewall Team,DC=example,DC=com)"));
            }
            finally
            {
                Console.SetOut(originalConsoleOut);
            }
        }

        [Test]
        public void WriteInfo_StripsControlCharactersButKeepsMeaningfulPunctuation()
        {
            const string logText = "User\tCN=Jane Doe,\u001BOU=Firewall Team,DC=example,DC=com\r\nnext\u0000";
            using StringWriter logOutput = new();
            TextWriter originalConsoleOut = Console.Out;

            try
            {
                Console.SetOut(logOutput);

                Log.WriteInfo("LDAP", logText);

                string writtenLog = logOutput.ToString().TrimEnd('\r', '\n');
                Assert.That(writtenLog, Does.Contain("User CN=Jane Doe,OU=Firewall Team,DC=example,DC=com next"));
                Assert.That(writtenLog.Any(char.IsControl), Is.False);
            }
            finally
            {
                Console.SetOut(originalConsoleOut);
            }
        }

        [Test]
        public void WriteInfo_StripsInvisibleUnicodeFormatCharacters()
        {
            const string logText = "prefix\u200B\u200D\u202A\u2066CN=Jane Doe,OU=Firewall Team,DC=example,DC=com\u2069suffix";
            using StringWriter logOutput = new();
            TextWriter originalConsoleOut = Console.Out;

            try
            {
                Console.SetOut(logOutput);

                Log.WriteInfo("LDAP", logText);

                string writtenLog = logOutput.ToString().TrimEnd('\r', '\n');
                Assert.That(writtenLog, Does.Contain("prefixCN=Jane Doe,OU=Firewall Team,DC=example,DC=comsuffix"));
                Assert.That(writtenLog.Any(ch => char.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.Format), Is.False);
            }
            finally
            {
                Console.SetOut(originalConsoleOut);
            }
        }
    }
}

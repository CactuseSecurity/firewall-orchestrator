using FWO.Logging;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    public class LoggingContextTest
    {
        [Test]
        public void WriteError_WithContext_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                Log.WriteError("Test Error", "Failure", new InvalidOperationException("boom"), new Log.ErrorContext(User: "tester", Role: "admin"));
            });
        }

        [Test]
        public void WriteAudit_WithContext_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                Log.WriteAudit("Test Audit", "Audit entry", new Log.AuditContext(UserName: "tester", UserDn: "cn=tester,dc=example,dc=com"));
            });
        }
    }
}

using FWO.Ui.Services;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class GraphqlSubscriptionExceptionHandlerTest
    {
        [Test]
        public void Handle_LogsBeforeInvokingUiCallback()
        {
            List<string> calls = [];
            Exception exception = new InvalidOperationException("boom");

            GraphqlSubscriptionExceptionHandler.Handle(
                exception,
                () => calls.Add("ui"),
                ex => calls.Add($"log:{ex.Message}"));

            Assert.That(calls, Is.EqualTo(["log:boom", "ui"]));
        }

        [Test]
        public void Handle_AllowsMissingUiCallback()
        {
            List<string> calls = [];
            Exception exception = new InvalidOperationException("boom");

            GraphqlSubscriptionExceptionHandler.Handle(exception, exceptionHandler: ex => calls.Add($"log:{ex.Message}"));

            Assert.That(calls, Is.EqualTo(["log:boom"]));
        }
    }
}

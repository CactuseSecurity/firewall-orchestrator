using FWO.Api.Client.ExceptionHandling;
using FWO.Ui.Services;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [NonParallelizable]
    public class GraphqlSubscriptionExceptionHandlerTest
    {
        [Test]
        public void Handle_LogsBeforeInvokingUiCallback()
        {
            List<string> calls = [];
            Action<Exception> originalHandler = GraphqlExceptionHandler.ExceptionHandler;
            Exception exception = new InvalidOperationException("boom");

            try
            {
                GraphqlExceptionHandler.ExceptionHandler = ex => calls.Add($"log:{ex.Message}");

                GraphqlSubscriptionExceptionHandler.Handle(exception, () => calls.Add("ui"));
            }
            finally
            {
                GraphqlExceptionHandler.ExceptionHandler = originalHandler;
            }

            Assert.That(calls, Is.EqualTo(["log:boom", "ui"]));
        }

        [Test]
        public void Handle_AllowsMissingUiCallback()
        {
            List<string> calls = [];
            Action<Exception> originalHandler = GraphqlExceptionHandler.ExceptionHandler;
            Exception exception = new InvalidOperationException("boom");

            try
            {
                GraphqlExceptionHandler.ExceptionHandler = ex => calls.Add($"log:{ex.Message}");

                GraphqlSubscriptionExceptionHandler.Handle(exception);
            }
            finally
            {
                GraphqlExceptionHandler.ExceptionHandler = originalHandler;
            }

            Assert.That(calls, Is.EqualTo(["log:boom"]));
        }
    }
}

using FWO.Config.Api;
using FWO.Data;
using FWO.Services;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    /// <summary>
    /// Tests of the delivery outcome an email send reports back. A workflow action has to tell a failed
    /// send apart from one that resolved to no recipient at all, while the legacy boolean callers must
    /// keep seeing exactly what they saw before that distinction was introduced.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class EmailHelperDeliveryResultTest
    {
        private static readonly List<string> kNoRecipients = [];

        private static EmailHelper CreateEmailHelper()
        {
            // No dummy override: it would replace the empty recipient list and push the test into a real
            // send attempt against a mail server that does not exist here.
            SimulatedUserConfig userConfig = new() { UseDummyEmailAddress = false };
            return new EmailHelper(new SimulatedApiConnection(), null, userConfig, DefaultInit.DoNothing);
        }

        private static async Task<object?> InvokeSendEmailWithResult(EmailHelper helper, List<string> tos)
        {
            MethodInfo method = typeof(EmailHelper).GetMethod("SendEmailWithResult",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(EmailHelper).FullName, "SendEmailWithResult");
            List<object?> arguments = [tos, "subject", "body", null, null, true, null];
            object task = method.Invoke(helper, arguments.ToArray())
                ?? throw new InvalidOperationException("SendEmailWithResult returned null.");
            await (Task)task;
            return task.GetType().GetProperty("Result")?.GetValue(task);
        }

        [Test]
        public async Task SendEmailWithResultReportsNoRecipientsInsteadOfAFailedSend()
        {
            EmailHelper helper = CreateEmailHelper();

            object? result = await InvokeSendEmailWithResult(helper, kNoRecipients);

            Assert.That(result, Is.EqualTo(WorkflowEmailDeliveryResult.NoRecipients));
        }

        [Test]
        public async Task SendEmailWithResultTreatsBlankAddressesAsNoRecipients()
        {
            EmailHelper helper = CreateEmailHelper();
            List<string> blankRecipients = ["", ""];

            object? result = await InvokeSendEmailWithResult(helper, blankRecipients);

            Assert.That(result, Is.EqualTo(WorkflowEmailDeliveryResult.NoRecipients));
        }

        [Test]
        public async Task SendEmailToOwnerResponsiblesStillReportsFalseWhenNothingCouldBeSent()
        {
            // The boolean overload is what the non-workflow callers use. Only a delivered email may come
            // back as true, so an unresolvable recipient list has to stay false.
            EmailHelper helper = CreateEmailHelper();

            bool sent = await helper.SendEmailToOwnerResponsibles(new FwoOwner(), "subject", "body", EmailRecipientOption.None);

            Assert.That(sent, Is.False);
        }
    }
}

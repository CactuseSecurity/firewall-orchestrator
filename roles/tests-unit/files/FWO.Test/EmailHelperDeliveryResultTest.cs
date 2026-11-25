using FWO.Basics.Enums;
using FWO.Config.Api;
using FWO.Data;
using FWO.Mail;
using FWO.Services;
using FWO.Test.Helpers;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using System.Reflection;
using System.Text;

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
        private static readonly List<string> kToWithBlank = ["to@example.test", ""];
        private static readonly List<string> kCcWithBlank = ["cc@example.test", ""];
        private static readonly List<string> kBcc = ["bcc@example.test"];
        private static readonly List<string> kAllRecipients = ["to@example.test", "cc@example.test", "bcc@example.test"];
        private static readonly List<string> kDummyRecipient = ["dummy@example.test"];
        private const string kSender = "sender@example.test";

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
            List<object?> arguments = [tos, "subject", "body", null, null, null];
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
        public async Task SendEmailWithResultDeliversToAllRecipientsWithAttachmentAndPriority()
        {
            using RecordingSmtpServer server = new();
            SendingEmailHelper helper = CreateSendingEmailHelper(server.Port);
            EmailSendOptions options = new(false, CreateAttachment("report.csv"), EmailPriority.High);

            WorkflowEmailDeliveryResult result = await helper.Send(kToWithBlank, kCcWithBlank, kBcc, options);
            await server.WaitForSessionEnd();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(WorkflowEmailDeliveryResult.Delivered));
                Assert.That(server.Recipients, Is.EquivalentTo(kAllRecipients));
                Assert.That(server.Data, Does.Contain("Subject: subject"));
                Assert.That(server.Data, Does.Contain("Importance: high"));
                Assert.That(server.Data, Does.Contain("X-Priority: 1"));
                Assert.That(server.Data, Does.Contain("report.csv"));
                Assert.That(server.Data, Does.Contain("Content-Type: text/plain"));
            });
        }

        [Test]
        public async Task SendEmailWithResultSendsHtmlAtNormalPriorityByDefault()
        {
            using RecordingSmtpServer server = new();
            SendingEmailHelper helper = CreateSendingEmailHelper(server.Port);

            WorkflowEmailDeliveryResult result = await helper.Send(kBcc, null, null, null);
            await server.WaitForSessionEnd();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(WorkflowEmailDeliveryResult.Delivered));
                Assert.That(server.Recipients, Is.EqualTo(kBcc));
                Assert.That(server.Data, Does.Contain("Content-Type: text/html"));
                Assert.That(server.Data, Does.Not.Contain("Importance:"));
                Assert.That(server.Data, Does.Not.Contain("X-Priority:"));
            });
        }

        [Test]
        public async Task SendEmailWithResultReplacesAllRecipientsByDummyAddress()
        {
            using RecordingSmtpServer server = new();
            SendingEmailHelper helper = CreateSendingEmailHelper(server.Port, "dummy@example.test");

            WorkflowEmailDeliveryResult result = await helper.Send(kToWithBlank, kCcWithBlank, kBcc, null);
            await server.WaitForSessionEnd();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(WorkflowEmailDeliveryResult.Delivered));
                Assert.That(server.Recipients, Is.EqualTo(kDummyRecipient));
            });
        }

        [Test]
        public async Task SendEmailWithResultReportsNoRecipientsWhenDummyAddressIsEmpty()
        {
            SendingEmailHelper helper = CreateSendingEmailHelper(RecordingSmtpServer.GetClosedPort(), "");

            WorkflowEmailDeliveryResult result = await helper.Send(kToWithBlank, kCcWithBlank, kBcc, null);

            Assert.That(result, Is.EqualTo(WorkflowEmailDeliveryResult.NoRecipients));
        }

        [Test]
        public async Task SendEmailWithResultReportsFailedWhenMailServerIsUnreachable()
        {
            SendingEmailHelper helper = CreateSendingEmailHelper(RecordingSmtpServer.GetClosedPort());

            WorkflowEmailDeliveryResult result = await helper.Send(kBcc, null, null, null);

            Assert.That(result, Is.EqualTo(WorkflowEmailDeliveryResult.Failed));
        }

        [Test]
        public async Task SendEmailToNotificationRecipientsReportsFalseWhenNothingCouldBeSent()
        {
            // Only a delivered email may come back as true, so an unresolvable recipient list has to stay false.
            EmailHelper helper = CreateEmailHelper();

            bool sent = await helper.SendEmailToNotificationRecipients(
                new FwoNotification { RecipientTo = EmailRecipientOption.None },
                new FwoOwner(),
                "subject",
                "body");

            Assert.That(sent, Is.False);
        }

        private static SendingEmailHelper CreateSendingEmailHelper(int port, string? dummyEmailAddress = null)
        {
            SimulatedUserConfig userConfig = new()
            {
                UseDummyEmailAddress = dummyEmailAddress != null,
                DummyEmailAddress = dummyEmailAddress ?? "",
                EmailServerAddress = "127.0.0.1",
                EmailPort = port,
                EmailTls = EmailEncryptionMethod.None,
                EmailSenderAddress = kSender
            };
            return new SendingEmailHelper(userConfig);
        }

        private static FormFile CreateAttachment(string fileName)
        {
            byte[] bytes = Encoding.UTF8.GetBytes("a;b");
            return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = "text/csv"
            };
        }

        /// <summary>
        /// Opens the protected send method of the email helper for the tests.
        /// </summary>
        private sealed class SendingEmailHelper(UserConfig userConfig)
            : EmailHelper(new SimulatedApiConnection(), null, userConfig, DefaultInit.DoNothing)
        {
            public Task<WorkflowEmailDeliveryResult> Send(List<string> tos, List<string>? ccs, List<string>? bccs, EmailSendOptions? options)
            {
                return SendEmailWithResult(tos.ToList(), "subject", "body", ccs?.ToList(), bccs?.ToList(), options);
            }
        }
    }
}

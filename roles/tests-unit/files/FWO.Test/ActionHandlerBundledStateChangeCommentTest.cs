using FWO.Basics;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Mail;
using FWO.Services;
using FWO.Services.Workflow;
using FWO.Test.Helpers;
using NUnit.Framework;
using System.Text.Json;

namespace FWO.Test
{
    /// <summary>
    /// Tests that a bundled workflow email keeps the comment of the state change it was captured in. The bundle is
    /// flushed by a later request without that state change, so the comment has to travel with the captured email.
    /// </summary>
    internal partial class ActionHandlerTest
    {
        private const string kBundledComment = "approved with <conditions>";

        private static WfStateAction BundlingEmailAction()
        {
            return new WfStateAction
            {
                Id = 5,
                Event = StateActionEvents.OnSet.ToString(),
                ActionType = StateActionTypes.SendEmail.ToString(),
                Scope = WfObjectScopes.RequestTask.ToString(),
                TaskType = WfTaskType.access.ToString(),
                ExternalParams = JsonSerializer.Serialize(new EmailActionParams
                {
                    NotificationIds = [7],
                    AttachedContent = EmailAttachedContent.RequestedConnections,
                    RequestTaskBundleMode = EmailRequestTaskBundleMode.SameTaskType
                })
            };
        }

        private static WfReqTask BundledAccessTask(long id, int taskNumber)
        {
            return new WfReqTask { Id = id, TicketId = 7, TaskType = WfTaskType.access.ToString(), TaskNumber = taskNumber, StateId = 60 };
        }

        [Test]
        public async Task FlushEmailBundleCollector_SendsTheStateChangeCommentTheEmailWasCapturedWith()
        {
            using RecordingSmtpServer server = new();
            ActionHandlerTestApiConn apiConn = new()
            {
                Notifications =
                [
                    new()
                    {
                        Id = 7,
                        NotificationClient = NotificationClient.WfAction,
                        Layout = NotificationLayout.HtmlInBody,
                        EmailSubject = $"Promoted: {Placeholder.STATECHANGE_COMMENT}",
                        EmailBody = $"<p>Comment: {Placeholder.STATECHANGE_COMMENT}</p>",
                        RecipientTo = EmailRecipientOption.OtherAddresses,
                        EmailAddressTo = "to@example.test"
                    }
                ],
                FullTicket = CreateTicket(BundledAccessTask(11, 1))
            };
            apiConn.FullTicket.Id = 7;
            SimulatedUserConfig userConfig = new()
            {
                UseDummyEmailAddress = false,
                EmailServerAddress = "127.0.0.1",
                EmailPort = server.Port,
                EmailTls = EmailEncryptionMethod.None,
                EmailSenderAddress = "sender@example.test"
            };
            WorkflowEmailBundleCollector collector = new();
            WfHandler capturingWfHandler = new() { userConfig = userConfig };
            ActionHandler capturingHandler = new(apiConn, capturingWfHandler) { EmailBundleCollector = collector };
            await capturingHandler.Init();
            using (capturingWfHandler.BeginStateChangeComment(kBundledComment))
            {
                await capturingHandler.SendEmail(BundlingEmailAction(), BundledAccessTask(11, 1), WfObjectScopes.RequestTask, null);
            }
            Assert.That(collector.PendingItems, Has.Count.EqualTo(1), "the email must be captured, not sent at once");

            // the flush-only request works with a fresh workflow handler outside of the state change
            WfHandler flushingWfHandler = new() { userConfig = userConfig, ActTicket = apiConn.FullTicket };
            SetMatrix(flushingWfHandler, WfTaskType.access.ToString());
            ActionHandler flushingHandler = new(apiConn, flushingWfHandler) { EmailBundleCollector = collector };
            await flushingHandler.Init();
            await flushingHandler.FlushEmailBundleCollector();
            await server.WaitForSessionEnd();

            Assert.Multiple(() =>
            {
                Assert.That(collector.PendingItems, Is.Empty);
                Assert.That(server.Data, Does.Contain($"Subject: Promoted: {kBundledComment}"));
                Assert.That(server.Data, Does.Contain("Comment: approved with &lt;conditions&gt;"));
            });
        }

        [Test]
        public async Task FlushEmailBundleCollector_DoesNotMergeEmailsWithDifferentStateChangeComments()
        {
            WorkflowEmailBundleCollector collector = new();
            collector.TryAdd(BundlingEmailAction(), BundledAccessTask(11, 1), null, null, new NotificationPlaceholderData { StateChangeComment = "first" });
            collector.TryAdd(BundlingEmailAction(), BundledAccessTask(12, 2), null, null, new NotificationPlaceholderData { StateChangeComment = "second" });
            collector.TryAdd(BundlingEmailAction(), BundledAccessTask(13, 3), null, null, new NotificationPlaceholderData { StateChangeComment = "first" });
            DeliveryControlledActionHandler handler = new(new ActionHandlerTestApiConn(), new WfHandler { userConfig = new SimulatedUserConfig() }, [])
            {
                EmailBundleCollector = collector
            };
            await handler.Init();

            await handler.FlushEmailBundleCollector();

            Assert.Multiple(() =>
            {
                Assert.That(handler.AttemptedTaskIds, Is.EqualTo(new List<long> { 11, 12 }));
                Assert.That(handler.AttemptedPlaceholderData.Select(data => data?.StateChangeComment), Is.EqualTo(new List<string?> { "first", "second" }));
                Assert.That(collector.PendingItems, Is.Empty);
            });
        }

        [Test]
        public void WorkflowEmailBundleCollector_AddCopiesThePlaceholderValues()
        {
            WorkflowEmailBundleCollector collector = new();
            NotificationPlaceholderData placeholderData = new() { StateChangeComment = "approved", Reason = "reason" };

            collector.TryAdd(BundlingEmailAction(), BundledAccessTask(11, 1), null, null, placeholderData);
            string originalBundleKey = collector.PendingItems[0].BundleKey;
            placeholderData.StateChangeComment = "changed";

            Assert.Multiple(() =>
            {
                Assert.That(collector.PendingItems[0].PlaceholderData, Is.Not.SameAs(placeholderData));
                Assert.That(collector.PendingItems[0].PlaceholderData?.StateChangeComment, Is.EqualTo("approved"));
                Assert.That(collector.PendingItems[0].PlaceholderData?.Reason, Is.EqualTo("reason"));
                Assert.That(collector.PendingItems[0].BundleKey, Is.EqualTo(originalBundleKey));
            });
        }

        [Test]
        public void BuildBundleKey_SeparatesDifferentPlaceholderValuesOnly()
        {
            WfStateAction action = BundlingEmailAction();
            WfReqTask task = BundledAccessTask(11, 1);

            string firstKey = WorkflowEmailBundleItem.BuildBundleKey(action, task, null, null, new NotificationPlaceholderData { StateChangeComment = "first" });
            string sameKey = WorkflowEmailBundleItem.BuildBundleKey(action, task, null, null, new NotificationPlaceholderData { StateChangeComment = "first" });
            string secondKey = WorkflowEmailBundleItem.BuildBundleKey(action, task, null, null, new NotificationPlaceholderData { StateChangeComment = "second" });
            string noValuesKey = WorkflowEmailBundleItem.BuildBundleKey(action, task, null, null);

            Assert.Multiple(() =>
            {
                Assert.That(sameKey, Is.EqualTo(firstKey));
                Assert.That(secondKey, Is.Not.EqualTo(firstKey));
                Assert.That(noValuesKey, Is.Not.EqualTo(firstKey));
                Assert.That(WorkflowEmailBundleItem.BuildBundleKey(action, task, null, null, null), Is.EqualTo(noValuesKey));
            });
        }
    }
}

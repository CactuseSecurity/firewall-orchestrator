using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Middleware.Server;
using FWO.Report;
using FWO.Report.Filter;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class NotificationTest
    {
        readonly NotificationTestApiConn apiConnection = new();
        readonly SimulatedGlobalConfig globalConfig = new() { UseDummyEmailAddress = true, DummyEmailAddress = "x@y.de" };
        const string EmailText = "email text";
        private static readonly string[] kDummyRecipients = ["x@y.de"];
        private static readonly string[] kToRecipients = ["a@b.de"];
        private static readonly string[] kCcRecipients = ["cc1@example.com", "cc2@example.com"];
        private static readonly string[] kBccRecipients = ["bcc1@example.com", "bcc2@example.com"];
        private static readonly string[] kJsonRecipients = ["json@example.test"];
        private static readonly string[] kMainRecipients = ["main@example.test"];
        private static readonly NotificationDeadline[] kNoneDeadline = [NotificationDeadline.None];

        [Test]
        public async Task TestInterfaceRequestNotification()
        {
            List<UserGroup> ownerGroups = [];
            NotificationService notificationService = await NotificationService.CreateAsync(NotificationClient.InterfaceRequest, globalConfig, apiConnection, ownerGroups);
            FwoOwner owner = new();

            int emailsSent = await notificationService.SendNotificationsIfDue(owner, DateTime.Now.AddDays(-8), EmailText);
            ClassicAssert.AreEqual(2, emailsSent);
            ClassicAssert.AreEqual(2, await notificationService.UpdateNotificationsLastSent());

            notificationService.Notifications[0].LastSent = DateTime.Now.AddDays(-1);
            emailsSent = await notificationService.SendNotificationsIfDue(owner, DateTime.Now.AddDays(-8), EmailText);
            ClassicAssert.AreEqual(1, emailsSent);
            ClassicAssert.AreEqual(1, await notificationService.UpdateNotificationsLastSent());

            notificationService.Notifications[1].LastSent = DateTime.Now.AddDays(-8);
            emailsSent = await notificationService.SendNotificationsIfDue(owner, DateTime.Now.AddDays(-15), EmailText);
            ClassicAssert.AreEqual(0, emailsSent);
            ClassicAssert.AreEqual(0, await notificationService.UpdateNotificationsLastSent());

            notificationService.Notifications[1].InitialOffsetAfterDeadline = 7;
            emailsSent = await notificationService.SendNotificationsIfDue(owner, DateTime.Now.AddDays(-15), EmailText);
            ClassicAssert.AreEqual(1, emailsSent);

            notificationService.Notifications[1].InitialOffsetAfterDeadline = -7;
            emailsSent = await notificationService.SendNotificationsIfDue(owner, DateTime.Now.AddDays(-1), EmailText);
            ClassicAssert.AreEqual(1, emailsSent);
        }

        [Test]
        public async Task TestRecertNotification()
        {
            List<UserGroup> ownerGroups = [];
            NotificationService notificationService = await NotificationService.CreateAsync(NotificationClient.Recertification, globalConfig, apiConnection, ownerGroups);
            FwoOwner owner = new() { NextRecertDate = DateTime.Now.AddDays(21) };

            int emailsSent = await notificationService.SendNotificationsIfDue(owner, null, EmailText, new ReportRecertEvent(new(""), UserConfig.ForTextOnly(globalConfig), Basics.ReportType.RecertificationEvent) { });
            ClassicAssert.AreEqual(1, emailsSent);

            notificationService.Notifications[0].LastSent = DateTime.Now;
            emailsSent = await notificationService.SendNotificationsIfDue(owner, null, EmailText);
            ClassicAssert.AreEqual(0, emailsSent);

            notificationService.Notifications[0].LastSent = DateTime.Now.AddDays(-7);
            owner.NextRecertDate = DateTime.Now.AddDays(-7);
            emailsSent = await notificationService.SendNotificationsIfDue(owner, null, EmailText);
            ClassicAssert.AreEqual(1, emailsSent);

            notificationService.Notifications[0].LastSent = DateTime.Now.AddDays(-7);
            owner.NextRecertDate = DateTime.Now.AddDays(-14);
            emailsSent = await notificationService.SendNotificationsIfDue(owner, null, EmailText);
            ClassicAssert.AreEqual(0, emailsSent);
        }

        [Test]
        public async Task TestRuleExpiryNotificationDueCalculation()
        {
            List<UserGroup> ownerGroups = [];
            NotificationService notificationService = await NotificationService.CreateAsync(NotificationClient.RuleTimer, globalConfig, apiConnection, ownerGroups);
            FwoOwner owner = new();
            FwoNotification notification = notificationService.Notifications[0];

            ClassicAssert.IsTrue(NotificationService.IsNotificationDue(owner, DateTime.Now.AddDays(-8), notification));
            notification.LastSent = DateTime.Now.AddDays(-1);
            ClassicAssert.IsFalse(NotificationService.IsNotificationDue(owner, DateTime.Now.AddDays(-8), notification));
        }

        [Test]
        public void IsNotificationDue_ReturnsFalse_WhenBeforeDeadlineIntervalIsMissing()
        {
            FwoOwner owner = new() { NextRecertDate = DateTime.Now.AddDays(7) };
            FwoNotification notification = new()
            {
                Deadline = NotificationDeadline.RecertDate,
                IntervalBeforeDeadline = null,
                OffsetBeforeDeadline = 1
            };

            ClassicAssert.IsFalse(NotificationService.IsNotificationDue(owner, null, notification));
        }

        [Test]
        public void IsNotificationDue_ReturnsFalse_WhenAfterDeadlineIntervalIsMissing()
        {
            FwoOwner owner = new();
            FwoNotification notification = new()
            {
                Deadline = NotificationDeadline.RuleExpiry,
                RepeatIntervalAfterDeadline = null,
                InitialOffsetAfterDeadline = 0,
                RepeatOffsetAfterDeadline = 1,
                RepetitionsAfterDeadline = 1
            };

            ClassicAssert.IsFalse(NotificationService.IsNotificationDue(owner, DateTime.Now.AddDays(-1), notification));
        }

        [Test]
        public async Task TestNotificationEmailBodyIsLoaded()
        {
            List<UserGroup> ownerGroups = [];
            NotificationService notificationService = await NotificationService.CreateAsync(NotificationClient.InterfaceRequest, globalConfig, apiConnection, ownerGroups);

            ClassicAssert.AreEqual("body1", notificationService.Notifications[0].EmailBody);
            ClassicAssert.AreEqual("body2", notificationService.Notifications[1].EmailBody);
        }

        [Test]
        public async Task SendNotification_UsesNotificationBodyWhenContentIsNull()
        {
            List<UserGroup> ownerGroups = [];
            NotificationService notificationService = await NotificationService.CreateAsync(NotificationClient.InterfaceRequest, globalConfig, apiConnection, ownerGroups);
            FwoNotification notification = notificationService.Notifications[0];
            FwoOwner owner = new();

            MethodInfo? prepareEmail = typeof(NotificationService).GetMethod("PrepareEmail", BindingFlags.Instance | BindingFlags.NonPublic);
            ClassicAssert.IsNotNull(prepareEmail);

            Task<FWO.Mail.MailData> task = (Task<FWO.Mail.MailData>)(prepareEmail?.Invoke(notificationService, [notification, null, owner, null, ""])
                ?? throw new InvalidOperationException("PrepareEmail returned null task."));
            FWO.Mail.MailData mailData = await task;

            ClassicAssert.AreEqual(notification.EmailBody, mailData.Body);
        }

        [Test]
        public async Task SendNotification_PrepareEmail_HandlesNullSubjectAndBody()
        {
            List<UserGroup> ownerGroups = [];
            NotificationService notificationService = await NotificationService.CreateAsync(NotificationClient.InterfaceRequest, globalConfig, apiConnection, ownerGroups);
            FwoNotification notification = notificationService.Notifications[0];
            notification.EmailSubject = null!;
            notification.EmailBody = null!;
            FwoOwner owner = new();

            MethodInfo? prepareEmail = typeof(NotificationService).GetMethod("PrepareEmail", BindingFlags.Instance | BindingFlags.NonPublic);
            ClassicAssert.IsNotNull(prepareEmail);

            Task<FWO.Mail.MailData> task = (Task<FWO.Mail.MailData>)(prepareEmail?.Invoke(notificationService, [notification, null, owner, null, ""])
                ?? throw new InvalidOperationException("PrepareEmail returned null task."));
            FWO.Mail.MailData mailData = await task;

            ClassicAssert.AreEqual(string.Empty, mailData.Subject);
            ClassicAssert.AreEqual(string.Empty, mailData.Body);
        }

        [Test]
        public async Task SendNotification_PrepareEmail_UsesContentPlaceholder_WhenPresent()
        {
            List<UserGroup> ownerGroups = [];
            NotificationService notificationService = await NotificationService.CreateAsync(NotificationClient.ImportChange, globalConfig, apiConnection, ownerGroups);
            FwoNotification notification = notificationService.Notifications[0];
            FwoOwner owner = new();

            MethodInfo? prepareEmail = typeof(NotificationService).GetMethod("PrepareEmail", BindingFlags.Instance | BindingFlags.NonPublic);
            ClassicAssert.IsNotNull(prepareEmail);

            Task<FWO.Mail.MailData> task = (Task<FWO.Mail.MailData>)(prepareEmail?.Invoke(notificationService, [notification, "\r\n\r\nMgmt A: 5 changes", owner, null, ""])
                ?? throw new InvalidOperationException("PrepareEmail returned null task."));
            FWO.Mail.MailData mailData = await task;

            ClassicAssert.AreEqual("configured import body<br><br>Mgmt A: 5 changes", mailData.Body);
        }

        [Test]
        public async Task SendNotification_PrepareEmail_AppendsHtmlReportBody_WhenLayoutIsHtmlInBody()
        {
            List<UserGroup> ownerGroups = [];
            NotificationService notificationService = await NotificationService.CreateAsync(NotificationClient.InterfaceRequest, globalConfig, apiConnection, ownerGroups);
            FwoNotification notification = notificationService.Notifications[0];
            notification.Layout = NotificationLayout.HtmlInBody;
            notification.EmailBody = "prefix ";
            FwoOwner owner = new();
            TestReport report = new();

            MethodInfo? prepareEmail = typeof(NotificationService).GetMethod("PrepareEmail", BindingFlags.Instance | BindingFlags.NonPublic);
            ClassicAssert.IsNotNull(prepareEmail);

            Task<FWO.Mail.MailData> task = (Task<FWO.Mail.MailData>)(prepareEmail?.Invoke(notificationService, [notification, null, owner, report, ""])
                ?? throw new InvalidOperationException("PrepareEmail returned null task."));
            FWO.Mail.MailData mailData = await task;

            ClassicAssert.AreEqual("prefix <p>report body</p>", mailData.Body);
            ClassicAssert.IsNull(mailData.Attachments);
        }

        [Test]
        public async Task SendNotification_PrepareEmail_IncludesBccRecipients()
        {
            List<UserGroup> ownerGroups = [];
            NotificationService notificationService = await NotificationService.CreateAsync(NotificationClient.InterfaceRequest, globalConfig, apiConnection, ownerGroups);
            FwoNotification notification = notificationService.Notifications[0];
            notification.RecipientBcc = EmailRecipientOption.OtherAddresses;
            notification.EmailAddressBcc = "bcc1@example.com,bcc2@example.com";
            FwoOwner owner = new();

            globalConfig.UseDummyEmailAddress = false;
            try
            {
                MethodInfo? prepareEmail = typeof(NotificationService).GetMethod("PrepareEmail", BindingFlags.Instance | BindingFlags.NonPublic);
                ClassicAssert.IsNotNull(prepareEmail);

                Task<FWO.Mail.MailData> task = (Task<FWO.Mail.MailData>)(prepareEmail?.Invoke(notificationService, [notification, null, owner, null, ""])
                    ?? throw new InvalidOperationException("PrepareEmail returned null task."));
                FWO.Mail.MailData mailData = await task;

                CollectionAssert.AreEqual(kBccRecipients, mailData.Bcc);
            }
            finally
            {
                globalConfig.UseDummyEmailAddress = true;
            }
        }

        [Test]
        public async Task SendNotification_PrepareEmail_IncludesCcRecipients()
        {
            List<UserGroup> ownerGroups = [];
            NotificationService notificationService = await NotificationService.CreateAsync(NotificationClient.InterfaceRequest, globalConfig, apiConnection, ownerGroups);
            FwoNotification notification = notificationService.Notifications[0];
            notification.RecipientCc = EmailRecipientOption.OtherAddresses;
            notification.EmailAddressCc = "cc1@example.com,cc2@example.com";
            FwoOwner owner = new();

            globalConfig.UseDummyEmailAddress = false;
            try
            {
                MethodInfo? prepareEmail = typeof(NotificationService).GetMethod("PrepareEmail", BindingFlags.Instance | BindingFlags.NonPublic);
                ClassicAssert.IsNotNull(prepareEmail);

                Task<FWO.Mail.MailData> task = (Task<FWO.Mail.MailData>)(prepareEmail?.Invoke(notificationService, [notification, null, owner, null, ""])
                    ?? throw new InvalidOperationException("PrepareEmail returned null task."));
                FWO.Mail.MailData mailData = await task;

                CollectionAssert.AreEqual(kToRecipients, mailData.To);
                CollectionAssert.AreEqual(kCcRecipients, mailData.Cc);
            }
            finally
            {
                globalConfig.UseDummyEmailAddress = true;
            }
        }

        [Test]
        public async Task SendNotification_PrepareEmail_AllowsNullBccFields()
        {
            List<UserGroup> ownerGroups = [];
            NotificationService notificationService = await NotificationService.CreateAsync(NotificationClient.InterfaceRequest, globalConfig, apiConnection, ownerGroups);
            FwoNotification notification = notificationService.Notifications[0];
            notification.RecipientBcc = EmailRecipientOption.None;
            notification.EmailAddressBcc = null!;
            FwoOwner owner = new();

            globalConfig.UseDummyEmailAddress = false;
            try
            {
                MethodInfo? prepareEmail = typeof(NotificationService).GetMethod("PrepareEmail", BindingFlags.Instance | BindingFlags.NonPublic);
                ClassicAssert.IsNotNull(prepareEmail);

                Task<FWO.Mail.MailData> task = (Task<FWO.Mail.MailData>)(prepareEmail?.Invoke(notificationService, [notification, null, owner, null, ""])
                    ?? throw new InvalidOperationException("PrepareEmail returned null task."));
                FWO.Mail.MailData mailData = await task;

                ClassicAssert.IsNotNull(mailData.Bcc);
                ClassicAssert.AreEqual(0, mailData.Bcc.Count);
            }
            finally
            {
                globalConfig.UseDummyEmailAddress = true;
            }
        }

        [Test]
        public async Task SendNotification_PrepareEmail_UsesToAddresses_WhenCcAndBccAreNotConfigured()
        {
            List<UserGroup> ownerGroups = [];
            NotificationService notificationService = await NotificationService.CreateAsync(NotificationClient.InterfaceRequest, globalConfig, apiConnection, ownerGroups);
            FwoNotification notification = notificationService.Notifications[0];
            notification.RecipientCc = EmailRecipientOption.None;
            notification.EmailAddressCc = "cc@example.com";
            notification.RecipientBcc = EmailRecipientOption.None;
            notification.EmailAddressBcc = "bcc@example.com";
            FwoOwner owner = new();

            globalConfig.UseDummyEmailAddress = false;
            try
            {
                MethodInfo? prepareEmail = typeof(NotificationService).GetMethod("PrepareEmail", BindingFlags.Instance | BindingFlags.NonPublic);
                ClassicAssert.IsNotNull(prepareEmail);

                Task<FWO.Mail.MailData> task = (Task<FWO.Mail.MailData>)(prepareEmail?.Invoke(notificationService, [notification, null, owner, null, ""])
                    ?? throw new InvalidOperationException("PrepareEmail returned null task."));
                FWO.Mail.MailData mailData = await task;

                CollectionAssert.AreEqual(kToRecipients, mailData.To);
                ClassicAssert.AreEqual(0, mailData.Cc.Count);
                ClassicAssert.AreEqual(0, mailData.Bcc.Count);
            }
            finally
            {
                globalConfig.UseDummyEmailAddress = true;
            }
        }

        [Test]
        public async Task SendBundledNotifications_PrepareBundledEmail_AddsAllBundleAttachments()
        {
            List<UserGroup> ownerGroups = [];
            NotificationService notificationService = await NotificationService.CreateAsync(NotificationClient.InterfaceRequest, globalConfig, apiConnection, ownerGroups);
            FwoOwner owner = new() { Name = "Owner", ExtAppId = "1" };
            TestReport report = new();
            string bundleId = Guid.NewGuid().ToString();
            FwoNotification htmlNotification = notificationService.Notifications[0];
            htmlNotification.Layout = NotificationLayout.HtmlAsAttachment;
            htmlNotification.BundleType = BundleType.Attachments;
            htmlNotification.BundleId = bundleId;

            FwoNotification jsonNotification = notificationService.Notifications[1];
            jsonNotification.Layout = NotificationLayout.JsonAsAttachment;
            jsonNotification.BundleType = BundleType.Attachments;
            jsonNotification.BundleId = bundleId;

            MethodInfo? prepareBundledEmail = typeof(NotificationService).GetMethod("PrepareBundledEmail", BindingFlags.Instance | BindingFlags.NonPublic);
            ClassicAssert.IsNotNull(prepareBundledEmail);

            Task<FWO.Mail.MailData> task = (Task<FWO.Mail.MailData>)(prepareBundledEmail?.Invoke(notificationService,
                [new List<FwoNotification> { htmlNotification, jsonNotification }, null, owner, report, ""])
                ?? throw new InvalidOperationException("PrepareBundledEmail returned null task."));
            FWO.Mail.MailData mailData = await task;

            ClassicAssert.AreEqual(htmlNotification.EmailBody, mailData.Body);
            ClassicAssert.IsNotNull(mailData.Attachments);
            ClassicAssert.AreEqual(2, mailData.Attachments?.Count);
        }

        [Test]
        public async Task PrepareBundledEmail_ReturnsBaseMailForNotificationsWithoutBundleType()
        {
            List<UserGroup> ownerGroups = [];
            NotificationService notificationService = await NotificationService.CreateAsync(NotificationClient.InterfaceRequest, globalConfig, apiConnection, ownerGroups);
            FwoOwner owner = new() { Name = "Owner", ExtAppId = "1" };
            FwoNotification notification = notificationService.Notifications[0];

            MethodInfo? prepareBundledEmail = typeof(NotificationService).GetMethod("PrepareBundledEmail", BindingFlags.Instance | BindingFlags.NonPublic);
            ClassicAssert.IsNotNull(prepareBundledEmail);

            Task<FWO.Mail.MailData> task = (Task<FWO.Mail.MailData>)(prepareBundledEmail?.Invoke(notificationService, [new List<FwoNotification> { notification }, null, owner, null, ""])
                ?? throw new InvalidOperationException("PrepareBundledEmail returned null task."));
            FWO.Mail.MailData mailData = await task;

            ClassicAssert.AreEqual(notification.EmailBody, mailData.Body);
            ClassicAssert.IsNull(mailData.Attachments);
        }

        [Test]
        public async Task PrepareBundledEmail_ThrowsForUnsupportedBundleType()
        {
            List<UserGroup> ownerGroups = [];
            NotificationService notificationService = await NotificationService.CreateAsync(NotificationClient.InterfaceRequest, globalConfig, apiConnection, ownerGroups);
            FwoOwner owner = new() { Name = "Owner", ExtAppId = "1" };
            FwoNotification notification = notificationService.Notifications[0];
            notification.BundleType = (BundleType)999;

            MethodInfo? prepareBundledEmail = typeof(NotificationService).GetMethod("PrepareBundledEmail", BindingFlags.Instance | BindingFlags.NonPublic);
            ClassicAssert.IsNotNull(prepareBundledEmail);

            Task<FWO.Mail.MailData> task = (Task<FWO.Mail.MailData>)(prepareBundledEmail?.Invoke(notificationService, [new List<FwoNotification> { notification }, null, owner, new TestReport(), ""])
                ?? throw new InvalidOperationException("PrepareBundledEmail returned null task."));

            Assert.ThrowsAsync<NotSupportedException>(async () => await task);
        }

        [Test]
        public void GetBundleGroupKeyUsesSingleAndGroupedKeys()
        {
            FwoNotification singleNotification = new() { Id = 17 };
            FwoNotification groupedNotification = new() { Id = 18, BundleType = BundleType.Attachments, BundleId = "bundle-42" };

            MethodInfo? getBundleGroupKey = typeof(NotificationService).GetMethod("GetBundleGroupKey", BindingFlags.Static | BindingFlags.NonPublic);
            ClassicAssert.IsNotNull(getBundleGroupKey);

            string singleKey = (string)(getBundleGroupKey?.Invoke(null, [singleNotification]) ?? throw new InvalidOperationException("GetBundleGroupKey returned null."));
            string groupedKey = (string)(getBundleGroupKey?.Invoke(null, [groupedNotification]) ?? throw new InvalidOperationException("GetBundleGroupKey returned null."));

            ClassicAssert.AreEqual("single:17", singleKey);
            ClassicAssert.AreEqual("Attachments:bundle-42", groupedKey);
        }

        [Test]
        public async Task CollectRecipientsSupportsJsonOtherAddressesAndConfiguredResponsibles()
        {
            NotificationServiceWithRecipientsApiConn recipientApi = new();
            List<UserGroup> ownerGroups = [];
            NotificationService notificationService = await NotificationService.CreateAsync(NotificationClient.InterfaceRequest, globalConfig, recipientApi, ownerGroups);
            FwoOwner owner = new() { Name = "Owner", ExtAppId = "1" };
            owner.AddOwnerResponsible(GlobalConst.kOwnerResponsibleTypeMain, "cn=main,dc=test");

            FwoNotification jsonNotification = new()
            {
                RecipientTo = EmailRecipientOption.OtherAddresses,
                EmailAddressTo = "{\"other_addresses\":true,\"other_address_list\":[\"json@example.test\"]}"
            };
            FwoNotification configuredNotification = new()
            {
                RecipientTo = EmailRecipientOption.ConfiguredResponsibles,
                EmailAddressTo = nameof(EmailRecipientOption.OwnerMainResponsible)
            };

            globalConfig.UseDummyEmailAddress = false;
            try
            {
                MethodInfo? collectRecipients = typeof(NotificationService).GetMethod("CollectRecipients", BindingFlags.Instance | BindingFlags.NonPublic);
                ClassicAssert.IsNotNull(collectRecipients);

                Task<List<string>> jsonTask = (Task<List<string>>)(collectRecipients?.Invoke(notificationService, [jsonNotification, owner, false, false])
                    ?? throw new InvalidOperationException("CollectRecipients returned null task."));
                Task<List<string>> configuredTask = (Task<List<string>>)(collectRecipients?.Invoke(notificationService, [configuredNotification, owner, false, false])
                    ?? throw new InvalidOperationException("CollectRecipients returned null task."));

                List<string> jsonRecipients = await jsonTask;
                List<string> configuredRecipients = await configuredTask;

                CollectionAssert.AreEqual(kJsonRecipients, jsonRecipients);
                CollectionAssert.AreEqual(kMainRecipients, configuredRecipients);
            }
            finally
            {
                globalConfig.UseDummyEmailAddress = true;
            }
        }

        [Test]
        public void TestDecommissionNotificationDueCalculation()
        {
            FwoOwner owner = new() { DecommDate = DateTime.Now.AddDays(-8) };
            FwoNotification notification = new()
            {
                Deadline = NotificationDeadline.DecommissionDate,
                RepeatIntervalAfterDeadline = SchedulerInterval.Days,
                RepeatOffsetAfterDeadline = 7,
                RepetitionsAfterDeadline = 2
            };

            ClassicAssert.IsTrue(NotificationService.IsNotificationDue(owner, null, notification));
            notification.LastSent = DateTime.Now.AddDays(-1);
            ClassicAssert.IsFalse(NotificationService.IsNotificationDue(owner, null, notification));
        }

        [Test]
        public void TestNotificationDeadlineIsAlwaysInPast()
        {
            ClassicAssert.IsTrue(NotificationDeadline.RequestDate.IsAlwaysInPast());
            ClassicAssert.IsTrue(NotificationDeadline.DecommissionDate.IsAlwaysInPast());
            ClassicAssert.IsFalse(NotificationDeadline.None.IsAlwaysInPast());
            ClassicAssert.IsFalse(NotificationDeadline.RecertDate.IsAlwaysInPast());
            ClassicAssert.IsFalse(NotificationDeadline.RuleExpiry.IsAlwaysInPast());
        }

        [Test]
        public void OfferedDeadlineOptions_ReturnsOnlyNone_ForImportChange()
        {
            CollectionAssert.AreEqual(kNoneDeadline, FwoNotification.OfferedDeadlineOptions(NotificationClient.ImportChange));
        }

        [Test]
        public void OfferedDeadlineOptions_ReturnsOnlyNone_ForWfAction()
        {
            CollectionAssert.AreEqual(kNoneDeadline, FwoNotification.OfferedDeadlineOptions(NotificationClient.WfAction));
        }

        [Test]
        public void NotificationClientGroups_ClassifiesWfActionAsWorkflowRecipientClient()
        {
            ClassicAssert.IsTrue(NotificationClient.WfAction.IsWorkflowRecipientClient());
            ClassicAssert.IsFalse(NotificationClient.WfAction.IsModellingRecipientClient());
        }

        [Test]
        public void GetNotificationText_UsesConfiguredNotificationLanguage_AndFallsBackToDefaultLanguage()
        {
            globalConfig.DefaultLanguage = "English";
            globalConfig.NotificationLanguage = "German";
            globalConfig.GermanTranslate["generated_on"] = "Erstellt am";
            globalConfig.DummyTranslate["generated_on"] = "Generated on";

            ClassicAssert.AreEqual("Erstellt am", globalConfig.GetNotificationText("generated_on"));

            globalConfig.NotificationLanguage = "";
            ClassicAssert.AreEqual("Generated on", globalConfig.GetNotificationText("generated_on"));
        }

        private class TestReport() : ReportBase(new DynGraphqlQuery(""), new SimulatedUserConfig(), Basics.ReportType.TicketReport)
        {
            public override Task Generate(int elementsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
            {
                return Task.CompletedTask;
            }

            public override string ExportToCsv()
            {
                return "csv";
            }

            public override string ExportToJson()
            {
                return "{\"a\":1}";
            }

            public override string ExportToHtml()
            {
                htmlBodyExport = "<p>report body</p>";
                htmlBodyExportValid = true;
                return "<html>report</html>";
            }

            public override string SetDescription()
            {
                return "";
            }
        }

        private sealed class NotificationServiceWithRecipientsApiConn : SimulatedApiConnection
        {
            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                if (typeof(QueryResponseType) == typeof(List<FwoNotification>) && query == NotificationQueries.getNotifications)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<FwoNotification>());
                }

                if (typeof(QueryResponseType) == typeof(List<OwnerResponsibleType>) && query == OwnerQueries.getOwnerResponsibleTypes)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<OwnerResponsibleType>
                    {
                        new() { Id = GlobalConst.kOwnerResponsibleTypeMain, Name = "Main", Active = true, SortOrder = 10 }
                    });
                }

                if (typeof(QueryResponseType) == typeof(List<UiUser>) && query == AuthQueries.getUserEmails)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<UiUser>
                    {
                        new() { Dn = "cn=main,dc=test", Email = "main@example.test" }
                    });
                }

                throw new NotImplementedException($"Query not implemented in notification service test api: {query}");
            }
        }
    }
}

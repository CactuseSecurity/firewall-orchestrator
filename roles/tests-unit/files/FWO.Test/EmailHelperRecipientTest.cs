using FWO.Api.Client;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class EmailHelperRecipientTest
    {
        private static EmailHelper CreateEmailHelper(List<UserGroup>? ownerGroups = null, bool useDummyEmailAddress = true,
            IWorkflowRecipientResolver? recipientResolver = null)
        {
            SimulatedUserConfig userConfig = new()
            {
                UseDummyEmailAddress = useDummyEmailAddress,
                DummyEmailAddress = "dummy@example.test"
            };
            return new EmailHelper(new SimulatedApiConnection(), null, userConfig, DefaultInit.DoNothing, ownerGroups, recipientResolver: recipientResolver);
        }

        [Test]
        public async Task GetRecipientsReturnsDummyForFallbackSelection()
        {
            EmailHelper helper = CreateEmailHelper();
            FwoOwner owner = new();
            owner.AddOwnerResponsible(GlobalConst.kOwnerResponsibleTypeSupporting, "cn=supporting,dc=test");
            owner.AddOwnerResponsible(GlobalConst.kOwnerResponsibleTypeMain, "cn=main,dc=test");

            List<string> recipients = await helper.GetRecipients(
                EmailRecipientOption.FallbackToMainResponsibleIfOwnerGroupEmpty,
                null,
                owner,
                null,
                null);

            Assert.That(recipients, Is.EqualTo(new[] { "dummy@example.test" }));
        }

        [Test]
        public async Task GetRecipientsReturnsDummyForOtherAddressesOption()
        {
            EmailHelper helper = CreateEmailHelper();
            List<string> recipients = await helper.GetRecipients(
                EmailRecipientOption.OtherAddresses,
                null,
                null,
                null,
                ["a@test", "b@test"]);

            Assert.That(recipients, Is.EqualTo(new[] { "dummy@example.test" }));
        }

        [Test]
        public async Task GetRecipientsReturnsOtherAddressesWhenDummyIsDisabled()
        {
            EmailHelper helper = CreateEmailHelper(useDummyEmailAddress: false);
            List<string> recipients = await helper.GetRecipients(
                EmailRecipientOption.OtherAddresses,
                null,
                null,
                null,
                ["a@test", "b@test"]);

            Assert.That(recipients, Is.EquivalentTo(new[] { "a@test", "b@test" }));
        }

        [Test]
        public async Task GetRecipientsReturnsDummyForJsonOtherAddressList()
        {
            EmailHelper helper = CreateEmailHelper();
            EmailRecipientSelection selection = new()
            {
                None = false,
                OtherAddresses = true,
                OtherAddressList = ["json-a@test", "json-b@test"]
            };

            List<string> recipients = await helper.GetRecipients(selection, null, ["legacy@test"]);

            Assert.That(recipients, Is.EqualTo(new[] { "dummy@example.test" }));
        }

        [Test]
        public async Task GetRecipientsReturnsDummyForJsonOtherAddressListFromConfigString()
        {
            EmailHelper helper = CreateEmailHelper();
            EmailRecipientSelection selection = new()
            {
                None = false,
                OtherAddresses = true,
                OtherAddressList = ["json-a@test", "json-b@test"]
            };

            List<string> recipients = await helper.GetRecipients(selection.ToConfigValue(), null, []);

            Assert.That(recipients, Is.EqualTo(new[] { "dummy@example.test" }));
        }

        [Test]
        public async Task GetRecipientsReturnsConfiguredResponsibleTypes()
        {
            EmailHelper helper = CreateEmailHelper();
            FwoOwner owner = new();
            owner.AddOwnerResponsible(3, "cn=escalation,dc=test");
            owner.AddOwnerResponsible(GlobalConst.kOwnerResponsibleTypeMain, "cn=main,dc=test");
            EmailRecipientSelection selection = new()
            {
                None = false,
                OwnerResponsibleTypeIds = [3]
            };

            List<string> recipients = await helper.GetRecipients(selection, owner, null);

            Assert.That(recipients, Is.EqualTo(new[] { "dummy@example.test" }));
        }

        [Test]
        public async Task GetRecipientsReturnsCurrentAndRecentHandlers()
        {
            EmailHelper helper = CreateEmailHelper();
            WfStatefulObject statefulObject = new()
            {
                CurrentHandler = new() { Dn = "cn=current,dc=test" },
                RecentHandler = new() { Dn = "cn=recent,dc=test" }
            };

            List<string> currentRecipients = await helper.GetRecipients(EmailRecipientOption.CurrentHandler, statefulObject, null, null, null);
            List<string> recentRecipients = await helper.GetRecipients(EmailRecipientOption.RecentHandler, statefulObject, null, null, null);

            Assert.That(currentRecipients, Is.EqualTo(new[] { "dummy@example.test" }));
            Assert.That(recentRecipients, Is.EqualTo(new[] { "dummy@example.test" }));
        }

        [Test]
        public async Task GetRecipientsUsesResolverForCurrentHandler()
        {
            EmailHelper helper = CreateEmailHelper(useDummyEmailAddress: false, recipientResolver: new TestWorkflowRecipientResolver(
                new() { Dn = "cn=current,dc=external", Email = "current@example.test" }));
            WfStatefulObject statefulObject = new()
            {
                CurrentHandler = new() { Dn = "cn=current,dc=external" }
            };

            List<string> recipients = await helper.GetRecipients(EmailRecipientOption.CurrentHandler, statefulObject, null, null, null);

            Assert.That(recipients, Is.EqualTo(new[] { "current@example.test" }));
        }

        [Test]
        public async Task GetRecipientsResolvesAssignedOwnerGroupUsers()
        {
            List<UserGroup> ownerGroups =
            [
                new()
                {
                    Dn = "cn=network-team,dc=test",
                    Users =
                    [
                        new() { Dn = "cn=alice,dc=test" },
                        new() { Dn = "cn=bob,dc=test" }
                    ]
                }
            ];
            EmailHelper helper = CreateEmailHelper(ownerGroups);
            WfStatefulObject statefulObject = new() { AssignedGroup = "cn=network-team,dc=test" };

            List<string> recipients = await helper.GetRecipients(EmailRecipientOption.AssignedGroup, statefulObject, null, null, null);

            Assert.That(recipients, Is.EqualTo(new[] { "dummy@example.test" }));
        }

        [Test]
        public void SplitAddressesReturnsTrimmedNonEmptyAddresses()
        {
            List<string> addresses = EmailHelper.SplitAddresses(" a@test ; b@test, c@test |  ");

            Assert.That(addresses, Is.EqualTo(new[] { "a@test", "b@test", "c@test" }));
        }

        [Test]
        public void CollectRecipientsFromConfigUsesDummyAddressWhenConfigured()
        {
            SimulatedUserConfig userConfig = new()
            {
                UseDummyEmailAddress = true,
                DummyEmailAddress = "dummy@example.test"
            };

            List<string> recipients = EmailHelper.CollectRecipientsFromConfig(userConfig, "a@test;b@test");

            Assert.That(recipients, Is.EqualTo(new[] { "dummy@example.test" }));
        }

        [Test]
        public void CollectRecipientsFromConfigSplitsConfiguredAddresses()
        {
            SimulatedUserConfig userConfig = new() { UseDummyEmailAddress = false };

            List<string> recipients = EmailHelper.CollectRecipientsFromConfig(userConfig, "a@test;b@test|c@test");

            Assert.That(recipients, Is.EqualTo(new[] { "a@test", "b@test", "c@test" }));
        }

        [Test]
        public async Task CreateAttachmentBuildsNamedUtf8Attachment()
        {
            FormFile? attachment = EmailHelper.CreateAttachment("body", GlobalConst.kJson, "Subject Line");

            Assert.That(attachment, Is.Not.Null);
            Assert.That(attachment!.ContentType, Is.EqualTo("application/json"));
            Assert.That(attachment.FileName, Does.StartWith("SubjectLine_"));
            Assert.That(attachment.FileName, Does.EndWith(".json"));
            Assert.That(await ReadFormFile(attachment), Is.EqualTo("body"));
        }

        [Test]
        public void EmailActionParamsCreatesActionNotificationWithoutDeadline()
        {
            EmailActionParams actionParams = new()
            {
                NotificationIds = [7, 9],
                AttachedContent = EmailAttachedContent.RequestedConnections,
                RecipientTo = EmailRecipientOption.CurrentHandler,
                RecipientCC = EmailRecipientOption.Requester,
                Subject = "subject",
                Body = "body"
            };

            FwoNotification notification = actionParams.ToNotification();

            Assert.That(notification.NotificationClient, Is.EqualTo(NotificationClient.WfAction));
            Assert.That(notification.Deadline, Is.EqualTo(NotificationDeadline.None));
            Assert.That(notification.RecipientTo, Is.EqualTo(EmailRecipientOption.CurrentHandler));
            Assert.That(notification.RecipientCc, Is.EqualTo(EmailRecipientOption.Requester));
            Assert.That(notification.EmailSubject, Is.EqualTo("subject"));
            Assert.That(notification.EmailBody, Is.EqualTo("body"));
            Assert.That(actionParams.NotificationIds, Is.EqualTo(new[] { 7, 9 }));
            Assert.That(actionParams.AttachedContent, Is.EqualTo(EmailAttachedContent.RequestedConnections));
        }

        [Test]
        public void EmailActionParamsSerializesAttachedContent()
        {
            EmailActionParams actionParams = new()
            {
                NotificationIds = [7],
                AttachedContent = EmailAttachedContent.RequestedConnections,
                ConfirmSentMail = true
            };

            string json = System.Text.Json.JsonSerializer.Serialize(actionParams);
            EmailActionParams? parsedParams = System.Text.Json.JsonSerializer.Deserialize<EmailActionParams>(json);

            Assert.That(json, Does.Contain("\"attached_content\":1"));
            Assert.That(json, Does.Contain("\"confirm_sent_mail\":true"));
            Assert.That(parsedParams?.AttachedContent, Is.EqualTo(EmailAttachedContent.RequestedConnections));
            Assert.That(parsedParams?.ConfirmSentMail, Is.True);
        }

        private static async Task<string> ReadFormFile(FormFile formFile)
        {
            using Stream stream = formFile.OpenReadStream();
            using StreamReader reader = new(stream);
            return await reader.ReadToEndAsync();
        }

        private class TestWorkflowRecipientResolver : IWorkflowRecipientResolver
        {
            private readonly UiUser user;

            public TestWorkflowRecipientResolver(UiUser user)
            {
                this.user = user;
            }

            public Task<List<string>> ResolveUserDns(IEnumerable<string> dns)
            {
                return Task.FromResult(dns.Contains(user.Dn, StringComparer.OrdinalIgnoreCase) ? new List<string> { user.Dn } : []);
            }

            public Task<List<UiUser>> ResolveUsers(IEnumerable<string> dns)
            {
                return Task.FromResult(dns.Contains(user.Dn, StringComparer.OrdinalIgnoreCase) ? new List<UiUser> { user } : []);
            }
        }
    }
}

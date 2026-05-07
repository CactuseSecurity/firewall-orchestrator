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
        private static EmailHelper CreateEmailHelper(List<UserGroup>? ownerGroups = null, bool useDummyEmailAddress = true)
        {
            SimulatedUserConfig userConfig = new()
            {
                UseDummyEmailAddress = useDummyEmailAddress,
                DummyEmailAddress = "dummy@example.test"
            };
            return new EmailHelper(new SimulatedApiConnection(), null, userConfig, DefaultInit.DoNothing, ownerGroups);
        }

        [Test]
        public async Task GetRecipientsReturnsOwnerGroupAndMainForFallbackSelection()
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

            Assert.That(recipients, Has.Count.EqualTo(2));
            Assert.That(recipients, Is.All.EqualTo("dummy@example.test"));
        }

        [Test]
        public async Task GetRecipientsReturnsOtherAddressesForOtherAddressesOption()
        {
            EmailHelper helper = CreateEmailHelper();
            List<string> recipients = await helper.GetRecipients(
                EmailRecipientOption.OtherAddresses,
                null,
                null,
                null,
                ["a@test", "b@test"]);

            Assert.That(recipients, Is.EquivalentTo(new[] { "a@test", "b@test" }));
        }

        [Test]
        public async Task GetRecipientsReturnsJsonOtherAddressList()
        {
            EmailHelper helper = CreateEmailHelper();
            EmailRecipientSelection selection = new()
            {
                None = false,
                OtherAddresses = true,
                OtherAddressList = ["json-a@test", "json-b@test"]
            };

            List<string> recipients = await helper.GetRecipients(selection, null, ["legacy@test"]);

            Assert.That(recipients, Is.EquivalentTo(new[] { "json-a@test", "json-b@test", "legacy@test" }));
        }

        [Test]
        public async Task GetRecipientsReturnsJsonOtherAddressListFromConfigStringWithoutLegacyAddresses()
        {
            EmailHelper helper = CreateEmailHelper();
            EmailRecipientSelection selection = new()
            {
                None = false,
                OtherAddresses = true,
                OtherAddressList = ["json-a@test", "json-b@test"]
            };

            List<string> recipients = await helper.GetRecipients(selection.ToConfigValue(), null, []);

            Assert.That(recipients, Is.EquivalentTo(new[] { "json-a@test", "json-b@test" }));
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

            Assert.That(recipients, Has.Count.EqualTo(1));
            Assert.That(recipients[0], Is.EqualTo("dummy@example.test"));
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

            Assert.That(recipients, Has.Count.EqualTo(2));
            Assert.That(recipients, Is.All.EqualTo("dummy@example.test"));
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
                AttachedContent = EmailAttachedContent.RequestedConnections
            };

            string json = System.Text.Json.JsonSerializer.Serialize(actionParams);
            EmailActionParams? parsedParams = System.Text.Json.JsonSerializer.Deserialize<EmailActionParams>(json);

            Assert.That(json, Does.Contain("\"attached_content\":1"));
            Assert.That(parsedParams?.AttachedContent, Is.EqualTo(EmailAttachedContent.RequestedConnections));
        }

        private static async Task<string> ReadFormFile(FormFile formFile)
        {
            using Stream stream = formFile.OpenReadStream();
            using StreamReader reader = new(stream);
            return await reader.ReadToEndAsync();
        }
    }
}

using FWO.Api.Client;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    public class EmailHelperRecipientTest
    {
        private static readonly string[] kDummyRecipients = ["dummy@example.test"];
        private static readonly string[] kOtherRecipients = ["a@test", "b@test"];
        private static readonly string[] kMainRecipients = ["main@example.test"];
        private static readonly string[] kSupportRecipients = ["support@example.test"];
        private static readonly string[] kOtherPlusExtraRecipients = ["other@test", "dup@test", "extra@test"];
        private static readonly string[] kCurrentRecipients = ["current@example.test"];
        private static readonly string[] kKnownRecipients = ["known@example.test"];
        private static readonly string[] kOwnerRecipients = ["owner@example.test"];
        private static readonly string[] kSplitRecipients = ["a@test", "b@test", "c@test"];
        private static readonly string[] kResolvedDns = ["cn=alice,dc=test", "cn=bob,dc=test", "cn=external,dc=test"];
        private static readonly string[] kOverrideRecipients = ["override@example.test"];
        private static readonly string[] kScopedRecipients = ["scoped@example.test"];
        private static readonly List<string> kOtherAddressRecipients = ["a@test", "b@test"];
        private static readonly List<string> kJsonOtherAddressList = ["json-a@test", "json-b@test"];
        private static readonly List<string> kLegacyRecipients = ["legacy@test"];
        private static readonly List<string> kEmptyRecipients = [];
        private static readonly List<string> kDupExtraRecipients = ["dup@test", "extra@test"];
        private static readonly List<string> kSupportAndMainRecipients = ["support@example.test", "main@example.test"];
        private static readonly List<int> kActiveOwnerResponsibleIds = [7, 11];
        private static readonly int[] kNotificationIds = [7, 9];
        private static readonly string[] kResolverDns = ["cn=existing,dc=test", "cn=fresh,dc=test"];
        private static readonly string[] kOwnerGroupDns = ["cn=network-team,dc=test", "cn=external,dc=test"];
        private static readonly string[] kResolvedRecipients = ["new@example.test", "fresh@example.test"];

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

            Assert.That(recipients, Is.EqualTo(kDummyRecipients));
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
                kOtherAddressRecipients);

            Assert.That(recipients, Is.EqualTo(kDummyRecipients));
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
                kOtherAddressRecipients);

            Assert.That(recipients, Is.EquivalentTo(kOtherRecipients));
        }

        [Test]
        public async Task GetRecipientsReturnsDummyForJsonOtherAddressList()
        {
            EmailHelper helper = CreateEmailHelper();
            EmailRecipientSelection selection = new()
            {
                None = false,
                OtherAddresses = true,
                OtherAddressList = kJsonOtherAddressList
            };

            List<string> recipients = await helper.GetRecipients(selection, null, kLegacyRecipients);

            Assert.That(recipients, Is.EqualTo(kDummyRecipients));
        }

        [Test]
        public async Task GetRecipientsReturnsDummyForJsonOtherAddressListFromConfigString()
        {
            EmailHelper helper = CreateEmailHelper();
            EmailRecipientSelection selection = new()
            {
                None = false,
                OtherAddresses = true,
                OtherAddressList = kJsonOtherAddressList
            };

            List<string> recipients = await helper.GetRecipients(selection.ToConfigValue(), null, kEmptyRecipients);

            Assert.That(recipients, Is.EqualTo(kDummyRecipients));
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

            Assert.That(recipients, Is.EqualTo(kDummyRecipients));
        }

        [Test]
        public async Task GetRecipientsReturnsEmptyForConfiguredResponsiblesWithoutDummy()
        {
            EmailHelper helper = CreateEmailHelper(useDummyEmailAddress: false);

            List<string> recipients = await helper.GetRecipients(EmailRecipientOption.ConfiguredResponsibles, null, null, null, null);

            Assert.That(recipients, Is.Empty);
        }

        [Test]
        public async Task GetRecipientsReturnsEmptyWhenSelectionHasNoRecipients()
        {
            EmailHelper helper = CreateEmailHelper(useDummyEmailAddress: false);
            EmailRecipientSelection selection = new();

            List<string> recipients = await helper.GetRecipients(selection, null, null);

            Assert.That(recipients, Is.Empty);
        }

        [Test]
        public async Task InitLoadsUsersAndOwnerTypesForLegacyRecipientParsing()
        {
            FakeApiConnection apiConnection = new()
            {
                OwnerResponsibleTypes =
                [
                    new OwnerResponsibleType { Id = GlobalConst.kOwnerResponsibleTypeMain, Active = true, SortOrder = 10 },
                    new OwnerResponsibleType { Id = GlobalConst.kOwnerResponsibleTypeSupporting, Active = true, SortOrder = 5 }
                ],
                Users =
                [
                    new UiUser { Dn = "cn=main,dc=test", Email = "main@example.test" }
                ]
            };
            EmailHelper helper = new(apiConnection, null, new SimulatedUserConfig { UseDummyEmailAddress = false }, DefaultInit.DoNothing);
            FwoOwner owner = new();
            owner.AddOwnerResponsible(GlobalConst.kOwnerResponsibleTypeMain, "cn=main,dc=test");

            await helper.Init();

            List<string> recipients = await helper.GetRecipients(nameof(EmailRecipientOption.OwnerMainResponsible), owner, null);

            Assert.That(recipients, Is.EqualTo(kMainRecipients));
        }

        [Test]
        public async Task GetRecipientsSelectionUsesOtherAddressesAndFallbackWhenNeeded()
        {
            EmailHelper helper = CreateEmailHelper(useDummyEmailAddress: false);
            SetPrivateField(helper, "ownerResponsibleTypes", new List<OwnerResponsibleType>
            {
                new() { Id = GlobalConst.kOwnerResponsibleTypeSupporting, Active = true, SortOrder = 20 },
                new() { Id = GlobalConst.kOwnerResponsibleTypeMain, Active = true, SortOrder = 10 }
            });
            SetPrivateField(helper, "uiUsers", new List<UiUser>
            {
                new() { Dn = "cn=support,dc=test", Email = "support@example.test" },
                new() { Dn = "cn=main,dc=test", Email = "main@example.test" }
            });

            FwoOwner owner = new();
            owner.AddOwnerResponsible(GlobalConst.kOwnerResponsibleTypeSupporting, "cn=support,dc=test");
            owner.AddOwnerResponsible(GlobalConst.kOwnerResponsibleTypeMain, "cn=main,dc=test");
            EmailRecipientSelection selection = new()
            {
                OtherAddresses = true,
                OtherAddressList = new List<string> { "other@test", "", "dup@test" },
                EnsureAtLeastOneNotification = true,
                OwnerResponsibleTypeIds = [99]
            };

            List<string> recipients = await helper.GetRecipients(selection, owner, kDupExtraRecipients);

            Assert.That(recipients, Is.EquivalentTo(kOtherPlusExtraRecipients));

            EmailRecipientSelection fallbackSelection = new()
            {
                EnsureAtLeastOneNotification = true,
                OwnerResponsibleTypeIds = [99]
            };

            List<string> fallbackRecipients = await helper.GetRecipients(fallbackSelection, owner, null);

            Assert.That(fallbackRecipients, Is.EqualTo(kSupportRecipients));
        }

        [Test]
        public async Task GetRecipientsScopedUserPrefersExplicitEmailAndFallsBackToDnResolution()
        {
            EmailHelper helper = CreateEmailHelper(useDummyEmailAddress: false);
            SetPrivateField(helper, "uiUsers", new List<UiUser>
            {
                new() { Dn = "cn=scoped,dc=test", Email = "scoped@example.test" }
            });

            List<string> explicitRecipients = await helper.GetRecipients(
                EmailRecipientOption.Requester,
                null,
                null,
                "cn=scoped,dc=test",
                null,
                "override@example.test");

            List<string> fallbackRecipients = await helper.GetRecipients(
                EmailRecipientOption.Requester,
                null,
                null,
                "cn=scoped,dc=test",
                null,
                null);

            Assert.That(explicitRecipients, Is.EqualTo(kOverrideRecipients));
            Assert.That(fallbackRecipients, Is.EqualTo(kScopedRecipients));
        }

        [Test]
        public async Task CollectEmailAddressesFromResolverUpdatesExistingUsersAndReturnsResolvedAddresses()
        {
            UiUser existingUser = new() { Dn = "cn=existing,dc=test", Email = "old@example.test" };
            UiUser resolvedUser = new() { Dn = "cn=existing,dc=test", Email = "new@example.test" };
            UiUser freshUser = new() { Dn = "cn=fresh,dc=test", Email = "fresh@example.test" };
            EmailHelper helper = CreateEmailHelper(useDummyEmailAddress: false, recipientResolver: new StaticWorkflowRecipientResolver([resolvedUser, freshUser]));
            SetPrivateField(helper, "uiUsers", new List<UiUser> { existingUser });

            List<string> recipients = await InvokePrivateAsync<List<string>>(helper, "CollectEmailAddressesFromResolver", new object?[] { kResolverDns });
            List<UiUser> uiUsers = GetPrivateField<List<UiUser>>(helper, "uiUsers");

            Assert.That(recipients, Is.EqualTo(kResolvedRecipients));
            Assert.That(uiUsers.Single(user => user.Dn == existingUser.Dn).Email, Is.EqualTo("new@example.test"));
            Assert.That(uiUsers.Any(user => user.Dn == freshUser.Dn && user.Email == "fresh@example.test"), Is.True);
        }

        [Test]
        public void ResolveUserDnsFromOwnerGroupsExpandsMembersAndKeepsUnmatchedDns()
        {
            EmailHelper helper = CreateEmailHelper(
                ownerGroups:
                [
                    new UserGroup
                    {
                        Dn = "cn=network-team,dc=test",
                        Users =
                        [
                            new UiUser { Dn = "cn=alice,dc=test" },
                            new UiUser { Dn = "cn=bob,dc=test" }
                        ]
                    }
                ],
                useDummyEmailAddress: false);

            List<string> resolvedDns = InvokePrivate<List<string>>(helper, "ResolveUserDnsFromOwnerGroups", new object?[] { kOwnerGroupDns.ToList() });

            Assert.That(resolvedDns, Is.EquivalentTo(kResolvedDns));
        }

        [Test]
        public void GetEmailAddressReturnsResolvedEmailOrEmptyString()
        {
            EmailHelper helper = CreateEmailHelper(useDummyEmailAddress: false);
            SetPrivateField(helper, "uiUsers", new List<UiUser>
            {
                new() { Dn = "cn=known,dc=test", Email = "known@example.test" }
            });

            string resolvedEmail = InvokePrivate<string>(helper, "GetEmailAddress", new object?[] { "cn=known,dc=test" });
            string missingEmail = InvokePrivate<string>(helper, "GetEmailAddress", new object?[] { "cn=missing,dc=test" });

            Assert.That(resolvedEmail, Is.EqualTo(kKnownRecipients[0]));
            Assert.That(missingEmail, Is.EqualTo(""));
        }

        [Test]
        public void GetOwnerMainResponsibleRecipientsSkipsMissingDns()
        {
            EmailHelper helper = CreateEmailHelper(useDummyEmailAddress: false);
            SetPrivateField(helper, "uiUsers", new List<UiUser>
            {
                new() { Dn = "cn=owner,dc=test", Email = "owner@example.test" }
            });
            List<UserGroup> owners =
            [
                new() { Dn = "", Users = [new UiUser { Dn = "cn=ignored,dc=test" }] },
                new() { Dn = "cn=owner,dc=test" }
            ];

            List<string> recipients = helper.GetOwnerMainResponsibleRecipients(owners);

            Assert.That(recipients, Is.EqualTo(kOwnerRecipients));
        }

        [Test]
        public void AssignedGroupRecipientContextKeepsStatefulObjectUnlessGroupIsExplicit()
        {
            WfStatefulObject statefulObject = new()
            {
                AssignedGroup = "cn=original,dc=test"
            };

            WfStatefulObject keptContext = InvokePrivateStatic<WfStatefulObject>("AssignedGroupRecipientContext", new object?[] { EmailRecipientOption.CurrentHandler, statefulObject, "cn=override,dc=test" });
            WfStatefulObject overriddenContext = InvokePrivateStatic<WfStatefulObject>("AssignedGroupRecipientContext", new object?[] { EmailRecipientOption.AssignedGroup, statefulObject, "cn=override,dc=test" });

            Assert.That(keptContext.AssignedGroup, Is.EqualTo("cn=original,dc=test"));
            Assert.That(overriddenContext.AssignedGroup, Is.EqualTo("cn=override,dc=test"));
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

            Assert.That(currentRecipients, Is.EqualTo(kDummyRecipients));
            Assert.That(recentRecipients, Is.EqualTo(kDummyRecipients));
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

            Assert.That(recipients, Is.EqualTo(kCurrentRecipients));
        }

        [Test]
        public async Task GetRecipientsCurrentHandlerUsesDirectEmailAndDnsFallback()
        {
            EmailHelper helper = CreateEmailHelper(useDummyEmailAddress: false);
            SetPrivateField(helper, "uiUsers", new List<UiUser>
            {
                new() { Dn = "cn=current,dc=test", Email = "current@example.test" }
            });

            WfStatefulObject directStatefulObject = new()
            {
                CurrentHandler = new() { Dn = "cn=current,dc=test", Email = "current@example.test" }
            };
            WfStatefulObject fallbackStatefulObject = new()
            {
                CurrentHandler = new() { Dn = "cn=current,dc=test" }
            };

            List<string> directRecipients = await helper.GetRecipients(EmailRecipientOption.CurrentHandler, directStatefulObject, null, null, null);
            List<string> fallbackRecipients = await helper.GetRecipients(EmailRecipientOption.CurrentHandler, fallbackStatefulObject, null, null, null);

            Assert.That(directRecipients, Is.EqualTo(kCurrentRecipients));
            Assert.That(fallbackRecipients, Is.EqualTo(kCurrentRecipients));
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

            Assert.That(recipients, Is.EqualTo(kDummyRecipients));
        }

        [Test]
        public void SplitAddressesReturnsTrimmedNonEmptyAddresses()
        {
            List<string> addresses = EmailHelper.SplitAddresses(" a@test ; b@test, c@test |  ");

            Assert.That(addresses, Is.EqualTo(kSplitRecipients));
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

            Assert.That(recipients, Is.EqualTo(kDummyRecipients));
        }

        [Test]
        public void CollectRecipientsFromConfigSplitsConfiguredAddresses()
        {
            SimulatedUserConfig userConfig = new() { UseDummyEmailAddress = false };

            List<string> recipients = EmailHelper.CollectRecipientsFromConfig(userConfig, "a@test;b@test|c@test");

            Assert.That(recipients, Is.EqualTo(kSplitRecipients));
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
        public async Task CreateAttachmentBuildsHtmlAndPdfAttachmentsAndReturnsNullForMissingContent()
        {
            FormFile? htmlAttachment = EmailHelper.CreateAttachment("<p>body</p>", GlobalConst.kHtml, "Subject Line");
            FormFile? pdfAttachment = EmailHelper.CreateAttachment(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("pdf")), GlobalConst.kPdf, "Subject Line");
            FormFile? nullAttachment = EmailHelper.CreateAttachment(null, GlobalConst.kHtml, "Subject Line");

            Assert.That(htmlAttachment, Is.Not.Null);
            Assert.That(htmlAttachment!.ContentType, Is.EqualTo("application/html"));
            Assert.That(await ReadFormFile(htmlAttachment), Is.EqualTo("<p>body</p>"));
            Assert.That(pdfAttachment, Is.Not.Null);
            Assert.That(pdfAttachment!.ContentType, Is.EqualTo("application/octet-stream"));
            Assert.That(await ReadFormFile(pdfAttachment), Is.EqualTo("pdf"));
            Assert.That(nullAttachment, Is.Null);
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
            Assert.That(actionParams.NotificationIds, Is.EqualTo(kNotificationIds));
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

        [Test]
        public void SplitAddressesReturnsEmptyForNullOrWhitespace()
        {
            Assert.That(EmailHelper.SplitAddresses(null), Is.Empty);
            Assert.That(EmailHelper.SplitAddresses("   "), Is.Empty);
        }

        [Test]
        public void CollectRecipientsFromConfigReturnsEmptyForWhitespaceWithoutDummy()
        {
            SimulatedUserConfig userConfig = new() { UseDummyEmailAddress = false };

            List<string> recipients = EmailHelper.CollectRecipientsFromConfig(userConfig, "   ");

            Assert.That(recipients, Is.Empty);
        }

        [Test]
        public void GetEmailAddressReturnsDummyWhenConfigured()
        {
            EmailHelper helper = CreateEmailHelper();

            string emailAddress = InvokePrivate<string>(helper, "GetEmailAddress", new object?[] { "cn=missing,dc=test" });

            Assert.That(emailAddress, Is.EqualTo(kDummyRecipients[0]));
        }

        [Test]
        public async Task CollectEmailAddressesFromScopedUserReturnsDummyAndEmailFallbacks()
        {
            EmailHelper dummyHelper = CreateEmailHelper();
            EmailHelper helper = CreateEmailHelper(useDummyEmailAddress: false);
            SetPrivateField(helper, "uiUsers", new List<UiUser>
            {
                new() { Dn = "cn=scoped,dc=test", Email = "scoped@example.test" }
            });

            List<string> dummyRecipients = await InvokePrivateAsync<List<string>>(dummyHelper, "CollectEmailAddressesFromScopedUser", new object?[] { "cn=scoped,dc=test", "scoped@example.test" });
            List<string> explicitRecipients = await InvokePrivateAsync<List<string>>(helper, "CollectEmailAddressesFromScopedUser", new object?[] { "cn=scoped,dc=test", "scoped@example.test" });
            List<string> fallbackRecipients = await InvokePrivateAsync<List<string>>(helper, "CollectEmailAddressesFromScopedUser", new object?[] { "cn=scoped,dc=test", null });

            Assert.That(dummyRecipients, Is.EqualTo(kDummyRecipients));
            Assert.That(explicitRecipients, Is.EqualTo(kScopedRecipients));
            Assert.That(fallbackRecipients, Is.EqualTo(kScopedRecipients));
        }

        [Test]
        public async Task CollectEmailAddressesFromUserReturnsEmptyForMissingDn()
        {
            EmailHelper helper = CreateEmailHelper(useDummyEmailAddress: false);

            List<string> nullUserRecipients = await InvokePrivateAsync<List<string>>(helper, "CollectEmailAddressesFromUser", new object?[] { null });
            List<string> emptyDnRecipients = await InvokePrivateAsync<List<string>>(helper, "CollectEmailAddressesFromUser", new object?[] { new UiUser { Dn = "" } });

            Assert.That(nullUserRecipients, Is.Empty);
            Assert.That(emptyDnRecipients, Is.Empty);
        }

        [Test]
        public async Task CollectEmailAddressesFromDnsReturnsEmptyForBlankInput()
        {
            EmailHelper helper = CreateEmailHelper(useDummyEmailAddress: false);

            List<string> nullRecipients = await InvokePrivateAsync<List<string>>(helper, "CollectEmailAddressesFromDns", new object?[] { null });
            List<string> whitespaceRecipients = await InvokePrivateAsync<List<string>>(helper, "CollectEmailAddressesFromDns", new object?[] { new List<string> { " ", "\t" } });

            Assert.That(nullRecipients, Is.Empty);
            Assert.That(whitespaceRecipients, Is.Empty);
        }

        [Test]
        public async Task GetOwnerGroupOrMainResponsibleRecipientsReturnsGroupAndMainAddresses()
        {
            EmailHelper helper = CreateEmailHelper(useDummyEmailAddress: false);
            SetPrivateField(helper, "uiUsers", new List<UiUser>
            {
                new() { Dn = "cn=support,dc=test", Email = "support@example.test" },
                new() { Dn = "cn=main,dc=test", Email = "main@example.test" }
            });
            FwoOwner owner = new();
            owner.AddOwnerResponsible(GlobalConst.kOwnerResponsibleTypeSupporting, "cn=support,dc=test");
            owner.AddOwnerResponsible(GlobalConst.kOwnerResponsibleTypeMain, "cn=main,dc=test");

            List<string> recipients = await InvokePrivateAsync<List<string>>(helper, "GetOwnerGroupOrMainResponsibleRecipients", new object?[] { owner });

            Assert.That(recipients, Is.EqualTo(kSupportAndMainRecipients));
        }

        [Test]
        public void GetActiveOwnerResponsibleTypeIdsReturnsOnlyActiveIds()
        {
            EmailHelper helper = CreateEmailHelper(useDummyEmailAddress: false);
            SetPrivateField(helper, "ownerResponsibleTypes", new List<OwnerResponsibleType>
            {
                new() { Id = 3, Active = false },
                new() { Id = 7, Active = true },
                new() { Id = 11, Active = true }
            });

            List<int> activeIds = InvokePrivate<List<int>>(helper, "GetActiveOwnerResponsibleTypeIds", Array.Empty<object?>());

            Assert.That(activeIds, Is.EqualTo(kActiveOwnerResponsibleIds));
        }

        [Test]
        public void ApplyDummyRecipientOverrideReplacesRecipientsAndClearsCopies()
        {
            EmailHelper helper = CreateEmailHelper();
            object?[] args =
            {
                new List<string> { "real@example.test" },
                new List<string> { "cc@example.test" },
                new List<string> { "bcc@example.test" }
            };

            InvokePrivateVoid(helper, "ApplyDummyRecipientOverride", args);

            Assert.That((List<string>)args[0]!, Is.EqualTo(kDummyRecipients));
            Assert.That((List<string>)args[1]!, Is.Empty);
            Assert.That((List<string>)args[2]!, Is.Empty);
        }

        private static void SetPrivateField<T>(EmailHelper helper, string fieldName, T value)
        {
            FieldInfo field = typeof(EmailHelper).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"Field '{fieldName}' not found.");
            field.SetValue(helper, value);
        }

        private static T GetPrivateField<T>(EmailHelper helper, string fieldName)
        {
            FieldInfo field = typeof(EmailHelper).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"Field '{fieldName}' not found.");
            return (T)field.GetValue(helper)!;
        }

        private static T InvokePrivate<T>(EmailHelper helper, string methodName, object?[] args)
        {
            MethodInfo method = typeof(EmailHelper).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"Method '{methodName}' not found.");
            return (T)method.Invoke(helper, args)!;
        }

        private static void InvokePrivateVoid(EmailHelper helper, string methodName, object?[] args)
        {
            MethodInfo method = typeof(EmailHelper).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"Method '{methodName}' not found.");
            method.Invoke(helper, args);
        }

        private static async Task<T> InvokePrivateAsync<T>(EmailHelper helper, string methodName, object?[] args)
        {
            MethodInfo method = typeof(EmailHelper).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"Method '{methodName}' not found.");
            object? result = method.Invoke(helper, args);
            if (result is not Task<T> typedTask)
            {
                throw new InvalidOperationException($"Method '{methodName}' returned unexpected task type.");
            }
            return await typedTask;
        }

        private static T InvokePrivateStatic<T>(string methodName, object?[] args)
        {
            MethodInfo method = typeof(EmailHelper).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"Method '{methodName}' not found.");
            return (T)method.Invoke(null, args)!;
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

        private sealed class StaticWorkflowRecipientResolver : IWorkflowRecipientResolver
        {
            private readonly List<UiUser> users;

            public StaticWorkflowRecipientResolver(IEnumerable<UiUser> users)
            {
                this.users = users.ToList();
            }

            public Task<List<string>> ResolveUserDns(IEnumerable<string> dns)
            {
                return Task.FromResult(dns.ToList());
            }

            public Task<List<UiUser>> ResolveUsers(IEnumerable<string> dns)
            {
                return Task.FromResult(users.Where(user => dns.Contains(user.Dn, StringComparer.OrdinalIgnoreCase)).ToList());
            }
        }

        private sealed class FakeApiConnection : SimulatedApiConnection
        {
            public List<OwnerResponsibleType> OwnerResponsibleTypes { get; init; } = [];
            public List<UiUser> Users { get; init; } = [];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                if (typeof(QueryResponseType) == typeof(List<OwnerResponsibleType>))
                {
                    return Task.FromResult((QueryResponseType)(object)OwnerResponsibleTypes);
                }

                if (typeof(QueryResponseType) == typeof(List<UiUser>))
                {
                    return Task.FromResult((QueryResponseType)(object)Users);
                }

                throw new NotSupportedException($"Unexpected query type {typeof(QueryResponseType).Name}.");
            }
        }
    }
}

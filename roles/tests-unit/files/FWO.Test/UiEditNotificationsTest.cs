using FWO.Config.Api;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Ui.Shared;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    internal class UiEditNotificationsTest
    {
        private static MethodInfo GetPrivateMethod(string name)
        {
            return typeof(EditNotifications).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(EditNotifications).FullName, name);
        }

        private static void SetPrivateField<T>(EditNotifications component, string fieldName, T value)
        {
            FieldInfo? field = typeof(EditNotifications).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(typeof(EditNotifications).FullName, fieldName);
            }

            field.SetValue(component, value);
        }

        private static void SetPrivateMember(EditNotifications component, string memberName, object? value)
        {
            FieldInfo? field = typeof(EditNotifications).GetField(memberName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(component, value);
                return;
            }

            PropertyInfo? property = typeof(EditNotifications).GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
            {
                property.SetValue(component, value);
                return;
            }

            throw new MissingMemberException(typeof(EditNotifications).FullName, memberName);
        }

        private static T GetPrivateField<T>(EditNotifications component, string fieldName)
        {
            FieldInfo? field = typeof(EditNotifications).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(typeof(EditNotifications).FullName, fieldName);
            }

            return (T)field.GetValue(component)!;
        }

        private static void SetInjectedUserConfig(EditNotifications component, UserConfig userConfig)
        {
            PropertyInfo? prop = typeof(EditNotifications).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(p => p.PropertyType == typeof(UserConfig));
            if (prop == null)
            {
                throw new MissingMemberException(typeof(EditNotifications).FullName, "userConfig");
            }

            prop.SetValue(component, userConfig);
        }

        private static void EnsureNotificationTranslations()
        {
        }

        private static void SetClient(EditNotifications component, NotificationClient client)
        {
            PropertyInfo? prop = typeof(EditNotifications).GetProperty(nameof(EditNotifications.Client), BindingFlags.Public | BindingFlags.Instance);
            if (prop == null)
            {
                throw new MissingMemberException(typeof(EditNotifications).FullName, nameof(EditNotifications.Client));
            }

            prop.SetValue(component, client);
        }

        [Test]
        public void InitActNotification_ForRuleTimer_SetsExpectedDefaults()
        {
            EditNotifications component = new();
            SetClient(component, NotificationClient.RuleTimer);

            GetPrivateMethod("InitActNotification").Invoke(component, null);
            FwoNotification actNotification = GetPrivateField<FwoNotification>(component, "actNotification");

            Assert.That(actNotification.NotificationClient, Is.EqualTo(NotificationClient.RuleTimer));
            Assert.That(actNotification.RecipientTo, Is.EqualTo(EmailRecipientOption.None));
            Assert.That(actNotification.RecipientCc, Is.EqualTo(EmailRecipientOption.None));
            Assert.That(actNotification.Layout, Is.EqualTo(NotificationLayout.HtmlInBody));
            Assert.That(actNotification.Deadline, Is.EqualTo(FwoNotification.OfferedDeadlineOptions(NotificationClient.RuleTimer).Single()));
            Assert.That(actNotification.Logging, Is.EqualTo(NotificationLoggingMode.SendOnly));
        }

        [Test]
        public void InitActNotification_ForRuleTimerWithConfiguredResponsibles_UsesConfiguredResponsibles()
        {
            EditNotifications component = new();
            SetClient(component, NotificationClient.RuleTimer);
            SetPrivateField(component, "activeOwnerResponsibleTypes", new List<OwnerResponsibleType>
            {
                new() { Id = 1, Name = "Main", Active = true, SortOrder = 10 }
            });

            GetPrivateMethod("InitActNotification").Invoke(component, null);
            EmailRecipientSelection selection = GetPrivateField<EmailRecipientSelection>(component, "ToRecipientSelection");

            Assert.That(selection.OwnerResponsibleTypeIds, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void InitActNotification_ForImportChangeWithConfiguredResponsibles_UsesOtherAddresses()
        {
            EditNotifications component = new();
            SetClient(component, NotificationClient.ImportChange);
            SetPrivateField(component, "activeOwnerResponsibleTypes", new List<OwnerResponsibleType>
            {
                new() { Id = 1, Name = "Main", Active = true, SortOrder = 10 }
            });

            GetPrivateMethod("InitActNotification").Invoke(component, null);
            EmailRecipientSelection selection = GetPrivateField<EmailRecipientSelection>(component, "ToRecipientSelection");

            Assert.That(selection.OtherAddresses, Is.True);
        }

        [Test]
        public void SyncAddresses_WritesOtherAddressSelections()
        {
            EditNotifications component = new();
            SetClient(component, NotificationClient.RuleTimer);
            FwoNotification notification = new();

            SetPrivateField(component, "actNotification", notification);
            SetPrivateField(component, "ToRecipientSelection", new EmailRecipientSelection
            {
                None = false,
                OtherAddresses = true,
                OtherAddressList = ["to-keep@example.org", "to-add@example.org"]
            });
            SetPrivateField(component, "CcRecipientSelection", new EmailRecipientSelection
            {
                None = false,
                OtherAddresses = true,
                OtherAddressList = ["cc-add-1@example.org", "cc-add-2@example.org"]
            });

            GetPrivateMethod("SyncAddresses").Invoke(component, null);

            EmailRecipientSelection toSelection = EmailRecipientSelection.Parse(notification.EmailAddressTo);
            EmailRecipientSelection ccSelection = EmailRecipientSelection.Parse(notification.EmailAddressCc);
            Assert.That(notification.RecipientTo, Is.EqualTo(EmailRecipientOption.OtherAddresses));
            Assert.That(notification.RecipientCc, Is.EqualTo(EmailRecipientOption.OtherAddresses));
            Assert.That(toSelection.OtherAddressList, Is.EqualTo(new[] { "to-keep@example.org", "to-add@example.org" }));
            Assert.That(ccSelection.OtherAddressList, Is.EqualTo(new[] { "cc-add-1@example.org", "cc-add-2@example.org" }));
        }

        [Test]
        public void SyncAddresses_WritesOtherAddressSelectionJson()
        {
            EditNotifications component = new();
            SetClient(component, NotificationClient.RuleTimer);
            FwoNotification notification = new()
            {
                RecipientTo = EmailRecipientOption.OtherAddresses
            };

            SetPrivateField(component, "actNotification", notification);
            SetPrivateField(component, "ToRecipientSelection", new EmailRecipientSelection
            {
                None = false,
                OtherAddresses = true,
                OtherAddressList = ["json@example.org"]
            });

            GetPrivateMethod("SyncAddresses").Invoke(component, null);

            EmailRecipientSelection selection = EmailRecipientSelection.Parse(notification.EmailAddressTo);
            Assert.That(selection.OtherAddressList, Is.EqualTo(new[] { "json@example.org" }));
        }

        [Test]
        public void SyncAddresses_IgnoresRetainedAddressesWhenOtherAddressesIsUnchecked()
        {
            EditNotifications component = new();
            SetClient(component, NotificationClient.RuleTimer);
            FwoNotification notification = new();

            SetPrivateField(component, "actNotification", notification);
            SetPrivateField(component, "ToRecipientSelection", new EmailRecipientSelection
            {
                None = true,
                OtherAddresses = false,
                OtherAddressList = ["retained@example.org"]
            });

            GetPrivateMethod("SyncAddresses").Invoke(component, null);

            Assert.That(notification.RecipientTo, Is.EqualTo(EmailRecipientOption.None));
            Assert.That(notification.EmailAddressTo, Is.Empty);
        }

        [Test]
        public void SyncAddresses_ForDirectAddressClient_InfersRecipientOptionsFromAddressFields()
        {
            EditNotifications component = new();
            SetClient(component, NotificationClient.ImportChange);
            FwoNotification notification = new()
            {
                EmailAddressTo = "to@example.org",
                EmailAddressCc = "",
                EmailAddressBcc = "bcc@example.org"
            };

            SetPrivateField(component, "actNotification", notification);

            GetPrivateMethod("SyncAddresses").Invoke(component, null);

            Assert.Multiple(() =>
            {
                Assert.That(notification.RecipientTo, Is.EqualTo(EmailRecipientOption.OtherAddresses));
                Assert.That(notification.RecipientCc, Is.EqualTo(EmailRecipientOption.None));
                Assert.That(notification.RecipientBcc, Is.EqualTo(EmailRecipientOption.OtherAddresses));
            });
        }

        [Test]
        public void SyncAddresses_ForWorkflowClient_KeepsWorkflowRecipientFields()
        {
            EditNotifications component = new();
            SetClient(component, NotificationClient.WfAction);
            FwoNotification notification = new()
            {
                RecipientTo = EmailRecipientOption.CurrentHandler,
                RecipientCc = EmailRecipientOption.Requester,
                EmailAddressTo = ""
            };

            SetPrivateField(component, "actNotification", notification);
            SetPrivateField(component, "ToRecipientSelection", new EmailRecipientSelection
            {
                None = false,
                OtherAddresses = true,
                OtherAddressList = ["ignored@example.org"]
            });

            GetPrivateMethod("SyncAddresses").Invoke(component, null);

            Assert.Multiple(() =>
            {
                Assert.That(notification.RecipientTo, Is.EqualTo(EmailRecipientOption.CurrentHandler));
                Assert.That(notification.RecipientCc, Is.EqualTo(EmailRecipientOption.Requester));
                Assert.That(notification.EmailAddressTo, Is.Empty);
            });
        }

        [Test]
        public void CheckConsistency_ReturnsFalse_WhenEmailSubjectIsMissing()
        {
            EnsureNotificationTranslations();
            EditNotifications component = new();
            SetClient(component, NotificationClient.RuleTimer);
            SetInjectedUserConfig(component, new SimulatedUserConfig());
            SetPrivateField(component, "actNotification", new FwoNotification
            {
                Channel = NotificationChannel.Email,
                EmailSubject = "",
                RecipientTo = EmailRecipientOption.OwnerMainResponsible,
                RecipientCc = EmailRecipientOption.None,
                Deadline = NotificationDeadline.None
            });

            bool isConsistent = (bool)GetPrivateMethod("CheckConsistency").Invoke(component, null)!;

            Assert.That(isConsistent, Is.False);
        }

        [Test]
        public void CheckConsistency_ReturnsTrue_ForValidOtherAddressNotification()
        {
            EnsureNotificationTranslations();
            EditNotifications component = new();
            SetClient(component, NotificationClient.RuleTimer);
            SetInjectedUserConfig(component, new SimulatedUserConfig());
            SetPrivateField(component, "actNotification", new FwoNotification
            {
                Channel = NotificationChannel.Email,
                EmailSubject = "Subject",
                RecipientTo = EmailRecipientOption.OtherAddresses,
                RecipientCc = EmailRecipientOption.None,
                Deadline = NotificationDeadline.None
            });
            SetPrivateField(component, "ToRecipientSelection", new EmailRecipientSelection
            {
                None = false,
                OtherAddresses = true,
                OtherAddressList = ["valid@example.org"]
            });

            bool isConsistent = (bool)GetPrivateMethod("CheckConsistency").Invoke(component, null)!;

            Assert.That(isConsistent, Is.True);
        }

        [Test]
        public void CheckConsistency_ReturnsFalse_WhenOtherAddressesHasNoAddress()
        {
            EnsureNotificationTranslations();
            EditNotifications component = new();
            SetClient(component, NotificationClient.RuleTimer);
            SetInjectedUserConfig(component, new SimulatedUserConfig());
            SetPrivateField(component, "actNotification", new FwoNotification
            {
                Channel = NotificationChannel.Email,
                EmailSubject = "Subject",
                Deadline = NotificationDeadline.None
            });
            SetPrivateField(component, "ToRecipientSelection", new EmailRecipientSelection
            {
                None = false,
                OtherAddresses = true
            });

            bool isConsistent = (bool)GetPrivateMethod("CheckConsistency").Invoke(component, null)!;

            Assert.That(isConsistent, Is.False);
        }

        [Test]
        public void CheckConsistency_ReturnsTrue_ForValidWorkflowRecipientNotification()
        {
            EnsureNotificationTranslations();
            EditNotifications component = new();
            SetClient(component, NotificationClient.WfAction);
            SetInjectedUserConfig(component, new SimulatedUserConfig());
            SetPrivateField(component, "actNotification", new FwoNotification
            {
                Channel = NotificationChannel.Email,
                EmailSubject = "Subject",
                RecipientTo = EmailRecipientOption.Requester,
                RecipientCc = EmailRecipientOption.None,
                Deadline = NotificationDeadline.None
            });

            bool isConsistent = (bool)GetPrivateMethod("CheckConsistency").Invoke(component, null)!;

            Assert.That(isConsistent, Is.True);
        }

        [Test]
        public void CheckConsistency_ReturnsFalse_ForWorkflowOtherAddressesWithoutAddress()
        {
            EnsureNotificationTranslations();
            EditNotifications component = new();
            SetClient(component, NotificationClient.WfAction);
            SetInjectedUserConfig(component, new SimulatedUserConfig());
            SetPrivateField(component, "actNotification", new FwoNotification
            {
                Channel = NotificationChannel.Email,
                EmailSubject = "Subject",
                RecipientTo = EmailRecipientOption.OtherAddresses,
                EmailAddressTo = "",
                RecipientCc = EmailRecipientOption.None,
                Deadline = NotificationDeadline.None
            });

            bool isConsistent = (bool)GetPrivateMethod("CheckConsistency").Invoke(component, null)!;

            Assert.That(isConsistent, Is.False);
        }

        [Test]
        public void DisplayRecipient_ShowsOtherAddressesAfterResponsibleTypes()
        {
            EditNotifications component = new();
            SetInjectedUserConfig(component, new SimulatedUserConfig());
            SetPrivateField(component, "activeOwnerResponsibleTypes", new List<OwnerResponsibleType>
            {
                new() { Id = 2, Name = "Supporting", Active = true, SortOrder = 20 },
                new() { Id = 1, Name = "Main", Active = true, SortOrder = 10 }
            });
            EmailRecipientSelection selection = new()
            {
                None = false,
                OwnerResponsibleTypeIds = [1, 2],
                OtherAddresses = true,
                OtherAddressList = ["other@example.org"]
            };

            string displayedRecipient = (string)GetPrivateMethod("DisplayRecipient").Invoke(component,
                [EmailRecipientOption.ConfiguredResponsibles, selection.ToConfigValue([1, 2])])!;

            Assert.That(displayedRecipient, Is.EqualTo("Responsible types (Main, Supporting, Other addresses)"));
        }

        [Test]
        public void DisplayRecipient_ReturnsLocalizedLabelForSimpleRecipients()
        {
            SimulatedUserConfig userConfig = new();
            EditNotifications component = new();
            SetInjectedUserConfig(component, userConfig);

            string displayedRecipient = (string)GetPrivateMethod("DisplayRecipient").Invoke(component,
                new object?[] { EmailRecipientOption.Requester, null })!;

            Assert.That(displayedRecipient, Is.EqualTo(userConfig.GetText(nameof(EmailRecipientOption.Requester))));
        }

        [Test]
        public void DisplayOtherAddresses_ReturnsLocalizedLabelWhenEmpty()
        {
            SimulatedUserConfig userConfig = new();
            EditNotifications component = new();
            SetInjectedUserConfig(component, userConfig);

            string displayedRecipient = (string)GetPrivateMethod("DisplayOtherAddresses").Invoke(component, new object?[] { "" })!;

            Assert.That(displayedRecipient, Is.EqualTo(userConfig.GetText(nameof(EmailRecipientOption.OtherAddresses))));
        }

        [Test]
        public void EditNotification_MigratesLegacyMainResponsibleToConfiguredSelection()
        {
            EditNotifications component = new();
            SetPrivateField(component, "activeOwnerResponsibleTypes", new List<OwnerResponsibleType>
            {
                new() { Id = 1, Name = "Main", Active = true, SortOrder = 10 }
            });
            FwoNotification notification = new()
            {
                RecipientTo = EmailRecipientOption.OwnerMainResponsible
            };

            GetPrivateMethod("EditNotification").Invoke(component, [notification]);

            EmailRecipientSelection selection = GetPrivateField<EmailRecipientSelection>(component, "ToRecipientSelection");
            FwoNotification actNotification = GetPrivateField<FwoNotification>(component, "actNotification");
            Assert.That(notification.RecipientTo, Is.EqualTo(EmailRecipientOption.OwnerMainResponsible));
            Assert.That(actNotification.RecipientTo, Is.EqualTo(EmailRecipientOption.ConfiguredResponsibles));
            Assert.That(selection.OwnerResponsibleTypeIds, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void EditNotification_KeepsLegacyMainResponsibleWhenMainTypeIsInactive()
        {
            EditNotifications component = new();
            SetPrivateField(component, "activeOwnerResponsibleTypes", new List<OwnerResponsibleType>
            {
                new() { Id = 3, Name = "Escalation", Active = true, SortOrder = 30 }
            });
            FwoNotification notification = new()
            {
                RecipientTo = EmailRecipientOption.OwnerMainResponsible
            };

            GetPrivateMethod("EditNotification").Invoke(component, [notification]);

            Assert.That(notification.RecipientTo, Is.EqualTo(EmailRecipientOption.OwnerMainResponsible));
        }

        [Test]
        public void SyncAddresses_ClearsRequesterSelectionWhenRequesterOptionIsNotAvailable()
        {
            EditNotifications component = new();
            SetClient(component, NotificationClient.RuleTimer);
            FwoNotification notification = new();

            SetPrivateField(component, "actNotification", notification);
            SetPrivateField(component, "ToRecipientSelection", new EmailRecipientSelection
            {
                None = false,
                Requester = true
            });

            GetPrivateMethod("SyncAddresses").Invoke(component, null);

            EmailRecipientSelection selection = GetPrivateField<EmailRecipientSelection>(component, "ToRecipientSelection");
            Assert.That(selection.Requester, Is.False);
            Assert.That(notification.RecipientTo, Is.EqualTo(EmailRecipientOption.None));
        }

        [Test]
        public void CheckConsistency_ReturnsTrue_ForConfiguredResponsibleNotification()
        {
            EnsureNotificationTranslations();
            EditNotifications component = new();
            SetClient(component, NotificationClient.RuleTimer);
            SetInjectedUserConfig(component, new SimulatedUserConfig());
            SetPrivateField(component, "actNotification", new FwoNotification
            {
                Channel = NotificationChannel.Email,
                EmailSubject = "Subject",
                RecipientTo = EmailRecipientOption.ConfiguredResponsibles,
                RecipientCc = EmailRecipientOption.None,
                Deadline = NotificationDeadline.None
            });
            SetPrivateField(component, "ToRecipientSelection", new EmailRecipientSelection
            {
                None = false,
                OwnerResponsibleTypeIds = [3]
            });

            bool isConsistent = (bool)GetPrivateMethod("CheckConsistency").Invoke(component, null)!;

            Assert.That(isConsistent, Is.True);
        }

        [Test]
        public void CheckConsistency_ReturnsFalse_WhenDeadlineRepeatsWithoutCount()
        {
            EnsureNotificationTranslations();
            EditNotifications component = new();
            SetClient(component, NotificationClient.RuleTimer);
            SetInjectedUserConfig(component, new SimulatedUserConfig());
            SetPrivateField(component, "actNotification", new FwoNotification
            {
                Channel = NotificationChannel.Email,
                EmailSubject = "Subject",
                RecipientTo = EmailRecipientOption.OtherAddresses,
                EmailAddressTo = "to@example.org",
                Deadline = NotificationDeadline.RuleExpiry,
                RepeatOffsetAfterDeadline = 2,
                RepeatIntervalAfterDeadline = SchedulerInterval.Weeks
            });

            bool isConsistent = (bool)GetPrivateMethod("CheckConsistency").Invoke(component, null)!;

            Assert.That(isConsistent, Is.False);
        }

        [Test]
        public void EnsureNotificationIntervals_SetsDefaultIntervalValues_WhenOffsetsAreFilled()
        {
            EditNotifications component = new();
            FwoNotification notification = new()
            {
                OffsetBeforeDeadline = 3,
                RepeatOffsetAfterDeadline = 2
            };

            SetPrivateField(component, "actNotification", notification);

            GetPrivateMethod("EnsureNotificationIntervals").Invoke(component, null);

            Assert.Multiple(() =>
            {
                Assert.That(notification.IntervalBeforeDeadline, Is.EqualTo(SchedulerInterval.Weeks));
                Assert.That(notification.RepeatIntervalAfterDeadline, Is.EqualTo(SchedulerInterval.Weeks));
            });
        }

        [Test]
        public void OfferedLayoutOptions_ExcludesAttachmentsForModellingClients()
        {
            EditNotifications component = new();
            SetClient(component, NotificationClient.InterfaceRequest);

            List<NotificationLayout> layouts = (List<NotificationLayout>)GetPrivateMethod("OfferedLayoutOptions").Invoke(component, null)!;

            Assert.That(layouts, Does.Not.Contain(NotificationLayout.PdfAsAttachment));
        }

        [Test]
        public void OfferedLayoutOptions_OffersAttachmentsForReportClient()
        {
            EditNotifications component = new();
            SetClient(component, NotificationClient.Report);

            List<NotificationLayout> layouts = (List<NotificationLayout>)GetPrivateMethod("OfferedLayoutOptions").Invoke(component, null)!;

            Assert.That(layouts, Does.Contain(NotificationLayout.PdfAsAttachment));
        }

        [Test]
        public void CheckConsistencyWithDeadline_ReturnsFalseWithoutAnyOffset()
        {
            EditNotifications component = new();
            SetInjectedUserConfig(component, new SimulatedUserConfig());
            SetPrivateField(component, "actNotification", new FwoNotification());

            bool result = (bool)GetPrivateMethod("CheckConsistencyWithDeadline").Invoke(component, null)!;

            Assert.That(result, Is.False);
        }

        [Test]
        public void CheckConsistencyWithDeadline_ReturnsFalseWhenBeforeOffsetHasNoInterval()
        {
            EditNotifications component = new();
            SetInjectedUserConfig(component, new SimulatedUserConfig());
            SetPrivateField(component, "actNotification", new FwoNotification { OffsetBeforeDeadline = 1 });

            bool result = (bool)GetPrivateMethod("CheckConsistencyWithDeadline").Invoke(component, null)!;

            Assert.That(result, Is.False);
        }

        [Test]
        public void CheckConsistencyWithDeadline_ReturnsTrueForCompleteBeforeDeadlineConfiguration()
        {
            EditNotifications component = new();
            SetInjectedUserConfig(component, new SimulatedUserConfig());
            SetPrivateField(component, "actNotification", new FwoNotification
            {
                OffsetBeforeDeadline = 1,
                IntervalBeforeDeadline = SchedulerInterval.Days
            });

            bool result = (bool)GetPrivateMethod("CheckConsistencyWithDeadline").Invoke(component, null)!;

            Assert.That(result, Is.True);
        }

        [Test]
        public void CheckConsistencyWithDeadline_ReturnsFalseWhenAfterOffsetHasNoInterval()
        {
            EditNotifications component = new();
            SetInjectedUserConfig(component, new SimulatedUserConfig());
            SetPrivateField(component, "actNotification", new FwoNotification { RepeatOffsetAfterDeadline = 1 });

            bool result = (bool)GetPrivateMethod("CheckConsistencyWithDeadline").Invoke(component, null)!;

            Assert.That(result, Is.False);
        }

        [Test]
        public void CheckConsistencyWithDeadline_ReturnsFalseWhenRepetitionsAreWithoutAfterOffset()
        {
            EditNotifications component = new();
            SetInjectedUserConfig(component, new SimulatedUserConfig());
            SetPrivateField(component, "actNotification", new FwoNotification { RepetitionsAfterDeadline = 2 });

            bool result = (bool)GetPrivateMethod("CheckConsistencyWithDeadline").Invoke(component, null)!;

            Assert.That(result, Is.False);
        }

        [Test]
        public void CheckConsistencyWithDeadline_ReturnsTrueForCompleteAfterDeadlineConfiguration()
        {
            EditNotifications component = new();
            SetInjectedUserConfig(component, new SimulatedUserConfig());
            SetPrivateField(component, "actNotification", new FwoNotification
            {
                RepeatOffsetAfterDeadline = 1,
                RepeatIntervalAfterDeadline = SchedulerInterval.Days,
                RepetitionsAfterDeadline = 2
            });

            bool result = (bool)GetPrivateMethod("CheckConsistencyWithDeadline").Invoke(component, null)!;

            Assert.That(result, Is.True);
        }

        [Test]
        public async Task SetDirectAddresses_WritesToCcAndBccFields()
        {
            EditNotifications component = new();
            FwoNotification notification = new();
            SetPrivateField(component, "actNotification", notification);

            await (Task)GetPrivateMethod("SetDirectAddressTo").Invoke(component, new object?[] { "to@example.org" })!;
            await (Task)GetPrivateMethod("SetDirectAddressCc").Invoke(component, new object?[] { "cc@example.org" })!;
            await (Task)GetPrivateMethod("SetDirectAddressBcc").Invoke(component, new object?[] { "bcc@example.org" })!;

            Assert.Multiple(() =>
            {
                Assert.That(notification.EmailAddressTo, Is.EqualTo("to@example.org"));
                Assert.That(notification.EmailAddressCc, Is.EqualTo("cc@example.org"));
                Assert.That(notification.EmailAddressBcc, Is.EqualTo("bcc@example.org"));
            });
        }

        [Test]
        public void ParseOtherAddressSelection_ParsesConfiguredSelectionJson()
        {
            EditNotifications component = new();
            SetPrivateField(component, "activeOwnerResponsibleTypes", new List<OwnerResponsibleType>
            {
                new() { Id = 1, Name = "Main", Active = true }
            });
            EmailRecipientSelection expected = new()
            {
                None = false,
                OtherAddresses = true,
                OtherAddressList = new List<string> { "json@example.org" }
            };

            EmailRecipientSelection result = (EmailRecipientSelection)GetPrivateMethod("ParseOtherAddressSelection")
                .Invoke(component, new object?[] { expected.ToConfigValue(new List<int> { 1 }), new List<string>() })!;

            Assert.That(result.OtherAddressList, Is.EqualTo(new List<string> { "json@example.org" }));
        }

        [Test]
        public void ParseOtherAddressSelection_UsesLegacyAddressesWhenValueIsNotJson()
        {
            EditNotifications component = new();

            EmailRecipientSelection result = (EmailRecipientSelection)GetPrivateMethod("ParseOtherAddressSelection")
                .Invoke(component, new object?[] { "legacy@example.org", new List<string> { "legacy@example.org" } })!;

            Assert.Multiple(() =>
            {
                Assert.That(result.OtherAddresses, Is.True);
                Assert.That(result.OtherAddressList, Is.EqualTo(new List<string> { "legacy@example.org" }));
            });
        }

        [Test]
        public void DisplayedInterval_UsesBeforeDeadlineIntervalWhenConfigured()
        {
            EditNotifications component = new();
            SetInjectedUserConfig(component, new SimulatedUserConfig());
            FwoNotification notification = new()
            {
                Deadline = NotificationDeadline.RecertDate,
                OffsetBeforeDeadline = 3,
                IntervalBeforeDeadline = SchedulerInterval.Days
            };

            string result = (string)GetPrivateMethod("DisplayedInterval").Invoke(component, new object?[] { notification })!;

            Assert.That(result, Is.EqualTo($"3 {new SimulatedUserConfig().GetText(SchedulerInterval.Days.ToString())}"));
        }

        [Test]
        public void DisplayedInterval_UsesRepeatIntervalWhenNoBeforeDeadlineIntervalExists()
        {
            EditNotifications component = new();
            SetInjectedUserConfig(component, new SimulatedUserConfig());
            FwoNotification notification = new()
            {
                Deadline = NotificationDeadline.RuleExpiry,
                RepeatOffsetAfterDeadline = 2,
                RepeatIntervalAfterDeadline = SchedulerInterval.Weeks
            };

            string result = (string)GetPrivateMethod("DisplayedInterval").Invoke(component, new object?[] { notification })!;

            Assert.That(result, Does.StartWith("2 "));
        }

        [Test]
        public void DisplayedInterval_IsEmptyForImmediateNotification()
        {
            EditNotifications component = new();
            string result = (string)GetPrivateMethod("DisplayedInterval").Invoke(component,
                new object?[] { new FwoNotification { Deadline = NotificationDeadline.None } })!;

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void EditNotification_UsesConfiguredSelectionForAllOwnerResponsibles()
        {
            EditNotifications component = new();
            SetClient(component, NotificationClient.InterfaceRequest);
            SetPrivateField(component, "activeOwnerResponsibleTypes", new List<OwnerResponsibleType>
            {
                new() { Id = GlobalConst.kOwnerResponsibleTypeMain, Active = true, SortOrder = 1 },
                new() { Id = GlobalConst.kOwnerResponsibleTypeSupporting, Active = true, SortOrder = 2 }
            });
            FwoNotification notification = new() { RecipientTo = EmailRecipientOption.AllOwnerResponsibles };

            GetPrivateMethod("EditNotification").Invoke(component, new object?[] { notification });

            EmailRecipientSelection selection = GetPrivateField<EmailRecipientSelection>(component, "ToRecipientSelection");
            Assert.That(selection.OwnerResponsibleTypeIds, Is.EqualTo(new List<int>
            {
                GlobalConst.kOwnerResponsibleTypeMain,
                GlobalConst.kOwnerResponsibleTypeSupporting
            }));
        }

        [Test]
        public void SyncAddresses_RemovesRequesterWhenClientDoesNotOfferRequester()
        {
            EditNotifications component = new();
            SetClient(component, NotificationClient.Recertification);
            FwoNotification notification = new();
            SetPrivateField(component, "actNotification", notification);
            SetPrivateField(component, "ToRecipientSelection", new EmailRecipientSelection
            {
                None = false,
                Requester = true,
                OtherAddresses = true,
                OtherAddressList = new List<string> { "to@example.org" }
            });

            GetPrivateMethod("SyncAddresses").Invoke(component, null);

            EmailRecipientSelection selection = EmailRecipientSelection.Parse(notification.EmailAddressTo);
            Assert.That(selection.Requester, Is.False);
        }

        [Test]
        public async Task AddAndRemoveNotificationId_IgnoreNullIds()
        {
            EditNotifications component = new();

            await (Task)GetPrivateMethod("AddNotificationId").Invoke(component, new object?[] { 7 })!;
            await (Task)GetPrivateMethod("RemoveNotificationId").Invoke(component, new object?[] { 7 })!;

            Assert.That(component.NotificationIds, Is.Null);
        }

        [Test]
        public async Task AddNotificationId_DoesNotAddDuplicateId()
        {
            EditNotifications component = new();
            List<int> notificationIds = new() { 7 };
            SetPrivateMember(component, nameof(EditNotifications.NotificationIds), notificationIds);

            await (Task)GetPrivateMethod("AddNotificationId").Invoke(component, new object?[] { 7 })!;

            Assert.That(notificationIds, Is.EqualTo(new List<int> { 7 }));
        }

        [Test]
        public async Task AddAndRemoveNotificationId_UpdatesBoundIds()
        {
            EditNotifications component = new();
            List<int> notificationIds = new() { 4 };
            SetPrivateMember(component, "NotificationIds", notificationIds);

            await (Task)GetPrivateMethod("AddNotificationId").Invoke(component, new object?[] { 7 })!;
            await (Task)GetPrivateMethod("AddNotificationId").Invoke(component, new object?[] { 4 })!;
            await (Task)GetPrivateMethod("RemoveNotificationId").Invoke(component, new object?[] { 4 })!;

            Assert.That(notificationIds, Is.EqualTo(new List<int> { 7 }));
        }

        [Test]
        public async Task ResetNotification_LeavesLastSentWhenUpdateFails()
        {
            EditNotifications component = new();
            DateTime lastSent = DateTime.Now;
            SetInjectedUserConfig(component, new SimulatedUserConfig());
            SetPrivateMember(component, "apiConnection", new EditNotificationsApiConnection { ThrowOnReset = true });
            SetPrivateField(component, "actNotification", new FwoNotification { Id = 9, LastSent = lastSent });
            SetPrivateField(component, "ResetNotifMode", true);

            MethodInfo resetMethod = typeof(EditNotifications).GetMethod("ResetNotification", BindingFlags.NonPublic | BindingFlags.Instance,
                binder: null, types: Type.EmptyTypes, modifiers: null)
                ?? throw new MissingMethodException(typeof(EditNotifications).FullName, "ResetNotification()");
            Assert.DoesNotThrowAsync(async () => await (Task)resetMethod.Invoke(component, null)!);

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<FwoNotification>(component, "actNotification").LastSent, Is.EqualTo(lastSent));
                Assert.That(GetPrivateField<bool>(component, "ResetNotifMode"), Is.True);
            });
        }

        [Test]
        public async Task Save_AddsNotificationAndReferenceWhenConfigurationIsValid()
        {
            EditNotifications component = new();
            SetClient(component, NotificationClient.Report);
            SetInjectedUserConfig(component, new SimulatedUserConfig());
            SetPrivateMember(component, "apiConnection", new EditNotificationsApiConnection());
            SetPrivateField(component, "Notifications", new List<FwoNotification>());
            SetPrivateMember(component, "NotificationIds", new List<int>());
            SetPrivateField(component, "actNotification", new FwoNotification
            {
                Channel = NotificationChannel.Email,
                EmailSubject = "Subject",
                EmailAddressTo = "to@example.org"
            });
            SetPrivateField(component, "AddNotifMode", true);

            await (Task)GetPrivateMethod("Save").Invoke(component, null)!;

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<List<FwoNotification>>(component, "Notifications"), Has.Count.EqualTo(1));
                Assert.That(component.NotificationIds, Is.EqualTo(new List<int> { 100 }));
                Assert.That(GetPrivateField<bool>(component, "EditNotifMode"), Is.False);
            });
        }

        [Test]
        public async Task Save_UpdatesExistingNotificationWhenConfigurationIsValid()
        {
            EditNotifications component = new();
            SetClient(component, NotificationClient.Report);
            SetInjectedUserConfig(component, new SimulatedUserConfig());
            SetPrivateMember(component, "apiConnection", new EditNotificationsApiConnection());
            FwoNotification notification = new()
            {
                Id = 9,
                Name = "Updated",
                Channel = NotificationChannel.Email,
                EmailSubject = "Subject",
                EmailAddressTo = "to@example.org"
            };
            SetPrivateField(component, "Notifications", new List<FwoNotification> { notification });
            SetPrivateField(component, "actNotification", notification);
            SetPrivateField(component, "EditNotifMode", true);

            await (Task)GetPrivateMethod("Save").Invoke(component, null)!;

            Assert.That(GetPrivateField<List<FwoNotification>>(component, "Notifications").Single().Name, Is.EqualTo("Updated"));
        }

        [Test]
        public async Task Delete_RestoresReferenceWhenDeleteFails()
        {
            EditNotificationsApiConnection apiConnection = new() { ThrowOnDelete = true };
            EditNotifications component = new();
            FwoNotification notification = new() { Id = 9, Name = "Notification" };
            SetInjectedUserConfig(component, new SimulatedUserConfig());
            SetPrivateMember(component, "apiConnection", apiConnection);
            SetPrivateField(component, "Notifications", new List<FwoNotification> { notification });
            SetPrivateMember(component, "NotificationIds", new List<int> { 9 });
            SetPrivateField(component, "actNotification", notification);

            await (Task)GetPrivateMethod("Delete").Invoke(component, null)!;

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<List<FwoNotification>>(component, "Notifications"), Has.Count.EqualTo(1));
                Assert.That(component.NotificationIds, Is.EqualTo(new List<int> { 9 }));
                Assert.That(apiConnection.DeletedIds, Is.EqualTo(new List<int> { 9 }));
            });
        }

        private sealed class EditNotificationsApiConnection : SimulatedApiConnection
        {
            private static readonly ReturnId[] AddedNotificationIds = new ReturnId[] { new() { NewId = 100 } };
            private static readonly ReturnId[] UpdatedNotificationIds = new ReturnId[] { new() { UpdatedId = 9 } };
            public int AffectedRows { get; init; }
            public bool ThrowOnDelete { get; init; }
            public bool ThrowOnReset { get; init; }
            public List<int> DeletedIds { get; } = new();

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null,
                string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == NotificationQueries.addNotification && typeof(QueryResponseType) == typeof(ReturnIdWrapper))
                {
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper
                    {
                        ReturnIds = AddedNotificationIds
                    });
                }
                if (query == NotificationQueries.updateNotification && typeof(QueryResponseType) == typeof(ReturnIdWrapper))
                {
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper
                    {
                        ReturnIds = UpdatedNotificationIds
                    });
                }
                if (query == NotificationQueries.deleteNotification && typeof(QueryResponseType) == typeof(object))
                {
                    DeletedIds.Add((int)(variables?.GetType().GetProperty("id")?.GetValue(variables) ?? 0));
                    if (ThrowOnDelete)
                    {
                        throw new InvalidOperationException("Delete failed.");
                    }
                    return Task.FromResult((QueryResponseType)(object)new object());
                }
                if (query == NotificationQueries.updateNotificationsLastSent && typeof(QueryResponseType) == typeof(ReturnId))
                {
                    if (ThrowOnReset)
                    {
                        throw new InvalidOperationException("Reset failed.");
                    }
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = AffectedRows });
                }

                throw new NotImplementedException($"Unexpected query: {query}");
            }
        }
    }
}

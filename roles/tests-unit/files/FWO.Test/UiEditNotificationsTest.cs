using FWO.Config.Api;
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
            SimulatedUserConfig.DummyTranslate.TryAdd("edit_notification", "Edit notification");
            SimulatedUserConfig.DummyTranslate.TryAdd("E4010", "Missing email subject");
            SimulatedUserConfig.DummyTranslate.TryAdd("E4011", "Missing recipient");
            SimulatedUserConfig.DummyTranslate.TryAdd("E4012", "Missing offsets");
            SimulatedUserConfig.DummyTranslate.TryAdd("E4013", "Incomplete repetition settings");
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
        public void CheckConsistency_ReturnsFalse_WhenEmailSubjectIsMissing()
        {
            EnsureNotificationTranslations();
            EditNotifications component = new();
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
        public void DisplayRecipient_ShowsOtherAddressesAfterResponsibleTypes()
        {
            SimulatedUserConfig.DummyTranslate.TryAdd(nameof(EmailRecipientOption.ConfiguredResponsibles), "Responsible types");
            SimulatedUserConfig.DummyTranslate.TryAdd(nameof(EmailRecipientOption.OtherAddresses), "Other addresses");
            SimulatedUserConfig.DummyTranslate.TryAdd("Main", "Main");
            SimulatedUserConfig.DummyTranslate.TryAdd("Supporting", "Supporting");
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
        public void CheckConsistency_ReturnsTrue_ForConfiguredResponsibleNotification()
        {
            EnsureNotificationTranslations();
            EditNotifications component = new();
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
    }
}

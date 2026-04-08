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
            Assert.That(actNotification.RecipientTo, Is.EqualTo(EmailRecipientOption.OwnerMainResponsible));
            Assert.That(actNotification.RecipientCc, Is.EqualTo(EmailRecipientOption.None));
            Assert.That(actNotification.Layout, Is.EqualTo(NotificationLayout.HtmlInBody));
            Assert.That(actNotification.Deadline, Is.EqualTo(FwoNotification.OfferedDeadlineOptions(NotificationClient.RuleTimer).Single()));
        }

        [Test]
        public void SyncAddresses_AppliesQueuedAddsAndDeletes()
        {
            EditNotifications component = new();
            FwoNotification notification = new();

            SetPrivateField(component, "actNotification", notification);
            SetPrivateField(component, "ToAddresses", new List<string> { "to-remove@example.org", "to-keep@example.org" });
            SetPrivateField(component, "ToAddressesToDelete", new List<string> { "to-remove@example.org" });
            SetPrivateField(component, "ToAddressesToAdd", new List<string> { "to-add@example.org" });
            SetPrivateField(component, "CcAddresses", new List<string> { "cc-remove@example.org" });
            SetPrivateField(component, "CcAddressesToDelete", new List<string> { "cc-remove@example.org" });
            SetPrivateField(component, "CcAddressesToAdd", new List<string> { "cc-add-1@example.org", "cc-add-2@example.org" });

            GetPrivateMethod("SyncAddresses").Invoke(component, null);

            Assert.That(notification.EmailAddressTo, Is.EqualTo("to-keep@example.org,to-add@example.org"));
            Assert.That(notification.EmailAddressCc, Is.EqualTo("cc-add-1@example.org,cc-add-2@example.org"));
            Assert.That(GetPrivateField<List<string>>(component, "ToAddressesToDelete"), Is.Empty);
            Assert.That(GetPrivateField<List<string>>(component, "ToAddressesToAdd"), Is.Empty);
            Assert.That(GetPrivateField<List<string>>(component, "CcAddressesToDelete"), Is.Empty);
            Assert.That(GetPrivateField<List<string>>(component, "CcAddressesToAdd"), Is.Empty);
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
            SetPrivateField(component, "ToAddresses", new List<string> { "valid@example.org" });
            SetPrivateField(component, "CcAddresses", new List<string>());

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

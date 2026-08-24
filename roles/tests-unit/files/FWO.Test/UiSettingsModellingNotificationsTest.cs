using FWO.Api.Client;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Ui.Pages.Settings;
using NUnit.Framework;
using System.Linq;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    internal class UiSettingsModellingNotificationsTest
    {
        private static readonly int[] RequestRecipientTypeIds = [1];
        private static readonly int[] DecommRecipientTypeIds = [2];
        private static readonly int[] ActiveRecipientTypeIds = [1, 2];
        private static readonly string[] RequestLegacyAddresses = ["legacy-request@example.org"];
        private static readonly string[] DecommLegacyAddresses = ["legacy-decomm@example.org"];

        private static MethodInfo GetPrivateMethod(string name)
        {
            return typeof(SettingsModellingNotifications).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                ?? throw new MissingMethodException(typeof(SettingsModellingNotifications).FullName, name);
        }

        private static void SetPrivateField(object component, string fieldName, object? value)
        {
            FieldInfo? field = component.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(component.GetType().FullName, fieldName);
            }
            field.SetValue(component, value);
        }

        private static T GetPrivateField<T>(object component, string fieldName)
        {
            FieldInfo? field = component.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(component.GetType().FullName, fieldName);
            }
            return (T)field.GetValue(component)!;
        }

        private static void SetInjectedGlobalConfig(SettingsModellingNotifications component, GlobalConfig globalConfig)
        {
            PropertyInfo? prop = typeof(SettingsModellingNotifications).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(p => p.PropertyType == typeof(GlobalConfig));
            if (prop == null)
            {
                throw new MissingMemberException(typeof(SettingsModellingNotifications).FullName, "globalConfig");
            }
            prop.SetValue(component, globalConfig);
        }

        private static void SetInjectedApiConnection(SettingsModellingNotifications component, RecordingSettingsApiConn apiConnection)
        {
            PropertyInfo? prop = typeof(SettingsModellingNotifications).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(p => p.PropertyType == typeof(ApiConnection));
            if (prop == null)
            {
                throw new MissingMemberException(typeof(SettingsModellingNotifications).FullName, "apiConnection");
            }
            prop.SetValue(component, apiConnection);
        }

        [Test]
        public void MergeLegacyOtherAddresses_MergesUniqueAddressesAndSetsSelectionFlags()
        {
            EmailRecipientSelection selection = new()
            {
                None = true,
                OtherAddresses = false,
                OtherAddressList = ["existing@example.org"]
            };

            GetPrivateMethod("MergeLegacyOtherAddresses").Invoke(null, [selection, "new@example.org; existing@example.org | second@example.org"]);

            Assert.Multiple(() =>
            {
                Assert.That(selection.OtherAddresses, Is.True);
                Assert.That(selection.None, Is.False);
                Assert.That(selection.OtherAddressList, Is.EqualTo(new List<string> { "existing@example.org", "new@example.org", "second@example.org" }));
            });
        }

        [Test]
        public async Task OnInitializedAsync_LoadsRecipientsAndMergesLegacyAddresses()
        {
            SettingsModellingNotifications component = new();
            SimulatedGlobalConfig globalConfig = new()
            {
                ModReqEmailReceiver = new EmailRecipientSelection
                {
                    OwnerResponsibleTypeIds = RequestRecipientTypeIds
                }.ToConfigValue(ActiveRecipientTypeIds),
                ModReqEmailOtherAddresses = "legacy-request@example.org",
                ModDecommEmailReceiver = new EmailRecipientSelection
                {
                    OwnerResponsibleTypeIds = DecommRecipientTypeIds
                }.ToConfigValue(ActiveRecipientTypeIds),
                ModDecommEmailOtherAddresses = "legacy-decomm@example.org"
            };
            RecordingSettingsApiConn apiConnection = new()
            {
                OwnerResponsibleTypes =
                [
                    new() { Id = 1, Active = true, Name = "Main", SortOrder = 1 },
                    new() { Id = 2, Active = true, Name = "Supporting", SortOrder = 2 },
                    new() { Id = 3, Active = false, Name = "Inactive", SortOrder = 3 }
                ]
            };

            SetInjectedGlobalConfig(component, globalConfig);
            SetInjectedApiConnection(component, apiConnection);

            Task initTask = (Task)GetPrivateMethod("OnInitializedAsync").Invoke(component, null)!;
            await initTask;

            EmailRecipientSelection modReq = GetPrivateField<EmailRecipientSelection>(component, "modReqEmailRecipients");
            EmailRecipientSelection modDecomm = GetPrivateField<EmailRecipientSelection>(component, "modDecommEmailRecipients");

            Assert.Multiple(() =>
            {
                Assert.That(modReq.OwnerResponsibleTypeIds, Is.EqualTo(RequestRecipientTypeIds));
                Assert.That(modReq.OtherAddressList, Is.EqualTo(RequestLegacyAddresses));
                Assert.That(modDecomm.OwnerResponsibleTypeIds, Is.EqualTo(DecommRecipientTypeIds));
                Assert.That(modDecomm.OtherAddressList, Is.EqualTo(DecommLegacyAddresses));
            });
        }

        [Test]
        public void PrepareConfigData_SerializesRecipientsAndClearsLegacyAddresses()
        {
            SettingsModellingNotifications component = new();
            ConfigData configData = new();
            SetPrivateField(component, "configData", configData);
            SetPrivateField(component, "activeOwnerResponsibleTypes", new List<OwnerResponsibleType>
            {
                new() { Id = 1, Active = true, Name = "Main" },
                new() { Id = 2, Active = true, Name = "Supporting" }
            });
            SetPrivateField(component, "modReqEmailRecipients", new EmailRecipientSelection
            {
                OwnerResponsibleTypeIds = [1],
                OtherAddresses = true,
                OtherAddressList = ["request@example.org"]
            });
            SetPrivateField(component, "modDecommEmailRecipients", new EmailRecipientSelection
            {
                OwnerResponsibleTypeIds = [2],
                OtherAddresses = true,
                OtherAddressList = ["decomm@example.org"]
            });

            GetPrivateMethod("PrepareConfigData").Invoke(component, null);

            Assert.Multiple(() =>
            {
                Assert.That(configData.ModReqEmailReceiver, Does.Contain("\"owner_responsible_type_ids\":[1]"));
                Assert.That(configData.ModDecommEmailReceiver, Does.Contain("\"owner_responsible_type_ids\":[2]"));
                Assert.That(configData.ModReqEmailOtherAddresses, Is.Empty);
                Assert.That(configData.ModDecommEmailOtherAddresses, Is.Empty);
            });
        }
    }
}

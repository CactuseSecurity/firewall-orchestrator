using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Ui.Pages.Settings;
using NUnit.Framework;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace FWO.Test
{
    [TestFixture]
    internal class UiSettingsNotificationsTest
    {
        private static MethodInfo GetPrivateMethod(string name)
        {
            return typeof(SettingsNotifications).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(SettingsNotifications).FullName, name);
        }

        private static MethodInfo GetPrivateStaticMethod(string name)
        {
            return typeof(SettingsNotifications).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(typeof(SettingsNotifications).FullName, name);
        }

        private static void SetPrivateField<T>(SettingsNotifications component, string fieldName, T value)
        {
            FieldInfo? field = typeof(SettingsNotifications).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(typeof(SettingsNotifications).FullName, fieldName);
            }
            field.SetValue(component, value);
        }

        private static T GetPrivateField<T>(SettingsNotifications component, string fieldName)
        {
            FieldInfo? field = typeof(SettingsNotifications).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(typeof(SettingsNotifications).FullName, fieldName);
            }
            return (T)field.GetValue(component)!;
        }

        private static void SetInjectedGlobalConfig(SettingsNotifications component, GlobalConfig globalConfig)
        {
            PropertyInfo? prop = typeof(SettingsNotifications).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(p => p.PropertyType == typeof(GlobalConfig));
            if (prop == null)
            {
                throw new MissingMemberException(typeof(SettingsNotifications).FullName, "globalConfig");
            }
            prop.SetValue(component, globalConfig);
        }

        private static IList CreateInitiatorList()
        {
            Type? entryType = typeof(SettingsNotifications).GetNestedType("RuleExpiryInitiatorText", BindingFlags.NonPublic);
            if (entryType == null)
            {
                throw new MissingMemberException(typeof(SettingsNotifications).FullName, "RuleExpiryInitiatorText");
            }

            Type listType = typeof(List<>).MakeGenericType(entryType);
            return (IList)(Activator.CreateInstance(listType) ?? throw new InvalidOperationException("Could not create initiator list."));
        }

        private static object CreateInitiatorEntry(string key, string text)
        {
            Type? entryType = typeof(SettingsNotifications).GetNestedType("RuleExpiryInitiatorText", BindingFlags.NonPublic);
            if (entryType == null)
            {
                throw new MissingMemberException(typeof(SettingsNotifications).FullName, "RuleExpiryInitiatorText");
            }

            object entry = Activator.CreateInstance(entryType) ?? throw new InvalidOperationException("Could not create initiator entry.");
            entryType.GetProperty("Key")?.SetValue(entry, key);
            entryType.GetProperty("Text")?.SetValue(entry, text);
            return entry;
        }

        private static Dictionary<string, string> ToDictionary(IList entries)
        {
            Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
            foreach (object entry in entries)
            {
                Type entryType = entry.GetType();
                string key = (string)(entryType.GetProperty("Key")?.GetValue(entry) ?? "");
                string text = (string)(entryType.GetProperty("Text")?.GetValue(entry) ?? "");
                result[key] = text;
            }
            return result;
        }

        [Test]
        public void ParseInitiatorKeys_ReturnsEntries_ForValidJson_AndEmptyForInvalid()
        {
            MethodInfo parseMethod = GetPrivateStaticMethod("ParseInitiatorKeys");

            IList parsed = (IList)(parseMethod.Invoke(null, ["{\"user\":\"A\",\"nsb\":\"B\"}"]) ?? CreateInitiatorList());
            Dictionary<string, string> parsedMap = ToDictionary(parsed);
            Assert.That(parsedMap.Count, Is.EqualTo(2));
            Assert.That(parsedMap["user"], Is.EqualTo("A"));
            Assert.That(parsedMap["nsb"], Is.EqualTo("B"));

            IList parsedInvalid = (IList)(parseMethod.Invoke(null, ["{invalid json}"]) ?? CreateInitiatorList());
            Assert.That(parsedInvalid.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task OnInitializedAsync_LoadsInitiatorKeys_FromGlobalConfig()
        {
            SettingsNotifications component = new();
            SimulatedGlobalConfig globalConfig = new()
            {
                RuleExpiryInitiatorKeys = "{\"user\":\"Ablauf user\",\"nsb\":\"Ablauf nsb\"}"
            };
            SetInjectedGlobalConfig(component, globalConfig);

            Task initTask = (Task)GetPrivateMethod("OnInitializedAsync").Invoke(component, null)!;
            await initTask;

            IList entries = GetPrivateField<IList>(component, "initiatorKeys");
            Dictionary<string, string> map = ToDictionary(entries);
            Assert.That(map.Count, Is.EqualTo(2));
            Assert.That(map["user"], Is.EqualTo("Ablauf user"));
            Assert.That(map["nsb"], Is.EqualTo("Ablauf nsb"));
        }

        [Test]
        public async Task OnInitializedAsync_UsesExplicitDefaultOption_WhenNotificationLanguageIsUnset()
        {
            SettingsNotifications component = new();
            SimulatedGlobalConfig globalConfig = new()
            {
                DefaultLanguage = "German",
                NotificationLanguage = "",
                UiLanguages =
                [
                    new Language { Name = "German", CultureInfo = "de-DE" },
                    new Language { Name = "English", CultureInfo = "en-US" }
                ]
            };
            SetInjectedGlobalConfig(component, globalConfig);

            Task initTask = (Task)GetPrivateMethod("OnInitializedAsync").Invoke(component, null)!;
            await initTask;

            Language selected = GetPrivateField<Language>(component, "selectedNotificationLanguage");
            Assert.That(selected.Name, Is.EqualTo(""));
        }

        [Test]
        public async Task OnInitializedAsync_UsesConcreteSelection_WhenNotificationLanguageIsConfigured()
        {
            SettingsNotifications component = new();
            SimulatedGlobalConfig globalConfig = new()
            {
                DefaultLanguage = "German",
                NotificationLanguage = "English",
                UiLanguages =
                [
                    new Language { Name = "German", CultureInfo = "de-DE" },
                    new Language { Name = "English", CultureInfo = "en-US" }
                ]
            };
            SetInjectedGlobalConfig(component, globalConfig);

            Task initTask = (Task)GetPrivateMethod("OnInitializedAsync").Invoke(component, null)!;
            await initTask;

            Language selected = GetPrivateField<Language>(component, "selectedNotificationLanguage");
            Assert.That(selected.Name, Is.EqualTo("English"));
        }

        [Test]
        public async Task OnInitializedAsync_FlagsUnknownStoredNotificationLanguage()
        {
            SettingsNotifications component = new();
            SimulatedGlobalConfig globalConfig = new()
            {
                DefaultLanguage = "German",
                NotificationLanguage = "French",
                UiLanguages =
                [
                    new Language { Name = "German", CultureInfo = "de-DE" },
                    new Language { Name = "English", CultureInfo = "en-US" }
                ]
            };
            SetInjectedGlobalConfig(component, globalConfig);

            Task initTask = (Task)GetPrivateMethod("OnInitializedAsync").Invoke(component, null)!;
            await initTask;

            Language selected = GetPrivateField<Language>(component, "selectedNotificationLanguage");
            bool hasUnknownSelection = GetPrivateField<bool>(component, "hasUnknownNotificationLanguageSelection");
            Assert.That(selected.Name, Is.EqualTo(""));
            Assert.That(hasUnknownSelection, Is.True);
        }

        [Test]
        public void AddInitiatorKey_AddsTrimmedValue_AndSkipsCaseInsensitiveDuplicates()
        {
            SettingsNotifications component = new();
            IList initiatorKeys = CreateInitiatorList();
            IList initiatorKeysToAdd = CreateInitiatorList();
            object existing = CreateInitiatorEntry("User", "Existing");
            initiatorKeys.Add(existing);

            SetPrivateField(component, "initiatorKeys", initiatorKeys);
            SetPrivateField(component, "initiatorKeysToAdd", initiatorKeysToAdd);

            SetPrivateField(component, "actInitiatorKey", CreateInitiatorEntry(" user ", "Duplicate"));
            GetPrivateMethod("AddInitiatorKey").Invoke(component, null);
            Assert.That(initiatorKeysToAdd.Count, Is.EqualTo(0), "Duplicate key should not be added.");

            SetPrivateField(component, "actInitiatorKey", CreateInitiatorEntry(" nsb ", "  Added text "));
            GetPrivateMethod("AddInitiatorKey").Invoke(component, null);
            Assert.That(initiatorKeysToAdd.Count, Is.EqualTo(1));

            Dictionary<string, string> addedMap = ToDictionary(initiatorKeysToAdd);
            Assert.That(addedMap.ContainsKey("nsb"), Is.True);
            Assert.That(addedMap["nsb"], Is.EqualTo("Added text"));
        }

        [Test]
        public void PrepareConfigData_AppliesAddsAndDeletes_AndSerializesDictionary()
        {
            SettingsNotifications component = new();
            ConfigData configData = new();

            IList initiatorKeys = CreateInitiatorList();
            object entryUser = CreateInitiatorEntry("user", "by user");
            object entryNsb = CreateInitiatorEntry("nsb", "by nsb");
            initiatorKeys.Add(entryUser);
            initiatorKeys.Add(entryNsb);

            IList initiatorKeysToDelete = CreateInitiatorList();
            initiatorKeysToDelete.Add(entryUser);

            IList initiatorKeysToAdd = CreateInitiatorList();
            initiatorKeysToAdd.Add(CreateInitiatorEntry("app", "from app"));

            SetPrivateField(component, "configData", configData);
            SetPrivateField(component, "initiatorKeys", initiatorKeys);
            SetPrivateField(component, "initiatorKeysToDelete", initiatorKeysToDelete);
            SetPrivateField(component, "initiatorKeysToAdd", initiatorKeysToAdd);

            GetPrivateMethod("PrepareConfigData").Invoke(component, null);

            Dictionary<string, string>? serializedMap = JsonSerializer.Deserialize<Dictionary<string, string>>(configData.RuleExpiryInitiatorKeys);
            Assert.That(serializedMap, Is.Not.Null);
            Assert.That(serializedMap!.ContainsKey("user"), Is.False);
            Assert.That(serializedMap["nsb"], Is.EqualTo("by nsb"));
            Assert.That(serializedMap["app"], Is.EqualTo("from app"));
        }

        [Test]
        public void PrepareConfigData_PreservesUnknownStoredNotificationLanguage()
        {
            SettingsNotifications component = new();
            ConfigData configData = new()
            {
                NotificationLanguage = "French"
            };
            SimulatedGlobalConfig globalConfig = new()
            {
                UiLanguages =
                [
                    new Language { Name = "German", CultureInfo = "de-DE" },
                    new Language { Name = "English", CultureInfo = "en-US" }
                ]
            };

            SetInjectedGlobalConfig(component, globalConfig);
            SetPrivateField(component, "configData", configData);
            SetPrivateField(component, "selectedNotificationLanguage", new Language());
            SetPrivateField(component, "hasUnknownNotificationLanguageSelection", true);
            SetPrivateField(component, "initiatorKeys", CreateInitiatorList());
            SetPrivateField(component, "initiatorKeysToDelete", CreateInitiatorList());
            SetPrivateField(component, "initiatorKeysToAdd", CreateInitiatorList());

            GetPrivateMethod("PrepareConfigData").Invoke(component, null);

            Assert.That(configData.NotificationLanguage, Is.EqualTo("French"));
        }
    }
}

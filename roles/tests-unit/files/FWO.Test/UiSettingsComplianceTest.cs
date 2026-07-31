using FWO.Ui.Pages.Settings;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    internal class UiSettingsComplianceTest
    {
        [Test]
        public void DeserializeChangeIdKeys_ReadsJsonAndLegacyValues()
        {
            List<string> jsonKeys = InvokeDeserialize("[\"field-2\",\"ChangeId\"]");
            List<string> legacyKeys = InvokeDeserialize(" LegacyChangeId ");
            List<string> emptyKeys = InvokeDeserialize(" ");

            Assert.That(jsonKeys, Is.EqualTo(new List<string> { "field-2", "ChangeId" }));
            Assert.That(legacyKeys, Is.EqualTo(new List<string> { "LegacyChangeId" }));
            Assert.That(emptyKeys, Is.Empty);
        }

        [Test]
        public void AddChangeIdKey_TrimsAndRejectsDuplicateKeys()
        {
            SettingsCompliance component = new();
            SetField(component, "ChangeIdKeys", new List<string> { "field-2" });
            SetField(component, "ChangeIdKeysToAdd", new List<string>());
            SetField(component, "ActiveChangeIdKey", " TicketId ");

            InvokeInstanceMethod(component, "AddChangeIdKey");

            Assert.That(GetField<List<string>>(component, "ChangeIdKeysToAdd"), Is.EqualTo(new List<string> { "TicketId" }));
            Assert.That(GetField<string>(component, "ActiveChangeIdKey"), Is.Empty);

            SetField(component, "ActiveChangeIdKey", "field-2");
            InvokeInstanceMethod(component, "AddChangeIdKey");

            Assert.That(GetField<List<string>>(component, "ChangeIdKeysToAdd"), Has.Count.EqualTo(1));
        }

        [Test]
        public void PrepareChangeIdKeys_AppliesPendingChangesAndClearsQueues()
        {
            SettingsCompliance component = new();
            SetField(component, "ChangeIdKeys", new List<string> { "field-2", "obsolete" });
            SetField(component, "ChangeIdKeysToAdd", new List<string> { "ChangeId" });
            SetField(component, "ChangeIdKeysToDelete", new List<string> { "obsolete" });

            InvokeInstanceMethod(component, "PrepareChangeIdKeys");

            Assert.That(GetField<List<string>>(component, "ChangeIdKeys"), Is.EqualTo(new List<string> { "field-2", "ChangeId" }));
            Assert.That(GetField<List<string>>(component, "ChangeIdKeysToAdd"), Is.Empty);
            Assert.That(GetField<List<string>>(component, "ChangeIdKeysToDelete"), Is.Empty);
        }

        private static List<string> InvokeDeserialize(string value)
        {
            MethodInfo method = GetMethod("DeserializeChangeIdKeys", BindingFlags.NonPublic | BindingFlags.Static);
            object?[] arguments = new object?[1];
            arguments[0] = value;
            return (List<string>)(method.Invoke(null, arguments)
                ?? throw new InvalidOperationException("DeserializeChangeIdKeys returned null."));
        }

        private static void InvokeInstanceMethod(SettingsCompliance component, string methodName)
        {
            GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(component, null);
        }

        private static MethodInfo GetMethod(string methodName, BindingFlags bindingFlags)
        {
            return typeof(SettingsCompliance).GetMethod(methodName, bindingFlags)
                ?? throw new MissingMethodException(typeof(SettingsCompliance).FullName, methodName);
        }

        private static void SetField<T>(SettingsCompliance component, string fieldName, T value)
        {
            GetFieldInfo(fieldName).SetValue(component, value);
        }

        private static T GetField<T>(SettingsCompliance component, string fieldName)
        {
            return (T)(GetFieldInfo(fieldName).GetValue(component)
                ?? throw new InvalidOperationException($"Field {fieldName} returned null."));
        }

        private static FieldInfo GetFieldInfo(string fieldName)
        {
            return typeof(SettingsCompliance).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(typeof(SettingsCompliance).FullName, fieldName);
        }
    }
}

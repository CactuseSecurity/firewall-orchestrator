using System.Reflection;
using System.Text.Json;
using FWO.Config.Api.Data;
using FWO.Ui.Pages.Settings;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class UiSettingsLoggingTest
    {
        [Test]
        public void AddPath_AddsNonEmptyPathAndClearsInput()
        {
            SettingsLogging component = new();
            SetPrivateField(component, "activePath", "/usr/local/fworch/scripts/customizing/log_data_import/import_log_data_from_git.py");
            SetPrivateField(component, "pathsToAdd", new List<string>());

            InvokePrivateMethod("AddPath", component);

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<List<string>>(component, "pathsToAdd"), Has.One.EqualTo("/usr/local/fworch/scripts/customizing/log_data_import/import_log_data_from_git.py"));
                Assert.That(GetPrivateField<string>(component, "activePath"), Is.Empty);
            });
        }

        [Test]
        public void PrepareConfigData_RemovesFileExtensionBeforeSaving()
        {
            SettingsLogging component = new();
            SetPrivateField(component, "configData", new ConfigData());
            SetPrivateField(component, "logDataPaths", new List<string>());
            SetPrivateField(component, "pathsToAdd", new List<string> { "source.py" });
            SetPrivateField(component, "pathsToDelete", new List<string>());

            InvokePrivateMethod("PrepareConfigData", component);

            ConfigData configData = GetPrivateField<ConfigData>(component, "configData");
            List<string> paths = JsonSerializer.Deserialize<List<string>>(configData.ImportLogDataPath) ?? [];
            Assert.That(paths, Has.One.EqualTo("source"));
        }

        [Test]
        public void PrepareConfigData_KeepsTheDisplayedPathsWhenAPathIsRejected()
        {
            SettingsLogging component = new();
            SetPrivateField(component, "configData", new ConfigData());
            SetPrivateField(component, "logDataPaths", new List<string> { "log_data_import/source" });
            SetPrivateField(component, "pathsToAdd", new List<string> { "/etc/passwd" });
            SetPrivateField(component, "pathsToDelete", new List<string>());

            Assert.Throws<TargetInvocationException>(() => InvokePrivateMethod("PrepareConfigData", component));

            List<string> unchangedPaths = ["log_data_import/source"];
            Assert.That(GetPrivateField<List<string>>(component, "logDataPaths"), Is.EqualTo(unchangedPaths));
        }

        [Test]
        public void PrepareConfigData_DoesNotDuplicatePathsOnASecondAttempt()
        {
            SettingsLogging component = new();
            SetPrivateField(component, "configData", new ConfigData());
            SetPrivateField(component, "logDataPaths", new List<string>());
            SetPrivateField(component, "pathsToAdd", new List<string> { "log_data_import/source.py" });
            SetPrivateField(component, "pathsToDelete", new List<string>());

            InvokePrivateMethod("PrepareConfigData", component);
            InvokePrivateMethod("PrepareConfigData", component);

            Assert.That(GetPrivateField<List<string>>(component, "logDataPaths"), Has.Count.EqualTo(1));
        }

        [Test]
        public void PrepareConfigData_RaisesNonPositiveMaxEntriesToOne()
        {
            SettingsLogging component = CreateComponentWithMaxEntries(0);

            InvokePrivateMethod("PrepareConfigData", component);

            Assert.That(GetPrivateField<ConfigData>(component, "configData").ImportLogDataMaxEntries, Is.EqualTo(1));
        }

        [Test]
        public void PrepareConfigData_KeepsConfiguredMaxEntries()
        {
            SettingsLogging component = CreateComponentWithMaxEntries(5000);

            InvokePrivateMethod("PrepareConfigData", component);

            Assert.That(GetPrivateField<ConfigData>(component, "configData").ImportLogDataMaxEntries, Is.EqualTo(5000));
        }

        [Test]
        public void PrepareConfigData_RaisesNonPositiveRetentionToOneDay()
        {
            SettingsLogging component = CreateComponentWithMaxEntries(1000);
            GetPrivateField<ConfigData>(component, "configData").LogDataRetentionDays = 0;

            InvokePrivateMethod("PrepareConfigData", component);

            Assert.That(GetPrivateField<ConfigData>(component, "configData").LogDataRetentionDays, Is.EqualTo(1));
        }

        [Test]
        public void PrepareConfigData_KeepsConfiguredRetention()
        {
            SettingsLogging component = CreateComponentWithMaxEntries(1000);
            GetPrivateField<ConfigData>(component, "configData").LogDataRetentionDays = 90;

            InvokePrivateMethod("PrepareConfigData", component);

            Assert.That(GetPrivateField<ConfigData>(component, "configData").LogDataRetentionDays, Is.EqualTo(90));
        }

        private static SettingsLogging CreateComponentWithMaxEntries(int maxEntries)
        {
            SettingsLogging component = new();
            SetPrivateField(component, "configData", new ConfigData { ImportLogDataMaxEntries = maxEntries });
            SetPrivateField(component, "logDataPaths", new List<string>());
            SetPrivateField(component, "pathsToAdd", new List<string>());
            SetPrivateField(component, "pathsToDelete", new List<string>());
            return component;
        }

        private static void InvokePrivateMethod(string methodName, SettingsLogging component)
        {
            MethodInfo method = typeof(SettingsLogging).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(SettingsLogging).FullName, methodName);
            method.Invoke(component, null);
        }

        private static void SetPrivateField<T>(SettingsLogging component, string fieldName, T value)
        {
            FieldInfo field = typeof(SettingsLogging).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(typeof(SettingsLogging).FullName, fieldName);
            field.SetValue(component, value);
        }

        private static T GetPrivateField<T>(SettingsLogging component, string fieldName)
        {
            FieldInfo field = typeof(SettingsLogging).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(typeof(SettingsLogging).FullName, fieldName);
            return (T)field.GetValue(component)!;
        }
    }
}

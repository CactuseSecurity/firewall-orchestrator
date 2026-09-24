using System.Reflection;
using System.Text.Json;
using System.Linq;
using FWO.Config.Api.Data;
using FWO.Data.Enums;
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

        [Test]
        public async Task Save_PersistsPreparedPathsAndClampsMinimumValues()
        {
            SettingsLogging component = new();
            RecordingSettingsApiConn apiConnection = new();
            SimulatedGlobalConfig globalConfig = new()
            {
                ImportLogDataPath = JsonSerializer.Serialize(new List<string> { "existing" }),
                ImportLogDataScriptArgs = "--old",
                ImportLogDataSleepTime = 60,
                ImportLogDataSleepTimeUnit = LogDataImportIntervalUnit.Minutes,
                ImportLogDataMaxEntries = 5,
                LogDataRetentionDays = 10,
                AllowLogDataPortWithoutProtocol = false,
                ReplaceExistingLogData = false,
                ShowLogDataInConnections = false
            };
            SimulatedUserConfig userConfig = new();
            ConfigData editableConfig = await globalConfig.GetEditableConfig();
            editableConfig.ImportLogDataMaxEntries = 0;
            editableConfig.LogDataRetentionDays = 0;
            editableConfig.ImportLogDataScriptArgs = "--new";

            SetMember(component, "globalConfig", globalConfig);
            SetMember(component, "apiConnection", apiConnection);
            SetMember(component, "userConfig", userConfig);
            SetPrivateField(component, "configData", editableConfig);
            SetPrivateField(component, "logDataPaths", new List<string> { "existing" });
            SetPrivateField(component, "pathsToAdd", new List<string> { "/usr/local/fworch/scripts/customizing/log_data_import/import_log_data_from_git.py" });
            SetPrivateField(component, "pathsToDelete", new List<string>());

            await InvokePrivateMethodAsync("Save", component);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.UpsertConfigCallCount, Is.EqualTo(1));
                Assert.That(apiConnection.LastUpsertConfigItems.Single(item => item.Key == "importLogDataPath").Value, Does.Contain("\"/usr/local/fworch/scripts/customizing/log_data_import/import_log_data_from_git\""));
                Assert.That(apiConnection.LastUpsertConfigItems.Single(item => item.Key == "importLogDataMaxEntries").Value, Is.EqualTo("1"));
                Assert.That(apiConnection.LastUpsertConfigItems.Single(item => item.Key == "logDataRetentionDays").Value, Is.EqualTo("1"));
            });
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

        private static async Task InvokePrivateMethodAsync(string methodName, object component)
        {
            MethodInfo method = component.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(component.GetType().FullName, methodName);
            Task task = (Task)(method.Invoke(component, null) ?? throw new InvalidOperationException($"{methodName} returned null task."));
            await task;
        }

        private static void SetPrivateField<T>(SettingsLogging component, string fieldName, T value)
        {
            FieldInfo field = typeof(SettingsLogging).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(typeof(SettingsLogging).FullName, fieldName);
            field.SetValue(component, value);
        }

        private static void SetMember(object component, string memberName, object? value)
        {
            Type type = component.GetType();
            FieldInfo? field = type.GetField(memberName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(component, value);
                return;
            }

            PropertyInfo? property = type.GetProperty(memberName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (property != null)
            {
                property.SetValue(component, value);
                return;
            }

            throw new MissingMemberException(type.FullName, memberName);
        }

        private static T GetPrivateField<T>(SettingsLogging component, string fieldName)
        {
            FieldInfo field = typeof(SettingsLogging).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(typeof(SettingsLogging).FullName, fieldName);
            return (T)field.GetValue(component)!;
        }
    }
}

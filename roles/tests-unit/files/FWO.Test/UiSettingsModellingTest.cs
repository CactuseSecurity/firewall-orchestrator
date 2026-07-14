using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Ui.Pages.Settings;
using NUnit.Framework;
using System.Reflection;
using System.Text.Json;

namespace FWO.Test
{
    [TestFixture]
    internal class UiSettingsModellingTest
    {
        private static SettingsModelling CreateComponent()
        {
            return new SettingsModelling();
        }

        private static MethodInfo GetPrivateMethod(string name)
        {
            return typeof(SettingsModelling).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                ?? throw new MissingMethodException(typeof(SettingsModelling).FullName, name);
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

        private static void InvokePrivate(string name, object component, params object?[]? args)
        {
            MethodInfo method = GetPrivateMethod(name);
            method.Invoke(component, args);
        }

        [Test]
        public void OpenReducedProtocolSelection_SetsPopupFlag()
        {
            SettingsModelling component = CreateComponent();

            InvokePrivate("OpenReducedProtocolSelection", component);

            Assert.That(GetPrivateField<bool>(component, "reducedProtocolSelectionMode"), Is.True);
        }

        [Test]
        public void VarianceOptions_SetsPopupFlag()
        {
            SettingsModelling component = CreateComponent();

            InvokePrivate("VarianceOptions", component);

            Assert.That(GetPrivateField<bool>(component, "varOptMode"), Is.True);
        }

        [Test]
        public void ModIntegrationStates_SetsPopupFlag()
        {
            SettingsModelling component = CreateComponent();

            InvokePrivate("ModIntegrationStates", component);

            Assert.That(GetPrivateField<bool>(component, "modIntegrationStatesMode"), Is.True);
        }

        [Test]
        public void AddExtraConfig_AddsNonEmptyValueAndClearsInput()
        {
            SettingsModelling component = CreateComponent();
            SetPrivateField(component, "actExtraConfig", "  keep me  ");
            SetPrivateField(component, "ExtraConfigsToAdd", new List<string>());

            InvokePrivate("AddExtraConfig", component);

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<List<string>>(component, "ExtraConfigsToAdd"), Is.EqualTo(new List<string> { "  keep me  " }));
                Assert.That(GetPrivateField<string>(component, "actExtraConfig"), Is.Empty);
            });
        }

        [Test]
        public void AddExtraConfig_IgnoresEmptyValue()
        {
            SettingsModelling component = CreateComponent();
            SetPrivateField(component, "actExtraConfig", "");
            SetPrivateField(component, "ExtraConfigsToAdd", new List<string> { "existing" });

            InvokePrivate("AddExtraConfig", component);

            Assert.That(GetPrivateField<List<string>>(component, "ExtraConfigsToAdd"), Is.EqualTo(new List<string> { "existing" }));
        }

        [Test]
        public void AddAppServerType_AddsUniqueEntryAndResetsInput()
        {
            SettingsModelling component = CreateComponent();
            SetPrivateField(component, "appServerTypes", new List<AppServerType>());
            SetPrivateField(component, "appServerTypesToAdd", new List<AppServerType>());
            SetPrivateField(component, "actAppServerType", new AppServerType { Id = 5, Name = "Type 5" });

            InvokePrivate("AddAppServerType", component);

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<List<AppServerType>>(component, "appServerTypesToAdd"), Has.Count.EqualTo(1));
                Assert.That(GetPrivateField<List<AppServerType>>(component, "appServerTypesToAdd")[0].Id, Is.EqualTo(5));
                Assert.That(GetPrivateField<AppServerType>(component, "actAppServerType").Id, Is.EqualTo(0));
            });
        }

        [Test]
        public void AddAppServerType_IgnoresDuplicates()
        {
            SettingsModelling component = CreateComponent();
            SetPrivateField(component, "appServerTypes", new List<AppServerType> { new() { Id = 5, Name = "Existing" } });
            SetPrivateField(component, "appServerTypesToAdd", new List<AppServerType>());
            SetPrivateField(component, "actAppServerType", new AppServerType { Id = 5, Name = "Duplicate" });

            InvokePrivate("AddAppServerType", component);

            Assert.That(GetPrivateField<List<AppServerType>>(component, "appServerTypesToAdd"), Is.Empty);
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
        public void PrepareAreas_RemovesDeletedEntriesAndSerializesRemainingAreas()
        {
            SettingsModelling component = CreateComponent();
            SetPrivateField(component, "configData", new ConfigData());
            SetPrivateField(component, "allAreas", new List<ModellingNwGroup>
            {
                new() { Id = 1, Name = "Area 1" },
                new() { Id = 2, Name = "Area 2" }
            });
            SetPrivateField(component, "CommonAreas", new List<CommonArea>
            {
                new() { Area = new() { Content = new ModellingNwGroup { Id = 1, Name = "Area 1" } }, UseInSrc = true, UseInDst = false },
                new() { Area = new() { Content = new ModellingNwGroup { Id = 2, Name = "Area 2" } }, UseInSrc = false, UseInDst = true }
            });
            SetPrivateField(component, "CommAreasToDelete", new List<CommonArea>
            {
                new() { Area = new() { Content = new ModellingNwGroup { Id = 2, Name = "Area 2" } } }
            });

            string serialized = (string)GetPrivateMethod("PrepareAreas").Invoke(component, [GetPrivateField<List<CommonArea>>(component, "CommonAreas"), GetPrivateField<List<CommonArea>>(component, "CommAreasToDelete")])!;
            List<CommonAreaConfig>? parsed = JsonSerializer.Deserialize<List<CommonAreaConfig>>(serialized);

            Assert.That(parsed, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(parsed, Has.Count.EqualTo(1));
                Assert.That(parsed![0].AreaId, Is.EqualTo(1));
                Assert.That(parsed[0].UseInSrc, Is.True);
                Assert.That(parsed[0].UseInDst, Is.False);
            });
        }

        [Test]
        public void PrepareAppServerTypes_UpdatesDefaultEntryAndSerializesRemainingTypes()
        {
            SettingsModelling component = CreateComponent();
            SetPrivateField(component, "configData", new ConfigData());
            AppServerType typeToRemove = new() { Id = 1, Name = "Remove me" };
            SetPrivateField(component, "appServerTypes", new List<AppServerType>
            {
                new() { Id = 0, Name = "Old default" },
                typeToRemove
            });
            SetPrivateField(component, "appServerTypesToAdd", new List<AppServerType>
            {
                new() { Id = 2, Name = "Add me" }
            });
            SetPrivateField(component, "appServerTypesToDelete", new List<AppServerType>
            {
                typeToRemove
            });
            SetPrivateField(component, "appServerDefaultTypeName", "Updated default");

            InvokePrivate("PrepareAppServerTypes", component);

            string serialized = GetPrivateField<ConfigData>(component, "configData").ModAppServerTypes;
            List<AppServerType>? parsed = JsonSerializer.Deserialize<List<AppServerType>>(serialized);

            Assert.That(parsed, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(parsed, Has.Count.EqualTo(2));
                Assert.That(parsed!.Any(type => type.Id == 0 && type.Name == "Updated default"), Is.True);
                Assert.That(parsed!.Any(type => type.Id == 1), Is.False);
                Assert.That(parsed!.Any(type => type.Id == 2 && type.Name == "Add me"), Is.True);
            });
        }

        [Test]
        public void PrepareConfigData_SerializesRulesAndTimeFields()
        {
            SettingsModelling component = CreateComponent();
            ConfigData configData = new();
            SetPrivateField(component, "configData", configData);
            SetPrivateField(component, "namingConvention", new ModellingNamingConvention
            {
                NetworkAreaRequired = true,
                UseAppPart = true,
                FixedPartLength = 12,
                FreePartLength = 7,
                NetworkAreaPattern = "net-*",
                AppRolePattern = "app-*",
                AppZone = "zone-*",
                AppServerPrefix = "srv-",
                NetworkPrefix = "net-",
                IpRangePrefix = "range-"
            });
            SetPrivateField(component, "ruleRecognitionOption", new RuleRecognitionOption
            {
                NwRegardIp = false,
                NwRegardName = true,
                NwRegardGroupName = true,
                NwResolveGroup = true,
                NwSeparateGroupAnalysis = false,
                SvcRegardPortAndProt = false,
                SvcRegardName = true,
                SvcRegardGroupName = true,
                SvcResolveGroup = false,
                SvcSplitPortRanges = true
            });
            SetPrivateField(component, "varAnalysisDate", new DateTime(2026, 7, 5, 0, 0, 0, DateTimeKind.Utc));
            SetPrivateField(component, "varAnalysisTime", new DateTime(2026, 7, 5, 14, 35, 0, DateTimeKind.Utc));
            SetPrivateField(component, "ExtraConfigs", new List<string> { "first" });
            SetPrivateField(component, "ExtraConfigsToAdd", new List<string> { "second" });
            SetPrivateField(component, "ExtraConfigsToDelete", new List<string>());
            SetPrivateField(component, "activeOwnerResponsibleTypes", new List<OwnerResponsibleType>
            {
                new() { Id = 1, Active = true, Name = "Main" },
                new() { Id = 2, Active = true, Name = "Supporting" }
            });
            SetPrivateField(component, "modReqEmailRecipients", new EmailRecipientSelection
            {
                OtherAddresses = true,
                OtherAddressList = ["mail@example.org"],
                OwnerResponsibleTypeIds = [1]
            });
            SetPrivateField(component, "modDecommEmailRecipients", new EmailRecipientSelection
            {
                OwnerResponsibleTypeIds = [2]
            });
            SetPrivateField(component, "appServerTypes", new List<AppServerType> { new() { Id = 0, Name = "Default" } });
            SetPrivateField(component, "appServerTypesToAdd", new List<AppServerType>());
            SetPrivateField(component, "appServerTypesToDelete", new List<AppServerType>());
            SetPrivateField(component, "CommonAreas", new List<CommonArea>());
            SetPrivateField(component, "CommAreasToDelete", new List<CommonArea>());
            SetPrivateField(component, "SpecUserAreas", new List<CommonArea>());
            SetPrivateField(component, "SpecUserAreasToDelete", new List<CommonArea>());
            SetPrivateField(component, "UpdObjAreas", new List<CommonArea>());
            SetPrivateField(component, "UpdObjAreasToDelete", new List<CommonArea>());

            InvokePrivate("PrepareConfigData", component);

            Assert.Multiple(() =>
            {
                Assert.That(configData.ModNamingConvention, Does.Contain("\"networkAreaRequired\":true"));
                Assert.That(configData.RuleRecognitionOption, Does.Contain("\"nwRegardName\":true"));
                Assert.That(configData.VarianceAnalysisStartAt, Is.EqualTo(new DateTime(2026, 7, 5, 14, 35, 0, DateTimeKind.Utc)));
                Assert.That(configData.ModReqEmailReceiver, Does.Contain("\"owner_responsible_type_ids\":[1]"));
                Assert.That(configData.ModDecommEmailReceiver, Does.Contain("\"owner_responsible_type_ids\":[2]"));
                Assert.That(configData.ModReqEmailOtherAddresses, Is.Empty);
                Assert.That(configData.ModDecommEmailOtherAddresses, Is.Empty);
                Assert.That(configData.ModExtraConfigs, Does.Contain("second"));
            });
        }
    }
}

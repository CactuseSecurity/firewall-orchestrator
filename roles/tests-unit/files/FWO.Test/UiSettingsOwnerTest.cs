using FWO.Basics;
using FWO.Data;
using FWO.Ui.Pages.Settings;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    internal class UiSettingsOwnerTest
    {
        private static MethodInfo GetPrivateMethod(string name)
        {
            return typeof(SettingsOwner).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(SettingsOwner).FullName, name);
        }

        private static MethodInfo GetPrivateStaticMethod(string name)
        {
            return typeof(SettingsOwner).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(typeof(SettingsOwner).FullName, name);
        }

        private static void SetPrivateField<T>(SettingsOwner component, string fieldName, T value)
        {
            FieldInfo? field = typeof(SettingsOwner).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(typeof(SettingsOwner).FullName, fieldName);
            }
            field.SetValue(component, value);
        }

        private static T GetPrivateField<T>(SettingsOwner component, string fieldName)
        {
            FieldInfo? field = typeof(SettingsOwner).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(typeof(SettingsOwner).FullName, fieldName);
            }
            return (T)field.GetValue(component)!;
        }

        private static void SetInjectedUserConfig(SettingsOwner component, EditOwnerTestUserConfig userConfig)
        {
            PropertyInfo? prop = typeof(SettingsOwner).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(p => p.PropertyType == typeof(FWO.Config.Api.UserConfig)
                    && (p.Name.Equals("userConfig", StringComparison.OrdinalIgnoreCase)
                        || p.Name.Equals("UserConfig", StringComparison.OrdinalIgnoreCase)));
            if (prop != null)
            {
                prop.SetValue(component, userConfig);
                return;
            }

            FieldInfo? field = typeof(SettingsOwner).GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(f => f.FieldType == typeof(FWO.Config.Api.UserConfig));
            if (field != null)
            {
                field.SetValue(component, userConfig);
                return;
            }

            throw new MissingFieldException(typeof(SettingsOwner).FullName, "userConfig/UserConfig");
        }

        [Test]
        public void AnalyseSampleOwners_ShowsCleanup_WhenDemoOwnersExist()
        {
            SettingsOwner component = new();
            SetPrivateField(component, "Owners", new List<FwoOwner>
            {
                new() { Id = 1, Name = $"App{GlobalConst.k_demo}" },
                new() { Id = 2, Name = "RegularApp" }
            });

            GetPrivateMethod("AnalyseSampleOwners").Invoke(component, null);

            Assert.That(GetPrivateField<bool>(component, "ShowCleanupButton"), Is.True);
            Assert.That(GetPrivateField<List<FwoOwner>>(component, "SampleOwners"), Has.Count.EqualTo(1));
        }

        [Test]
        public void AnalyseSampleOwners_HidesCleanup_WhenNoDemoOwnersExist()
        {
            SettingsOwner component = new();
            SetPrivateField(component, "Owners", new List<FwoOwner>
            {
                new() { Id = 1, Name = "RegularApp1" },
                new() { Id = 2, Name = "RegularApp2" }
            });

            GetPrivateMethod("AnalyseSampleOwners").Invoke(component, null);

            Assert.That(GetPrivateField<bool>(component, "ShowCleanupButton"), Is.False);
            Assert.That(GetPrivateField<List<FwoOwner>>(component, "SampleOwners"), Is.Empty);
        }

        [Test]
        public void OpenOwnerMapping_SetsOwnerMappingMode()
        {
            SettingsOwner component = new();

            GetPrivateMethod("OpenOwnerMapping").Invoke(component, null);

            Assert.That(GetPrivateField<bool>(component, "OwnerMappingMode"), Is.True);
        }

        [Test]
        public void ManageLifeCycles_SetsManageLifeCyclesMode()
        {
            SettingsOwner component = new();

            GetPrivateMethod("ManageLifeCycles").Invoke(component, null);

            Assert.That(GetPrivateField<bool>(component, "ManageLifeCyclesMode"), Is.True);
        }

        [Test]
        public void RequestDeleteOwner_SetsCurrentOwnerAndDeleteMode()
        {
            SettingsOwner component = new();
            FwoOwner owner = new() { Id = 77, Name = "OwnerX" };

            GetPrivateMethod("RequestDeleteOwner").Invoke(component, [owner]);

            Assert.That(GetPrivateField<bool>(component, "DeleteOwnerMode"), Is.True);
            Assert.That(GetPrivateField<FwoOwner>(component, "ActOwner").Id, Is.EqualTo(77));
        }

        [Test]
        public async Task HandleOwnerSaved_ResetsEditFlags_AndReanalysesSamples()
        {
            SettingsOwner component = new();
            SetPrivateField(component, "Owners", new List<FwoOwner>
            {
                new() { Id = 1, Name = $"Demo{GlobalConst.k_demo}" }
            });
            SetPrivateField(component, "EditOwnerMode", true);
            SetPrivateField(component, "Readonly", true);

            Task task = (Task)GetPrivateMethod("HandleOwnerSaved").Invoke(component, null)!;
            await task;

            Assert.That(GetPrivateField<bool>(component, "EditOwnerMode"), Is.False);
            Assert.That(GetPrivateField<bool>(component, "Readonly"), Is.False);
            Assert.That(GetPrivateField<bool>(component, "ShowCleanupButton"), Is.True);
        }

        [Test]
        public async Task CancelOwnerEdit_ResetsEditFlags()
        {
            SettingsOwner component = new();
            SetPrivateField(component, "EditOwnerMode", true);
            SetPrivateField(component, "Readonly", true);

            Task task = (Task)GetPrivateMethod("CancelOwnerEdit").Invoke(component, null)!;
            await task;

            Assert.That(GetPrivateField<bool>(component, "EditOwnerMode"), Is.False);
            Assert.That(GetPrivateField<bool>(component, "Readonly"), Is.False);
        }

        [Test]
        public void RequestRemoveSampleData_EnablesCleanupModeAndMessage()
        {
            SettingsOwner component = new();
            SetInjectedUserConfig(component, new EditOwnerTestUserConfig());

            GetPrivateMethod("RequestRemoveSampleData").Invoke(component, null);

            Assert.That(GetPrivateField<bool>(component, "CleanupMode"), Is.True);
            Assert.That(GetPrivateField<string>(component, "CleanupMessage"), Is.EqualTo("U5218"));
        }

        [Test]
        public void FormatOwnerResponsibles_FormatsAndJoinsValues()
        {
            string dnUser = "CN=Max Mustermann,OU=Users,DC=example,DC=com";
            string dnGroup = "CN=NetOps,OU=Groups,DC=example,DC=com";
            string dnRaw = "invalid-dn";

            string formatted = (string)GetPrivateStaticMethod("FormatOwnerResponsibles").Invoke(null, [new List<string> { dnUser, dnGroup, dnRaw }])!;

            Assert.That(formatted, Does.Contain("Max Mustermann"));
            Assert.That(formatted, Does.Contain("NetOps"));
            Assert.That(formatted, Does.Contain("invalid-dn"));
            Assert.That(formatted, Does.Contain(","));
        }

        [Test]
        public void OrderActiveResponsibleTypes_FiltersInactiveAndSorts()
        {
            List<OwnerResponsibleType> input =
            [
                new() { Id = 10, Name = "Zulu", SortOrder = 2, Active = true },
                new() { Id = 20, Name = "Alpha", SortOrder = 2, Active = true },
                new() { Id = 30, Name = "Inactive", SortOrder = 1, Active = false },
                new() { Id = 40, Name = "Beta", SortOrder = 1, Active = true }
            ];

            SettingsOwner component = new();
            List<OwnerResponsibleType> result = (List<OwnerResponsibleType>)GetPrivateMethod("OrderActiveResponsibleTypes")
                .Invoke(component, [input])!;

            Assert.That(result.Select(type => type.Id).ToList(), Is.EqualTo(new List<int> { 40, 20, 10 }));
            Assert.That(result.All(type => type.Active), Is.True);
        }
    }
}

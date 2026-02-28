using System;
using System.Collections.Generic;
using System.Reflection;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Middleware.Server;
using NUnit.Framework;
using NUnit.Framework.Legacy;


namespace FWO.Test
{
    [TestFixture]
    internal class AppDataImportTest
    {

        [SetUp]
        public void Initialize()
        {
            // No setup required for these tests
        }

        [Test]
        public void ResolveOwnerGroupPathPrefersWritePath()
        {
            Ldap ldap = new()
            {
                GroupWritePath = "ou=groups,dc=example,dc=com",
                GroupSearchPath = "ou=search,dc=example,dc=com"
            };

            string resolved = InvokeResolveOwnerGroupPath(ldap);

            Assert.That(resolved, Is.EqualTo("ou=groups,dc=example,dc=com"));
        }

        [Test]
        public void ResolveOwnerGroupPathFallsBackToSearchPath()
        {
            Ldap ldap = new()
            {
                GroupWritePath = "",
                GroupSearchPath = "ou=search,dc=example,dc=com"
            };

            string resolved = InvokeResolveOwnerGroupPath(ldap);

            ClassicAssert.That(resolved, Is.EqualTo("ou=search,dc=example,dc=com"));
        }

        [Test]
        public void ResolveOwnerGroupPathSkipsWhitespaceWritePath()
        {
            Ldap ldap = new()
            {
                GroupWritePath = "   ",
                GroupSearchPath = "ou=search,dc=example,dc=com"
            };

            string resolved = InvokeResolveOwnerGroupPath(ldap);

            Assert.That(resolved, Is.EqualTo("ou=search,dc=example,dc=com"));
        }

        [Test]
        public void ResolveOwnerGroupPathThrowsWhenMissing()
        {
            Ldap ldap = new()
            {
                GroupWritePath = "",
                GroupSearchPath = ""
            };

            Assert.That(
                () => InvokeResolveOwnerGroupPath(ldap),
                Throws.TypeOf<TargetInvocationException>()
                    .With.InnerException.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void BuildOwnerResponsibles_UsesConfiguredTypeNames()
        {
            AppDataImport import = CreateImportWithTypeMap(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Main"] = 1,
                ["Supporting"] = 2
            });
            ModellingImportAppData incomingApp = new()
            {
                Name = "App-A",
                ExtAppId = "APP-1",
                Responsibles = new Dictionary<string, List<string>>
                {
                    ["Main"] = ["cn=user1,dc=example,dc=com"],
                    ["Supporting"] = ["cn=group1,dc=example,dc=com"]
                }
            };

            List<OwnerResponsible> result = InvokeBuildOwnerResponsibles(import, incomingApp, "cn=fallback,dc=example,dc=com", []);

            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result.Exists(r => r.Dn == "cn=user1,dc=example,dc=com" && r.ResponsibleTypeId == 1), Is.True);
            Assert.That(result.Exists(r => r.Dn == "cn=group1,dc=example,dc=com" && r.ResponsibleTypeId == 2), Is.True);
        }

        [Test]
        public void BuildOwnerResponsibles_SkipsUnknownTypesAndContinues()
        {
            AppDataImport import = CreateImportWithTypeMap(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Known"] = 7
            });
            ModellingImportAppData incomingApp = new()
            {
                Name = "App-B",
                ExtAppId = "APP-2",
                Responsibles = new Dictionary<string, List<string>>
                {
                    ["Unknown"] = ["cn=ignored,dc=example,dc=com"],
                    ["Known"] = ["cn=kept,dc=example,dc=com"]
                }
            };

            List<OwnerResponsible> result = InvokeBuildOwnerResponsibles(import, incomingApp, "cn=fallback,dc=example,dc=com", []);

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].Dn, Is.EqualTo("cn=kept,dc=example,dc=com"));
            Assert.That(result[0].ResponsibleTypeId, Is.EqualTo(7));
        }

        [Test]
        public void BuildOwnerResponsibles_DeduplicatesPerTypeAndDnAndTrims()
        {
            AppDataImport import = CreateImportWithTypeMap(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Main"] = 1
            });
            ModellingImportAppData incomingApp = new()
            {
                Name = "App-C",
                ExtAppId = "APP-3",
                Responsibles = new Dictionary<string, List<string>>
                {
                    ["Main"] =
                    [
                        " cn=user1,dc=example,dc=com ",
                        "cn=user1,dc=example,dc=com",
                        "   "
                    ]
                }
            };

            List<OwnerResponsible> result = InvokeBuildOwnerResponsibles(import, incomingApp, "cn=fallback,dc=example,dc=com", []);

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].Dn, Is.EqualTo("cn=user1,dc=example,dc=com"));
            Assert.That(result[0].ResponsibleTypeId, Is.EqualTo(1));
        }

        [Test]
        public void BuildOwnerResponsibles_FallsBackToLegacyWhenResponsiblesMissing()
        {
            AppDataImport import = CreateImportWithTypeMap(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
            ModellingImportAppData incomingApp = new()
            {
                Name = "App-D",
                ExtAppId = "APP-4",
                MainUser = "cn=main,dc=example,dc=com",
                Responsibles = null
            };

            List<OwnerResponsible> result = InvokeBuildOwnerResponsibles(
                import,
                incomingApp,
                "cn=support,dc=example,dc=com",
                ["cn=optional,dc=example,dc=com"]);

            Assert.That(result, Has.Count.EqualTo(3));
            Assert.That(result.Exists(r => r.Dn == "cn=main,dc=example,dc=com" && r.ResponsibleTypeId == 1), Is.True);
            Assert.That(result.Exists(r => r.Dn == "cn=support,dc=example,dc=com" && r.ResponsibleTypeId == 2), Is.True);
            Assert.That(result.Exists(r => r.Dn == "cn=optional,dc=example,dc=com" && r.ResponsibleTypeId == 3), Is.True);
        }

        [Test]
        public void BuildOwnerResponsibles_DoesNotUseLegacyWhenResponsiblesPresentButEmpty()
        {
            AppDataImport import = CreateImportWithTypeMap(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Main"] = 1
            });
            ModellingImportAppData incomingApp = new()
            {
                Name = "App-E",
                ExtAppId = "APP-5",
                MainUser = "cn=main,dc=example,dc=com",
                Responsibles = new Dictionary<string, List<string>>
                {
                    ["Main"] = []
                }
            };

            List<OwnerResponsible> result = InvokeBuildOwnerResponsibles(import, incomingApp, "cn=support,dc=example,dc=com", []);

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void BuildOwnerResponsibles_UsesCaseInsensitiveTypeLookup()
        {
            AppDataImport import = CreateImportWithTypeMap(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Main"] = 1
            });
            ModellingImportAppData incomingApp = new()
            {
                Name = "App-F",
                ExtAppId = "APP-6",
                Responsibles = new Dictionary<string, List<string>>
                {
                    ["mAiN"] = ["cn=user1,dc=example,dc=com"]
                }
            };

            List<OwnerResponsible> result = InvokeBuildOwnerResponsibles(import, incomingApp, "cn=fallback,dc=example,dc=com", []);

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].ResponsibleTypeId, Is.EqualTo(1));
        }

        [Test]
        public void BuildOwnerResponsibles_KeepsSameDnAcrossDifferentTypes()
        {
            AppDataImport import = CreateImportWithTypeMap(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Main"] = 1,
                ["Supporting"] = 2
            });
            ModellingImportAppData incomingApp = new()
            {
                Name = "App-G",
                ExtAppId = "APP-7",
                Responsibles = new Dictionary<string, List<string>>
                {
                    ["Main"] = ["cn=user1,dc=example,dc=com"],
                    ["Supporting"] = ["cn=user1,dc=example,dc=com"]
                }
            };

            List<OwnerResponsible> result = InvokeBuildOwnerResponsibles(import, incomingApp, "cn=fallback,dc=example,dc=com", []);

            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result.Exists(r => r.ResponsibleTypeId == 1), Is.True);
            Assert.That(result.Exists(r => r.ResponsibleTypeId == 2), Is.True);
        }

        [Test]
        public void TryResolveOwnerLifeCycleStateId_ReturnsTrueAndNullForEmptyState()
        {
            AppDataImport import = CreateImportWithTypeMap(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
            SetOwnerLifeCycleMap(import, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Active"] = 10
            });
            ModellingImportAppData incomingApp = new()
            {
                Name = "App-L1",
                ExtAppId = "APP-L1",
                OwnerLifecycleState = "   "
            };

            (bool ok, int? id) = InvokeTryResolveOwnerLifeCycleStateId(import, incomingApp);

            Assert.That(ok, Is.True);
            Assert.That(id, Is.Null);
        }

        [Test]
        public void TryResolveOwnerLifeCycleStateId_ResolvesKnownState_CaseInsensitiveAndTrimmed()
        {
            AppDataImport import = CreateImportWithTypeMap(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
            SetOwnerLifeCycleMap(import, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Active"] = 10,
                ["Frozen"] = 20
            });
            ModellingImportAppData incomingApp = new()
            {
                Name = "App-L2",
                ExtAppId = "APP-L2",
                OwnerLifecycleState = "  frozen  "
            };

            (bool ok, int? id) = InvokeTryResolveOwnerLifeCycleStateId(import, incomingApp);

            Assert.That(ok, Is.True);
            Assert.That(id, Is.EqualTo(20));
        }

        [Test]
        public void TryResolveOwnerLifeCycleStateId_ReturnsFalseForUnknownState()
        {
            AppDataImport import = CreateImportWithTypeMap(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
            SetOwnerLifeCycleMap(import, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Active"] = 10
            });
            ModellingImportAppData incomingApp = new()
            {
                Name = "App-L3",
                ExtAppId = "APP-L3",
                OwnerLifecycleState = "UnknownState"
            };

            (bool ok, int? id) = InvokeTryResolveOwnerLifeCycleStateId(import, incomingApp);

            Assert.That(ok, Is.False);
            Assert.That(id, Is.Null);
        }

        [Test]
        public void GetGroupName_UsesExternalAppIdPlaceholder()
        {
            AppDataImport import = CreateImportWithTypeMap(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
            SetOwnerGroupPattern(import, $"grp-{Placeholder.ExternalAppId}");

            string groupName = InvokeGetGroupName(import, "ABC-123");

            Assert.That(groupName, Is.EqualTo("grp-ABC-123"));
        }

        [Test]
        public void GetGroupName_UsesAppPrefixAndAppIdPlaceholders()
        {
            AppDataImport import = CreateImportWithTypeMap(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
            SetOwnerGroupPattern(import, $"grp-{Placeholder.AppPrefix}-{Placeholder.AppId}");

            string groupName = InvokeGetGroupName(import, "APP-4711");

            Assert.That(groupName, Is.EqualTo("grp-APP-4711"));
        }

        [Test]
        public void GetGroupName_ReturnsPatternUnchangedWhenNoKnownPlaceholderExists()
        {
            AppDataImport import = CreateImportWithTypeMap(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
            SetOwnerGroupPattern(import, "grp-static-name");

            string groupName = InvokeGetGroupName(import, "APP-9999");

            Assert.That(groupName, Is.EqualTo("grp-static-name"));
        }

        private static string InvokeResolveOwnerGroupPath(Ldap ldap)
        {
            MethodInfo method = typeof(AppDataImport).GetMethod(
                "ResolveOwnerGroupPath",
                BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("ResolveOwnerGroupPath helper not found.");
            return (string)method.Invoke(null, [ldap])!;
        }

        private static AppDataImport CreateImportWithTypeMap(Dictionary<string, int> typeMap)
        {
            AppDataImport import = new(new SimulatedApiConnection(), new GlobalConfig());
            FieldInfo field = typeof(AppDataImport).GetField("ownerResponsibleTypeIdByName", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("ownerResponsibleTypeIdByName field not found.");
            field.SetValue(import, typeMap);
            return import;
        }

        private static void SetOwnerLifeCycleMap(AppDataImport import, Dictionary<string, int> stateMap)
        {
            FieldInfo field = typeof(AppDataImport).GetField("ownerLifeCycleStateIdsByName", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("ownerLifeCycleStateIdsByName field not found.");
            field.SetValue(import, stateMap);
        }

        private static void SetOwnerGroupPattern(AppDataImport import, string pattern)
        {
            FieldInfo field = typeof(DataImportBase).GetField("globalConfig", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("globalConfig field not found.");
            GlobalConfig globalConfig = (GlobalConfig)field.GetValue(import)!;
            globalConfig.OwnerLdapGroupNames = pattern;
        }

        private static List<OwnerResponsible> InvokeBuildOwnerResponsibles(AppDataImport import, ModellingImportAppData incomingApp, string userGroupDn, IEnumerable<string> extraDns)
        {
            MethodInfo method = typeof(AppDataImport).GetMethod(
                "BuildOwnerResponsibles",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("BuildOwnerResponsibles helper not found.");
            return (List<OwnerResponsible>)method.Invoke(import, [incomingApp, userGroupDn, extraDns])!;
        }

        private static (bool ok, int? id) InvokeTryResolveOwnerLifeCycleStateId(AppDataImport import, ModellingImportAppData incomingApp)
        {
            MethodInfo method = typeof(AppDataImport).GetMethod(
                "TryResolveOwnerLifeCycleStateId",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("TryResolveOwnerLifeCycleStateId helper not found.");

            object?[] args = [incomingApp, null];
            bool ok = (bool)method.Invoke(import, args)!;
            return (ok, (int?)args[1]);
        }

        private static string InvokeGetGroupName(AppDataImport import, string extAppId)
        {
            MethodInfo method = typeof(AppDataImport).GetMethod(
                "GetGroupName",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("GetGroupName helper not found.");
            return (string)method.Invoke(import, [extAppId])!;
        }

    }
}

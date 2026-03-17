using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Reflection;
using FWO.Api.Client;
using FWO.Basics;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
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
        public void BuildOwnerResponsibles_UsesSortOrderKeyPositions()
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
                    ["1"] = ["cn=user1,dc=example,dc=com"],
                    ["2"] = ["cn=group1,dc=example,dc=com"]
                }
            };

            List<OwnerResponsible> result = InvokeBuildOwnerResponsibles(import, incomingApp, "cn=fallback,dc=example,dc=com", []);

            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result.Exists(r => r.Dn == "cn=user1,dc=example,dc=com" && r.ResponsibleTypeId == 1), Is.True);
            Assert.That(result.Exists(r => r.Dn == "cn=group1,dc=example,dc=com" && r.ResponsibleTypeId == 2), Is.True);
        }

        [Test]
        public void BuildOwnerResponsibles_MapsNumericKeysBySortOrderPosition()
        {
            AppDataImport import = CreateImportWithTypeMap(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Main"] = 1,
                ["Supporting"] = 2,
                ["Escalation"] = 3
            });
            SetResponsibleTypes(import,
            [
                new OwnerResponsibleType { Id = 2, Name = "Supporting", SortOrder = 1 },
                new OwnerResponsibleType { Id = 1, Name = "Main", SortOrder = 2 },
                new OwnerResponsibleType { Id = 3, Name = "Escalation", SortOrder = 3 }
            ]);
            ModellingImportAppData incomingApp = new()
            {
                Name = "App-A1",
                ExtAppId = "APP-1A",
                Responsibles = new Dictionary<string, List<string>>
                {
                    ["1"] = ["cn=first,dc=example,dc=com"],
                    ["2"] = ["cn=second,dc=example,dc=com"]
                }
            };

            List<OwnerResponsible> result = InvokeBuildOwnerResponsibles(import, incomingApp, "cn=fallback,dc=example,dc=com", []);

            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result.Exists(r => r.Dn == "cn=first,dc=example,dc=com" && r.ResponsibleTypeId == 2), Is.True);
            Assert.That(result.Exists(r => r.Dn == "cn=second,dc=example,dc=com" && r.ResponsibleTypeId == 1), Is.True);
        }

        [Test]
        public void BuildOwnerResponsibles_MapsNonSequentialNumericKeysByOrder()
        {
            AppDataImport import = CreateImportWithTypeMap(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Main"] = 1,
                ["Supporting"] = 2
            });
            SetResponsibleTypes(import,
            [
                new OwnerResponsibleType { Id = 2, Name = "Supporting", SortOrder = 1 },
                new OwnerResponsibleType { Id = 1, Name = "Main", SortOrder = 2 }
            ]);
            ModellingImportAppData incomingApp = new()
            {
                Name = "App-A2",
                ExtAppId = "APP-1B",
                Responsibles = new Dictionary<string, List<string>>
                {
                    ["10"] = ["cn=first,dc=example,dc=com"],
                    ["20"] = ["cn=second,dc=example,dc=com"]
                }
            };

            List<OwnerResponsible> result = InvokeBuildOwnerResponsibles(import, incomingApp, "cn=fallback,dc=example,dc=com", []);

            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result.Exists(r => r.Dn == "cn=first,dc=example,dc=com" && r.ResponsibleTypeId == 2), Is.True);
            Assert.That(result.Exists(r => r.Dn == "cn=second,dc=example,dc=com" && r.ResponsibleTypeId == 1), Is.True);
        }

        [Test]
        public void BuildOwnerResponsibles_MapsOnlyActiveTypesByOrder()
        {
            AppDataImport import = CreateImportWithTypeMap(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Main"] = 1,
                ["Supporting"] = 2
            });
            SetResponsibleTypes(import,
            [
                new OwnerResponsibleType { Id = 1, Name = "Main", SortOrder = 1, Active = false },
                new OwnerResponsibleType { Id = 2, Name = "Supporting", SortOrder = 2, Active = true }
            ]);
            ModellingImportAppData incomingApp = new()
            {
                Name = "App-A2-ActiveOnly",
                ExtAppId = "APP-1B-2",
                Responsibles = new Dictionary<string, List<string>>
                {
                    ["1"] = ["cn=first,dc=example,dc=com"],
                    ["2"] = ["cn=second,dc=example,dc=com"]
                }
            };

            List<OwnerResponsible> result = InvokeBuildOwnerResponsibles(import, incomingApp, "cn=fallback,dc=example,dc=com", []);

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result.Exists(r => r.Dn == "cn=first,dc=example,dc=com" && r.ResponsibleTypeId == 2), Is.True);
        }

        [Test]
        public void BuildOwnerResponsibles_OmitsWholeOwnerWhenAnyKeyIsNonNumeric()
        {
            AppDataImport import = CreateImportWithTypeMap(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Main"] = 1,
                ["Supporting"] = 2,
                ["Escalation"] = 3
            });
            SetResponsibleTypes(import,
            [
                new OwnerResponsibleType { Id = 2, Name = "Supporting", SortOrder = 1 },
                new OwnerResponsibleType { Id = 1, Name = "Main", SortOrder = 2 },
                new OwnerResponsibleType { Id = 3, Name = "Escalation", SortOrder = 3 }
            ]);
            ModellingImportAppData incomingApp = new()
            {
                Name = "App-A3",
                ExtAppId = "APP-1C",
                Responsibles = new Dictionary<string, List<string>>
                {
                    ["10"] = ["cn=first,dc=example,dc=com"],
                    ["x"] = ["cn=invalid,dc=example,dc=com"],
                    ["20"] = ["cn=second,dc=example,dc=com"]
                }
            };

            List<OwnerResponsible> result = InvokeBuildOwnerResponsibles(import, incomingApp, "cn=fallback,dc=example,dc=com", []);

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void BuildOwnerResponsibles_SkipsUnknownKeysAndContinues()
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
                    ["99"] = ["cn=ignored,dc=example,dc=com"],
                    ["1"] = ["cn=kept,dc=example,dc=com"]
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
                    ["1"] =
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
            AppDataImport import = CreateImportWithTypeMap(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Main"] = 1,
                ["Supporting"] = 2,
                ["Escalation"] = 3
            });
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

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result.Exists(r => r.Dn == "cn=main,dc=example,dc=com" && r.ResponsibleTypeId == 1), Is.True);
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
                    ["1"] = []
                }
            };

            List<OwnerResponsible> result = InvokeBuildOwnerResponsibles(import, incomingApp, "cn=support,dc=example,dc=com", []);

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result.Exists(r => r.Dn == "cn=main,dc=example,dc=com" && r.ResponsibleTypeId == 1), Is.True);
        }

        [Test]
        public void BuildOwnerResponsibles_UsesLegacyWhenResponsiblesDictionaryIsEmpty()
        {
            AppDataImport import = CreateImportWithTypeMap(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Main"] = 1,
                ["Supporting"] = 2
            });
            ModellingImportAppData incomingApp = new()
            {
                Name = "App-E2",
                ExtAppId = "APP-5-2",
                MainUser = "cn=main,dc=example,dc=com",
                Responsibles = new Dictionary<string, List<string>>()
            };

            List<OwnerResponsible> result = InvokeBuildOwnerResponsibles(import, incomingApp, "cn=support,dc=example,dc=com", []);

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result.Exists(r => r.Dn == "cn=main,dc=example,dc=com" && r.ResponsibleTypeId == 1), Is.True);
        }

        [Test]
        public void BuildOwnerResponsibles_LegacySkipsInactiveTypes()
        {
            AppDataImport import = CreateImportWithTypeMap(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
            SetResponsibleTypes(import,
            [
                new OwnerResponsibleType { Id = 1, Name = "Main", SortOrder = 1, Active = false },
                new OwnerResponsibleType { Id = 2, Name = "Supporting", SortOrder = 2, Active = true },
                new OwnerResponsibleType { Id = 3, Name = "Escalation", SortOrder = 3, Active = false }
            ]);
            ModellingImportAppData incomingApp = new()
            {
                Name = "App-E3",
                ExtAppId = "APP-5-3",
                MainUser = "cn=main,dc=example,dc=com",
                Responsibles = new Dictionary<string, List<string>>()
            };

            List<OwnerResponsible> result = InvokeBuildOwnerResponsibles(
                import,
                incomingApp,
                "cn=support,dc=example,dc=com",
                ["cn=optional,dc=example,dc=com"]);

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void BuildOwnerResponsibles_UsesTrimmedNumericKey()
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
                    [" 1 "] = ["cn=user1,dc=example,dc=com"]
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
                    ["1"] = ["cn=user1,dc=example,dc=com"],
                    ["2"] = ["cn=user1,dc=example,dc=com"]
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
        public void ParseRolesWithImport_ParsesLegacyArrayIntoSupportingType()
        {
            Dictionary<int, List<string>> parsed = InvokeParseRolesWithImport("[\"roleA\",\"roleB\"]");

            Assert.That(parsed.Keys, Is.EquivalentTo(new[] { GlobalConst.kOwnerResponsibleTypeSupporting }));
            Assert.That(parsed[GlobalConst.kOwnerResponsibleTypeSupporting], Is.EquivalentTo(new[] { "roleA", "roleB" }));
        }

        [Test]
        public void ParseRolesWithImport_ParsesTypeMappingAndSkipsInvalidKeys()
        {
            Dictionary<int, List<string>> parsed = InvokeParseRolesWithImport("{\"1\":[\"mainRole\"],\"x\":[\"ignored\"],\"2\":[\"supportRole\"]}");

            Assert.That(parsed.Keys, Is.EquivalentTo(new[] { 1, 2 }));
            Assert.That(parsed[1], Is.EquivalentTo(new[] { "mainRole" }));
            Assert.That(parsed[2], Is.EquivalentTo(new[] { "supportRole" }));
        }

        [Test]
        public void ParseRolesWithImport_ReturnsEmptyForWhitespaceInput()
        {
            Dictionary<int, List<string>> parsed = InvokeParseRolesWithImport("   ");

            Assert.That(parsed, Is.Empty);
        }

        [Test]
        public async Task ApplyRolesToResponsibles_Skips_WhenNoRolesConfiguredForType()
        {
            AppDataImport import = CreateImportWithTypeMap(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Main"] = 1
            });
            SetInternalLdap(import, null);
            List<OwnerResponsible> responsibles =
            [
                new() { Dn = "cn=user1,dc=example,dc=com", ResponsibleTypeId = 1 }
            ];
            Dictionary<int, List<string>> rolesByType = new()
            {
                [2] = ["anyRole"]
            };

            Assert.DoesNotThrowAsync(async () => await InvokeApplyRolesToResponsibles(import, responsibles, rolesByType));
        }

        [Test]
        public async Task ApplyRolesToResponsibles_Skips_WhenFilteringRemovesAllRoles()
        {
            AppDataImport import = CreateImportWithTypeMap(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Main"] = 1
            });
            SetResponsibleTypes(import,
            [
                new OwnerResponsibleType { Id = 1, Name = "Main", SortOrder = 1, Active = true, AllowModelling = false, AllowRecertification = false }
            ]);
            SetInternalLdap(import, null);
            List<OwnerResponsible> responsibles =
            [
                new() { Dn = "cn=user2,dc=example,dc=com", ResponsibleTypeId = 1 }
            ];
            Dictionary<int, List<string>> rolesByType = new()
            {
                [1] = [Roles.Modeller, Roles.Recertifier]
            };

            Assert.DoesNotThrowAsync(async () => await InvokeApplyRolesToResponsibles(import, responsibles, rolesByType));
        }

        [Test]
        public void ApplyRolesToResponsibles_CallsUpdateRoles_WhenFilteredRolesRemain()
        {
            AppDataImport import = CreateImportWithTypeMap(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Main"] = 1
            });
            SetResponsibleTypes(import,
            [
                new OwnerResponsibleType { Id = 1, Name = "Main", SortOrder = 1, Active = true, AllowModelling = false, AllowRecertification = false }
            ]);
            SetInternalLdap(import, null);
            List<OwnerResponsible> responsibles =
            [
                new() { Dn = "cn=user3,dc=example,dc=com", ResponsibleTypeId = 1 }
            ];
            Dictionary<int, List<string>> rolesByType = new()
            {
                [1] = ["ReadOnlyRole"]
            };

            Assert.ThrowsAsync<NullReferenceException>(async () => await InvokeApplyRolesToResponsibles(import, responsibles, rolesByType));
        }

        [Test]
        public void GetRolesForType_ReturnsConfiguredRoles_AndEmptyWhenMissing()
        {
            AppDataImport import = CreateImportWithTypeMap(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
            SetRolesByType(import, new Dictionary<int, List<string>>
            {
                [7] = ["roleA", "roleB"]
            });

            List<string> existing = InvokeGetRolesForType(import, 7);
            List<string> missing = InvokeGetRolesForType(import, 8);

            Assert.That(existing, Is.EquivalentTo(new[] { "roleA", "roleB" }));
            Assert.That(missing, Is.Empty);
        }

        [Test]
        public void IsResponsibleTypeActive_ReturnsTrueOnlyForActiveType()
        {
            AppDataImport import = CreateImportWithTypeMap(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
            SetResponsibleTypes(import,
            [
                new OwnerResponsibleType { Id = 1, Name = "Main", SortOrder = 1, Active = true },
                new OwnerResponsibleType { Id = 2, Name = "Supporting", SortOrder = 2, Active = false }
            ]);

            bool active = InvokeIsResponsibleTypeActive(import, 1);
            bool inactive = InvokeIsResponsibleTypeActive(import, 2);
            bool missing = InvokeIsResponsibleTypeActive(import, 3);

            Assert.That(active, Is.True);
            Assert.That(inactive, Is.False);
            Assert.That(missing, Is.False);
        }

        [Test]
        public async Task AddAllResponsiblesToUiUser_IgnoresWhitespaceAndDuplicates_WhenNoLdapsConfigured()
        {
            AppDataImport import = new(new SimulatedApiConnection(), new GlobalConfig());
            SetConnectedLdaps(import, []);
            List<OwnerResponsible> responsibles =
            [
                new() { Dn = "cn=user1,dc=example,dc=com" },
                new() { Dn = " cn=user1,dc=example,dc=com " },
                new() { Dn = "   " },
                new() { Dn = "" }
            ];

            Assert.DoesNotThrowAsync(async () => await InvokeAddAllResponsiblesToUiUser(import, responsibles));
        }

        [Test]
        public async Task AddResponsibleDnToUiUser_DoesNothing_WhenNoConnectedLdaps()
        {
            AppDataImport import = new(new SimulatedApiConnection(), new GlobalConfig());
            SetConnectedLdaps(import, []);
            HashSet<string> handledUserDns = new(StringComparer.OrdinalIgnoreCase);
            HashSet<string> handledGroupDnsByLdap = new(StringComparer.OrdinalIgnoreCase);

            await InvokeAddResponsibleDnToUiUser(import, "cn=group1,dc=example,dc=com", handledUserDns, handledGroupDnsByLdap);

            Assert.That(handledUserDns, Is.Empty);
            Assert.That(handledGroupDnsByLdap, Is.Empty);
        }

        [Test]
        public async Task AddResponsibleDnToUiUser_SkipsGroupProcessing_WhenGroupKeyAlreadyHandled()
        {
            AppDataImport import = new(new SimulatedApiConnection(), new GlobalConfig());
            SetConnectedLdaps(import,
            [
                new Ldap
                {
                    Id = 5,
                    UserSearchPath = "ou=users,dc=example,dc=com"
                }
            ]);
            HashSet<string> handledUserDns = new(StringComparer.OrdinalIgnoreCase);
            HashSet<string> handledGroupDnsByLdap = new(StringComparer.OrdinalIgnoreCase)
            {
                "5|cn=group1,dc=example,dc=com"
            };

            await InvokeAddResponsibleDnToUiUser(import, "cn=group1,dc=example,dc=com", handledUserDns, handledGroupDnsByLdap);

            Assert.That(handledUserDns, Is.Empty);
            Assert.That(handledGroupDnsByLdap.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task AddResponsibleDnToUiUser_ImportsUsersFromGroupDn_OutsideConfiguredGroupPath()
        {
            ResolverTestAppDataImport import = new(
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, UiUser>(StringComparer.OrdinalIgnoreCase)
                {
                    ["cn=user1,ou=users,dc=example,dc=com"] = new() { Dn = "cn=user1,ou=users,dc=example,dc=com", Name = "user1" },
                    ["cn=user2,ou=users,dc=example,dc=com"] = new() { Dn = "cn=user2,ou=users,dc=example,dc=com", Name = "user2" }
                },
                new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["cn=group1,ou=external-groups,dc=example,dc=com"] =
                    [
                        "cn=user1,ou=users,dc=example,dc=com",
                        "cn=user2,ou=users,dc=example,dc=com"
                    ]
                });
            SetConnectedLdaps(import,
            [
                new Ldap
                {
                    Id = 5,
                    GroupSearchPath = "ou=internal-groups,dc=example,dc=com"
                }
            ]);
            HashSet<string> handledUserDns = new(StringComparer.OrdinalIgnoreCase);
            HashSet<string> handledGroupDnsByLdap = new(StringComparer.OrdinalIgnoreCase);

            await InvokeAddResponsibleDnToUiUser(import, "cn=group1,ou=external-groups,dc=example,dc=com", handledUserDns, handledGroupDnsByLdap);

            Assert.That(handledGroupDnsByLdap.Contains("5|cn=group1,ou=external-groups,dc=example,dc=com"), Is.True);
            Assert.That(handledUserDns, Is.EquivalentTo(new[]
            {
                "cn=user1,ou=users,dc=example,dc=com",
                "cn=user2,ou=users,dc=example,dc=com"
            }));
        }

        [Test]
        public async Task AddResponsibleDnToUiUser_TriesDnAsUserBeforeResolvingGroupMembers()
        {
            ResolverTestAppDataImport import = new(
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, UiUser>(StringComparer.OrdinalIgnoreCase)
                {
                    ["cn=user-or-group,ou=mixed,dc=example,dc=com"] = new() { Dn = "cn=user-or-group,ou=mixed,dc=example,dc=com", Name = "mixedUser" }
                },
                new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["cn=user-or-group,ou=mixed,dc=example,dc=com"] =
                    [
                        "cn=group-member,ou=users,dc=example,dc=com"
                    ]
                });
            SetConnectedLdaps(import,
            [
                new Ldap
                {
                    Id = 5,
                    GroupSearchPath = "ou=groups,dc=example,dc=com"
                }
            ]);
            HashSet<string> handledUserDns = new(StringComparer.OrdinalIgnoreCase);
            HashSet<string> handledGroupDnsByLdap = new(StringComparer.OrdinalIgnoreCase);

            await InvokeAddResponsibleDnToUiUser(import, "cn=user-or-group,ou=mixed,dc=example,dc=com", handledUserDns, handledGroupDnsByLdap);

            Assert.That(handledUserDns, Is.EquivalentTo(new[]
            {
                "cn=user-or-group,ou=mixed,dc=example,dc=com"
            }));
            Assert.That(handledGroupDnsByLdap, Is.Empty);
        }

        [Test]
        public async Task ConvertLdapToUiUser_ReturnsNull_WhenNoUserSearchPathMatchesDn()
        {
            AppDataImport import = new(new SimulatedApiConnection(), new GlobalConfig());
            SetConnectedLdaps(import,
            [
                new Ldap
                {
                    Id = 7,
                    UserSearchPath = "ou=users,dc=example,dc=com"
                }
            ]);

            UiUser? uiUser = await InvokeConvertLdapToUiUser(import, "cn=user3,ou=other,dc=example,dc=com");

            Assert.That(uiUser, Is.Null);
        }

        [Test]
        public async Task DeactivateMissingApps_DeactivatesOnlyActiveAppsFromSameSourceThatAreMissingInImport()
        {
            AppDataImportFlowTestApiConn apiConn = new();
            AppDataImport import = new(apiConn, new GlobalConfig());
            OwnerChangeImportTracker tracker = new(apiConn);
            List<FwoOwner> existingApps =
            [
                new() { Id = 1, ExtAppId = "APP-1", ImportSource = "SRC-A", Active = true },
                new() { Id = 2, ExtAppId = "APP-2", ImportSource = "SRC-A", Active = true },
                new() { Id = 3, ExtAppId = "APP-3", ImportSource = "SRC-A", Active = false },
                new() { Id = 4, ExtAppId = "APP-4", ImportSource = "SRC-B", Active = true }
            ];
            List<ModellingImportAppData> importedApps =
            [
                new() { Name = "Imported2", ExtAppId = "APP-2", ImportSource = "SRC-A" }
            ];

            (int deleted, int failed) = await InvokeDeactivateMissingApps(import, "SRC-A", existingApps, importedApps, tracker);

            Assert.That(deleted, Is.EqualTo(1));
            Assert.That(failed, Is.EqualTo(0));
            Assert.That(apiConn.DeactivateOwnerCalls, Is.EqualTo(1));
            Assert.That(apiConn.UpdateChangelogOwnerCalls, Is.EqualTo(1));
        }

        [Test]
        public async Task DeactivateMissingApps_CountsFailures_WhenDeactivateThrows()
        {
            AppDataImportFlowTestApiConn apiConn = new();
            apiConn.FailDeactivateOwnerIds.Add(2);
            AppDataImport import = new(apiConn, new GlobalConfig());
            OwnerChangeImportTracker tracker = new(apiConn);
            List<FwoOwner> existingApps =
            [
                new() { Id = 1, ExtAppId = "APP-1", ImportSource = "SRC-A", Active = true },
                new() { Id = 2, ExtAppId = "APP-2", ImportSource = "SRC-A", Active = true }
            ];

            (int deleted, int failed) = await InvokeDeactivateMissingApps(import, "SRC-A", existingApps, [], tracker);

            Assert.That(deleted, Is.EqualTo(1));
            Assert.That(failed, Is.EqualTo(1));
            Assert.That(apiConn.DeactivateOwnerCalls, Is.EqualTo(2));
            Assert.That(apiConn.UpdateChangelogOwnerCalls, Is.EqualTo(1));
        }

        [Test]
        public void CheckResponsibles_ReturnsInsertAndDelete_WhenSyncEnabled()
        {
            AppDataImport import = new(new SimulatedApiConnection(), new GlobalConfig());
            SetOwnerDataImportSyncUsers(import, true);
            List<OwnerResponsible> existingResponsibles =
            [
                new() { Dn = "cn=keep,dc=example,dc=com", ResponsibleTypeId = 1 },
                new() { Dn = "cn=delete,dc=example,dc=com", ResponsibleTypeId = 2 }
            ];
            List<OwnerResponsible> incomingResponsibles =
            [
                new() { Dn = "cn=keep,dc=example,dc=com", ResponsibleTypeId = 1 },
                new() { Dn = "cn=insert,dc=example,dc=com", ResponsibleTypeId = 2 }
            ];

            (List<OwnerResponsible> toInsert, List<OwnerResponsible> toDelete) =
                InvokeCheckResponsibles(import, existingResponsibles, incomingResponsibles);

            Assert.That(toInsert, Has.Count.EqualTo(1));
            Assert.That(toInsert[0].Dn, Is.EqualTo("cn=insert,dc=example,dc=com"));
            Assert.That(toInsert[0].ResponsibleTypeId, Is.EqualTo(2));
            Assert.That(toDelete, Has.Count.EqualTo(1));
            Assert.That(toDelete[0].Dn, Is.EqualTo("cn=delete,dc=example,dc=com"));
            Assert.That(toDelete[0].ResponsibleTypeId, Is.EqualTo(2));
        }

        [Test]
        public void CheckResponsibles_ReturnsInsertOnly_WhenSyncDisabled()
        {
            AppDataImport import = new(new SimulatedApiConnection(), new GlobalConfig());
            SetOwnerDataImportSyncUsers(import, false);
            List<OwnerResponsible> existingResponsibles =
            [
                new() { Dn = "cn=delete,dc=example,dc=com", ResponsibleTypeId = 2 }
            ];
            List<OwnerResponsible> incomingResponsibles =
            [
                new() { Dn = "cn=insert,dc=example,dc=com", ResponsibleTypeId = 2 }
            ];

            (List<OwnerResponsible> toInsert, List<OwnerResponsible> toDelete) =
                InvokeCheckResponsibles(import, existingResponsibles, incomingResponsibles);

            Assert.That(toInsert, Has.Count.EqualTo(1));
            Assert.That(toInsert[0].Dn, Is.EqualTo("cn=insert,dc=example,dc=com"));
            Assert.That(toDelete, Is.Empty);
        }

        [Test]
        public async Task UpdateOwnerResponsibles_CallsInsertAndDelete_WhenSyncEnabled()
        {
            AppDataImportResponsiblesApiConn apiConn = new();
            AppDataImport import = new(apiConn, new GlobalConfig());
            SetOwnerDataImportSyncUsers(import, true);
            List<OwnerResponsible> existingResponsibles =
            [
                new() { Dn = "cn=keep,dc=example,dc=com", ResponsibleTypeId = 1 },
                new() { Dn = "cn=delete,dc=example,dc=com", ResponsibleTypeId = 2 }
            ];
            List<OwnerResponsible> incomingResponsibles =
            [
                new() { Dn = "cn=keep,dc=example,dc=com", ResponsibleTypeId = 1 },
                new() { Dn = "cn=insert,dc=example,dc=com", ResponsibleTypeId = 2 }
            ];

            await InvokeUpdateOwnerResponsibles(import, 123, incomingResponsibles, existingResponsibles);

            Assert.That(apiConn.DeleteSpecificOwnerResponsiblesCalls, Is.EqualTo(1));
            Assert.That(apiConn.NewOwnerResponsiblesCalls, Is.EqualTo(1));
            Assert.That(apiConn.Deleted, Has.Count.EqualTo(1));
            Assert.That(apiConn.Deleted[0], Is.EqualTo((123, "cn=delete,dc=example,dc=com", 2)));
            Assert.That(apiConn.Inserted, Has.Count.EqualTo(1));
            Assert.That(apiConn.Inserted[0], Is.EqualTo((123, "cn=insert,dc=example,dc=com", 2)));
        }

        [Test]
        public async Task UpdateOwnerResponsibles_RemovesMappedRoles_ForDeletedResponsibles()
        {
            AppDataImportResponsiblesApiConn apiConn = new();
            RoleRemovalTrackingImport import = new(apiConn);
            SetOwnerDataImportSyncUsers(import, true);
            SetResponsibleTypes(import,
            [
                new OwnerResponsibleType { Id = 2, Name = "Supporting", SortOrder = 2, Active = true, AllowModelling = false, AllowRecertification = false }
            ]);
            SetRolesByType(import, new Dictionary<int, List<string>>
            {
                [2] = ["ReadOnlyRole"]
            });
            List<OwnerResponsible> existingResponsibles =
            [
                new() { Dn = "cn=delete,dc=example,dc=com", ResponsibleTypeId = 2 }
            ];

            await InvokeUpdateOwnerResponsibles(import, 123, [], existingResponsibles);

            Assert.That(import.RemovedRoleAssignments, Is.EqualTo(new[]
            {
                ("cn=delete,dc=example,dc=com", "ReadOnlyRole")
            }));
        }

        [Test]
        public async Task UpdateOwnerResponsibles_DoesNothing_WhenNoChanges()
        {
            AppDataImportResponsiblesApiConn apiConn = new();
            AppDataImport import = new(apiConn, new GlobalConfig());
            SetOwnerDataImportSyncUsers(import, true);
            List<OwnerResponsible> existingResponsibles =
            [
                new() { Dn = "cn=keep,dc=example,dc=com", ResponsibleTypeId = 1 }
            ];
            List<OwnerResponsible> incomingResponsibles =
            [
                new() { Dn = "cn=keep,dc=example,dc=com", ResponsibleTypeId = 1 }
            ];

            await InvokeUpdateOwnerResponsibles(import, 123, incomingResponsibles, existingResponsibles);

            Assert.That(apiConn.DeleteSpecificOwnerResponsiblesCalls, Is.EqualTo(0));
            Assert.That(apiConn.NewOwnerResponsiblesCalls, Is.EqualTo(0));
            Assert.That(apiConn.Deleted, Is.Empty);
            Assert.That(apiConn.Inserted, Is.Empty);
        }

        [Test]
        public async Task UpdateOwnerResponsibles_InsertsOnly_WhenSyncDisabled()
        {
            AppDataImportResponsiblesApiConn apiConn = new();
            AppDataImport import = new(apiConn, new GlobalConfig());
            SetOwnerDataImportSyncUsers(import, false);
            List<OwnerResponsible> existingResponsibles =
            [
                new() { Dn = "cn=delete,dc=example,dc=com", ResponsibleTypeId = 2 }
            ];
            List<OwnerResponsible> incomingResponsibles =
            [
                new() { Dn = "cn=insert,dc=example,dc=com", ResponsibleTypeId = 2 }
            ];

            await InvokeUpdateOwnerResponsibles(import, 123, incomingResponsibles, existingResponsibles);

            Assert.That(apiConn.DeleteSpecificOwnerResponsiblesCalls, Is.EqualTo(0));
            Assert.That(apiConn.NewOwnerResponsiblesCalls, Is.EqualTo(1));
            Assert.That(apiConn.Deleted, Is.Empty);
            Assert.That(apiConn.Inserted, Has.Count.EqualTo(1));
            Assert.That(apiConn.Inserted[0], Is.EqualTo((123, "cn=insert,dc=example,dc=com", 2)));
        }

        [Test]
        public async Task SaveApp_DoesNotImport_WhenOwnerLifecycleStateIsUnknown()
        {
            AppDataImportSaveAppApiConn apiConn = new();
            AppDataImport import = new(apiConn, new GlobalConfig());
            SetOwnerLifeCycleMap(import, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Active"] = 10
            });
            SetExistingApps(import, []);
            ModellingImportAppData incomingApp = new()
            {
                Name = "App-L4",
                ExtAppId = "APP-L4",
                ImportSource = "SRC-L4",
                OwnerLifecycleState = "UnknownLifecycle"
            };
            OwnerChangeImportTracker tracker = new(apiConn);

            bool imported = await InvokeSaveApp(import, incomingApp, tracker);

            Assert.That(imported, Is.False);
            Assert.That(apiConn.NewOwnerCalls, Is.EqualTo(0));
            Assert.That(apiConn.UpdateOwnerCalls, Is.EqualTo(0));
        }

        [Test]
        public async Task AddOwnerLifeCycleStateActiveChangeIfNeeded_LogsReactivate_WhenLifecycleStateChangesToActive()
        {
            AppDataImportSaveAppApiConn apiConn = new();
            AppDataImport import = new(apiConn, new GlobalConfig());
            SetOwnerLifeCycleActiveMap(import, new Dictionary<int, bool>
            {
                [10] = true,
                [11] = false
            });
            FwoOwner existingApp = new() { Id = 7, OwnerLifeCycleStateId = 11 };
            OwnerChangeImportTracker tracker = new(apiConn);

            await InvokeAddOwnerLifeCycleStateActiveChangeIfNeeded(import, existingApp, 10, "SRC-7", tracker);

            Assert.That(apiConn.UpdateChangelogOwnerCalls, Is.EqualTo(1));
            Assert.That(apiConn.ChangelogActions, Is.EqualTo(new[] { ChangelogActionType.REACTIVATE }));
        }

        [Test]
        public async Task AddOwnerLifeCycleStateActiveChangeIfNeeded_LogsDeactivate_WhenLifecycleStateChangesToInactive()
        {
            AppDataImportSaveAppApiConn apiConn = new();
            AppDataImport import = new(apiConn, new GlobalConfig());
            SetOwnerLifeCycleActiveMap(import, new Dictionary<int, bool>
            {
                [10] = true,
                [11] = false
            });
            FwoOwner existingApp = new() { Id = 8, OwnerLifeCycleStateId = 10 };
            OwnerChangeImportTracker tracker = new(apiConn);

            await InvokeAddOwnerLifeCycleStateActiveChangeIfNeeded(import, existingApp, 11, "SRC-8", tracker);

            Assert.That(apiConn.UpdateChangelogOwnerCalls, Is.EqualTo(1));
            Assert.That(apiConn.ChangelogActions, Is.EqualTo(new[] { ChangelogActionType.DEACTIVATE }));
        }

        [Test]
        public async Task AddOwnerLifeCycleStateActiveChangeIfNeeded_DoesNotLog_WhenActiveStateDoesNotChange()
        {
            AppDataImportSaveAppApiConn apiConn = new();
            AppDataImport import = new(apiConn, new GlobalConfig());
            SetOwnerLifeCycleActiveMap(import, new Dictionary<int, bool>
            {
                [10] = true,
                [12] = true
            });
            FwoOwner existingApp = new() { Id = 9, OwnerLifeCycleStateId = 10 };
            OwnerChangeImportTracker tracker = new(apiConn);

            await InvokeAddOwnerLifeCycleStateActiveChangeIfNeeded(import, existingApp, 12, "SRC-9", tracker);

            Assert.That(apiConn.UpdateChangelogOwnerCalls, Is.EqualTo(0));
        }

        [Test]
        public async Task AddOwnerChangeIfNeeded_LogsChange_WhenAppServersChanged()
        {
            AppDataImportSaveAppApiConn apiConn = new();
            AppDataImport import = new(apiConn, new GlobalConfig());
            SetExistingAppServers(import,
            [
                new ModellingAppServer
                {
                    Id = 100,
                    AppId = 19,
                    Name = "server-old",
                    Ip = "10.0.0.1/32",
                    IpEnd = "10.0.0.1/32",
                    ImportSource = "SRC-19",
                    IsDeleted = false
                }
            ]);
            FwoOwner existingApp = new() { Id = 19, Name = "App-19", ExtAppId = "APP-19" };
            ModellingImportAppData incomingApp = new()
            {
                Name = "App-19",
                ExtAppId = "APP-19",
                ImportSource = "SRC-19",
                AppServers =
                [
                    new ModellingImportAppServer
                    {
                        Name = "server-new",
                        Ip = "10.0.0.2/32",
                        IpEnd = "10.0.0.2/32"
                    }
                ]
            };
            OwnerChangeImportTracker tracker = new(apiConn);

            await InvokeAddOwnerChangeIfNeeded(import, existingApp, incomingApp, tracker);

            Assert.That(apiConn.UpdateChangelogOwnerCalls, Is.EqualTo(1));
            Assert.That(apiConn.ChangelogActions, Is.EqualTo(new[] { ChangelogActionType.CHANGE }));
        }

        [Test]
        public async Task AddOwnerChangeIfNeeded_LogsChange_WhenInactiveOwnerIsFoundAgainInImport()
        {
            AppDataImportSaveAppApiConn apiConn = new();
            AppDataImport import = new(apiConn, new GlobalConfig());
            SetExistingAppServers(import, []);
            FwoOwner existingApp = new() { Id = 21, Name = "App-21", ExtAppId = "APP-21", Active = false };
            ModellingImportAppData incomingApp = new()
            {
                Name = "App-21",
                ExtAppId = "APP-21",
                ImportSource = "SRC-21"
            };
            OwnerChangeImportTracker tracker = new(apiConn);

            await InvokeAddOwnerChangeIfNeeded(import, existingApp, incomingApp, tracker);

            Assert.That(apiConn.UpdateChangelogOwnerCalls, Is.EqualTo(1));
            Assert.That(apiConn.ChangelogActions, Is.EqualTo(new[] { ChangelogActionType.CHANGE }));
        }

        [Test]
        public async Task SaveApp_LogsChangeAndReactivate_WhenAppServersChangeAndLifecycleStateBecomesActive()
        {
            AppDataImportSaveAppApiConn apiConn = new();
            AppDataImport import = new(apiConn, new GlobalConfig());
            SetOwnerLifeCycleMap(import, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Active"] = 10
            });
            SetOwnerLifeCycleActiveMap(import, new Dictionary<int, bool>
            {
                [10] = true,
                [11] = false
            });
            SetExistingApps(import,
            [
                new() { Id = 20, Name = "App-20", ExtAppId = "APP-20", OwnerLifeCycleStateId = 11 }
            ]);
            apiConn.AppServersByOwner[(20, "SRC-20")] =
            [
                new ModellingAppServer
                {
                    Id = 200,
                    AppId = 20,
                    Name = "server-old",
                    Ip = "10.0.0.1/32",
                    IpEnd = "10.0.0.1/32",
                    ImportSource = "SRC-20",
                    IsDeleted = false
                }
            ];
            ModellingImportAppData incomingApp = new()
            {
                Name = "App-20",
                ExtAppId = "APP-20",
                ImportSource = "SRC-20",
                OwnerLifecycleState = "Active",
                AppServers =
                [
                    new ModellingImportAppServer
                    {
                        Name = "server-new",
                        Ip = "10.0.0.2/32",
                        IpEnd = "10.0.0.2/32"
                    }
                ]
            };
            OwnerChangeImportTracker tracker = new(apiConn);

            bool imported = await InvokeSaveApp(import, incomingApp, tracker);

            Assert.That(imported, Is.True);
            Assert.That(apiConn.UpdateChangelogOwnerCalls, Is.EqualTo(2));
            Assert.That(apiConn.ChangelogActions, Is.EqualTo(new[] { ChangelogActionType.CHANGE, ChangelogActionType.REACTIVATE }));
        }

        [Test]
        public async Task NormalizeImportedUserReferences_ResolvesPlainUserIds_AndKeepsDns()
        {
            ResolverTestAppDataImport import = new(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["main.user"] = "cn=main.user,ou=users,dc=example,dc=com",
                ["support.user"] = "cn=support.user,ou=users,dc=example,dc=com"
            });
            ModellingImportAppData incomingApp = new()
            {
                Name = "App-Resolve",
                ExtAppId = "APP-RESOLVE",
                MainUser = " main.user ",
                Responsibles = new Dictionary<string, List<string>>
                {
                    ["1"] =
                    [
                        "support.user",
                        "cn=group1,ou=groups,dc=example,dc=com"
                    ]
                }
            };

            ModellingImportAppData normalizedApp = await InvokeNormalizeImportedUserReferences(import, incomingApp);

            Assert.That(normalizedApp.MainUser, Is.EqualTo("cn=main.user,ou=users,dc=example,dc=com"));
            Assert.That(normalizedApp.Responsibles?["1"], Is.EquivalentTo(new[]
            {
                "cn=support.user,ou=users,dc=example,dc=com",
                "cn=group1,ou=groups,dc=example,dc=com"
            }));
            Assert.That(import.ResolvedIdentifiers, Is.EqualTo(new[] { "main.user", "support.user" }));
        }

        [Test]
        public async Task NormalizeImportedUserReferences_DropsUnresolvablePlainUserIds()
        {
            ResolverTestAppDataImport import = new(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase));
            ModellingImportAppData incomingApp = new()
            {
                Name = "App-Unresolved",
                ExtAppId = "APP-UNRESOLVED",
                MainUser = "missing.user",
                Responsibles = new Dictionary<string, List<string>>
                {
                    ["1"] =
                    [
                        "missing.user",
                        "cn=group1,ou=groups,dc=example,dc=com"
                    ]
                }
            };

            ModellingImportAppData normalizedApp = await InvokeNormalizeImportedUserReferences(import, incomingApp);

            Assert.That(normalizedApp.MainUser, Is.Null);
            Assert.That(normalizedApp.Responsibles?["1"], Is.EqualTo(new[]
            {
                "cn=group1,ou=groups,dc=example,dc=com"
            }));
            Assert.That(import.ResolvedIdentifiers, Is.EqualTo(new[] { "missing.user", "missing.user" }));
        }

        [Test]
        public async Task NormalizeImportedUserReferences_ResolvesPlainGroupNamesToGroupDns()
        {
            ResolverTestAppDataImport import = new(
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
                null,
                null,
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["app-support"] = "cn=app-support,ou=groups,dc=example,dc=com"
                });
            ModellingImportAppData incomingApp = new()
            {
                Name = "App-GroupResolve",
                ExtAppId = "APP-GROUP-RESOLVE",
                Responsibles = new Dictionary<string, List<string>>
                {
                    ["2"] = ["app-support"]
                }
            };

            ModellingImportAppData normalizedApp = await InvokeNormalizeImportedUserReferences(import, incomingApp);

            Assert.That(normalizedApp.Responsibles?["2"], Is.EqualTo(new[]
            {
                "cn=app-support,ou=groups,dc=example,dc=com"
            }));
            Assert.That(import.ResolvedGroupIdentifiers, Is.EqualTo(new[] { "app-support" }));
        }

        private static AppDataImport CreateImportWithTypeMap(Dictionary<string, int> typeMap)
        {
            AppDataImport import = new(new SimulatedApiConnection(), new GlobalConfig());
            FieldInfo field = typeof(AppDataImport).GetField("ownerResponsibleTypeIdByName", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("ownerResponsibleTypeIdByName field not found.");
            field.SetValue(import, typeMap);
            SetResponsibleTypes(import, [.. typeMap.Select(entry => new OwnerResponsibleType
            {
                Id = entry.Value,
                Name = entry.Key,
                SortOrder = entry.Value
            })]);
            return import;
        }

        private static void SetResponsibleTypes(AppDataImport import, IEnumerable<OwnerResponsibleType> responsibleTypes)
        {
            Dictionary<int, OwnerResponsibleType> byId = responsibleTypes.ToDictionary(type => type.Id, type => type);
            FieldInfo field = typeof(AppDataImport).GetField("ownerResponsibleTypeById", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("ownerResponsibleTypeById field not found.");
            field.SetValue(import, byId);
        }

        private static void SetOwnerLifeCycleMap(AppDataImport import, Dictionary<string, int> stateMap)
        {
            FieldInfo field = typeof(AppDataImport).GetField("ownerLifeCycleStateIdsByName", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("ownerLifeCycleStateIdsByName field not found.");
            field.SetValue(import, stateMap);
        }

        private static void SetOwnerLifeCycleActiveMap(AppDataImport import, Dictionary<int, bool> stateMap)
        {
            FieldInfo field = typeof(AppDataImport).GetField("ownerLifeCycleStateActiveById", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("ownerLifeCycleStateActiveById field not found.");
            field.SetValue(import, stateMap);
        }

        private static void SetOwnerDataImportSyncUsers(AppDataImport import, bool syncUsers)
        {
            FieldInfo field = typeof(DataImportBase).GetField("globalConfig", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("globalConfig field not found.");
            GlobalConfig globalConfig = (GlobalConfig)field.GetValue(import)!;
            globalConfig.OwnerDataImportSyncUsers = syncUsers;
        }

        private static void SetRolesByType(AppDataImport import, Dictionary<int, List<string>> rolesByType)
        {
            FieldInfo field = typeof(AppDataImport).GetField("rolesToSetByType", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("rolesToSetByType field not found.");
            field.SetValue(import, rolesByType);
        }

        private static void SetInternalLdap(AppDataImport import, Ldap? ldap)
        {
            FieldInfo field = typeof(AppDataImport).GetField("internalLdap", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("internalLdap field not found.");
            field.SetValue(import, ldap);
        }

        private static void SetConnectedLdaps(AppDataImport import, List<Ldap> ldaps)
        {
            FieldInfo field = typeof(AppDataImport).GetField("connectedLdaps", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("connectedLdaps field not found.");
            field.SetValue(import, ldaps);
        }

        private static List<OwnerResponsible> InvokeBuildOwnerResponsibles(AppDataImport import, ModellingImportAppData incomingApp, string userGroupDn, IEnumerable<string> extraDns)
        {
            MethodInfo method = typeof(AppDataImport).GetMethod(
                "BuildOwnerResponsibles",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("BuildOwnerResponsibles helper not found.");
            return (List<OwnerResponsible>)method.Invoke(import, [incomingApp])!;
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

        private static Dictionary<int, List<string>> InvokeParseRolesWithImport(string rolesJson)
        {
            MethodInfo method = typeof(AppDataImport).GetMethod(
                "ParseRolesWithImport",
                BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("ParseRolesWithImport helper not found.");
            return (Dictionary<int, List<string>>)method.Invoke(null, [rolesJson])!;
        }

        private static async Task InvokeApplyRolesToResponsibles(
            AppDataImport import,
            List<OwnerResponsible> responsibles,
            Dictionary<int, List<string>> rolesByType)
        {
            MethodInfo method = typeof(AppDataImport).GetMethod(
                "ApplyRolesToResponsibles",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("ApplyRolesToResponsibles helper not found.");
            await (Task)method.Invoke(import, [responsibles, rolesByType])!;
        }

        private static bool InvokeIsResponsibleTypeActive(AppDataImport import, int typeId)
        {
            MethodInfo method = typeof(AppDataImport).GetMethod(
                "IsResponsibleTypeActive",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("IsResponsibleTypeActive helper not found.");
            return (bool)method.Invoke(import, [typeId])!;
        }

        private static List<string> InvokeGetRolesForType(AppDataImport import, int typeId)
        {
            MethodInfo method = typeof(AppDataImport).GetMethod(
                "GetRolesForType",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("GetRolesForType helper not found.");
            return (List<string>)method.Invoke(import, [typeId])!;
        }

        private static async Task InvokeAddAllResponsiblesToUiUser(AppDataImport import, IEnumerable<OwnerResponsible> responsibles)
        {
            MethodInfo method = typeof(AppDataImport).GetMethod(
                "AddAllResponsiblesToUiUser",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("AddAllResponsiblesToUiUser helper not found.");
            await (Task)method.Invoke(import, [responsibles])!;
        }

        private static async Task InvokeAddResponsibleDnToUiUser(
            AppDataImport import,
            string responsibleDn,
            HashSet<string> handledUserDns,
            HashSet<string> handledGroupDnsByLdap)
        {
            MethodInfo method = typeof(AppDataImport).GetMethod(
                "AddResponsibleDnToUiUser",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("AddResponsibleDnToUiUser helper not found.");
            await (Task)method.Invoke(import, [responsibleDn, handledUserDns, handledGroupDnsByLdap])!;
        }

        private static async Task<UiUser?> InvokeConvertLdapToUiUser(AppDataImport import, string userDn)
        {
            MethodInfo method = typeof(AppDataImport).GetMethod(
                "ConvertLdapToUiUser",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("ConvertLdapToUiUser helper not found.");
            return await (Task<UiUser?>)method.Invoke(import, [userDn])!;
        }

        private static async Task InvokeImportApps(AppDataImport import, string importfileName, OwnerChangeImportTracker tracker)
        {
            MethodInfo method = typeof(AppDataImport).GetMethod(
                "ImportApps",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("ImportApps helper not found.");
            await (Task)method.Invoke(import, [importfileName, tracker])!;
        }

        private static async Task<bool> InvokeSaveApp(AppDataImport import, ModellingImportAppData incomingApp, OwnerChangeImportTracker tracker)
        {
            MethodInfo method = typeof(AppDataImport).GetMethod(
                "SaveApp",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("SaveApp helper not found.");
            return await (Task<bool>)method.Invoke(import, [incomingApp, tracker])!;
        }

        private static async Task InvokeAddOwnerLifeCycleStateActiveChangeIfNeeded(
            AppDataImport import,
            FwoOwner existingApp,
            int? ownerLifeCycleStateId,
            string? importSource,
            OwnerChangeImportTracker tracker)
        {
            MethodInfo method = typeof(AppDataImport).GetMethod(
                "AddOwnerLifeCycleStateActiveChangeIfNeeded",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("AddOwnerLifeCycleStateActiveChangeIfNeeded helper not found.");
            await (Task)method.Invoke(import, [existingApp, ownerLifeCycleStateId, importSource, tracker])!;
        }

        private static async Task InvokeAddOwnerChangeIfNeeded(
            AppDataImport import,
            FwoOwner existingApp,
            ModellingImportAppData incomingApp,
            OwnerChangeImportTracker tracker)
        {
            MethodInfo method = typeof(AppDataImport).GetMethod(
                "AddOwnerChangeIfNeeded",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("AddOwnerChangeIfNeeded helper not found.");
            await (Task)method.Invoke(import, [existingApp, incomingApp, tracker])!;
        }

        private static async Task<ModellingImportAppData> InvokeNormalizeImportedUserReferences(AppDataImport import, ModellingImportAppData incomingApp)
        {
            MethodInfo method = typeof(AppDataImport).GetMethod(
                "NormalizeImportedUserReferences",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("NormalizeImportedUserReferences helper not found.");
            return await (Task<ModellingImportAppData>)method.Invoke(import, [incomingApp])!;
        }

        private static async Task<(int deleted, int failed)> InvokeDeactivateMissingApps(
            AppDataImport import,
            string importSource,
            IEnumerable<FwoOwner> existingApps,
            List<ModellingImportAppData> importedApps,
            OwnerChangeImportTracker tracker)
        {
            SetExistingApps(import, existingApps.ToList());
            SetImportedApps(import, importedApps);
            MethodInfo method = typeof(AppDataImport).GetMethod(
                "DeactivateMissingApps",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("DeactivateMissingApps helper not found.");
            return ((int deleted, int failed))await (Task<(int deleted, int failed)>)method.Invoke(import, [importSource, tracker])!;
        }

        private static async Task InvokeUpdateOwnerResponsibles(
            AppDataImport import,
            int ownerId,
            List<OwnerResponsible> responsibles,
            List<OwnerResponsible> existingResponsibles)
        {
            MethodInfo method = typeof(AppDataImport).GetMethod(
                "UpdateOwnerResponsibles",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("UpdateOwnerResponsibles helper not found.");
            await (Task)method.Invoke(import, [ownerId, responsibles, existingResponsibles])!;
        }

        private static (List<OwnerResponsible> toInsert, List<OwnerResponsible> toDelete) InvokeCheckResponsibles(
            AppDataImport import,
            List<OwnerResponsible> existingResponsibles,
            List<OwnerResponsible> incomingResponsibles)
        {
            MethodInfo method = typeof(AppDataImport).GetMethod(
                "CheckResponsibles",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("CheckResponsibles helper not found.");
            object result = method.Invoke(import, [existingResponsibles, incomingResponsibles])!;

            Type resultType = result.GetType();
            List<OwnerResponsible> toInsert = (List<OwnerResponsible>)(resultType.GetField("Item1")?.GetValue(result)
                ?? throw new InvalidOperationException("CheckResponsibles result Item1 not found."));
            List<OwnerResponsible> toDelete = (List<OwnerResponsible>)(resultType.GetField("Item2")?.GetValue(result)
                ?? throw new InvalidOperationException("CheckResponsibles result Item2 not found."));

            return (toInsert, toDelete);
        }

        private static void SetImportedApps(AppDataImport import, List<ModellingImportAppData> importedApps)
        {
            FieldInfo field = typeof(AppDataImport).GetField("ImportedApps", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("ImportedApps field not found.");
            field.SetValue(import, importedApps);
        }

        private static void SetExistingApps(AppDataImport import, List<FwoOwner> existingApps)
        {
            FieldInfo field = typeof(AppDataImport).GetField("ExistingApps", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("ExistingApps field not found.");
            field.SetValue(import, existingApps);
        }

        private static void SetExistingAppServers(AppDataImport import, List<ModellingAppServer> existingAppServers)
        {
            FieldInfo field = typeof(AppDataImport).GetField("ExistingAppServers", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("ExistingAppServers field not found.");
            field.SetValue(import, existingAppServers);
        }

        private sealed class AppDataImportFlowTestApiConn : SimulatedApiConnection
        {
            public int GetOwnersCalls { get; private set; }
            public int DeactivateOwnerCalls { get; private set; }
            public int UpdateChangelogOwnerCalls { get; private set; }
            public HashSet<int> FailDeactivateOwnerIds { get; } = [];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
            {
                if (query == OwnerQueries.getOwners)
                {
                    ++GetOwnersCalls;
                    return Task.FromResult((QueryResponseType)(object)new List<FwoOwner>());
                }

                if (query == OwnerQueries.deactivateOwner)
                {
                    ++DeactivateOwnerCalls;
                    int ownerId = GetAnonymousInt(variables, "id");
                    if (FailDeactivateOwnerIds.Contains(ownerId))
                    {
                        throw new InvalidOperationException($"Deactivate failed for owner {ownerId}.");
                    }
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper
                    {
                        ReturnIds = [new ReturnId { NewId = ownerId }]
                    });
                }

                if (query == ImportQueries.addImportForOwner)
                {
                    return Task.FromResult((QueryResponseType)(object)new InsertImportControl
                    {
                        Returning = new List<ImportControl>
                        {
                            new ImportControl { ControlId = 123 }
                        }
                    });
                }

                if (query == OwnerQueries.updateChangelogOwner)
                {
                    ++UpdateChangelogOwnerCalls;
                    return Task.FromResult((QueryResponseType)(object)new object());
                }

                if (query == MonitorQueries.addDataImportLogEntry)
                {
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper
                    {
                        ReturnIds = [new ReturnId { NewId = 1 }]
                    });
                }

                throw new NotImplementedException($"Query not implemented in test api: {query}");
            }

            private static int GetAnonymousInt(object? variables, string propertyName)
            {
                if (variables == null)
                {
                    return 0;
                }
                PropertyInfo? property = variables.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property == null)
                {
                    return 0;
                }
                object? value = property.GetValue(variables);
                return value is int intValue ? intValue : 0;
            }
        }

        private sealed class AppDataImportResponsiblesApiConn : SimulatedApiConnection
        {
            public int NewOwnerResponsiblesCalls { get; private set; }
            public int DeleteSpecificOwnerResponsiblesCalls { get; private set; }
            public List<(int ownerId, string dn, int responsibleType)> Inserted { get; } = [];
            public List<(int ownerId, string dn, int responsibleType)> Deleted { get; } = [];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
            {
                if (query == OwnerQueries.newOwnerResponsibles)
                {
                    ++NewOwnerResponsiblesCalls;
                    if (variables != null)
                    {
                        object? objects = GetAnonymousValue(variables, "responsibles");
                        if (objects is IEnumerable enumerable)
                        {
                            foreach (object entry in enumerable)
                            {
                                Inserted.Add((
                                    GetAnonymousInt(entry, "owner_id"),
                                    GetAnonymousString(entry, "dn"),
                                    GetAnonymousInt(entry, "responsible_type")));
                            }
                        }
                    }
                    return Task.FromResult((QueryResponseType)(object)new object());
                }

                if (query == OwnerQueries.deleteSpecificOwnerResponsibles)
                {
                    ++DeleteSpecificOwnerResponsiblesCalls;
                    int ownerId = GetAnonymousInt(variables, "ownerId");
                    object? objects = GetAnonymousValue(variables, "objects");
                    if (objects is IEnumerable enumerable)
                    {
                        foreach (object entry in enumerable)
                        {
                            object? dnObject = GetAnonymousValue(entry, "dn");
                            object? typeObject = GetAnonymousValue(entry, "responsible_type");
                            Deleted.Add((
                                ownerId,
                                GetAnonymousString(dnObject, "_eq"),
                                GetAnonymousInt(typeObject, "_eq")));
                        }
                    }
                    return Task.FromResult((QueryResponseType)(object)new object());
                }

                throw new NotImplementedException($"Query not implemented in responsibles test api: {query}");
            }

            private static int GetAnonymousInt(object? variables, string propertyName)
            {
                object? value = GetAnonymousValue(variables, propertyName);
                return value is int intValue ? intValue : 0;
            }

            private static string GetAnonymousString(object? variables, string propertyName)
            {
                object? value = GetAnonymousValue(variables, propertyName);
                return value as string ?? "";
            }

            private static object? GetAnonymousValue(object? variables, string propertyName)
            {
                if (variables == null)
                {
                    return null;
                }
                PropertyInfo? property = variables.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                return property?.GetValue(variables);
            }
        }

        private sealed class AppDataImportSaveAppApiConn : SimulatedApiConnection
        {
            public int NewOwnerCalls { get; private set; }
            public int UpdateOwnerCalls { get; private set; }
            public int UpdateChangelogOwnerCalls { get; private set; }
            public List<char> ChangelogActions { get; } = [];
            public Dictionary<(int ownerId, string importSource), List<ModellingAppServer>> AppServersByOwner { get; } = [];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
            {
                if (query == OwnerQueries.newOwner)
                {
                    ++NewOwnerCalls;
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper
                    {
                        ReturnIds = [new ReturnId { NewId = 1 }]
                    });
                }

                if (query == OwnerQueries.updateOwner)
                {
                    ++UpdateOwnerCalls;
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper
                    {
                        ReturnIds = [new ReturnId { NewId = 1 }]
                    });
                }

                if (query == OwnerQueries.updateChangelogOwner)
                {
                    ++UpdateChangelogOwnerCalls;
                    char? action = GetAnonymousChar(variables, "change_action");
                    if (action.HasValue)
                    {
                        ChangelogActions.Add(action.Value);
                    }
                    return Task.FromResult((QueryResponseType)(object)new object());
                }

                if (query == ModellingQueries.getAppServersBySource)
                {
                    int ownerId = GetAnonymousInt(variables, "appId");
                    string importSource = GetAnonymousString(variables, "importSource");
                    List<ModellingAppServer> appServers = AppServersByOwner.TryGetValue((ownerId, importSource), out List<ModellingAppServer>? value)
                        ? value
                        : [];
                    return Task.FromResult((QueryResponseType)(object)appServers);
                }

                if (query == ImportQueries.addImportForOwner)
                {
                    return Task.FromResult((QueryResponseType)(object)new InsertImportControl
                    {
                        Returning = new List<ImportControl>
                        {
                            new ImportControl { ControlId = 123 }
                        }
                    });
                }

                if (query == MonitorQueries.addDataImportLogEntry)
                {
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper
                    {
                        ReturnIds = [new ReturnId { NewId = 1 }]
                    });
                }

                throw new NotImplementedException($"Query not implemented in save-app test api: {query}");
            }

            private static char? GetAnonymousChar(object? variables, string propertyName)
            {
                if (variables == null)
                {
                    return null;
                }
                PropertyInfo? property = variables.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property == null)
                {
                    return null;
                }
                object? value = property.GetValue(variables);
                return value is char charValue ? charValue : null;
            }

            private static int GetAnonymousInt(object? variables, string propertyName)
            {
                if (variables == null)
                {
                    return 0;
                }
                PropertyInfo? property = variables.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property == null)
                {
                    return 0;
                }
                object? value = property.GetValue(variables);
                return value is int intValue ? intValue : 0;
            }

            private static string GetAnonymousString(object? variables, string propertyName)
            {
                if (variables == null)
                {
                    return "";
                }
                PropertyInfo? property = variables.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property == null)
                {
                    return "";
                }
                object? value = property.GetValue(variables);
                return value as string ?? "";
            }
        }

        private sealed class ResolverTestAppDataImport : AppDataImport
        {
            private readonly Dictionary<string, string?> resolutions;
            private readonly Dictionary<string, UiUser> uiUsersByDn;
            private readonly Dictionary<string, List<string>> groupMembersByDn;
            private readonly Dictionary<string, string?> groupResolutions;
            public List<string> ResolvedIdentifiers { get; } = [];
            public List<string> ResolvedGroupIdentifiers { get; } = [];

            public ResolverTestAppDataImport(
                Dictionary<string, string?> resolutions,
                Dictionary<string, UiUser>? uiUsersByDn = null,
                Dictionary<string, List<string>>? groupMembersByDn = null,
                Dictionary<string, string?>? groupResolutions = null)
                : base(new SimulatedApiConnection(), new GlobalConfig())
            {
                this.resolutions = resolutions;
                this.uiUsersByDn = uiUsersByDn ?? new(StringComparer.OrdinalIgnoreCase);
                this.groupMembersByDn = groupMembersByDn ?? new(StringComparer.OrdinalIgnoreCase);
                this.groupResolutions = groupResolutions ?? new(StringComparer.OrdinalIgnoreCase);
            }

            protected override Task<string?> ResolveImportedUserIdentifierToDn(string userIdentifier)
            {
                ResolvedIdentifiers.Add(userIdentifier);
                return Task.FromResult(resolutions.TryGetValue(userIdentifier, out string? resolvedDn) ? resolvedDn : null);
            }

            protected override Task<UiUser?> ResolveImportedUiUser(string responsibleDn)
            {
                return Task.FromResult(uiUsersByDn.TryGetValue(responsibleDn, out UiUser? uiUser) ? uiUser : null);
            }

            protected override Task<List<string>> ResolveImportedGroupMembers(Ldap ldap, string groupDn)
            {
                return Task.FromResult(groupMembersByDn.TryGetValue(groupDn, out List<string>? members) ? members : []);
            }

            protected override Task<string?> ResolveImportedGroupIdentifierToDn(string groupIdentifier)
            {
                ResolvedGroupIdentifiers.Add(groupIdentifier);
                return Task.FromResult(groupResolutions.TryGetValue(groupIdentifier, out string? resolvedDn) ? resolvedDn : null);
            }
        }

        private sealed class RoleRemovalTrackingImport : AppDataImport
        {
            public List<(string dn, string role)> RemovedRoleAssignments { get; } = [];

            public RoleRemovalTrackingImport(ApiConnection apiConnection)
                : base(apiConnection, new GlobalConfig())
            {
            }

            protected override Task RemoveRoleFromDn(string dn, string role)
            {
                RemovedRoleAssignments.Add((dn, role));
                return Task.CompletedTask;
            }
        }
    }
}

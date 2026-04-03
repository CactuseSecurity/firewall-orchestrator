using System;
using System.Collections.Generic;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Middleware.Server;
using Novell.Directory.Ldap;
using NUnit.Framework;

namespace FWO.Test
{
    internal partial class AppDataImportTest
    {
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
    }
}

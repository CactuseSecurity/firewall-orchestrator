using System;
using System.Collections.Generic;
using FWO.Data;
using FWO.Middleware.Server;
using NUnit.Framework;

namespace FWO.Test
{
    internal partial class AppDataImportTest
    {
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
    }
}

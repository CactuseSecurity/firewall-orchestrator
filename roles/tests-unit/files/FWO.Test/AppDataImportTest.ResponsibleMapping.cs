using System;
using System.Collections.Generic;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Middleware.Server;
using NUnit.Framework;

namespace FWO.Test
{
    internal partial class AppDataImportTest
    {
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
    }
}

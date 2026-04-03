using System.Collections.Generic;
using System;
using FWO.Config.Api;
using FWO.Data;
using FWO.Middleware.Server;
using NUnit.Framework;

namespace FWO.Test
{
    internal partial class AppDataImportTest
    {
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
    }
}

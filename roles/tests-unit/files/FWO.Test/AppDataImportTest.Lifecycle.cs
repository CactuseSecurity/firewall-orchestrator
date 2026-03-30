using System;
using System.Collections.Generic;
using System.Linq;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Middleware.Server;
using NUnit.Framework;

namespace FWO.Test
{
    internal partial class AppDataImportTest
    {
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
        public async Task SaveApp_SetsDecommDate_WhenLifecycleStateChangesToInactive()
        {
            AppDataImportSaveAppApiConn apiConn = new();
            TestAppDataImport import = new(apiConn, new GlobalConfig());
            SetHasImmediateAppDecommNotificationForImport(import, true);
            SetOwnerLifeCycleMap(import, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Inactive"] = 11
            });
            SetOwnerLifeCycleActiveMap(import, new Dictionary<int, bool>
            {
                [10] = true,
                [11] = false
            });
            SetExistingApps(import,
            [
                new() { Id = 30, Name = "App-30", ExtAppId = "APP-30", OwnerLifeCycleStateId = 10 }
            ]);
            ModellingImportAppData incomingApp = new()
            {
                Name = "App-30",
                ExtAppId = "APP-30",
                ImportSource = "SRC-30",
                OwnerLifecycleState = "Inactive"
            };

            bool imported = await InvokeSaveApp(import, incomingApp, new OwnerChangeImportTracker(apiConn));

            Assert.That(imported, Is.True);
            Assert.That(apiConn.LastUpdateOwnerDecommDate, Is.Not.Null);
        }

        [Test]
        public async Task SaveApp_DoesNotCheckActiveRules_WhenImmediateAppDecommNotificationIsMissing()
        {
            AppDataImportSaveAppApiConn apiConn = new();
            TestAppDataImport import = new(apiConn, new GlobalConfig());
            SetHasImmediateAppDecommNotificationForImport(import, false);
            SetOwnerLifeCycleMap(import, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Inactive"] = 11
            });
            SetOwnerLifeCycleActiveMap(import, new Dictionary<int, bool>
            {
                [10] = true,
                [11] = false
            });
            SetExistingApps(import,
            [
                new() { Id = 35, Name = "App-35", ExtAppId = "APP-35", OwnerLifeCycleStateId = 10 }
            ]);
            ModellingImportAppData incomingApp = new()
            {
                Name = "App-35",
                ExtAppId = "APP-35",
                ImportSource = "SRC-35",
                OwnerLifecycleState = "Inactive"
            };

            bool imported = await InvokeSaveApp(import, incomingApp, new OwnerChangeImportTracker(apiConn));

            Assert.That(imported, Is.True);
            Assert.That(import.CheckedOwners, Is.Empty);
        }

        [Test]
        public async Task SaveApp_ChecksActiveRules_WhenImmediateAppDecommNotificationExists()
        {
            AppDataImportSaveAppApiConn apiConn = new();
            TestAppDataImport import = new(apiConn, new GlobalConfig());
            SetHasImmediateAppDecommNotificationForImport(import, true);
            SetOwnerLifeCycleMap(import, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Inactive"] = 11
            });
            SetOwnerLifeCycleActiveMap(import, new Dictionary<int, bool>
            {
                [10] = true,
                [11] = false
            });
            SetExistingApps(import,
            [
                new() { Id = 36, Name = "App-36", ExtAppId = "APP-36", OwnerLifeCycleStateId = 10 }
            ]);
            ModellingImportAppData incomingApp = new()
            {
                Name = "App-36",
                ExtAppId = "APP-36",
                ImportSource = "SRC-36",
                OwnerLifecycleState = "Inactive"
            };

            bool imported = await InvokeSaveApp(import, incomingApp, new OwnerChangeImportTracker(apiConn));

            Assert.That(imported, Is.True);
            Assert.That(import.CheckedOwners.Select(owner => owner.Id), Is.EqualTo(new[] { 36 }));
        }

        [Test]
        public async Task SaveApp_SetsDecommDate_WhenNewOwnerStartsInactive()
        {
            AppDataImportSaveAppApiConn apiConn = new();
            AppDataImport import = new(apiConn, new GlobalConfig());
            SetOwnerLifeCycleMap(import, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Inactive"] = 11
            });
            SetOwnerLifeCycleActiveMap(import, new Dictionary<int, bool>
            {
                [11] = false
            });
            SetExistingApps(import, []);
            ModellingImportAppData incomingApp = new()
            {
                Name = "App-32",
                ExtAppId = "APP-32",
                ImportSource = "SRC-32",
                OwnerLifecycleState = "Inactive"
            };

            bool imported = await InvokeSaveApp(import, incomingApp, new OwnerChangeImportTracker(apiConn));

            Assert.That(imported, Is.True);
            Assert.That(apiConn.LastNewOwnerDecommDate, Is.Not.Null);
        }

        [Test]
        public async Task SaveApp_ClearsDecommDate_WhenLifecycleStateChangesToActive()
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
                new() { Id = 31, Name = "App-31", ExtAppId = "APP-31", OwnerLifeCycleStateId = 11, DecommDate = DateTime.UtcNow.AddDays(-1) }
            ]);
            ModellingImportAppData incomingApp = new()
            {
                Name = "App-31",
                ExtAppId = "APP-31",
                ImportSource = "SRC-31",
                OwnerLifecycleState = "Active"
            };

            bool imported = await InvokeSaveApp(import, incomingApp, new OwnerChangeImportTracker(apiConn));

            Assert.That(imported, Is.True);
            Assert.That(apiConn.LastUpdateOwnerDecommDate, Is.Null);
        }

        [Test]
        public async Task SaveApp_SetsDecommDate_WhenOwnerStaysInactiveButDateWasMissing()
        {
            AppDataImportSaveAppApiConn apiConn = new();
            TestAppDataImport import = new(apiConn, new GlobalConfig());
            SetOwnerLifeCycleMap(import, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Inactive"] = 11
            });
            SetOwnerLifeCycleActiveMap(import, new Dictionary<int, bool>
            {
                [11] = false
            });
            SetExistingApps(import,
            [
                new() { Id = 33, Name = "App-33", ExtAppId = "APP-33", OwnerLifeCycleStateId = 11, DecommDate = null }
            ]);
            ModellingImportAppData incomingApp = new()
            {
                Name = "App-33",
                ExtAppId = "APP-33",
                ImportSource = "SRC-33",
                OwnerLifecycleState = "Inactive"
            };

            bool imported = await InvokeSaveApp(import, incomingApp, new OwnerChangeImportTracker(apiConn));

            Assert.That(imported, Is.True);
            Assert.That(apiConn.LastUpdateOwnerDecommDate, Is.Not.Null);
        }

        [Test]
        public async Task SaveApp_SetsDecommDate_WhenPreviousLifecycleStateWasMissingAndNewStateIsInactive()
        {
            AppDataImportSaveAppApiConn apiConn = new();
            TestAppDataImport import = new(apiConn, new GlobalConfig());
            SetOwnerLifeCycleMap(import, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Inactive"] = 11
            });
            SetOwnerLifeCycleActiveMap(import, new Dictionary<int, bool>
            {
                [11] = false
            });
            SetExistingApps(import,
            [
                new() { Id = 34, Name = "App-34", ExtAppId = "APP-34", OwnerLifeCycleStateId = null, DecommDate = null }
            ]);
            ModellingImportAppData incomingApp = new()
            {
                Name = "App-34",
                ExtAppId = "APP-34",
                ImportSource = "SRC-34",
                OwnerLifecycleState = "Inactive"
            };

            bool imported = await InvokeSaveApp(import, incomingApp, new OwnerChangeImportTracker(apiConn));

            Assert.That(imported, Is.True);
            Assert.That(apiConn.LastUpdateOwnerDecommDate, Is.Not.Null);
        }

        [Test]
        public async Task AddOwnerLifeCycleStateActiveChangeIfNeeded_LogsDeactivate_WhenLifecycleStateChangesToInactive()
        {
            AppDataImportSaveAppApiConn apiConn = new();
            TestAppDataImport import = new(apiConn, new GlobalConfig());
            SetHasImmediateAppDecommNotificationForImport(import, true);
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
            Assert.That(import.CheckedOwners, Has.Count.EqualTo(1));
            Assert.That(import.CheckedOwners[0].Id, Is.EqualTo(8));
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
        public async Task AddOwnerLifeCycleStateActiveChangeIfNeeded_DoesNotCheckActiveRules_WhenLifecycleStateChangesToActive()
        {
            AppDataImportSaveAppApiConn apiConn = new();
            TestAppDataImport import = new(apiConn, new GlobalConfig());
            SetOwnerLifeCycleActiveMap(import, new Dictionary<int, bool>
            {
                [10] = true,
                [11] = false
            });
            FwoOwner existingApp = new() { Id = 10, OwnerLifeCycleStateId = 11 };
            OwnerChangeImportTracker tracker = new(apiConn);

            await InvokeAddOwnerLifeCycleStateActiveChangeIfNeeded(import, existingApp, 10, "SRC-10", tracker);

            Assert.That(import.CheckedOwners, Is.Empty);
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
    }
}

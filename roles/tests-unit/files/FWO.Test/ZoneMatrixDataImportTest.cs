using System.Linq;
using System.Reflection;
using System.Text.Json;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Middleware.Server;
using FWO.Services;
using NetTools;
using NUnit.Framework;
using System.Net;

namespace FWO.Test
{
    [TestFixture]
    internal class ZoneMatrixDataImportTest
    {
        private static List<ComplianceNetworkZone> CreateInitialMatrixZones()
        {
            return
            [
                new ComplianceNetworkZone
                {
                    Id = 10,
                    Name = "Zone A old",
                    IdString = "zone-a",
                    IPRanges = [new IPAddressRange(IPAddress.Parse("192.0.2.0"), IPAddress.Parse("192.0.2.255"))],
                    AllowedCommunicationDestinations =
                    [
                        new ComplianceNetworkZone { Id = 20, IdString = "zone-b" }
                    ]
                },
                new ComplianceNetworkZone
                {
                    Id = 20,
                    Name = "Zone B stale",
                    IdString = "zone-b",
                    IPRanges = [new IPAddressRange(IPAddress.Parse("198.51.100.0"), IPAddress.Parse("198.51.100.255"))]
                }
            ];
        }

        private static List<ComplianceNetworkZone> CreateReloadedMatrixZones()
        {
            return
            [
                new ComplianceNetworkZone
                {
                    Id = 10,
                    Name = "Zone A updated",
                    IdString = "zone-a",
                    IPRanges = [new IPAddressRange(IPAddress.Parse("192.0.2.0"), IPAddress.Parse("192.0.2.255"))],
                    AllowedCommunicationDestinations =
                    [
                        new ComplianceNetworkZone { Id = 20, IdString = "zone-b" }
                    ]
                },
                new ComplianceNetworkZone
                {
                    Id = 30,
                    Name = "Zone C",
                    IdString = "zone-c",
                    IPRanges = [new IPAddressRange(IPAddress.Parse("203.0.113.0"), IPAddress.Parse("203.0.113.255"))]
                }
            ];
        }

        private static readonly string[] kZoneCDestination = ["zone-c"];
        private static readonly string[] kMissingZoneDestination = ["zone-does-not-exist"];
        private static readonly string[] kOtherMissingZoneDestination = ["zone-also-missing"];
        private static readonly string[] kAutoInternetDestination = [NetworkZoneService.kAutoCalculatedInternetZoneIdString];
        private static readonly string[] kAutoUndefinedInternalDestination = [NetworkZoneService.kAutoCalculatedUndefinedInternalZoneIdString];

        private const string kMgmtA = "mgmt-a";
        private const string kMgmtB = "mgmt-b";
        private const string kFwAccess = "fw-access-01";
        private const string kFwCore = "fw-core-01";
        private const string kBorderRouter = "border-router-01";
        private const string kUnknownDevice = "fw-does-not-exist";
        private const string kPathToRootField = "path_to_root";
        private const string kPathToInternetField = "path_to_internet";
        private const string kOrderToRootField = "order_to_root";
        private const string kOrderToInternetField = "order_to_internet";

        private const int kZoneAId = 101;
        private const int kZoneBId = 102;
        private const int kZoneAIpRangeId = 501;
        private const int kZoneASecondIpRangeId = 502;
        private const int kZoneBIpRangeId = 503;
        private const int kFwAccessId = 11;
        private const int kFwCoreId = 12;
        private const int kBorderRouterId = 21;
        private const string kZoneASubnet = "192.0.2.0/24";
        private const string kZoneASubnetStart = "192.0.2.0";
        private const string kZoneASubnetEnd = "192.0.2.255";
        private const string kSecondSubnet = "198.51.100.0/24";
        private const string kSecondSubnetStart = "198.51.100.0";
        private const string kSecondSubnetEnd = "198.51.100.255";

        private static readonly DeviceRefData[] kRootPath =
        [
            new() { MgmtName = kMgmtA, DeviceName = kFwAccess },
            new() { MgmtName = kMgmtA, DeviceName = kFwCore }
        ];

        private static readonly DeviceRefData[] kInternetPath =
        [
            new() { MgmtName = kMgmtA, DeviceName = kFwCore },
            new() { MgmtName = kMgmtB, DeviceName = kBorderRouter }
        ];

        private static readonly DeviceRefData[] kUnknownDevicePath =
        [
            new() { MgmtName = kMgmtA, DeviceName = kUnknownDevice }
        ];

        private static readonly DeviceRefData[] kSingleCoreDevicePath =
        [
            new() { MgmtName = kMgmtA, DeviceName = kFwCore }
        ];

        private static readonly DeviceRefData[] kDuplicateDevicePath =
        [
            new() { MgmtName = kMgmtA, DeviceName = kFwCore },
            new() { MgmtName = kMgmtA, DeviceName = kFwCore }
        ];

        private const string kDocumentedKeysJson = """
        {
          "name": "Matrix Raw",
          "comment": "raw json using the documented wire names",
          "areas": [
            {
              "name": "Zone A",
              "id_string": "zone-a",
              "subnets": [
                {
                  "name": "Office network",
                  "ip": "192.0.2.0/24",
                  "path_to_root": [
                    { "mgmt_name": "mgmt-a", "device_name": "fw-does-not-exist" }
                  ],
                  "path_to_internet": [
                    { "mgmt_name": "mgmt-a", "device_name": "fw-core-01" }
                  ]
                }
              ],
              "communication_to": []
            }
          ]
        }
        """;

        private const string kLegacyFormatJson = """
        {
          "name": "Matrix Legacy",
          "comment": "old export without path keys",
          "areas": [
            {
              "name": "Zone A",
              "id_string": "zone-a",
              "subnets": [ { "name": "Zone A subnet", "ip": "192.0.2.0/24" } ],
              "communication_to": []
            }
          ]
        }
        """;

        [Test]
        public async Task Run_ReturnsErrorWhenMatrixNameMissing()
        {
            ZoneMatrixImportApiConnection apiConnection = new();
            SimulatedGlobalConfig globalConfig = CreateNoAutoCalcConfig();
            ZoneMatrixDataImport import = new(apiConnection, globalConfig);

            string result = await import.Run(
                "missing-name.json",
                CreateImportJson(string.Empty, CreateZone("zone-a", "Zone A", "192.0.2.0/24")),
                "tester",
                "cn=tester");

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.Contain("No Matrix Name"));
                Assert.That(apiConnection.Count(MonitorQueries.addDataImportLogEntry), Is.EqualTo(1));
                Assert.That(apiConnection.Count(ComplianceQueries.getMatrixByName), Is.EqualTo(0));
            });
        }

        [Test]
        public async Task Run_ReturnsErrorWhenZoneNamesAreDuplicate()
        {
            ZoneMatrixImportApiConnection apiConnection = new();
            SimulatedGlobalConfig globalConfig = CreateNoAutoCalcConfig();
            ZoneMatrixDataImport import = new(apiConnection, globalConfig);

            string result = await import.Run(
                "duplicate-names.json",
                CreateImportJson(
                    "Matrix A",
                    CreateZone("zone-a", "Zone A", "192.0.2.0/24"),
                    CreateZone("zone-b", "Zone A", "198.51.100.10", "198.51.100.20")),
                "tester",
                "cn=tester");

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.Contain("Duplicate Zone Names"));
                Assert.That(apiConnection.Count(MonitorQueries.addDataImportLogEntry), Is.EqualTo(1));
                Assert.That(apiConnection.Count(ComplianceQueries.getMatrixByName), Is.EqualTo(0));
            });
        }

        [Test]
        public async Task Run_ReturnsErrorWhenZoneIdStringsAreDuplicate()
        {
            ZoneMatrixImportApiConnection apiConnection = new();
            SimulatedGlobalConfig globalConfig = CreateNoAutoCalcConfig();
            ZoneMatrixDataImport import = new(apiConnection, globalConfig);

            string result = await import.Run(
                "duplicate-idstrings.json",
                CreateImportJson(
                    "Matrix A",
                    CreateZone("zone-a", "Zone A", "192.0.2.0/24"),
                    CreateZone("zone-a", "Zone B", "198.51.100.10", "198.51.100.20")),
                "tester",
                "cn=tester");

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.Contain("Duplicate Zone IdStrings"));
                Assert.That(apiConnection.Count(MonitorQueries.addDataImportLogEntry), Is.EqualTo(1));
                Assert.That(apiConnection.Count(ComplianceQueries.getMatrixByName), Is.EqualTo(0));
            });
        }

        [Test]
        public async Task Run_CreatesNewMatrixAndImportsZonesWithSpecialZones()
        {
            ZoneMatrixImportApiConnection apiConnection = new()
            {
                MatrixByNameResponse = []
            };
            apiConnection.MatrixZoneResponses.Add([]);
            apiConnection.MatrixZoneResponses.Add([CreateExistingZone(101, "zone-a", "Zone A")]);
            apiConnection.AddCriterionResponse = new ReturnIdWrapper
            {
                ReturnIds = [new ReturnId { InsertedId = 77 }]
            };

            SimulatedGlobalConfig globalConfig = CreateAutoCalcConfig();
            ZoneMatrixDataImport import = new(apiConnection, globalConfig);

            string result = await import.Run(
                "zones.json",
                CreateImportJson(
                    "Matrix A",
                    CreateZone(
                        "zone-a",
                        "Zone A",
                        "192.0.2.0/24")),
                "tester",
                "cn=tester");

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.StartWith("Ok: Imported from zones.json"));
                Assert.That(result, Does.Contain("Total number of network zones: 1"));
                Assert.That(result, Does.Contain("new: 1"));
                Assert.That(result, Does.Contain("updated: 0"));
                Assert.That(result, Does.Contain("Deleted: 0"));
                Assert.That(result, Does.Contain("Inserted connections: 0"));
                Assert.That(apiConnection.Count(ComplianceQueries.getMatrixByName), Is.EqualTo(1));
                Assert.That(apiConnection.Count(ComplianceQueries.addCriterion), Is.EqualTo(1));
                Assert.That(apiConnection.Count(NetworkZoneQueries.getNetworkZonesForMatrix), Is.EqualTo(2));
                Assert.That(apiConnection.Count(NetworkZoneQueries.addNetworkZone), Is.EqualTo(3));
                Assert.That(apiConnection.Count(MonitorQueries.addDataImportLogEntry), Is.EqualTo(1));
            });
        }

        [Test]
        public async Task Run_UpdatesExistingMatrixAndRemovesStaleZones()
        {
            ZoneMatrixImportApiConnection apiConnection = new()
            {
                MatrixByNameResponse =
                [
                    new ComplianceCriterion
                    {
                        Id = 55,
                        Name = "Matrix B",
                        ImportSource = "seed.json"
                    }
                ]
            };
            apiConnection.MatrixZoneResponses.Add(CreateInitialMatrixZones());
            apiConnection.MatrixZoneResponses.Add(CreateInitialMatrixZones());
            apiConnection.MatrixZoneResponses.Add(CreateReloadedMatrixZones());

            SimulatedGlobalConfig globalConfig = CreateNoAutoCalcConfig();
            ZoneMatrixDataImport import = new(apiConnection, globalConfig);

            string result = await import.Run(
                "matrix-b.json",
                CreateImportJson(
                    "Matrix B",
                    CreateZone("zone-a", "Zone A updated", "192.0.2.0/24", commTargets: kZoneCDestination),
                    CreateZone("zone-c", "Zone C", "203.0.113.10", "203.0.113.20")),
                "tester",
                "cn=tester");

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.StartWith("Ok: Imported from matrix-b.json"));
                Assert.That(result, Does.Contain("new: 1"));
                Assert.That(result, Does.Contain("updated: 1"));
                Assert.That(result, Does.Contain("Deleted: 1"));
                Assert.That(result, Does.Contain("Inserted connections: 1"));
                Assert.That(result, Does.Contain("removed connections: 1"));
                Assert.That(apiConnection.Count(ComplianceQueries.getMatrixByName), Is.EqualTo(1));
                Assert.That(apiConnection.Count(ComplianceQueries.updateCriterionMetadata), Is.EqualTo(1));
                Assert.That(apiConnection.Count(NetworkZoneQueries.getNetworkZonesForMatrix), Is.EqualTo(3));
                Assert.That(apiConnection.Count(NetworkZoneQueries.removeNetworkZone), Is.EqualTo(1));
                Assert.That(apiConnection.Count(MonitorQueries.addDataImportLogEntry), Is.EqualTo(1));
            });
        }

        [Test]
        public async Task Run_DoesNotReportAutoCalculatedZonesAsDeletedOnReimport()
        {
            ZoneMatrixImportApiConnection apiConnection = new()
            {
                MatrixByNameResponse =
                [
                    new ComplianceCriterion
                    {
                        Id = 55,
                        Name = "Matrix A",
                        ImportSource = "seed.json"
                    }
                ],
                Managements = CreateDeviceInventory()
            };
            apiConnection.MatrixZoneResponses.Add(CreateMatrixZonesWithAutoCalculatedZones());
            ZoneMatrixDataImport import = new(apiConnection, CreateAutoCalcConfig());

            string result = await import.Run(
                "reimport.json",
                CreateImportJson("Matrix A", CreateZone("zone-a", "Zone A", "192.0.2.0/24")),
                "tester",
                "cn=tester");

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.StartWith("Ok: Imported from reimport.json"));
                Assert.That(result, Does.Contain("Deleted: 0"));
                Assert.That(result, Does.Contain("failed deletions: 0"));
            });
        }

        [Test]
        public async Task Run_StillDeactivatesStaleZonesBesideAutoCalculatedOnes()
        {
            ZoneMatrixImportApiConnection apiConnection = new()
            {
                MatrixByNameResponse =
                [
                    new ComplianceCriterion
                    {
                        Id = 55,
                        Name = "Matrix A",
                        ImportSource = "seed.json"
                    }
                ],
                Managements = CreateDeviceInventory()
            };
            List<ComplianceNetworkZone> existingZones = CreateMatrixZonesWithAutoCalculatedZones();
            existingZones.Add(CreateExistingZone(104, "zone-stale", "Zone stale"));
            apiConnection.MatrixZoneResponses.Add(existingZones);
            ZoneMatrixDataImport import = new(apiConnection, CreateAutoCalcConfig());

            string result = await import.Run(
                "reimport-with-stale.json",
                CreateImportJson("Matrix A", CreateZone("zone-a", "Zone A", "192.0.2.0/24")),
                "tester",
                "cn=tester");

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.StartWith("Ok: Imported from reimport-with-stale.json"));
                Assert.That(result, Does.Contain("Deleted: 1"));
            });
        }

        [Test]
        public async Task Run_ReturnsErrorWhenMatrixExistsWithoutImportSource()
        {
            ZoneMatrixImportApiConnection apiConnection = new()
            {
                MatrixByNameResponse =
                [
                    new ComplianceCriterion
                    {
                        Id = 55,
                        Name = "Matrix B",
                        ImportSource = string.Empty
                    }
                ]
            };
            SimulatedGlobalConfig globalConfig = CreateNoAutoCalcConfig();
            ZoneMatrixDataImport import = new(apiConnection, globalConfig);

            string result = await import.Run(
                "manual-matrix.json",
                CreateImportJson("Matrix B", CreateZone("zone-a", "Zone A", "192.0.2.0/24")),
                "tester",
                "cn=tester");

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.Contain("Manually created matrix existing with same Name"));
                Assert.That(apiConnection.Count(ComplianceQueries.getMatrixByName), Is.EqualTo(1));
                Assert.That(apiConnection.Count(MonitorQueries.addDataImportLogEntry), Is.EqualTo(1));
            });
        }

        [Test]
        public async Task Run_ImportsSubnetPathsWhenDevicesExist()
        {
            ZoneMatrixImportApiConnection apiConnection = CreateNewMatrixConnection();
            ZoneMatrixDataImport import = new(apiConnection, CreateNoAutoCalcConfig());

            string result = await import.Run(
                "paths.json",
                CreateImportJson(
                    "Matrix A",
                    CreateZone("zone-a", "Zone A", "192.0.2.0/24", pathToRoot: kRootPath, pathToInternet: kInternetPath)),
                "tester",
                "cn=tester");

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.StartWith("Ok: Imported from paths.json"));
                Assert.That(apiConnection.Count(DeviceQueries.getManagementNames), Is.EqualTo(1));
                Assert.That(apiConnection.Count(NetworkZoneQueries.addNetworkZone), Is.EqualTo(1));
            });
        }

        [Test]
        public async Task Run_AcceptsLegacyFormatWithoutPathKeys()
        {
            ZoneMatrixImportApiConnection apiConnection = CreateNewMatrixConnection();
            ZoneMatrixDataImport import = new(apiConnection, CreateNoAutoCalcConfig());

            string result = await import.Run("legacy.json", kLegacyFormatJson, "tester", "cn=tester");

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.StartWith("Ok: Imported from legacy.json"));
                Assert.That(result, Does.Contain("Total number of network zones: 1"));
                Assert.That(apiConnection.Count(NetworkZoneQueries.addNetworkZone), Is.EqualTo(1));
            });
        }

        [Test]
        public async Task Run_AcceptsSameDeviceInRootAndInternetPath()
        {
            ZoneMatrixImportApiConnection apiConnection = CreateNewMatrixConnection();
            ZoneMatrixDataImport import = new(apiConnection, CreateNoAutoCalcConfig());

            string result = await import.Run(
                "shared-device.json",
                CreateImportJson(
                    "Matrix A",
                    CreateZone("zone-a", "Zone A", "192.0.2.0/24",
                        pathToRoot: kSingleCoreDevicePath, pathToInternet: kSingleCoreDevicePath)),
                "tester",
                "cn=tester");

            Assert.That(result, Does.StartWith("Ok: Imported from shared-device.json"));
        }

        [Test]
        public async Task Run_AcceptsSameDeviceInPathsOfDifferentSubnets()
        {
            ZoneMatrixImportApiConnection apiConnection = CreateNewMatrixConnection();
            ZoneMatrixDataImport import = new(apiConnection, CreateNoAutoCalcConfig());

            NetworkZoneData zone = CreateZone("zone-a", "Zone A", "192.0.2.0/24", pathToRoot: kSingleCoreDevicePath);
            zone.IpData.Add(new ZoneIpRangeData
            {
                Name = "Zone A second subnet",
                Ip = "198.51.100.0/24",
                PathToRoot = [.. kSingleCoreDevicePath]
            });

            string result = await import.Run("two-subnets.json", CreateImportJson("Matrix A", zone), "tester", "cn=tester");

            Assert.That(result, Does.StartWith("Ok: Imported from two-subnets.json"));
        }

        [Test]
        public async Task Run_ReturnsErrorWhenPathDeviceIsUnknown()
        {
            ZoneMatrixImportApiConnection apiConnection = CreateNewMatrixConnection();
            ZoneMatrixDataImport import = new(apiConnection, CreateNoAutoCalcConfig());

            string result = await import.Run(
                "unknown-device.json",
                CreateImportJson(
                    "Matrix A",
                    CreateZone("zone-a", "Zone A", "192.0.2.0/24", pathToRoot: kUnknownDevicePath)),
                "tester",
                "cn=tester");

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.Contain("Could not resolve devices"));
                Assert.That(result, Does.Contain($"{kMgmtA}/{kUnknownDevice}"));
                Assert.That(apiConnection.Count(NetworkZoneQueries.addNetworkZone), Is.EqualTo(0));
                Assert.That(apiConnection.Count(MonitorQueries.addDataImportLogEntry), Is.EqualTo(1));
            });
        }

        [Test]
        public async Task Run_ReturnsErrorWhenPathDeviceNameIsAmbiguous()
        {
            ZoneMatrixImportApiConnection apiConnection = CreateNewMatrixConnection();
            apiConnection.Managements = CreateAmbiguousDeviceInventory();
            ZoneMatrixDataImport import = new(apiConnection, CreateNoAutoCalcConfig());

            string result = await import.Run(
                "ambiguous-device.json",
                CreateImportJson(
                    "Matrix A",
                    CreateZone("zone-a", "Zone A", "192.0.2.0/24", pathToRoot: kSingleCoreDevicePath)),
                "tester",
                "cn=tester");

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.Contain("are ambiguous"));
                Assert.That(result, Does.Contain($"{kMgmtA}/{kFwCore}"));
                Assert.That(apiConnection.Count(NetworkZoneQueries.addNetworkZone), Is.EqualTo(0));
            });
        }

        [Test]
        public async Task Run_ReturnsErrorWhenPathContainsDuplicateDevice()
        {
            ZoneMatrixImportApiConnection apiConnection = CreateNewMatrixConnection();
            ZoneMatrixDataImport import = new(apiConnection, CreateNoAutoCalcConfig());

            string result = await import.Run(
                "duplicate-device.json",
                CreateImportJson(
                    "Matrix A",
                    CreateZone("zone-a", "Zone A", "192.0.2.0/24", pathToRoot: kDuplicateDevicePath)),
                "tester",
                "cn=tester");

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.Contain($"Duplicate device {kMgmtA}/{kFwCore}"));
                Assert.That(result, Does.Contain($"in {kPathToRootField} in subnet 192.0.2.0/24"));
                Assert.That(result, Does.Not.Contain(kPathToInternetField));
                Assert.That(apiConnection.Count(NetworkZoneQueries.addNetworkZone), Is.EqualTo(0));
            });
        }

        [Test]
        public async Task Run_NamesTheInternetPathWhenItContainsDuplicateDevice()
        {
            ZoneMatrixImportApiConnection apiConnection = CreateNewMatrixConnection();
            ZoneMatrixDataImport import = new(apiConnection, CreateNoAutoCalcConfig());

            string result = await import.Run(
                "duplicate-device-internet.json",
                CreateImportJson(
                    "Matrix A",
                    CreateZone("zone-a", "Zone A", "192.0.2.0/24", pathToInternet: kDuplicateDevicePath)),
                "tester",
                "cn=tester");

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.Contain($"Duplicate device {kMgmtA}/{kFwCore}"));
                Assert.That(result, Does.Contain($"in {kPathToInternetField} in subnet 192.0.2.0/24"));
                Assert.That(result, Does.Not.Contain(kPathToRootField));
                Assert.That(apiConnection.Count(NetworkZoneQueries.addNetworkZone), Is.EqualTo(0));
            });
        }

        [Test]
        public async Task Run_ReturnsErrorWhenCommunicationTargetIsUnknown()
        {
            ZoneMatrixImportApiConnection apiConnection = CreateNewMatrixConnection();
            ZoneMatrixDataImport import = new(apiConnection, CreateNoAutoCalcConfig());

            string result = await import.Run(
                "unknown-comm-target.json",
                CreateImportJson(
                    "Matrix A",
                    CreateZone("zone-a", "Zone A", "192.0.2.0/24", commTargets: kMissingZoneDestination)),
                "tester",
                "cn=tester");

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.Contain("Unknown communication target zone-does-not-exist in zone zone-a"));
                Assert.That(apiConnection.Count(ComplianceQueries.addCriterion), Is.EqualTo(0));
                Assert.That(apiConnection.Count(NetworkZoneQueries.addNetworkZone), Is.EqualTo(0));
            });
        }

        [Test]
        public async Task Run_ReportsEveryUnknownCommunicationTarget()
        {
            ZoneMatrixImportApiConnection apiConnection = CreateNewMatrixConnection();
            ZoneMatrixDataImport import = new(apiConnection, CreateNoAutoCalcConfig());

            string result = await import.Run(
                "unknown-comm-targets.json",
                CreateImportJson(
                    "Matrix A",
                    CreateZone("zone-a", "Zone A", "192.0.2.0/24", commTargets: kMissingZoneDestination),
                    CreateZone("zone-b", "Zone B", "198.51.100.0/24", commTargets: kOtherMissingZoneDestination)),
                "tester",
                "cn=tester");

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.Contain("Unknown communication target zone-does-not-exist in zone zone-a"));
                Assert.That(result, Does.Contain("Unknown communication target zone-also-missing in zone zone-b"));
                Assert.That(apiConnection.Count(NetworkZoneQueries.addNetworkZone), Is.EqualTo(0));
            });
        }

        [Test]
        public async Task Run_AcceptsAutoCalculatedInternetZoneAsCommunicationTargetWhenEnabled()
        {
            ZoneMatrixImportApiConnection apiConnection = CreateNewMatrixConnection();
            apiConnection.MatrixZoneResponses.Clear();
            apiConnection.MatrixZoneResponses.Add(CreateReloadedZonesWithAutoInternet());
            ZoneMatrixDataImport import = new(apiConnection, CreateAutoCalcConfig());

            string result = await import.Run(
                "auto-comm-target.json",
                CreateImportJson(
                    "Matrix A",
                    CreateZone("zone-a", "Zone A", "192.0.2.0/24", commTargets: kAutoInternetDestination)),
                "tester",
                "cn=tester");

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.StartWith("Ok: Imported from auto-comm-target.json"));
                Assert.That(result, Does.Not.Contain("Unknown communication target"));
            });
        }

        [Test]
        public async Task Run_RejectsAutoCalculatedInternetZoneAsCommunicationTargetWhenDisabled()
        {
            ZoneMatrixImportApiConnection apiConnection = CreateNewMatrixConnection();
            ZoneMatrixDataImport import = new(apiConnection, CreateNoAutoCalcConfig());

            string result = await import.Run(
                "auto-comm-target-disabled.json",
                CreateImportJson(
                    "Matrix A",
                    CreateZone("zone-a", "Zone A", "192.0.2.0/24", commTargets: kAutoInternetDestination)),
                "tester",
                "cn=tester");

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.Contain($"Unknown communication target {NetworkZoneService.kAutoCalculatedInternetZoneIdString} in zone zone-a"));
                Assert.That(apiConnection.Count(NetworkZoneQueries.addNetworkZone), Is.EqualTo(0));
            });
        }

        [Test]
        public async Task Run_RejectsAutoCalculatedUndefinedInternalZoneAsCommunicationTarget()
        {
            ZoneMatrixImportApiConnection apiConnection = CreateNewMatrixConnection();
            ZoneMatrixDataImport import = new(apiConnection, CreateAutoCalcConfig());

            string result = await import.Run(
                "auto-undefined-target.json",
                CreateImportJson(
                    "Matrix A",
                    CreateZone("zone-a", "Zone A", "192.0.2.0/24", commTargets: kAutoUndefinedInternalDestination)),
                "tester",
                "cn=tester");

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.Contain($"Unknown communication target {NetworkZoneService.kAutoCalculatedUndefinedInternalZoneIdString} in zone zone-a"));
                Assert.That(apiConnection.Count(NetworkZoneQueries.addNetworkZone), Is.EqualTo(0));
            });
        }

        [Test]
        public async Task Run_ReturnsErrorWhenIpCannotBeParsed()
        {
            ZoneMatrixImportApiConnection apiConnection = CreateNewMatrixConnection();
            ZoneMatrixDataImport import = new(apiConnection, CreateNoAutoCalcConfig());

            string result = await import.Run(
                "bad-ip.json",
                CreateImportJson("Matrix A", CreateZone("zone-a", "Zone A", "not-an-ip")),
                "tester",
                "cn=tester");

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.Contain("Bad Ips for subnet not-an-ip"));
                Assert.That(apiConnection.Count(ComplianceQueries.addCriterion), Is.EqualTo(0));
                Assert.That(apiConnection.Count(NetworkZoneQueries.addNetworkZone), Is.EqualTo(0));
            });
        }

        [Test]
        public async Task Run_ReturnsErrorWhenRangeMixesAddressFamilies()
        {
            ZoneMatrixImportApiConnection apiConnection = CreateNewMatrixConnection();
            ZoneMatrixDataImport import = new(apiConnection, CreateNoAutoCalcConfig());

            string result = await import.Run(
                "mixed-family.json",
                CreateImportJson("Matrix A", CreateZone("zone-a", "Zone A", "10.0.0.1", "2001:db8::ff")),
                "tester",
                "cn=tester");

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.Contain("Bad Ips for subnet 10.0.0.1"));
                Assert.That(apiConnection.Count(NetworkZoneQueries.addNetworkZone), Is.EqualTo(0));
            });
        }

        [Test]
        public async Task Run_ReturnsErrorWhenRangeEndsBeforeItStarts()
        {
            ZoneMatrixImportApiConnection apiConnection = CreateNewMatrixConnection();
            ZoneMatrixDataImport import = new(apiConnection, CreateNoAutoCalcConfig());

            string result = await import.Run(
                "descending-range.json",
                CreateImportJson("Matrix A", CreateZone("zone-a", "Zone A", "192.0.2.5", "192.0.2.1")),
                "tester",
                "cn=tester");

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.Contain("Bad Ips for subnet 192.0.2.5"));
                Assert.That(apiConnection.Count(NetworkZoneQueries.addNetworkZone), Is.EqualTo(0));
            });
        }

        [Test]
        public async Task Run_TreatsBlankIpEndAsAbsentInValidationAndWrite()
        {
            ZoneMatrixImportApiConnection apiConnection = CreateNewMatrixConnection();
            ZoneMatrixDataImport import = new(apiConnection, CreateNoAutoCalcConfig());

            string result = await import.Run(
                "blank-ip-end.json",
                CreateImportJson("Matrix A", CreateZone("zone-a", "Zone A", "192.0.2.0/24", "   ")),
                "tester",
                "cn=tester");

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.StartWith("Ok: Imported from blank-ip-end.json"));
                Assert.That(result, Does.Not.Contain("Bad Ips"));
            });
        }

        [Test]
        public async Task Run_AcceptsIpv6Subnets()
        {
            ZoneMatrixImportApiConnection apiConnection = CreateNewMatrixConnection();
            ZoneMatrixDataImport import = new(apiConnection, CreateNoAutoCalcConfig());

            string result = await import.Run(
                "ipv6.json",
                CreateImportJson("Matrix A", CreateZone("zone-a", "Zone A", "2001:db8::/32")),
                "tester",
                "cn=tester");

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.StartWith("Ok: Imported from ipv6.json"));
                Assert.That(result, Does.Not.Contain("Bad Ips"));
            });
        }

        [Test]
        public async Task Run_ParsesTheDocumentedPathKeysFromRawJson()
        {
            ZoneMatrixImportApiConnection apiConnection = CreateNewMatrixConnection();
            ZoneMatrixDataImport import = new(apiConnection, CreateNoAutoCalcConfig());

            string result = await import.Run("raw-keys.json", kDocumentedKeysJson, "tester", "cn=tester");

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.Contain($"Could not resolve devices {kMgmtA}/{kUnknownDevice}"));
                Assert.That(result, Does.Not.Contain(kFwCore));
                Assert.That(apiConnection.Count(NetworkZoneQueries.addNetworkZone), Is.EqualTo(0));
            });
        }

        [Test]
        public async Task Run_RejectsZoneUsingTheReservedInternetIdString()
        {
            ZoneMatrixImportApiConnection apiConnection = CreateNewMatrixConnection();
            ZoneMatrixDataImport import = new(apiConnection, CreateAutoCalcConfig());

            string result = await import.Run(
                "reserved-internet.json",
                CreateImportJson(
                    "Matrix A",
                    CreateZone(NetworkZoneService.kAutoCalculatedInternetZoneIdString, "Reserved", "192.0.2.0/24")),
                "tester",
                "cn=tester");

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.Contain($"Use of internally reserved zone {NetworkZoneService.kAutoCalculatedInternetZoneIdString}"));
                Assert.That(apiConnection.Count(ComplianceQueries.addCriterion), Is.EqualTo(0));
                Assert.That(apiConnection.Count(NetworkZoneQueries.addNetworkZone), Is.EqualTo(0));
                Assert.That(apiConnection.Count(NetworkZoneQueries.updateNetworkZone), Is.EqualTo(0));
            });
        }

        [Test]
        public async Task Run_RejectsZoneUsingTheReservedUndefinedInternalIdString()
        {
            ZoneMatrixImportApiConnection apiConnection = CreateNewMatrixConnection();
            ZoneMatrixDataImport import = new(apiConnection, CreateAutoCalcConfig());

            string result = await import.Run(
                "reserved-undefined-internal.json",
                CreateImportJson(
                    "Matrix A",
                    CreateZone(NetworkZoneService.kAutoCalculatedUndefinedInternalZoneIdString, "Reserved", "192.0.2.0/24")),
                "tester",
                "cn=tester");

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.Contain($"Use of internally reserved zone {NetworkZoneService.kAutoCalculatedUndefinedInternalZoneIdString}"));
                Assert.That(apiConnection.Count(ComplianceQueries.addCriterion), Is.EqualTo(0));
                Assert.That(apiConnection.Count(NetworkZoneQueries.addNetworkZone), Is.EqualTo(0));
                Assert.That(apiConnection.Count(NetworkZoneQueries.updateNetworkZone), Is.EqualTo(0));
            });
        }

        [Test]
        public async Task Run_RejectsReservedZoneIdStringAlsoWhenAutoCalculationIsDisabled()
        {
            ZoneMatrixImportApiConnection apiConnection = CreateNewMatrixConnection();
            ZoneMatrixDataImport import = new(apiConnection, CreateNoAutoCalcConfig());

            string result = await import.Run(
                "reserved-internet-disabled.json",
                CreateImportJson(
                    "Matrix A",
                    CreateZone(NetworkZoneService.kAutoCalculatedInternetZoneIdString, "Reserved", "192.0.2.0/24")),
                "tester",
                "cn=tester");

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.Contain($"Use of internally reserved zone {NetworkZoneService.kAutoCalculatedInternetZoneIdString}"));
                Assert.That(apiConnection.Count(ComplianceQueries.addCriterion), Is.EqualTo(0));
                Assert.That(apiConnection.Count(NetworkZoneQueries.addNetworkZone), Is.EqualTo(0));
            });
        }

        [Test]
        public async Task Run_ReportsZoneAndDeviceErrorsTogether()
        {
            ZoneMatrixImportApiConnection apiConnection = CreateNewMatrixConnection();
            ZoneMatrixDataImport import = new(apiConnection, CreateNoAutoCalcConfig());

            string result = await import.Run(
                "many-errors.json",
                CreateImportJson(
                    "Matrix A",
                    CreateZone("zone-a", "Zone A", "192.0.2.0/24", pathToRoot: kUnknownDevicePath),
                    CreateZone("zone-b", "Zone A", "198.51.100.0/24", pathToRoot: kDuplicateDevicePath)),
                "tester",
                "cn=tester");

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.Contain("Duplicate Zone Names"));
                Assert.That(result, Does.Contain("Could not resolve devices"));
                Assert.That(result, Does.Contain("in subnet 198.51.100.0/24"));
                Assert.That(apiConnection.Count(NetworkZoneQueries.addNetworkZone), Is.EqualTo(0));
            });
        }

        [Test]
        public async Task Run_WritesRootAndInternetPathsWithHopOrder()
        {
            ZoneMatrixImportApiConnection apiConnection = CreateNewMatrixConnectionWithIpRanges();
            ZoneMatrixDataImport import = new(apiConnection, CreateNoAutoCalcConfig());

            string result = await import.Run(
                "paths.json",
                CreateImportJson(
                    "Matrix A",
                    CreateZone("zone-a", "Zone A", kZoneASubnet,
                        pathToRoot: kRootPath, pathToInternet: kInternetPath)),
                "tester",
                "cn=tester");

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.StartWith("Ok: Imported from paths.json"));
                Assert.That(apiConnection.InsertedRootPaths, Is.EqualTo(new List<PathItemCall>
                {
                    new(kFwAccessId, kZoneAIpRangeId, 1),
                    new(kFwCoreId, kZoneAIpRangeId, 2)
                }));
                Assert.That(apiConnection.InsertedInternetPaths, Is.EqualTo(new List<PathItemCall>
                {
                    new(kFwCoreId, kZoneAIpRangeId, 1),
                    new(kBorderRouterId, kZoneAIpRangeId, 2)
                }));
                Assert.That(result, Does.Contain("Inserted paths to root: 2"));
                Assert.That(result, Does.Contain("Inserted paths to internet: 2"));
            });
        }

        [Test]
        public async Task Run_DeletesExistingPathsBeforeInsertingAndReportsBothCounts()
        {
            ZoneMatrixImportApiConnection apiConnection = CreateNewMatrixConnectionWithIpRanges();
            ZoneMatrixDataImport import = new(apiConnection, CreateNoAutoCalcConfig());

            string result = await import.Run(
                "delete-first.json",
                CreateImportJson(
                    "Matrix A",
                    CreateZone("zone-a", "Zone A", kZoneASubnet, pathToRoot: kRootPath)),
                "tester",
                "cn=tester");

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.StartWith("Ok: Imported from delete-first.json"));
                Assert.That(apiConnection.Count(NetworkZoneQueries.deleteNetworkZoneDeviceIpRangeRoot), Is.EqualTo(1));
                Assert.That(apiConnection.Count(NetworkZoneQueries.deleteNetworkZoneDeviceIpRangeInternet), Is.EqualTo(1));
                // the delete clears the whole matrix, so it has to precede the rows that replace it
                Assert.That(apiConnection.FirstCallIndex(NetworkZoneQueries.deleteNetworkZoneDeviceIpRangeRoot),
                    Is.LessThan(apiConnection.FirstCallIndex(NetworkZoneQueries.addPathItemsRoot)));
                Assert.That(apiConnection.FirstCallIndex(NetworkZoneQueries.deleteNetworkZoneDeviceIpRangeInternet),
                    Is.LessThan(apiConnection.FirstCallIndex(NetworkZoneQueries.addPathItemsInternet)));
                // Both deletes are criterion wide, so between them and the inserts the matrix has no
                // path data at all. HandleIpRangePaths therefore reads the ip ranges and builds both
                // insert lists first and deletes only immediately before inserting, which keeps that
                // round trip and the whole matching loop out of the exposed window. Nothing else states
                // that ordering, so moving the deletes back to the top of the method has to fail here.
                Assert.That(apiConnection.FirstCallIndex(NetworkZoneQueries.getIpRangesForMatrix),
                    Is.LessThan(apiConnection.FirstCallIndex(NetworkZoneQueries.deleteNetworkZoneDeviceIpRangeRoot)));
                // The ip ranges are read once for the whole matrix and matched from memory afterwards.
                // Resolving them per zone or per subnet would be a round trip inside the loop.
                Assert.That(apiConnection.Count(NetworkZoneQueries.getIpRangesForMatrix), Is.EqualTo(1));
                Assert.That(result, Does.Contain("removed paths to root: 3"));
                Assert.That(result, Does.Contain("removed paths to internet: 2"));
            });
        }

        [Test]
        public async Task Run_SkipsPathsWhenIpRangeIsUnknown()
        {
            ZoneMatrixImportApiConnection apiConnection = CreateNewMatrixConnection();
            apiConnection.IpRangesResponse = [];
            ZoneMatrixDataImport import = new(apiConnection, CreateNoAutoCalcConfig());

            string result = await import.Run(
                "unknown-range.json",
                CreateImportJson(
                    "Matrix A",
                    CreateZone("zone-a", "Zone A", kZoneASubnet, pathToRoot: kRootPath)),
                "tester",
                "cn=tester");

            Assert.Multiple(() =>
            {
                // the zones themselves still import, only their paths are left out
                Assert.That(result, Does.StartWith("Ok: Imported from unknown-range.json"));
                Assert.That(apiConnection.InsertedRootPaths, Is.Empty);
                Assert.That(result, Does.Contain("Inserted paths to root: 0"));
            });
        }

        [Test]
        public async Task Run_MatchesIpRangeReportedWithPrefixNotation()
        {
            ZoneMatrixImportApiConnection apiConnection = CreateNewMatrixConnection();
            // the API returns inet columns with a prefix, which the import has to parse away
            apiConnection.IpRangesResponse =
            [
                CreateIpRange(kZoneAIpRangeId, kZoneAId, $"{kZoneASubnetStart}/32", $"{kZoneASubnetEnd}/32")
            ];
            ZoneMatrixDataImport import = new(apiConnection, CreateNoAutoCalcConfig());

            string result = await import.Run(
                "prefixed-range.json",
                CreateImportJson(
                    "Matrix A",
                    CreateZone("zone-a", "Zone A", kZoneASubnet, pathToRoot: kSingleCoreDevicePath)),
                "tester",
                "cn=tester");

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.StartWith("Ok: Imported from prefixed-range.json"));
                Assert.That(apiConnection.InsertedRootPaths, Is.EqualTo(new List<PathItemCall>
                {
                    new(kFwCoreId, kZoneAIpRangeId, 1)
                }));
            });
        }

        [Test]
        public async Task Run_IgnoresIpRangeOfAnotherZone()
        {
            ZoneMatrixImportApiConnection apiConnection = CreateNewMatrixConnection();
            // same addresses, different zone: ownership of a range is decided by its zone
            apiConnection.IpRangesResponse =
            [
                CreateIpRange(kZoneBIpRangeId, kZoneBId, kZoneASubnetStart, kZoneASubnetEnd)
            ];
            ZoneMatrixDataImport import = new(apiConnection, CreateNoAutoCalcConfig());

            string result = await import.Run(
                "foreign-range.json",
                CreateImportJson(
                    "Matrix A",
                    CreateZone("zone-a", "Zone A", kZoneASubnet, pathToRoot: kSingleCoreDevicePath)),
                "tester",
                "cn=tester");

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.StartWith("Ok: Imported from foreign-range.json"));
                Assert.That(apiConnection.InsertedRootPaths, Is.Empty);
            });
        }

        [Test]
        public async Task Run_RestartsPathOrderPerSubnet()
        {
            ZoneMatrixImportApiConnection apiConnection = CreateNewMatrixConnection();
            apiConnection.IpRangesResponse =
            [
                CreateIpRange(kZoneAIpRangeId, kZoneAId, kZoneASubnetStart, kZoneASubnetEnd),
                CreateIpRange(kZoneASecondIpRangeId, kZoneAId, kSecondSubnetStart, kSecondSubnetEnd)
            ];
            ZoneMatrixDataImport import = new(apiConnection, CreateNoAutoCalcConfig());

            NetworkZoneData zone = CreateZone("zone-a", "Zone A", kZoneASubnet, pathToRoot: kRootPath);
            zone.IpData.Add(new ZoneIpRangeData
            {
                Name = "Zone A second subnet",
                Ip = kSecondSubnet,
                PathToRoot = [.. kRootPath]
            });

            string result = await import.Run("order-per-subnet.json", CreateImportJson("Matrix A", zone), "tester", "cn=tester");

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.StartWith("Ok: Imported from order-per-subnet.json"));
                // the unique index is per ip range, so the hop order starts at 1 again for every subnet
                Assert.That(apiConnection.InsertedRootPaths, Is.EqualTo(new List<PathItemCall>
                {
                    new(kFwAccessId, kZoneAIpRangeId, 1),
                    new(kFwCoreId, kZoneAIpRangeId, 2),
                    new(kFwAccessId, kZoneASecondIpRangeId, 1),
                    new(kFwCoreId, kZoneASecondIpRangeId, 2)
                }));
            });
        }

        [Test]
        public async Task Run_ReturnsErrorWhenSubnetIsDuplicateInZone()
        {
            ZoneMatrixImportApiConnection apiConnection = CreateNewMatrixConnectionWithIpRanges();
            ZoneMatrixDataImport import = new(apiConnection, CreateNoAutoCalcConfig());

            NetworkZoneData zone = CreateZone("zone-a", "Zone A", kZoneASubnet, pathToRoot: kRootPath);
            zone.IpData.Add(new ZoneIpRangeData
            {
                Name = "Zone A subnet repeated",
                Ip = kZoneASubnet,
                PathToRoot = [.. kSingleCoreDevicePath]
            });

            string result = await import.Run("duplicate-subnet.json", CreateImportJson("Matrix A", zone), "tester", "cn=tester");

            Assert.Multiple(() =>
            {
                // two identical subnets would collide on the (ip_range_id, dev_id) key of the path
                // tables after the delete has already run, so the file has to be refused up front
                Assert.That(result, Does.Contain("Duplicate subnet"));
                Assert.That(result, Does.Contain(kZoneASubnet));
                Assert.That(apiConnection.Count(NetworkZoneQueries.addNetworkZone), Is.EqualTo(0));
                Assert.That(apiConnection.Count(NetworkZoneQueries.deleteNetworkZoneDeviceIpRangeRoot), Is.EqualTo(0));
                Assert.That(apiConnection.InsertedRootPaths, Is.Empty);
            });
        }

        [Test]
        public async Task Run_UsesTheLastIpRangeWhenTheApiReportsDuplicates()
        {
            ZoneMatrixImportApiConnection apiConnection = CreateNewMatrixConnection();
            // Two rows of the same zone covering the same addresses. CheckDuplicateSubnet refuses an
            // import file that would create them, but rows written before that check existed are still
            // in the database, so the lookup has to survive them rather than abort the import.
            apiConnection.IpRangesResponse =
            [
                CreateIpRange(kZoneAIpRangeId, kZoneAId, kZoneASubnetStart, kZoneASubnetEnd),
                CreateIpRange(kZoneASecondIpRangeId, kZoneAId, kZoneASubnetStart, kZoneASubnetEnd)
            ];
            ZoneMatrixDataImport import = new(apiConnection, CreateNoAutoCalcConfig());

            string result = await import.Run(
                "duplicate-range-rows.json",
                CreateImportJson(
                    "Matrix A",
                    CreateZone("zone-a", "Zone A", kZoneASubnet, pathToRoot: kSingleCoreDevicePath)),
                "tester",
                "cn=tester");

            Assert.Multiple(() =>
            {
                Assert.That(result, Does.StartWith("Ok: Imported from duplicate-range-rows.json"));
                // Last one wins: the lookup is filled with the indexer, which overwrites. Filling it
                // with Add would throw here and abort an import that has already written its zones.
                Assert.That(apiConnection.InsertedRootPaths, Is.EqualTo(new List<PathItemCall>
                {
                    new(kFwCoreId, kZoneASecondIpRangeId, 1)
                }));
            });
        }

        [Test]
        public async Task Run_AcceptsSameSubnetInDifferentZones()
        {
            ZoneMatrixImportApiConnection apiConnection = new()
            {
                MatrixByNameResponse = [],
                Managements = CreateDeviceInventory()
            };
            apiConnection.MatrixZoneResponses.Add(
            [
                CreateExistingZone(kZoneAId, "zone-a", "Zone A"),
                CreateExistingZone(kZoneBId, "zone-b", "Zone B")
            ]);
            apiConnection.IpRangesResponse =
            [
                CreateIpRange(kZoneAIpRangeId, kZoneAId, kZoneASubnetStart, kZoneASubnetEnd),
                CreateIpRange(kZoneBIpRangeId, kZoneBId, kZoneASubnetStart, kZoneASubnetEnd)
            ];
            ZoneMatrixDataImport import = new(apiConnection, CreateNoAutoCalcConfig());

            string result = await import.Run(
                "same-subnet-two-zones.json",
                CreateImportJson(
                    "Matrix A",
                    CreateZone("zone-a", "Zone A", kZoneASubnet, pathToRoot: kSingleCoreDevicePath),
                    CreateZone("zone-b", "Zone B", kZoneASubnet, pathToRoot: kSingleCoreDevicePath)),
                "tester",
                "cn=tester");

            Assert.Multiple(() =>
            {
                // the duplicate check is per zone, and each zone owns its own ip range row
                Assert.That(result, Does.StartWith("Ok: Imported from same-subnet-two-zones.json"));
                Assert.That(apiConnection.InsertedRootPaths, Is.EqualTo(new List<PathItemCall>
                {
                    new(kFwCoreId, kZoneAIpRangeId, 1),
                    new(kFwCoreId, kZoneBIpRangeId, 1)
                }));
            });
        }

        private static ZoneMatrixImportApiConnection CreateNewMatrixConnection()
        {
            ZoneMatrixImportApiConnection apiConnection = new()
            {
                MatrixByNameResponse = [],
                Managements = CreateDeviceInventory()
            };
            apiConnection.MatrixZoneResponses.Add(CreateReloadedZoneA());
            return apiConnection;
        }

        private static List<ComplianceNetworkZone> CreateMatrixZonesWithAutoCalculatedZones()
        {
            ComplianceNetworkZone internetZone =
                CreateExistingZone(102, NetworkZoneService.kAutoCalculatedInternetZoneIdString, "Internet");
            internetZone.IsAutoCalculatedInternetZone = true;
            ComplianceNetworkZone undefinedInternalZone =
                CreateExistingZone(103, NetworkZoneService.kAutoCalculatedUndefinedInternalZoneIdString, "Undefined internal");
            undefinedInternalZone.IsAutoCalculatedUndefinedInternalZone = true;
            return [CreateExistingZone(101, "zone-a", "Zone A"), internetZone, undefinedInternalZone];
        }

        private static List<ComplianceNetworkZone> CreateReloadedZonesWithAutoInternet()
        {
            return
            [
                CreateExistingZone(101, "zone-a", "Zone A"),
                CreateExistingZone(102, NetworkZoneService.kAutoCalculatedInternetZoneIdString, "Internet")
            ];
        }

        private static List<ComplianceNetworkZone> CreateReloadedZoneA()
        {
            return [CreateExistingZone(101, "zone-a", "Zone A")];
        }

        private static List<Management> CreateDeviceInventory()
        {
            return
            [
                new Management
                {
                    Id = 1,
                    Name = kMgmtA,
                    Devices =
                    [
                        new Device { Id = 11, Name = kFwAccess },
                        new Device { Id = 12, Name = kFwCore }
                    ]
                },
                new Management
                {
                    Id = 2,
                    Name = kMgmtB,
                    Devices = [new Device { Id = 21, Name = kBorderRouter }]
                }
            ];
        }

        private static List<Management> CreateAmbiguousDeviceInventory()
        {
            return
            [
                new Management
                {
                    Id = 1,
                    Name = kMgmtA,
                    Devices = [new Device { Id = 12, Name = kFwCore }]
                },
                new Management
                {
                    Id = 2,
                    Name = kMgmtA,
                    Devices = [new Device { Id = 13, Name = kFwCore }]
                }
            ];
        }

        private static SimulatedGlobalConfig CreateNoAutoCalcConfig()
        {
            return new SimulatedGlobalConfig
            {
                AutoCalculateInternetZone = false,
                AutoCalculateUndefinedInternalZone = false
            };
        }

        private static SimulatedGlobalConfig CreateAutoCalcConfig()
        {
            return new SimulatedGlobalConfig
            {
                AutoCalculateInternetZone = true,
                AutoCalculateUndefinedInternalZone = true,
                InternalZoneRange_10_0_0_0_8 = false,
                InternalZoneRange_172_16_0_0_12 = false,
                InternalZoneRange_192_168_0_0_16 = false,
                InternalZoneRange_0_0_0_0_8 = false,
                InternalZoneRange_127_0_0_0_8 = false,
                InternalZoneRange_169_254_0_0_16 = false,
                InternalZoneRange_224_0_0_0_4 = false,
                InternalZoneRange_240_0_0_0_4 = false,
                InternalZoneRange_255_255_255_255_32 = false,
                InternalZoneRange_192_0_2_0_24 = false,
                InternalZoneRange_198_51_100_0_24 = false,
                InternalZoneRange_203_0_113_0_24 = false,
                InternalZoneRange_100_64_0_0_10 = false,
                InternalZoneRange_192_0_0_0_24 = false,
                InternalZoneRange_192_88_99_0_24 = false,
                InternalZoneRange_198_18_0_0_15 = false
            };
        }

        private static string CreateImportJson(string matrixName, params NetworkZoneData[] zones)
        {
            ImportNwZoneMatrixData importData = new()
            {
                Name = matrixName,
                Comment = "Imported by tests",
                NetworkZones = [.. zones]
            };

            return JsonSerializer.Serialize(importData);
        }

        private static NetworkZoneData CreateZone(string idString, string name, string ip, string? ipEnd = null,
            string[]? commTargets = null, DeviceRefData[]? pathToRoot = null, DeviceRefData[]? pathToInternet = null)
        {
            NetworkZoneData zone = new()
            {
                IdString = idString,
                Name = name,
                IpData =
                [
                    new ZoneIpRangeData
                    {
                        Name = $"{name} subnet",
                        Ip = ip,
                        IpEnd = ipEnd,
                        PathToRoot = pathToRoot == null ? [] : [.. pathToRoot],
                        PathToInternet = pathToInternet == null ? [] : [.. pathToInternet]
                    }
                ]
            };

            if (commTargets != null)
            {
                zone.CommData = commTargets.Select(target => new CommunicationData { IdString = target }).ToList();
            }

            return zone;
        }

        private static ComplianceNetworkZone CreateExistingZone(int id, string idString, string name)
        {
            return new ComplianceNetworkZone
            {
                Id = id,
                IdString = idString,
                Name = name,
                IPRanges = [new IPAddressRange(IPAddress.Parse("192.0.2.0"), IPAddress.Parse("192.0.2.255"))]
            };
        }

        /// <summary>
        /// Builds an ip range row the way the API reports it for a matrix.
        /// </summary>
        /// <param name="id">Database id of the ip range.</param>
        /// <param name="zoneId">Network zone the range belongs to.</param>
        /// <param name="start">First address, as the API spells it.</param>
        /// <param name="end">Last address, as the API spells it.</param>
        /// <returns>The ip range row.</returns>
        private static NetworkZoneIpRange CreateIpRange(int id, int zoneId, string start, string end)
        {
            return new NetworkZoneIpRange
            {
                Id = id,
                NetworkZoneId = zoneId,
                IpRangeStart = start,
                IpRangeEnd = end
            };
        }

        /// <summary>
        /// Connection for a new matrix whose reloaded zone A already carries its ip range.
        /// </summary>
        /// <returns>The prepared connection.</returns>
        private static ZoneMatrixImportApiConnection CreateNewMatrixConnectionWithIpRanges()
        {
            ZoneMatrixImportApiConnection apiConnection = CreateNewMatrixConnection();
            apiConnection.IpRangesResponse =
            [
                CreateIpRange(kZoneAIpRangeId, kZoneAId, kZoneASubnetStart, kZoneASubnetEnd)
            ];
            return apiConnection;
        }

        private sealed class ZoneMatrixImportApiConnection : SimulatedApiConnection
        {
            public List<ComplianceCriterion> MatrixByNameResponse { get; set; } = [];
            public List<Management> Managements { get; set; } = [];
            public List<List<ComplianceNetworkZone>> MatrixZoneResponses { get; } = [];
            public ReturnIdWrapper AddCriterionResponse { get; set; } = new()
            {
                ReturnIds = [new ReturnId { InsertedId = 1 }]
            };

            public List<(string Query, object? Variables)> Calls { get; } = [];

            /// <summary>Ip ranges the API reports for the matrix, keyed by network zone id.</summary>
            public List<NetworkZoneIpRange> IpRangesResponse { get; set; } = [];

            /// <summary>Rows the two path delete mutations report as affected.</summary>
            public int DeletedPathRootRows { get; set; } = 3;
            public int DeletedPathInternetRows { get; set; } = 2;

            /// <summary>Path items the import handed to the two bulk insert mutations.</summary>
            public List<PathItemCall> InsertedRootPaths { get; } = [];
            public List<PathItemCall> InsertedInternetPaths { get; } = [];

            /// <summary>Position of a query in the call sequence, or -1 when it was never sent.</summary>
            public int FirstCallIndex(string query)
            {
                return Calls.FindIndex(call => call.Query == query);
            }

            public int Count(string query)
            {
                return Calls.Count(call => call.Query == query);
            }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                Calls.Add((query, variables));

                if (typeof(QueryResponseType) == typeof(List<Management>) && query == DeviceQueries.getManagementNames)
                {
                    return Task.FromResult((QueryResponseType)(object)Managements);
                }

                if (typeof(QueryResponseType) == typeof(List<ComplianceCriterion>) && query == ComplianceQueries.getMatrixByName)
                {
                    return Task.FromResult((QueryResponseType)(object)MatrixByNameResponse);
                }

                if (typeof(QueryResponseType) == typeof(List<ComplianceNetworkZone>) && query == NetworkZoneQueries.getNetworkZonesForMatrix)
                {
                    int responseIndex = Math.Min(Count(NetworkZoneQueries.getNetworkZonesForMatrix) - 1, Math.Max(MatrixZoneResponses.Count - 1, 0));
                    List<ComplianceNetworkZone> response = MatrixZoneResponses.Count == 0 ? [] : MatrixZoneResponses[responseIndex];
                    // hand out a fresh list per call: NetworkZoneService.UpdateSpecialZones removes
                    // entries from the list it receives, which would corrupt later responses
                    return Task.FromResult((QueryResponseType)(object)new List<ComplianceNetworkZone>(response));
                }

                if (typeof(QueryResponseType) == typeof(ReturnIdWrapper) && query == ComplianceQueries.addCriterion)
                {
                    return Task.FromResult((QueryResponseType)(object)AddCriterionResponse);
                }

                if (typeof(QueryResponseType) == typeof(ReturnIdWrapper) && query == ComplianceQueries.updateCriterionMetadata)
                {
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper
                    {
                        ReturnIds = [new ReturnId { UpdatedId = 1 }]
                    });
                }

                if (query == NetworkZoneQueries.addNetworkZone
                    || query == NetworkZoneQueries.updateNetworkZone
                    || query == NetworkZoneQueries.removeNetworkZone
                    || query == MonitorQueries.addDataImportLogEntry)
                {
                    if (typeof(QueryResponseType) == typeof(ReturnIdWrapper))
                    {
                        return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper
                        {
                            ReturnIds = [new ReturnId { NewId = 1, NewIdLong = 1, AffectedRows = 1 }]
                        });
                    }

                    return Task.FromResult(default(QueryResponseType)!);
                }

                if (query == NetworkZoneQueries.deleteNetworkZoneDeviceIpRangeRoot)
                {
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = DeletedPathRootRows });
                }

                if (query == NetworkZoneQueries.deleteNetworkZoneDeviceIpRangeInternet)
                {
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = DeletedPathInternetRows });
                }

                if (typeof(QueryResponseType) == typeof(List<NetworkZoneIpRange>) && query == NetworkZoneQueries.getIpRangesForMatrix)
                {
                    // a fresh list per call, so a test cannot observe a list the import mutated
                    return Task.FromResult((QueryResponseType)(object)new List<NetworkZoneIpRange>(IpRangesResponse));
                }

                if (query == NetworkZoneQueries.addPathItemsRoot)
                {
                    List<PathItemCall> items = ReadPathItems(variables, kOrderToRootField);
                    InsertedRootPaths.AddRange(items);
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = items.Count });
                }

                if (query == NetworkZoneQueries.addPathItemsInternet)
                {
                    List<PathItemCall> items = ReadPathItems(variables, kOrderToInternetField);
                    InsertedInternetPaths.AddRange(items);
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = items.Count });
                }

                throw new InvalidOperationException($"Unexpected query in zone matrix test: {query}");
            }

            /// <summary>
            /// Reads the anonymous insert objects the import builds. They carry the database column
            /// names, so a rename on either side surfaces here rather than only against a live API.
            /// </summary>
            /// <param name="variables">Variables object carrying the "objects" list.</param>
            /// <param name="orderFieldName">Column the order is written to, which differs per path.</param>
            /// <returns>One entry per insert object, in the order the import built them.</returns>
            private static List<PathItemCall> ReadPathItems(object? variables, string orderFieldName)
            {
                List<PathItemCall> items = [];
                object? objects = variables?.GetType().GetProperty("objects")?.GetValue(variables);
                if (objects is not System.Collections.IEnumerable enumerable)
                {
                    return items;
                }

                foreach (object? item in enumerable)
                {
                    if (item is null)
                    {
                        continue;
                    }
                    Type itemType = item.GetType();
                    items.Add(new PathItemCall(
                        ReadIntProperty(item, itemType, "dev_id"),
                        ReadIntProperty(item, itemType, "ip_range_id"),
                        ReadIntProperty(item, itemType, orderFieldName)));
                }
                return items;
            }

            /// <summary>
            /// Reads one integer column off an anonymous insert object.
            /// </summary>
            /// <param name="item">The insert object.</param>
            /// <param name="itemType">Its runtime type.</param>
            /// <param name="propertyName">Database column name the property carries.</param>
            /// <returns>The column value.</returns>
            private static int ReadIntProperty(object item, Type itemType, string propertyName)
            {
                object? value = itemType.GetProperty(propertyName)?.GetValue(item)
                    ?? throw new InvalidOperationException(
                        $"Insert object has no property {propertyName}, it carries {string.Join(", ", itemType.GetProperties().Select(property => property.Name))}.");
                return Convert.ToInt32(value);
            }
        }

        /// <summary>One row the import sent to a path insert mutation.</summary>
        internal sealed record PathItemCall(int DeviceId, int IpRangeId, int Order);
    }
}

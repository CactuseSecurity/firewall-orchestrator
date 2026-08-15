using System.Linq;
using System.Reflection;
using System.Text.Json;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Middleware.Server;
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
                Assert.That(apiConnection.Count(ComplianceQueries.getNetworkZonesForMatrix), Is.EqualTo(2));
                Assert.That(apiConnection.Count(ComplianceQueries.addNetworkZone), Is.EqualTo(3));
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
                Assert.That(apiConnection.Count(ComplianceQueries.getNetworkZonesForMatrix), Is.EqualTo(3));
                Assert.That(apiConnection.Count(ComplianceQueries.removeNetworkZone), Is.EqualTo(1));
                Assert.That(apiConnection.Count(MonitorQueries.addDataImportLogEntry), Is.EqualTo(1));
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

        private static NetworkZoneData CreateZone(string idString, string name, string ip, string? ipEnd = null, string[]? commTargets = null)
        {
            NetworkZoneData zone = new()
            {
                IdString = idString,
                Name = name,
                IpData =
                [
                    new ModellingImportAreaIpData
                    {
                        Name = $"{name} subnet",
                        Ip = ip,
                        IpEnd = ipEnd
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

        private sealed class ZoneMatrixImportApiConnection : SimulatedApiConnection
        {
            public List<ComplianceCriterion> MatrixByNameResponse { get; set; } = [];
            public List<List<ComplianceNetworkZone>> MatrixZoneResponses { get; } = [];
            public ReturnIdWrapper AddCriterionResponse { get; set; } = new()
            {
                ReturnIds = [new ReturnId { InsertedId = 1 }]
            };

            public List<(string Query, object? Variables)> Calls { get; } = [];

            public int Count(string query)
            {
                return Calls.Count(call => call.Query == query);
            }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                Calls.Add((query, variables));

                if (typeof(QueryResponseType) == typeof(List<ComplianceCriterion>) && query == ComplianceQueries.getMatrixByName)
                {
                    return Task.FromResult((QueryResponseType)(object)MatrixByNameResponse);
                }

                if (typeof(QueryResponseType) == typeof(List<ComplianceNetworkZone>) && query == ComplianceQueries.getNetworkZonesForMatrix)
                {
                    int responseIndex = Math.Min(Count(ComplianceQueries.getNetworkZonesForMatrix) - 1, Math.Max(MatrixZoneResponses.Count - 1, 0));
                    List<ComplianceNetworkZone> response = MatrixZoneResponses.Count == 0 ? [] : MatrixZoneResponses[responseIndex];
                    return Task.FromResult((QueryResponseType)(object)response);
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

                if (query == ComplianceQueries.addNetworkZone
                    || query == ComplianceQueries.updateNetworkZone
                    || query == ComplianceQueries.removeNetworkZone
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

                throw new InvalidOperationException($"Unexpected query in zone matrix test: {query}");
            }
        }
    }
}

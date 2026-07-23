using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.File;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Middleware.Server;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class AreaIpDataImportTest
    {
        [Test]
        public async Task Run_ImportsMergedAreasAndHandlesUpdateNewAndDeletePaths()
        {
            string tempRoot = CreateTempRoot();
            object? originalConfigData = null;
            object? originalJwtPrivateKey = null;
            object? originalJwtPublicKey = null;
            bool configSnapshotTaken = false;
            try
            {
                (originalConfigData, originalJwtPrivateKey, originalJwtPublicKey) = SnapshotConfigFileState();
                configSnapshotTaken = true;
                ConfigureAllowedCustomizationRoots(tempRoot);

                string customizationRoot = Path.Combine(tempRoot, "etc");
                Directory.CreateDirectory(customizationRoot);

                string source1 = Path.Combine(customizationRoot, "area-1-source-a");
                string source2 = Path.Combine(customizationRoot, "area-1-source-b");
                string source3 = Path.Combine(customizationRoot, "area-2-source");

                WriteImportFile(source1, new ModellingImportNwData
                {
                    Areas =
                    [
                        new ModellingImportAreaData("Area One", "area-1")
                        {
                            IpData =
                            [
                                new ModellingImportAreaIpData { Name = "net-a", Ip = "192.0.2.10/24" }
                            ]
                        }
                    ]
                });

                WriteImportFile(source2, new ModellingImportNwData
                {
                    Areas =
                    [
                        new ModellingImportAreaData("Area One", "area-1")
                        {
                            IpData =
                            [
                                new ModellingImportAreaIpData { Name = "net-a", Ip = "192.0.2.10/24" },
                                new ModellingImportAreaIpData { Name = "net-b", Ip = "198.51.100.20/24" }
                            ]
                        }
                    ]
                });

                WriteImportFile(source3, new ModellingImportNwData
                {
                    Areas =
                    [
                        new ModellingImportAreaData("Area Two", "area-2")
                        {
                            IpData =
                            [
                                new ModellingImportAreaIpData { Name = "net-c", Ip = "203.0.113.7/32" }
                            ]
                        }
                    ]
                });

                GlobalConfig globalConfig = new()
                {
                    ImportSubnetDataPath = JsonSerializer.Serialize(new[] { source1, source2, source3 })
                };

                AreaIpDataImportTestApiConnection apiConnection = new()
                {
                    ExistingAreasResponse =
                    [
                        CreateExistingArea(
                            id: 101,
                            idString: "area-1",
                            name: "Area One",
                            isDeleted: true,
                            new NetworkDataWrapper
                            {
                                Content = new NetworkSubnet
                                {
                                    Id = 201,
                                    Name = "net-a",
                                    Ip = "192.0.2.0/24",
                                    IpEnd = "192.0.2.255"
                                }
                            },
                            new NetworkDataWrapper
                            {
                                Content = new NetworkSubnet
                                {
                                    Id = 202,
                                    Name = "net-old",
                                    Ip = "10.10.10.0/24",
                                    IpEnd = "10.10.10.255"
                                }
                            }
                        ),
                        CreateExistingArea(id: 102, idString: "area-3", name: "Stale Area")
                    ]
                };

                AreaIpDataImport import = new(apiConnection, globalConfig);

                List<string> failedImports = await import.Run();

                Assert.Multiple(() =>
                {
                    Assert.That(failedImports, Is.Empty);
                    Assert.That(apiConnection.CallCounts[ModellingQueries.getAreas], Is.EqualTo(1));
                    Assert.That(apiConnection.CallCounts[ModellingQueries.newArea], Is.EqualTo(1));
                    Assert.That(apiConnection.CallCounts[ModellingQueries.newAreaIpData], Is.EqualTo(2));
                    Assert.That(apiConnection.CallCounts[ModellingQueries.addNwObjectToNwGroup], Is.EqualTo(2));
                    Assert.That(apiConnection.CallCounts[ModellingQueries.setNwGroupDeletedState], Is.EqualTo(2));
                    Assert.That(apiConnection.CallCounts[OwnerQueries.deleteAreaIpData], Is.EqualTo(1));
                    Assert.That(apiConnection.CallCounts[ModellingQueries.removeSelectedNwGroupObjectFromAllApps], Is.EqualTo(1));
                    Assert.That(apiConnection.CallCounts[MonitorQueries.addDataImportLogEntry], Is.EqualTo(1));
                });

                List<object?> newAreaIpDataCalls = apiConnection.Calls
                    .Where(call => call.Query == ModellingQueries.newAreaIpData)
                    .Select(call => call.Variables)
                    .ToList();

                Assert.That(newAreaIpDataCalls, Has.Count.EqualTo(2));
                Assert.That(GetAnonymousValue(newAreaIpDataCalls[0], "name"), Is.EqualTo("net-b"));
                Assert.That(GetAnonymousValue(newAreaIpDataCalls[1], "name"), Is.EqualTo("net-c"));

                (string expectedStartB, string expectedEndB) = IpOperations.SplitIpToRange("198.51.100.20/24");
                object? firstNewAreaIpDataVariables = apiConnection.Calls.First(call => call.Query == ModellingQueries.newAreaIpData).Variables;
                Assert.That(GetAnonymousValue(firstNewAreaIpDataVariables, "ip"), Is.EqualTo(expectedStartB));
                Assert.That(GetAnonymousValue(firstNewAreaIpDataVariables, "ipEnd"), Is.EqualTo(expectedEndB));
            }
            finally
            {
                if (configSnapshotTaken)
                {
                    RestoreConfigFileState(originalConfigData, originalJwtPrivateKey, originalJwtPublicKey);
                }

                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Test]
        public async Task Run_ReturnsFailedImportAndLogsWhenSourceFileIsMalformed()
        {
            string tempRoot = CreateTempRoot();
            object? originalConfigData = null;
            object? originalJwtPrivateKey = null;
            object? originalJwtPublicKey = null;
            bool configSnapshotTaken = false;
            try
            {
                (originalConfigData, originalJwtPrivateKey, originalJwtPublicKey) = SnapshotConfigFileState();
                configSnapshotTaken = true;
                ConfigureAllowedCustomizationRoots(tempRoot);

                string customizationRoot = Path.Combine(tempRoot, "etc");
                Directory.CreateDirectory(customizationRoot);
                string missingSource = Path.Combine(customizationRoot, "broken-area");
                File.WriteAllText(missingSource + ".json", "{");

                GlobalConfig globalConfig = new()
                {
                    ImportSubnetDataPath = JsonSerializer.Serialize(new[] { missingSource })
                };

                AreaIpDataImportTestApiConnection apiConnection = new();
                AreaIpDataImport import = new(apiConnection, globalConfig);

                List<string> failedImports = await import.Run();

                Assert.Multiple(() =>
                {
                    Assert.That(failedImports, Is.EqualTo(new[] { missingSource }));
                    Assert.That(apiConnection.CallCounts[MonitorQueries.addDataImportLogEntry], Is.EqualTo(2));
                    Assert.That(apiConnection.CallCounts.ContainsKey(ModellingQueries.getAreas), Is.False);
                });
            }
            finally
            {
                if (configSnapshotTaken)
                {
                    RestoreConfigFileState(originalConfigData, originalJwtPrivateKey, originalJwtPublicKey);
                }

                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        private static string CreateTempRoot()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), $"fwo-area-ip-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempRoot);
            return tempRoot;
        }

        private static void ConfigureAllowedCustomizationRoots(string fwoHome)
        {
            string configFilePath = Path.Combine(fwoHome, "config.json");
            string privateKeyPath = Path.Combine(fwoHome, "private.pem");
            string publicKeyPath = Path.Combine(fwoHome, "public.pem");
            File.WriteAllText(configFilePath, $"{{\"fworch_home\":\"{fwoHome.Replace("\\", "\\\\")}\"}}");
            File.WriteAllText(privateKeyPath, "");
            File.WriteAllText(publicKeyPath, "");
            TestHelper.InvokeMethod<ConfigFile, object?>("Read", [configFilePath, privateKeyPath, publicKeyPath]);
        }

        private static void WriteImportFile(string sourcePath, ModellingImportNwData data)
        {
            string? directory = Path.GetDirectoryName(sourcePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(sourcePath + ".json", JsonSerializer.Serialize(data));
        }

        private static ModellingNetworkArea CreateExistingArea(int id, string idString, string name, bool isDeleted = false, params NetworkDataWrapper[] ipData)
        {
            return new ModellingNetworkArea
            {
                Id = id,
                IdString = idString,
                Name = name,
                IsDeleted = isDeleted,
                IpData = [.. ipData]
            };
        }

        private static (object? Data, object? JwtPrivateKey, object? JwtPublicKey) SnapshotConfigFileState()
        {
            Type configFileType = typeof(ConfigFile);
            object? data = configFileType.GetProperty("Data", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null);
            object? jwtPrivateKey = configFileType.GetField("jwtPrivateKey", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null);
            object? jwtPublicKey = configFileType.GetField("jwtPublicKey", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null);
            return (data, jwtPrivateKey, jwtPublicKey);
        }

        private static void RestoreConfigFileState(object? data, object? jwtPrivateKey, object? jwtPublicKey)
        {
            Type configFileType = typeof(ConfigFile);
            configFileType.GetProperty("Data", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, data);
            configFileType.GetField("jwtPrivateKey", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, jwtPrivateKey);
            configFileType.GetField("jwtPublicKey", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, jwtPublicKey);
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

        private sealed class AreaIpDataImportTestApiConnection : SimulatedApiConnection
        {
            public Dictionary<string, int> CallCounts { get; } = new(StringComparer.Ordinal);
            public List<(string Query, object? Variables)> Calls { get; } = [];
            public List<ModellingNetworkArea> ExistingAreasResponse { get; set; } = [];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                Calls.Add((query, variables));
                if (!CallCounts.TryGetValue(query, out int count))
                {
                    count = 0;
                }
                CallCounts[query] = count + 1;

                if (query == ModellingQueries.getAreas)
                {
                    return Task.FromResult((QueryResponseType)(object)ExistingAreasResponse);
                }

                if (query == ModellingQueries.newArea || query == ModellingQueries.newAreaIpData
                    || query == ModellingQueries.setNwGroupDeletedState
                    || query == OwnerQueries.deleteAreaIpData
                    || query == ModellingQueries.removeSelectedNwGroupObjectFromAllApps
                    || query == MonitorQueries.addDataImportLogEntry)
                {
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper
                    {
                        ReturnIds = [new ReturnId { NewId = 1, NewIdLong = 1, AffectedRows = 1 }]
                    });
                }

                if (query == ModellingQueries.addNwObjectToNwGroup)
                {
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = 1 });
                }

                throw new NotImplementedException($"Query not implemented in area import test api: {query}");
            }
        }
    }
}

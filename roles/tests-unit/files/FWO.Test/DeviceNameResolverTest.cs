using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Middleware.Server;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class DeviceNameResolverTest
    {
        private const string kMgmtA = "mgmt-a";
        private const string kMgmtB = "mgmt-b";
        private const string kFwCore = "fw-core-01";
        private const string kBorderRouter = "border-router-01";
        private const string kNamelessDeviceMgmt = "mgmt-nameless";
        private const int kFwCoreId = 12;
        private const int kBorderRouterId = 21;
        private const int kFirstAmbiguousId = 31;
        private const int kSecondAmbiguousId = 32;

        /// <summary>
        /// Verifies that a device is resolved by its management and device name.
        /// </summary>
        [Test]
        public async Task Resolve_ReturnsDeviceIdForKnownNames()
        {
            DeviceNameResolver resolver = await CreateResolver(CreateInventory());

            Assert.Multiple(() =>
            {
                Assert.That(resolver.Resolve(kMgmtA, kFwCore), Is.EqualTo(kFwCoreId));
                Assert.That(resolver.Resolve(kMgmtB, kBorderRouter), Is.EqualTo(kBorderRouterId));
            });
        }

        /// <summary>
        /// Verifies that an unknown device name and a known device name in the wrong
        /// management are both reported as unresolvable.
        /// </summary>
        [Test]
        public async Task Resolve_ReturnsNullForUnknownNames()
        {
            DeviceNameResolver resolver = await CreateResolver(CreateInventory());

            Assert.Multiple(() =>
            {
                Assert.That(resolver.Resolve(kMgmtA, "fw-does-not-exist"), Is.Null);
                Assert.That(resolver.Resolve("mgmt-does-not-exist", kFwCore), Is.Null);
                Assert.That(resolver.Resolve(kMgmtB, kFwCore), Is.Null);
            });
        }

        /// <summary>
        /// Verifies that device names are matched case sensitively, which is what the
        /// import file format requires.
        /// </summary>
        [Test]
        public async Task Resolve_IsCaseSensitive()
        {
            DeviceNameResolver resolver = await CreateResolver(CreateInventory());

            Assert.That(resolver.Resolve(kMgmtA, kFwCore.ToUpperInvariant()), Is.Null);
        }

        /// <summary>
        /// Verifies that devices without a usable name are skipped instead of being
        /// registered under an empty key. The dev_name column is nullable.
        /// </summary>
        [Test]
        public async Task Resolve_SkipsDevicesWithoutName()
        {
            DeviceNameResolver resolver = await CreateResolver(CreateInventoryWithNamelessDevices());

            Assert.Multiple(() =>
            {
                Assert.That(resolver.Resolve(kNamelessDeviceMgmt, string.Empty), Is.Null);
                Assert.That(resolver.Resolve(kNamelessDeviceMgmt, " "), Is.Null);
                Assert.That(resolver.Resolve(kNamelessDeviceMgmt, kFwCore), Is.EqualTo(kFwCoreId));
            });
        }

        /// <summary>
        /// Verifies that a management and device name combination existing more than once
        /// is reported as ambiguous, while unique combinations are not.
        /// </summary>
        [Test]
        public async Task IsAmbiguous_DetectsRepeatedNameCombinations()
        {
            DeviceNameResolver resolver = await CreateResolver(CreateAmbiguousInventory());

            Assert.Multiple(() =>
            {
                Assert.That(resolver.IsAmbiguous(kMgmtA, kFwCore), Is.True);
                Assert.That(resolver.IsAmbiguous(kMgmtB, kBorderRouter), Is.False);
                Assert.That(resolver.IsAmbiguous(kMgmtA, "fw-does-not-exist"), Is.False);
            });
        }

        /// <summary>
        /// Documents that an ambiguous combination still resolves to the device found
        /// first, so callers have to consult IsAmbiguous before using the id.
        /// </summary>
        [Test]
        public async Task Resolve_ReturnsFirstMatchForAmbiguousNames()
        {
            DeviceNameResolver resolver = await CreateResolver(CreateAmbiguousInventory());

            Assert.That(resolver.Resolve(kMgmtA, kFwCore), Is.EqualTo(kFirstAmbiguousId));
        }

        /// <summary>
        /// Verifies that an empty inventory resolves nothing and reports nothing as ambiguous.
        /// </summary>
        [Test]
        public async Task Resolve_HandlesEmptyInventory()
        {
            DeviceNameResolver resolver = await CreateResolver([]);

            Assert.Multiple(() =>
            {
                Assert.That(resolver.Resolve(kMgmtA, kFwCore), Is.Null);
                Assert.That(resolver.IsAmbiguous(kMgmtA, kFwCore), Is.False);
            });
        }

        /// <summary>
        /// Verifies the text used in import error messages.
        /// </summary>
        [Test]
        public void Describe_JoinsManagementAndDeviceName()
        {
            Assert.That(DeviceNameResolver.Describe(kMgmtA, kFwCore), Is.EqualTo($"{kMgmtA}/{kFwCore}"));
        }

        /// <summary>
        /// Verifies that the inventory is fetched with a single query.
        /// </summary>
        [Test]
        public async Task ConstructAsync_QueriesManagementNamesOnce()
        {
            DeviceResolverApiConnection apiConnection = new() { Managements = CreateInventory() };

            await DeviceNameResolver.ConstructAsync(apiConnection);

            Assert.That(apiConnection.ManagementNamesCallCount, Is.EqualTo(1));
        }

        private static async Task<DeviceNameResolver> CreateResolver(List<Management> managements)
        {
            DeviceResolverApiConnection apiConnection = new() { Managements = managements };
            return await DeviceNameResolver.ConstructAsync(apiConnection);
        }

        private static List<Management> CreateInventory()
        {
            return
            [
                new Management
                {
                    Id = 1,
                    Name = kMgmtA,
                    Devices = [new Device { Id = kFwCoreId, Name = kFwCore }]
                },
                new Management
                {
                    Id = 2,
                    Name = kMgmtB,
                    Devices = [new Device { Id = kBorderRouterId, Name = kBorderRouter }]
                }
            ];
        }

        private static List<Management> CreateInventoryWithNamelessDevices()
        {
            return
            [
                new Management
                {
                    Id = 1,
                    Name = kNamelessDeviceMgmt,
                    Devices =
                    [
                        new Device { Id = 41, Name = null },
                        new Device { Id = 42, Name = string.Empty },
                        new Device { Id = 43, Name = " " },
                        new Device { Id = kFwCoreId, Name = kFwCore }
                    ]
                }
            ];
        }

        private static List<Management> CreateAmbiguousInventory()
        {
            return
            [
                new Management
                {
                    Id = 1,
                    Name = kMgmtA,
                    Devices = [new Device { Id = kFirstAmbiguousId, Name = kFwCore }]
                },
                new Management
                {
                    Id = 2,
                    Name = kMgmtA,
                    Devices = [new Device { Id = kSecondAmbiguousId, Name = kFwCore }]
                },
                new Management
                {
                    Id = 3,
                    Name = kMgmtB,
                    Devices = [new Device { Id = kBorderRouterId, Name = kBorderRouter }]
                }
            ];
        }

        private sealed class DeviceResolverApiConnection : SimulatedApiConnection
        {
            public List<Management> Managements { get; set; } = [];

            public int ManagementNamesCallCount { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (typeof(QueryResponseType) == typeof(List<Management>) && query == DeviceQueries.getManagementNames)
                {
                    ManagementNamesCallCount++;
                    return Task.FromResult((QueryResponseType)(object)Managements);
                }

                throw new InvalidOperationException($"Unexpected query in device name resolver test: {query}");
            }
        }
    }
}

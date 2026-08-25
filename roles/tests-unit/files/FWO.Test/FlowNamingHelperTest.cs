using FWO.Data.Flow;
using FWO.Data;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class FlowNamingHelperTest
    {
        [Test]
        public void ResolvePreferredName_UsesPreferredManagementName()
        {
            List<(int? MgmId, string? Name)> mappings =
            [
                (1, "forti-name"),
                (2, "checkpoint-name")
            ];

            string result = FlowNamingHelper.ResolvePreferredName(
                mappings,
                preferredManagementId: 2,
                managementIdSelector: mapping => mapping.MgmId,
                nameSelector: mapping => mapping.Name);

            Assert.That(result, Is.EqualTo("checkpoint-name"));
        }

        [Test]
        public void ResolvePreferredName_FallsBackToFirstUsableName()
        {
            List<(int? MgmId, string? Name)> mappings =
            [
                (1, ""),
                (2, "fallback-name")
            ];

            string result = FlowNamingHelper.ResolvePreferredName(
                mappings,
                preferredManagementId: 99,
                managementIdSelector: mapping => mapping.MgmId,
                nameSelector: mapping => mapping.Name);

            Assert.That(result, Is.EqualTo("fallback-name"));
        }

        [Test]
        public void ResolvePreferredName_ReturnsFallbackWhenNoNamesExist()
        {
            List<(int? MgmId, string? Name)> mappings =
            [
                (1, "")
            ];

            string result = FlowNamingHelper.ResolvePreferredName(
                mappings,
                preferredManagementId: 1,
                managementIdSelector: mapping => mapping.MgmId,
                nameSelector: mapping => mapping.Name,
                fallbackName: "unnamed-flow");

            Assert.That(result, Is.EqualTo("unnamed-flow"));
        }

        [Test]
        public void ResolvePreferredNameByRanking_UsesTheFirstManagementWithAUsableName()
        {
            Dictionary<int, string?> namesByManagement = new()
            {
                [1] = "",
                [2] = "checkpoint-name",
                [3] = "third-name"
            };

            string result = FlowNamingHelper.ResolvePreferredNameByRanking(
                [1, 2, 3],
                managementId => namesByManagement.GetValueOrDefault(managementId),
                fallbackName: "fallback");

            Assert.That(result, Is.EqualTo("checkpoint-name"));
        }

        [Test]
        public void NormalizeManagementRanking_AppendsMissingManagementsAndDropsDuplicates()
        {
            List<int> ranking = FlowNamingHelper.NormalizeManagementRanking(
                [3, 1, 3, 9],
                [1, 2, 3, 4]);

            Assert.That(ranking, Is.EqualTo(new[] { 3, 1, 2, 4 }));
        }

        [Test]
        public void ParseManagementRanking_ReturnsEmptyListForInvalidJson()
        {
            List<int> ranking = FlowNamingHelper.ParseManagementRanking("not-json");

            Assert.That(ranking, Is.Empty);
        }

        [Test]
        public void ResolveNwObjectName_UsesFirstActiveLink()
        {
            FlowNwObject nwObject = new()
            {
                Name = "old-name",
                Objects =
                [
                    new NetworkObject
                    {
                        Id = 1,
                        Name = "forti-name",
                        FlowActive = true
                    },
                    new NetworkObject
                    {
                        Id = 2,
                        Name = "checkpoint-name",
                        FlowActive = true
                    }
                ]
            };

            string result = FlowNamingHelper.ResolveNwObjectName(nwObject, preferredManagementId: 2, fallbackName: nwObject.Name!);

            Assert.That(result, Is.EqualTo("forti-name"));
        }

        [Test]
        public void ResolveNwObjectName_FallsBackToInactiveMappingWhenNoActiveOneExists()
        {
            FlowNwObject nwObject = new()
            {
                Name = "old-name",
                Objects =
                [
                    new NetworkObject
                    {
                        Id = 1,
                        Name = "fallback-name",
                        FlowActive = false
                    }
                ]
            };

            string result = FlowNamingHelper.ResolveNwObjectName(nwObject, preferredManagementId: 1, fallbackName: nwObject.Name!);

            Assert.That(result, Is.EqualTo("fallback-name"));
        }

        [Test]
        public void ResolveMissingNwObjectName_KeepsExistingName()
        {
            FlowNwObject nwObject = new()
            {
                Name = "already-named",
                Objects =
                [
                    new NetworkObject
                    {
                        Id = 1,
                        Name = "replacement-name",
                        FlowActive = true
                    }
                ]
            };

            string result = FlowNamingHelper.ResolveMissingNwObjectName(nwObject, preferredManagementId: 1, fallbackName: "");

            Assert.That(result, Is.EqualTo("already-named"));
        }

        [Test]
        public void ResolveMissingNwObjectName_UsesCandidateWhenNameMissing()
        {
            FlowNwObject nwObject = new()
            {
                Name = "",
                Objects =
                [
                    new NetworkObject
                    {
                        Id = 1,
                        Name = "replacement-name",
                        FlowActive = true
                    }
                ]
            };

            string result = FlowNamingHelper.ResolveMissingNwObjectName(nwObject, preferredManagementId: 1, fallbackName: "");

            Assert.That(result, Is.EqualTo("replacement-name"));
        }

        [Test]
        public void ResolveMissingNameFromDuplicateSelection_ReturnsSelectedNameOnlyForUnnamedObjects()
        {
            Assert.Multiple(() =>
            {
                Assert.That(FlowNamingHelper.ResolveMissingNameFromDuplicateSelection("", "duplicate-name"), Is.EqualTo("duplicate-name"));
                Assert.That(FlowNamingHelper.ResolveMissingNameFromDuplicateSelection("existing-name", "duplicate-name"), Is.Null);
                Assert.That(FlowNamingHelper.ResolveMissingNameFromDuplicateSelection("", ""), Is.Null);
            });
        }

        [Test]
        public void ResolveNwObjectNameByRanking_UsesFirstRankedManagementWithMappedName()
        {
            FlowNwObject nwObject = new()
            {
                Id = 42,
                Name = "old-name"
            };
            List<Management> managements =
            [
                new Management
                {
                    Id = 1,
                    Objects =
                    [
                        new NetworkObject
                        {
                            Id = 100,
                            Name = "forti-name",
                            FlowNetworkObjectId = 42,
                            FlowActive = true
                        }
                    ]
                },
                new Management
                {
                    Id = 2,
                    Objects =
                    [
                        new NetworkObject
                        {
                            Id = 200,
                            Name = "checkpoint-name",
                            FlowNetworkObjectId = 42,
                            FlowActive = true
                        }
                    ]
                }
            ];

            string result = FlowNamingHelper.ResolveNwObjectNameByRanking(nwObject, managements, [2, 1], nwObject.Name!);

            Assert.That(result, Is.EqualTo("checkpoint-name"));
        }

        [Test]
        public void ResolveNwGroupNameByRanking_FallsBackToActiveCandidateWhenRankingHasNoName()
        {
            FlowNwGroup nwGroup = new()
            {
                Id = 55,
                Name = "old-group"
            };
            List<Management> managements =
            [
                new Management
                {
                    Id = 1,
                    Objects =
                    [
                        new NetworkObject
                        {
                            Id = 100,
                            Name = "",
                            FlowNetworkGroupId = 55,
                            FlowActive = false
                        }
                    ]
                },
                new Management
                {
                    Id = 2,
                    Objects =
                    [
                        new NetworkObject
                        {
                            Id = 200,
                            Name = "active-group-name",
                            FlowNetworkGroupId = 55,
                            FlowActive = true
                        }
                    ]
                }
            ];

            string result = FlowNamingHelper.ResolveNwGroupNameByRanking(nwGroup, managements, [1], nwGroup.Name);

            Assert.That(result, Is.EqualTo("active-group-name"));
        }

        [Test]
        public void ResolveSvcObjectNameByRanking_UsesFirstUsableMappedName()
        {
            FlowSvcObject svcObject = new()
            {
                Id = 77,
                Name = "old-service"
            };
            List<Management> managements =
            [
                new Management
                {
                    Id = 1,
                    Services =
                    [
                        new NetworkService
                        {
                            Id = 100,
                            Name = "",
                            FlowServiceObjectId = 77,
                            FlowActive = true
                        }
                    ]
                },
                new Management
                {
                    Id = 2,
                    Services =
                    [
                        new NetworkService
                        {
                            Id = 200,
                            Name = "usable-service-name",
                            FlowServiceObjectId = 77,
                            FlowActive = false
                        }
                    ]
                }
            ];

            string result = FlowNamingHelper.ResolveSvcObjectNameByRanking(svcObject, managements, [1], svcObject.Name);

            Assert.That(result, Is.EqualTo("usable-service-name"));
        }

        [Test]
        public void ResolveSvcGroupNameByRanking_UsesRankedManagementBeforeEarlierActiveCandidate()
        {
            FlowSvcGroup svcGroup = new()
            {
                Id = 88,
                Name = "old-service-group"
            };
            List<Management> managements =
            [
                new Management
                {
                    Id = 1,
                    Services =
                    [
                        new NetworkService
                        {
                            Id = 100,
                            Name = "first-active-name",
                            FlowServiceGroupId = 88,
                            FlowActive = true
                        }
                    ]
                },
                new Management
                {
                    Id = 2,
                    Services =
                    [
                        new NetworkService
                        {
                            Id = 200,
                            Name = "ranked-name",
                            FlowServiceGroupId = 88,
                            FlowActive = false
                        }
                    ]
                }
            ];

            string result = FlowNamingHelper.ResolveSvcGroupNameByRanking(svcGroup, managements, [2, 1], svcGroup.Name);

            Assert.That(result, Is.EqualTo("ranked-name"));
        }

        [Test]
        public void ResolveTimeObjectNameByRanking_ReturnsFallbackWhenNoMappedNamesExist()
        {
            FlowTimeObject timeObject = new()
            {
                Id = 99,
                Name = "old-time"
            };
            List<Management> managements =
            [
                new Management
                {
                    Id = 1,
                    TimeObjects =
                    [
                        new TimeObject
                        {
                            Id = 100,
                            Name = "",
                            FlowTimeObjectId = 99,
                            FlowActive = true
                        }
                    ]
                }
            ];

            string result = FlowNamingHelper.ResolveTimeObjectNameByRanking(timeObject, managements, [1], timeObject.Name);

            Assert.That(result, Is.EqualTo("old-time"));
        }

    }
}

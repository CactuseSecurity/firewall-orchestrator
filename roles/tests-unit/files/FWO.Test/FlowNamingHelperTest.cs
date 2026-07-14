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

    }
}

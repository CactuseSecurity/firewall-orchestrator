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
            List<FlowNwObjectMapping> mappings =
            [
                new()
                {
                    MgmId = 1,
                    FlowNwObject = new FlowNwObject { Name = "forti-name" }
                },
                new()
                {
                    MgmId = 2,
                    FlowNwObject = new FlowNwObject { Name = "checkpoint-name" }
                }
            ];

            string result = FlowNamingHelper.ResolvePreferredName(
                mappings,
                preferredManagementId: 2,
                managementIdSelector: mapping => mapping.MgmId,
                nameSelector: mapping => mapping.FlowNwObject.Name);

            Assert.That(result, Is.EqualTo("checkpoint-name"));
        }

        [Test]
        public void ResolvePreferredName_FallsBackToFirstUsableName()
        {
            List<FlowNwObjectMapping> mappings =
            [
                new()
                {
                    MgmId = 1,
                    FlowNwObject = new FlowNwObject { Name = "" }
                },
                new()
                {
                    MgmId = 2,
                    FlowNwObject = new FlowNwObject { Name = "fallback-name" }
                }
            ];

            string result = FlowNamingHelper.ResolvePreferredName(
                mappings,
                preferredManagementId: 99,
                managementIdSelector: mapping => mapping.MgmId,
                nameSelector: mapping => mapping.FlowNwObject.Name);

            Assert.That(result, Is.EqualTo("fallback-name"));
        }

        [Test]
        public void ResolvePreferredName_ReturnsFallbackWhenNoNamesExist()
        {
            List<FlowNwObjectMapping> mappings =
            [
                new()
                {
                    MgmId = 1,
                    FlowNwObject = new FlowNwObject { Name = "" }
                }
            ];

            string result = FlowNamingHelper.ResolvePreferredName(
                mappings,
                preferredManagementId: 1,
                managementIdSelector: mapping => mapping.MgmId,
                nameSelector: mapping => mapping.FlowNwObject.Name,
                fallbackName: "unnamed-flow");

            Assert.That(result, Is.EqualTo("unnamed-flow"));
        }

        [Test]
        public void ResolveNwObjectName_UsesPreferredActiveMapping()
        {
            FlowNwObject nwObject = new()
            {
                Name = "old-name",
                NwObjectMappings =
                [
                    new FlowNwObjectMapping
                    {
                        MgmId = 1,
                        ActiveOnMgm = true,
                        Object = new NetworkObject { Name = "forti-name" }
                    },
                    new FlowNwObjectMapping
                    {
                        MgmId = 2,
                        ActiveOnMgm = true,
                        Object = new NetworkObject { Name = "checkpoint-name" }
                    }
                ]
            };

            string result = FlowNamingHelper.ResolveNwObjectName(nwObject, preferredManagementId: 2, fallbackName: nwObject.Name!);

            Assert.That(result, Is.EqualTo("checkpoint-name"));
        }

        [Test]
        public void ResolveNwObjectName_FallsBackToInactiveMappingWhenNoActiveOneExists()
        {
            FlowNwObject nwObject = new()
            {
                Name = "old-name",
                NwObjectMappings =
                [
                    new FlowNwObjectMapping
                    {
                        MgmId = 1,
                        ActiveOnMgm = false,
                        Object = new NetworkObject { Name = "fallback-name" }
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
                NwObjectMappings =
                [
                    new FlowNwObjectMapping
                    {
                        MgmId = 1,
                        ActiveOnMgm = true,
                        Object = new NetworkObject { Name = "replacement-name" }
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
                NwObjectMappings =
                [
                    new FlowNwObjectMapping
                    {
                        MgmId = 1,
                        ActiveOnMgm = true,
                        Object = new NetworkObject { Name = "replacement-name" }
                    }
                ]
            };

            string result = FlowNamingHelper.ResolveMissingNwObjectName(nwObject, preferredManagementId: 1, fallbackName: "");

            Assert.That(result, Is.EqualTo("replacement-name"));
        }
    }
}

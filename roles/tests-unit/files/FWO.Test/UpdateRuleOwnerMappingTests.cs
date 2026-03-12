using FWO.Api.Client;
using FWO.Config.Api;
using FWO.Data;
using FWO.Services;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace FWO.Test
{
    public class UpdateRuleOwnerMappingTests
    {
        private UpdateRuleOwnerMapping service = null!;

        [SetUp]
        public void Setup()
        {
            var globalConfig = new GlobalConfig
            {
                OwnerSourceCustomFieldKey = "owner"
            };

            service = new UpdateRuleOwnerMapping(null!, globalConfig);
        }

        [Test]
        public void BuildNewRuleOwnersCustomField_ShouldCreateMapping_WhenOwnerExists()
        {
            var rule = BuildRule();
            var owners = new List<FwoOwner> { BuildOwner() };

            var result = service.BuildNewRuleOwnersCustomField(new List<Rule> { rule }, owners);

            Assert.Multiple(() =>
            {
                Assert.That(result.Count, Is.EqualTo(1));
                Assert.That(result[0].OwnerId, Is.EqualTo(10));
                Assert.That(result[0].RuleId, Is.EqualTo(1));
            });
        }

        [TestCase("TeamB", 10, false)]
        [TestCase("TeamA", 10, true)]
        public void BuildNewRuleOwnersCustomField_ShouldReturnExpectedOwnerMatching(string ownerExtAppId, int expectedOwnerId, bool shouldMatch)
        {
            var rule = BuildRule();
            var owners = new List<FwoOwner> { BuildOwner(id: expectedOwnerId, extAppId: ownerExtAppId) };

            var result = service.BuildNewRuleOwnersCustomField(new List<Rule> { rule }, owners);

            Assert.That(result.Any(), Is.EqualTo(shouldMatch));
            if (shouldMatch)
            {
                Assert.That(result[0].OwnerId, Is.EqualTo(expectedOwnerId));
            }
        }

        [Test]
        public void BuildNewRuleOwnersCustomField_ShouldReturnEmpty_WhenKeyMissing()
        {
            var rule = new Rule
            {
                Id = 1,
                CustomFields = "{'someOtherKey':'TeamA'}",
                Metadata = new RuleMetadata { Id = 100 }
            };
            var owners = new List<FwoOwner> { new FwoOwner { Id = 10, ExtAppId = "TeamA" } };

            var result = service.BuildNewRuleOwnersCustomField(new List<Rule> { rule }, owners);

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void BuildNewRuleOwnersCustomField_ShouldReturnEmpty_WhenCustomFieldMissing()
        {
            var rule = BuildRule(customFields: "{}");
            var owners = new List<FwoOwner> { BuildOwner() };

            var result = service.BuildNewRuleOwnersCustomField(new List<Rule> { rule }, owners);

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void DeserializeCustomFields_ShouldParseValidJson()
        {
            var json = "{'field-1': '20251119', 'field-2': 'CHG000816', 'field-3': '123'}";

            var result = UpdateRuleOwnerMapping.DeserializeCustomFields(json);

            Assert.That(result["field-3"], Is.EqualTo("123"));
        }

        [Test]
        public void DeserializeCustomFields_ShouldReturnEmpty_WhenInvalidJson()
        {
            var json = "{invalid json}";

            var result = UpdateRuleOwnerMapping.DeserializeCustomFields(json);

            Assert.That(result.Count, Is.EqualTo(0));
        }

        [Test]
        public void DeserializeCustomFields_ShouldReturnEmpty_WhenNullOrWhitespace()
        {
            Assert.That(UpdateRuleOwnerMapping.DeserializeCustomFields(null), Is.Empty);
            Assert.That(UpdateRuleOwnerMapping.DeserializeCustomFields(""), Is.Empty);
            Assert.That(UpdateRuleOwnerMapping.DeserializeCustomFields("   "), Is.Empty);
        }

        [Test]
        public void IsOwnerSourceFieldChanged_ShouldReturnTrue_WhenValueChanges()
        {
            var oldRule = new Rule { CustomFields = "{'owner':'TeamA'}" };
            var newRule = new Rule { CustomFields = "{'owner':'TeamB'}" };
            var change = new RuleChange { OldRule = oldRule, NewRule = newRule };

            bool result = service.IsOwnerSourceFieldChanged(change);

            Assert.That(result, Is.True);
        }

        [Test]
        public void IsOwnerSourceFieldChanged_ShouldReturnFalse_WhenValueUnchanged()
        {
            var oldRule = new Rule { CustomFields = "{'owner':'TeamA'}" };
            var newRule = new Rule { CustomFields = "{'owner':'TeamA'}" };
            var change = new RuleChange { OldRule = oldRule, NewRule = newRule };

            bool result = service.IsOwnerSourceFieldChanged(change);

            Assert.That(result, Is.False);
        }

        [Test]
        public void GetIpRangeAndVersion_ShouldReturnValidRange()
        {
            var (range, version) = UpdateRuleOwnerMapping.GetIpRangeAndVersion(
                "192.168.1.1",
                "192.168.1.10"
            );

            Assert.Multiple(() =>
            {
                Assert.That(range, Is.Not.Null);
                Assert.That(version, Is.EqualTo(AddressFamily.InterNetwork));
            });
        }

        [Test]
        public void GetMatchingOwnerIds_ShouldMatchOwner_WhenIpOverlaps()
        {
            var rule = BuildRule(
                froms: new[] { BuildNetworkLocation("192.168.1.5", "192.168.1.5") }
            );

            var owners = new List<FwoOwner>
            {
                BuildOwner(ownerNetworks: new[] { BuildOwnerNetwork("192.168.1.0", "192.168.1.255") })
            };

            var prepared = service.PrepareOwnerNetworks(owners);
            var result = UpdateRuleOwnerMapping.GetMatchingOwnerIds(rule, prepared);

            Assert.That(result.Contains(10));
        }

        [Test]
        public void GetMatchingOwnerIds_ShouldReturnEmpty_WhenNoFromsTos()
        {
            var rule = new Rule { Id = 1, Froms = Array.Empty<NetworkLocation>(), Tos = Array.Empty<NetworkLocation>() };
            var ownerNetworks = new List<UpdateRuleOwnerMapping.OwnerNetworkPrepared>();

            var result = UpdateRuleOwnerMapping.GetMatchingOwnerIds(rule, ownerNetworks);

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void BuildNewRuleOwnersIpBased_ShouldMapMultipleOwners_WhenMultipleRangesOverlap()
        {
            var rule = new Rule
            {
                Id = 1,
                Froms = new[]
                {
                    new NetworkLocation(new NetworkUser(), new NetworkObject { IP = "192.168.1.5", IpEnd = "192.168.1.5" })
                }
            };

            var owners = new List<FwoOwner>
            {
                new FwoOwner
                {
                    Id = 10,
                    OwnerNetworks = new[]
                    {
                        new OwnerNetwork { IP = "192.168.1.0", IpEnd = "192.168.1.10" }
                    }
                },
                new FwoOwner
                {
                    Id = 20,
                    OwnerNetworks = new[]
                    {
                        new OwnerNetwork { IP = "192.168.1.0", IpEnd = "192.168.1.255" }
                    }
                }
            };

            var result = service.BuildNewRuleOwnersIpBased(new List<Rule> { rule }, owners);

            Assert.That(result.Select(r => r.OwnerId), Is.EquivalentTo(new[] { 10, 20 }));
        }

        [Test]
        public void PrepareOwnerNetworks_ShouldSkipInvalidOwnerNetworks()
        {
            var owners = new List<FwoOwner>
            {
                new FwoOwner
                {
                    Id = 10,
                    OwnerNetworks = new[]
                    {
                        new OwnerNetwork { IP = "invalid", IpEnd = "192.168.1.255" },
                        new OwnerNetwork { IP = "192.168.2.0", IpEnd = "192.168.2.255" }
                    }
                }
            };

            var prepared = service.PrepareOwnerNetworks(owners);

            Assert.That(prepared.Count, Is.EqualTo(1));
            Assert.That(prepared[0].Ranges.Count, Is.EqualTo(1));
        }

        [Test]
        public void GetIpRangeAndVersion_ShouldReturnNull_ForInvalidRange()
        {
            var (range, version) = UpdateRuleOwnerMapping.GetIpRangeAndVersion("invalid", "192.168.1.10");

            Assert.That(range, Is.Null);
            Assert.That(version, Is.Null);

            (range, version) = UpdateRuleOwnerMapping.GetIpRangeAndVersion("192.168.1.10", "192.168.1.1");

            Assert.That(range, Is.Null);
            Assert.That(version, Is.Null);
        }

        [Test]
        public void GetIpRangeAndVersion_ShouldHandleIPv6()
        {
            var (range, version) = UpdateRuleOwnerMapping.GetIpRangeAndVersion("2001:db8::1", "2001:db8::10");

            Assert.That(range, Is.Not.Null);
            Assert.That(version, Is.EqualTo(AddressFamily.InterNetworkV6));
        }

        #region Test Data Builders
        private static Rule BuildRule(
            int id = 1,
            string customFields = "{'owner':'TeamA'}",
            long metadataId = 100,
            NetworkLocation[]? froms = null)
        {
            return new Rule
            {
                Id = id,
                CustomFields = customFields,
                Metadata = new RuleMetadata { Id = metadataId },
                Froms = froms ?? Array.Empty<NetworkLocation>()
            };
        }

        private static FwoOwner BuildOwner(
            int id = 10,
            string extAppId = "TeamA",
            OwnerNetwork[]? ownerNetworks = null)
        {
            return new FwoOwner
            {
                Id = id,
                ExtAppId = extAppId,
                OwnerNetworks = ownerNetworks ?? Array.Empty<OwnerNetwork>()
            };
        }

        private static NetworkLocation BuildNetworkLocation(string ipStart, string ipEnd)
        {
            return new NetworkLocation(
                new NetworkUser { },
                new NetworkObject { IP = ipStart, IpEnd = ipEnd }
            );
        }

        private static OwnerNetwork BuildOwnerNetwork(string ipStart, string ipEnd)
        {
            return new OwnerNetwork { IP = ipStart, IpEnd = ipEnd };
        }

        #endregion
    }
}

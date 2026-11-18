using FWO.Data.Labelling;
using FWO.Data.Modelling;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Groups stand for labels by a name "key: value" or by the prefix the naming convention gives app roles, app zones and
    /// network areas.
    /// </summary>
    [TestFixture]
    internal class DynamicIpLabelTest
    {
        private static readonly ModellingNamingConvention NamingConvention = new() { AppRolePattern = "AR", AppZone = "AZ", NetworkAreaPattern = "NA" };
        private static readonly ModellingNamingConvention OverlappingConvention = new() { AppRolePattern = "A", AppZone = "AZ" };

        [Test]
        public void ForModelledGroup_UsesTheKeyOfTheObjectType()
        {
            DynamicIpLabel label = DynamicIpLabel.ForModelledGroup(ModellingTypes.ModObjectType.AppRole, "AR4711-001");
            Assert.Multiple(() =>
            {
                Assert.That(label.Name, Is.EqualTo("AppRole: AR4711-001"));
                Assert.That(label.ObjectType, Is.EqualTo(ModellingTypes.ModObjectType.AppRole));
                Assert.That(DynamicIpLabel.ForModelledGroup(ModellingTypes.ModObjectType.NetworkArea, "NA1").Key, Is.EqualTo("NetworkArea"));
                Assert.That(DynamicIpLabel.IsLabelType(ModellingTypes.ModObjectType.AppZone), Is.True);
                Assert.That(DynamicIpLabel.IsLabelType(ModellingTypes.ModObjectType.AppServer), Is.False);
            });
        }

        [Test]
        public void ForModelledGroup_RefusesOtherObjectTypes()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => DynamicIpLabel.ForModelledGroup(ModellingTypes.ModObjectType.Service, "x"));
        }

        [TestCase("AR4711-001", "AppRole", "AR4711-001")]
        [TestCase("AZ4711", "AppZone", "AZ4711")]
        [TestCase("NA50", "NetworkArea", "NA50")]
        [TestCase("Stage: Prod", "Stage", "Prod")]
        [TestCase(" AppRole: AR1 ", "AppRole", "AR1")]
        public void FromGroupName_TellsKeyAndValue(string groupName, string key, string value)
        {
            Assert.That(DynamicIpLabel.FromGroupName(groupName, NamingConvention), Is.EqualTo(new DynamicIpLabel(key, value)));
        }

        [TestCase("WebServers")]
        [TestCase("AR")]
        [TestCase(": Prod")]
        [TestCase("Stage: ")]
        [TestCase(null)]
        public void FromGroupName_IsNullForOtherNames(string? groupName)
        {
            Assert.That(DynamicIpLabel.FromGroupName(groupName, NamingConvention), Is.Null);
        }

        [Test]
        public void GroupObjectType_PrefersTheLongestPrefixAndLabelKeys()
        {
            Assert.Multiple(() =>
            {
                Assert.That(DynamicIpLabel.GroupObjectType("AZ4711", OverlappingConvention), Is.EqualTo(ModellingTypes.ModObjectType.AppZone));
                Assert.That(DynamicIpLabel.GroupObjectType("AR4711", OverlappingConvention), Is.EqualTo(ModellingTypes.ModObjectType.AppRole));
                Assert.That(DynamicIpLabel.GroupObjectType("NetworkArea: NA1", new ModellingNamingConvention()), Is.EqualTo(ModellingTypes.ModObjectType.NetworkArea));
                Assert.That(DynamicIpLabel.GroupObjectType("Stage: Prod", NamingConvention), Is.Null);
                Assert.That(DynamicIpLabel.GroupObjectType("AR4711", new ModellingNamingConvention()), Is.Null);
            });
        }
    }
}

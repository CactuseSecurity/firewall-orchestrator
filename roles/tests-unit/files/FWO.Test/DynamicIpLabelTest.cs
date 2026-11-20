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

        [TestCase("AR4711-001", ModellingTypes.ModObjectType.AppRole, "AppRole", "AR4711-001")]
        [TestCase("AZ4711", ModellingTypes.ModObjectType.AppZone, "AppZone", "AZ4711")]
        [TestCase("NA50", ModellingTypes.ModObjectType.NetworkArea, "NetworkArea", "NA50")]
        [TestCase("ARCHIVE_SRV", ModellingTypes.ModObjectType.AppZone, "AppZone", "ARCHIVE_SRV")]
        [TestCase("Stage: Prod", null, "Stage", "Prod")]
        [TestCase(" AppRole: AR1 ", ModellingTypes.ModObjectType.NetworkArea, "AppRole", "AR1")]
        public void FromGroupName_TellsKeyAndValue(string groupName, ModellingTypes.ModObjectType? objectType, string key, string value)
        {
            Assert.That(DynamicIpLabel.FromGroupName(groupName, objectType), Is.EqualTo(new DynamicIpLabel(key, value)));
        }

        [TestCase("WebServers", null)]
        [TestCase("AR4711-001", null)]
        [TestCase("Svc", ModellingTypes.ModObjectType.ServiceGroup)]
        [TestCase(": Prod", null)]
        [TestCase("Stage: ", null)]
        [TestCase(null, ModellingTypes.ModObjectType.AppRole)]
        public void FromGroupName_IsNullForOtherNames(string? groupName, ModellingTypes.ModObjectType? objectType)
        {
            Assert.That(DynamicIpLabel.FromGroupName(groupName, objectType), Is.Null);
        }

        [Test]
        public void GroupObjectType_TakesLabelKeysElseTheRequestedType()
        {
            Assert.Multiple(() =>
            {
                Assert.That(DynamicIpLabel.GroupObjectType("NetworkArea: NA1", ModellingTypes.ModObjectType.AppRole), Is.EqualTo(ModellingTypes.ModObjectType.NetworkArea));
                Assert.That(DynamicIpLabel.GroupObjectType("Stage: Prod", ModellingTypes.ModObjectType.AppRole), Is.Null);
                Assert.That(DynamicIpLabel.GroupObjectType("Svc", ModellingTypes.ModObjectType.ServiceGroup), Is.EqualTo(ModellingTypes.ModObjectType.ServiceGroup));
                Assert.That(DynamicIpLabel.GroupObjectType("AR4711", null), Is.Null);
            });
        }

        [TestCase("AppRole", ModellingTypes.ModObjectType.AppRole)]
        [TestCase(" NetworkArea ", ModellingTypes.ModObjectType.NetworkArea)]
        [TestCase("ServiceGroup", ModellingTypes.ModObjectType.ServiceGroup)]
        [TestCase("appRole", null)]
        [TestCase("20", null)]
        [TestCase("Unknown", null)]
        [TestCase("", null)]
        [TestCase(null, null)]
        public void ParseObjectType_ReadsTheNamesOfObjectTypes(string? text, ModellingTypes.ModObjectType? expected)
        {
            Assert.That(DynamicIpLabel.ParseObjectType(text), Is.EqualTo(expected));
        }
    }
}

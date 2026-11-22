using FWO.Data.Labelling;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>Guards the names of AND-groups of labels, which must match those of the Guardicore import, and their label lists.</summary>
    [TestFixture]
    [Parallelizable]
    internal class LabelAndGroupsTest
    {
        private static readonly List<string> kGroupNames = ["OS: Linux & Stage: Prod", "Stage: Test"];
        private static readonly List<string> kLabelNames = ["OS: Linux", "Stage: Prod"];
        private static readonly List<string> kLabelWithSeparator = ["Team: R & D"];

        private static ConnectionLabel Label(int andGroup, string key, string value) =>
            new() { AndGroup = andGroup, Label = new() { Value = value, Key = new() { Name = key } } };

        [Test]
        public void GroupNames_SortTheLabelsOfEachGroupAndKeepTheGroupOrder()
        {
            List<ConnectionLabel> labels = [Label(1, "Stage", "Test"), Label(0, "Stage", "Prod"), Label(0, "OS", "Linux"), Label(2, "Stage", "Test")];

            Assert.That(LabelAndGroups.GroupNames(labels), Is.EqualTo(kGroupNames), "a repeated group is named once");
        }

        [Test]
        public void Groups_KeepTheLabelsOfEachGroupAsList()
        {
            List<LabelAndGroup> groups = LabelAndGroups.Groups([Label(0, "Stage", "Prod"), Label(0, "OS", "Linux"), Label(1, "Team", "R & D")]);

            Assert.Multiple(() =>
            {
                Assert.That(groups[0].LabelNames, Is.EqualTo(kLabelNames));
                Assert.That(groups[1].Name, Is.EqualTo("Team: R & D"));
                Assert.That(groups[1].LabelNames, Is.EqualTo(kLabelWithSeparator), "a value containing the separator stays one label");
                Assert.That(LabelAndGroups.GroupName(["Stage: Prod", "OS: Linux", "OS: Linux"]), Is.EqualTo(kGroupNames[0]));
            });
        }
    }
}

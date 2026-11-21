using FWO.Data.Labelling;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>Guards the names of AND-groups of labels, which must match those of the Guardicore import.</summary>
    [TestFixture]
    [Parallelizable]
    internal class LabelAndGroupsTest
    {
        private static readonly List<string> kGroupNames = ["OS: Linux & Stage: Prod", "Stage: Test"];
        private static readonly List<string> kLabelNames = ["OS: Linux", "Stage: Prod"];

        private static ConnectionLabel Label(int andGroup, string key, string value) =>
            new() { AndGroup = andGroup, Label = new() { Value = value, Key = new() { Name = key } } };

        [Test]
        public void GroupNames_SortTheLabelsOfEachGroupAndKeepTheGroupOrder()
        {
            List<ConnectionLabel> labels = [Label(1, "Stage", "Test"), Label(0, "Stage", "Prod"), Label(0, "OS", "Linux"), Label(2, "Stage", "Test")];

            Assert.That(LabelAndGroups.GroupNames(labels), Is.EqualTo(kGroupNames), "a repeated group is named once");
        }

        [Test]
        public void LabelNames_SplitAGroupName()
        {
            Assert.Multiple(() =>
            {
                Assert.That(LabelAndGroups.LabelNames("OS: Linux & Stage: Prod"), Is.EqualTo(kLabelNames));
                Assert.That(LabelAndGroups.LabelNames("Stage: Test").Single(), Is.EqualTo("Stage: Test"));
                Assert.That(LabelAndGroups.GroupName(["Stage: Prod", "OS: Linux", "OS: Linux"]), Is.EqualTo(kGroupNames[0]));
            });
        }
    }
}

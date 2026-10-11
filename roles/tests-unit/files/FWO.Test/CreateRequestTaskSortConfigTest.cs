using FWO.Data.Workflow;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal class CreateRequestTaskSortConfigTest
{
    [Test]
    public void Parse_ReturnsDefaultsWhenConfigIsEmpty()
    {
        CreateRequestTaskSortConfig config = CreateRequestTaskSortConfig.Parse("");

        Assert.Multiple(() =>
        {
            Assert.That(config.GroupCreatePriority, Is.EqualTo(0));
            Assert.That(config.GroupModifyAddPriority, Is.EqualTo(1));
            Assert.That(config.AccessPriority, Is.EqualTo(2));
            Assert.That(config.RuleModifyPriority, Is.EqualTo(3));
            Assert.That(config.RuleDeletePriority, Is.EqualTo(4));
            Assert.That(config.GroupModifyRemovePriority, Is.EqualTo(5));
            Assert.That(config.GroupDeletePriority, Is.EqualTo(6));
            Assert.That(config.AllowTaskSplit, Is.True);
        });
    }

    [Test]
    public void Parse_ReturnsDefaultsWhenConfigIsInvalid()
    {
        CreateRequestTaskSortConfig config = CreateRequestTaskSortConfig.Parse("{not valid json");

        Assert.Multiple(() =>
        {
            Assert.That(config.GroupCreatePriority, Is.EqualTo(0));
            Assert.That(config.GroupModifyAddPriority, Is.EqualTo(1));
            Assert.That(config.AccessPriority, Is.EqualTo(2));
            Assert.That(config.RuleModifyPriority, Is.EqualTo(3));
            Assert.That(config.RuleDeletePriority, Is.EqualTo(4));
            Assert.That(config.GroupModifyRemovePriority, Is.EqualTo(5));
            Assert.That(config.GroupDeletePriority, Is.EqualTo(6));
            Assert.That(config.AllowTaskSplit, Is.True);
        });
    }
}

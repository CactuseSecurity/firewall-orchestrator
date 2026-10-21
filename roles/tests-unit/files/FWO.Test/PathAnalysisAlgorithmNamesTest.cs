using FWO.Basics;
using FWO.Data;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Tests the mapping between the stable API names of the path analysis algorithms and their internal ids.
/// </summary>
[TestFixture]
[Parallelizable]
internal class PathAnalysisAlgorithmNamesTest
{
    private const long kUnknownAlgorithmId = 99;

    /// <summary>Verifies every name maps to its id, compared case-insensitively.</summary>
    [TestCase("none", GlobalConst.kPathAnalysisAlgorithmNone)]
    [TestCase("networkZoneTree", GlobalConst.kPathAnalysisAlgorithmNetworkZoneTree)]
    [TestCase("NetworkZoneTree", GlobalConst.kPathAnalysisAlgorithmNetworkZoneTree)]
    [TestCase("NONE", GlobalConst.kPathAnalysisAlgorithmNone)]
    public void TryGetId_WithKnownName_ReturnsId(string name, long expectedId)
    {
        bool found = PathAnalysisAlgorithmNames.TryGetId(name, out long id);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(id, Is.EqualTo(expectedId));
        });
    }

    /// <summary>Verifies an unknown name is not mapped.</summary>
    [TestCase("routing")]
    [TestCase("")]
    [TestCase("network zone tree")]
    public void TryGetId_WithUnknownName_ReturnsFalse(string name)
    {
        Assert.That(PathAnalysisAlgorithmNames.TryGetId(name, out _), Is.False);
    }

    /// <summary>Verifies every id maps back to its name in the spelling of the contract.</summary>
    [TestCase(GlobalConst.kPathAnalysisAlgorithmNone, PathAnalysisAlgorithmNames.kNone)]
    [TestCase(GlobalConst.kPathAnalysisAlgorithmNetworkZoneTree, PathAnalysisAlgorithmNames.kNetworkZoneTree)]
    public void GetName_WithKnownId_ReturnsName(long id, string expectedName)
    {
        Assert.That(PathAnalysisAlgorithmNames.GetName(id), Is.EqualTo(expectedName));
    }

    /// <summary>Verifies an unknown id has no name.</summary>
    [Test]
    public void GetName_WithUnknownId_ReturnsNull()
    {
        Assert.That(PathAnalysisAlgorithmNames.GetName(kUnknownAlgorithmId), Is.Null);
    }

    /// <summary>Verifies all names contain every algorithm, and the requestable names leave out none.</summary>
    [Test]
    public void Requestable_ExcludesNoneOnly()
    {
        Assert.Multiple(() =>
        {
            Assert.That(PathAnalysisAlgorithmNames.All,
                Is.EquivalentTo(new List<string> { PathAnalysisAlgorithmNames.kNone, PathAnalysisAlgorithmNames.kNetworkZoneTree }));
            Assert.That(PathAnalysisAlgorithmNames.Requestable,
                Is.EquivalentTo(new List<string> { PathAnalysisAlgorithmNames.kNetworkZoneTree }));
        });
    }
}

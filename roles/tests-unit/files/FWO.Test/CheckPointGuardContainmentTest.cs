using FWO.Data.RulePositioning.CheckPoint;
using FWO.Services.RulePositioning.CheckPoint;
using NetTools;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal class CheckPointGuardContainmentTest
{
    [TestCase("10.0.0.0-10.0.0.255", "10.0.0.10-10.0.0.20", CheckPointGuardContainmentResult.Contained)]
    [TestCase("10.0.0.10-10.0.0.20", "10.0.0.0-10.0.0.255", CheckPointGuardContainmentResult.NotContained)]
    [TestCase("10.0.0.0-10.0.0.9", "10.0.0.10-10.0.0.20", CheckPointGuardContainmentResult.NotContained)]
    [TestCase("2001:db8::/120", "2001:db8::10/124", CheckPointGuardContainmentResult.Contained)]
    public void PositiveKnownCoverage_ReturnsExpected(
        string guard,
        string requested,
        CheckPointGuardContainmentResult expected)
    {
        CheckPointGuardSide side = Side(false, [Term([Range(guard)])]);

        Assert.That(CheckPointGuardContainmentEvaluator.Evaluate(side, [Range(requested)]), Is.EqualTo(expected));
    }

    [Test]
    public void AdjacentIndependentTerms_CoverRequestedUnion()
    {
        CheckPointGuardSide side = Side(false,
        [
            Term([Range("10.0.0.0-10.0.0.9")]),
            Term([Range("10.0.0.10-10.0.0.20")])
        ]);

        Assert.That(CheckPointGuardContainmentEvaluator.Evaluate(
            side, [Range("10.0.0.0-10.0.0.20")]), Is.EqualTo(CheckPointGuardContainmentResult.Contained));
    }

    [Test]
    public void UnresolvedIncludedTerm_IsIndeterminateOnlyWhenKnownCoverageIsIncomplete()
    {
        CheckPointGuardSide complete = Side(false,
        [
            Term([Range("10.0.0.0/24")]),
            Term([], unresolvedIncluded: true)
        ]);
        CheckPointGuardSide incomplete = Side(false,
        [
            Term([Range("10.0.0.0/28")]),
            Term([], unresolvedIncluded: true)
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(CheckPointGuardContainmentEvaluator.Evaluate(complete, [Range("10.0.0.10")]),
                Is.EqualTo(CheckPointGuardContainmentResult.Contained));
            Assert.That(CheckPointGuardContainmentEvaluator.Evaluate(incomplete, [Range("10.0.0.20")]),
                Is.EqualTo(CheckPointGuardContainmentResult.Indeterminate));
        });
    }

    [Test]
    public void ExclusionsRemainScopedToTheirOwnTopLevelTerm()
    {
        CheckPointGuardSide side = Side(false,
        [
            Term([Range("10.0.0.0/24")], [Range("10.0.0.10")]),
            Term([Range("10.0.0.10")])
        ]);

        Assert.That(CheckPointGuardContainmentEvaluator.Evaluate(side, [Range("10.0.0.10")]),
            Is.EqualTo(CheckPointGuardContainmentResult.Contained));
    }

    [TestCase("10.0.0.0-10.0.0.10", "10.0.0.0", "10.0.0.10", "10.0.0.1-10.0.0.9")]
    [TestCase("2001:db8::-2001:db8::10", "2001:db8::", "2001:db8::10", "2001:db8::1-2001:db8::9")]
    public void InclusiveBoundaryExclusions_LeaveOnlyTheInterior(
        string included,
        string first,
        string last,
        string interior)
    {
        CheckPointGuardSide side = Side(false,
        [Term([Range(included)], [Range(first), Range(last)])]);

        Assert.Multiple(() =>
        {
            Assert.That(CheckPointGuardContainmentEvaluator.Evaluate(side, [Range(first)]),
                Is.EqualTo(CheckPointGuardContainmentResult.NotContained));
            Assert.That(CheckPointGuardContainmentEvaluator.Evaluate(side, [Range(last)]),
                Is.EqualTo(CheckPointGuardContainmentResult.NotContained));
            Assert.That(CheckPointGuardContainmentEvaluator.Evaluate(side, [Range(interior)]),
                Is.EqualTo(CheckPointGuardContainmentResult.Contained));
        });
    }

    [Test]
    public void OverlappingAndAdjacentExclusions_RemoveTheirWholeUnion()
    {
        CheckPointGuardSide side = Side(false,
        [
            Term(
                [Range("10.0.0.0-10.0.0.30")],
                [Range("10.0.0.5-10.0.0.15"), Range("10.0.0.10-10.0.0.20"), Range("10.0.0.21")])
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(CheckPointGuardContainmentEvaluator.Evaluate(side, [Range("10.0.0.4")]),
                Is.EqualTo(CheckPointGuardContainmentResult.Contained));
            Assert.That(CheckPointGuardContainmentEvaluator.Evaluate(side, [Range("10.0.0.5-10.0.0.21")]),
                Is.EqualTo(CheckPointGuardContainmentResult.NotContained));
            Assert.That(CheckPointGuardContainmentEvaluator.Evaluate(side, [Range("10.0.0.22-10.0.0.30")]),
                Is.EqualTo(CheckPointGuardContainmentResult.Contained));
        });
    }

    [Test]
    public void MixedAddressFamilies_DoNotOverlapOrSubtractEachOther()
    {
        CheckPointGuardSide ipv4Side = Side(false,
        [Term([Range("10.0.0.0/24")], [Range("2001:db8::/120")])]);
        CheckPointGuardSide ipv6Side = Side(false,
        [Term([Range("2001:db8::/120")], [Range("10.0.0.0/24")])]);

        Assert.Multiple(() =>
        {
            Assert.That(CheckPointGuardContainmentEvaluator.Evaluate(ipv4Side, [Range("10.0.0.10")]),
                Is.EqualTo(CheckPointGuardContainmentResult.Contained));
            Assert.That(CheckPointGuardContainmentEvaluator.Evaluate(ipv4Side, [Range("2001:db8::10")]),
                Is.EqualTo(CheckPointGuardContainmentResult.NotContained));
            Assert.That(CheckPointGuardContainmentEvaluator.Evaluate(ipv6Side, [Range("2001:db8::10")]),
                Is.EqualTo(CheckPointGuardContainmentResult.Contained));
            Assert.That(CheckPointGuardContainmentEvaluator.Evaluate(ipv6Side, [Range("10.0.0.10")]),
                Is.EqualTo(CheckPointGuardContainmentResult.NotContained));
        });
    }

    [Test]
    public void NegatedGuard_UsesInclusiveOverlapAtExactBoundary()
    {
        CheckPointGuardSide side = Side(true, [Term([Range("10.0.0.10-10.0.0.20")])]);

        Assert.Multiple(() =>
        {
            Assert.That(CheckPointGuardContainmentEvaluator.Evaluate(side, [Range("10.0.0.20")]),
                Is.EqualTo(CheckPointGuardContainmentResult.NotContained));
            Assert.That(CheckPointGuardContainmentEvaluator.Evaluate(side, [Range("10.0.0.21")]),
                Is.EqualTo(CheckPointGuardContainmentResult.Contained));
        });
    }

    [TestCase(false, CheckPointGuardContainmentResult.Contained)]
    [TestCase(true, CheckPointGuardContainmentResult.NotContained)]
    public void FullFamilyAny_RespectsRuleSideNegation(bool negated, CheckPointGuardContainmentResult expected)
    {
        CheckPointGuardSide side = Side(negated, [Term([Range("0.0.0.0/0")])]);

        Assert.That(CheckPointGuardContainmentEvaluator.Evaluate(side, [Range("198.51.100.10")]), Is.EqualTo(expected));
    }

    [Test]
    public void NegatedUnknownSemantics_UsesOverlapPrecedence()
    {
        CheckPointGuardSide knownOverlap = Side(true,
        [Term([Range("10.0.0.0/24")], unresolvedIncluded: true)]);
        CheckPointGuardSide unknownOnly = Side(true, [Term([], unresolvedIncluded: true)]);
        CheckPointGuardSide knownDisjoint = Side(true, [Term([Range("10.0.0.0/24")])]);

        Assert.Multiple(() =>
        {
            Assert.That(CheckPointGuardContainmentEvaluator.Evaluate(knownOverlap, [Range("10.0.0.1")]),
                Is.EqualTo(CheckPointGuardContainmentResult.NotContained));
            Assert.That(CheckPointGuardContainmentEvaluator.Evaluate(unknownOnly, [Range("192.0.2.1")]),
                Is.EqualTo(CheckPointGuardContainmentResult.Indeterminate));
            Assert.That(CheckPointGuardContainmentEvaluator.Evaluate(knownDisjoint, [Range("192.0.2.1")]),
                Is.EqualTo(CheckPointGuardContainmentResult.Contained));
        });
    }

    [Test]
    public void Cancellation_IsPropagated()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => CheckPointGuardContainmentEvaluator.Evaluate(
            Side(false, [Term([Range("10.0.0.0/24")])]),
            [Range("10.0.0.1")], cancellation.Token));
    }

    private static CheckPointGuardSide Side(bool negated, IReadOnlyCollection<CheckPointGuardAddressTerm> terms) =>
        new(negated, terms);

    private static CheckPointGuardAddressTerm Term(
        IReadOnlyCollection<IPAddressRange> included,
        IReadOnlyCollection<IPAddressRange>? excluded = null,
        bool unresolvedIncluded = false,
        bool unresolvedExcluded = false) =>
        new(included, excluded ?? [], unresolvedIncluded, unresolvedExcluded);

    private static IPAddressRange Range(string value) => IPAddressRange.Parse(value);
}

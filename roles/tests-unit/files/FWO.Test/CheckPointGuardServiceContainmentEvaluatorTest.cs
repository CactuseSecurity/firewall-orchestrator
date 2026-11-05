using FWO.Basics;
using FWO.Data.RulePositioning;
using FWO.Data.RulePositioning.CheckPoint;
using FWO.Services.RulePositioning.CheckPoint;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
public class CheckPointGuardServiceContainmentEvaluatorTest
{
    [Test]
    public void AnyGuard_CoversMixedRequestedServices()
    {
        CheckPointGuardContainmentResult result = Evaluate(
            Scope(false, Any()),
            Tcp(443), Udp(53), Protocol(1));

        Assert.That(result, Is.EqualTo(CheckPointGuardContainmentResult.Contained));
    }

    [Test]
    public void AdjacentGuardRanges_JointlyCoverRequestedRange()
    {
        CheckPointGuardContainmentResult result = Evaluate(
            Scope(false, Tcp(100, 150), Tcp(151, 200)),
            Tcp(100, 200));

        Assert.That(result, Is.EqualTo(CheckPointGuardContainmentResult.Contained));
    }

    [Test]
    public void GuardRange_MustCoverEveryRequestedPort()
    {
        CheckPointGuardContainmentResult result = Evaluate(
            Scope(false, Tcp(80, 443)),
            Tcp(443), Tcp(22));

        Assert.That(result, Is.EqualTo(CheckPointGuardContainmentResult.NotContained));
    }

    [Test]
    public void GuardProtocol_MustCoverEveryRequestedProtocol()
    {
        CheckPointGuardContainmentResult result = Evaluate(
            Scope(false, Tcp(443)),
            Tcp(443), Udp(443));

        Assert.That(result, Is.EqualTo(CheckPointGuardContainmentResult.NotContained));
    }

    [Test]
    public void NarrowGuard_DoesNotCoverRequestedAny()
    {
        CheckPointGuardContainmentResult result = Evaluate(
            Scope(false, Tcp(1, GlobalConst.kMaxPortNumber)),
            Any());

        Assert.That(result, Is.EqualTo(CheckPointGuardContainmentResult.NotContained));
    }

    [Test]
    public void NegatedGuard_AcceptsDisjointServiceAndRejectsAnyOverlap()
    {
        CheckPointGuardServiceScope scope = Scope(true, Tcp(100, 200));

        Assert.Multiple(() =>
        {
            Assert.That(Evaluate(scope, Tcp(443)), Is.EqualTo(CheckPointGuardContainmentResult.Contained));
            Assert.That(Evaluate(scope, Tcp(50, 100)), Is.EqualTo(CheckPointGuardContainmentResult.NotContained));
        });
    }

    [Test]
    public void UnresolvedPositiveTerms_AreIndeterminateOnlyWhenKnownCoverageIsInsufficient()
    {
        CheckPointGuardServiceScope scope = Scope(false, true, Tcp(80, 443));

        Assert.Multiple(() =>
        {
            Assert.That(Evaluate(scope, Tcp(443)), Is.EqualTo(CheckPointGuardContainmentResult.Contained));
            Assert.That(Evaluate(scope, Tcp(22)), Is.EqualTo(CheckPointGuardContainmentResult.Indeterminate));
        });
    }

    [Test]
    public void UnresolvedNegatedTerms_AreIndeterminateUnlessKnownExclusionAlreadyRejects()
    {
        CheckPointGuardServiceScope scope = Scope(true, true, Tcp(80, 443));

        Assert.Multiple(() =>
        {
            Assert.That(Evaluate(scope, Tcp(443)), Is.EqualTo(CheckPointGuardContainmentResult.NotContained));
            Assert.That(Evaluate(scope, Tcp(22)), Is.EqualTo(CheckPointGuardContainmentResult.Indeterminate));
        });
    }

    [Test]
    public void Evaluation_PropagatesCancellation()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            CheckPointGuardServiceContainmentEvaluator.Evaluate(
                Scope(false, Any()),
                [Tcp(443)],
                cancellation.Token));
    }

    private static CheckPointGuardContainmentResult Evaluate(
        CheckPointGuardServiceScope scope,
        params PositioningService[] requested) =>
        CheckPointGuardServiceContainmentEvaluator.Evaluate(scope, requested);

    private static CheckPointGuardServiceScope Scope(
        bool isNegated,
        params PositioningService[] services) => new(isNegated, services, false);

    private static CheckPointGuardServiceScope Scope(
        bool isNegated,
        bool hasUnresolvedTerms,
        params PositioningService[] services) => new(isNegated, services, hasUnresolvedTerms);

    private static PositioningService Any() =>
        new(GlobalConst.kAnyIpProtocolId, null, null);

    private static PositioningService Tcp(int port) => Tcp(port, port);

    private static PositioningService Tcp(int start, int end) => new(6, start, end);

    private static PositioningService Udp(int port) => new(17, port, port);

    private static PositioningService Protocol(int protocolId) => new(protocolId, null, null);
}

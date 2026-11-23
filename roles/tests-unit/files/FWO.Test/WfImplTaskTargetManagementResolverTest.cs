using FWO.Data;
using FWO.Data.Workflow;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal class WfImplTaskTargetManagementResolverTest
{
    private const int kRequestManagementId = 11;
    private const int kGatewayManagementId = 22;
    private const int kGatewayId = 33;

    [Test]
    public void TryResolveManagementId_GatewayOwnerOverridesRequestManagementWhenImplementationManagementIsNull()
    {
        bool resolved = Resolve(
            new WfReqTask { ManagementId = kRequestManagementId },
            new WfImplTask { DeviceId = kGatewayId },
            Gateway(),
            out int managementId);

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Is.True);
            Assert.That(managementId, Is.EqualTo(kGatewayManagementId));
        });
    }

    [Test]
    public void TryResolveManagementId_AcceptsGatewayOwnerAsExplicitImplementationManagement()
    {
        bool resolved = Resolve(
            new WfReqTask { ManagementId = kRequestManagementId },
            new WfImplTask { DeviceId = kGatewayId, ManagementId = kGatewayManagementId },
            Gateway(),
            out int managementId);

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Is.True);
            Assert.That(managementId, Is.EqualTo(kGatewayManagementId));
        });
    }

    [Test]
    public void TryResolveManagementId_RejectsExplicitManagementThatDiffersFromGatewayOwner()
    {
        bool resolved = Resolve(
            new WfReqTask { ManagementId = kRequestManagementId },
            new WfImplTask { DeviceId = kGatewayId, ManagementId = kRequestManagementId },
            Gateway(),
            out int managementId);

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Is.False);
            Assert.That(managementId, Is.Zero);
        });
    }

    [Test]
    public void TryResolveManagementId_RejectsGatewayWhoseIdDiffersFromImplementationTarget()
    {
        bool resolved = Resolve(
            new WfReqTask { ManagementId = kRequestManagementId },
            new WfImplTask { DeviceId = kGatewayId },
            Gateway(kGatewayId + 1),
            out int managementId);

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Is.False);
            Assert.That(managementId, Is.Zero);
        });
    }

    [TestCase(false, kGatewayManagementId)]
    [TestCase(true, 0)]
    public void TryResolveManagementId_RejectsGatewayBoundTaskWithoutUsableGateway(
        bool supplyGateway,
        int gatewayManagementId)
    {
        Device? gateway = supplyGateway ? Gateway(managementId: gatewayManagementId) : null;

        bool resolved = Resolve(
            null,
            new WfImplTask { DeviceId = kGatewayId },
            gateway,
            out int managementId);

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Is.False);
            Assert.That(managementId, Is.Zero);
        });
    }

    [Test]
    public void TryResolveManagementId_NonGatewayTaskPrefersExplicitImplementationManagement()
    {
        bool resolved = Resolve(
            new WfReqTask { ManagementId = kRequestManagementId },
            new WfImplTask { ManagementId = kGatewayManagementId },
            null,
            out int managementId);

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Is.True);
            Assert.That(managementId, Is.EqualTo(kGatewayManagementId));
        });
    }

    [Test]
    public void TryResolveManagementId_NonGatewayTaskFallsBackToRequestManagement()
    {
        bool resolved = Resolve(
            new WfReqTask { ManagementId = kRequestManagementId },
            new WfImplTask(),
            null,
            out int managementId);

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Is.True);
            Assert.That(managementId, Is.EqualTo(kRequestManagementId));
        });
    }

    [TestCase(null)]
    [TestCase(0)]
    public void TryResolveManagementId_RejectsNonGatewayTaskWithoutPositiveManagement(int? managementIdValue)
    {
        bool resolved = Resolve(
            new WfReqTask { ManagementId = managementIdValue },
            new WfImplTask(),
            null,
            out int managementId);

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Is.False);
            Assert.That(managementId, Is.Zero);
        });
    }

    private static bool Resolve(
        WfReqTask? requestTask,
        WfImplTask implementationTask,
        Device? gateway,
        out int managementId) =>
        WfImplTaskTargetManagementResolver.TryResolveManagementId(
            requestTask,
            implementationTask,
            gateway,
            out managementId);

    private static Device Gateway(int id = kGatewayId, int managementId = kGatewayManagementId) =>
        new()
        {
            Id = id,
            Management = new Management { Id = managementId }
        };
}

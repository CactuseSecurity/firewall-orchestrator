using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Report;
using FWO.Report;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ReportVariancesTest
    {
        private const int kManagementId = 1;

        [Test]
        public void CollectObjectsInReport_KeepsNameAndOwner()
        {
            FwoOwner owner = new() { Id = 5, Name = "Core Banking", ExtAppId = "APP-1001" };
            OwnerConnectionReport ownerReport = new() { Name = owner.Name, Owner = owner };

            OwnerConnectionReport collected = ReportVariances.CollectObjectsInReport(ownerReport);

            Assert.Multiple(() =>
            {
                Assert.That(collected.Name, Is.EqualTo(owner.Name));
                Assert.That(collected.Owner, Is.SameAs(owner));
            });
        }

        [Test]
        public void CollectObjectsInReport_AddsObjectsOfDifferencesWithoutChangingTheReport()
        {
            ModellingConnection notImplemented = new() { Id = 1 };
            ModellingConnection withDifferences = new() { Id = 2 };
            ModellingAppRole missingAppRole = new() { Id = 3 };
            ModellingAppRole differingAppRole = new() { Id = 4 };
            OwnerConnectionReport ownerReport = new()
            {
                Connections = [notImplemented],
                RuleDifferences = [new() { ModelledConnection = withDifferences }],
                MissingAppRoles = new() { [kManagementId] = [missingAppRole] },
                DifferingAppRoles = new() { [kManagementId] = [differingAppRole] }
            };

            OwnerConnectionReport collected = ReportVariances.CollectObjectsInReport(ownerReport);

            Assert.Multiple(() =>
            {
                Assert.That(collected.Connections, Has.Count.EqualTo(3));
                Assert.That(collected.Connections[0], Is.SameAs(notImplemented));
                Assert.That(collected.Connections[1], Is.SameAs(withDifferences));
                Assert.That(collected.Connections[2].SourceAppRoles.Select(a => a.Content.Id), Is.EqualTo(new long[] { missingAppRole.Id }));
                Assert.That(collected.Connections[2].DestinationAppRoles.Select(a => a.Content.Id), Is.EqualTo(new long[] { differingAppRole.Id }));
                Assert.That(ownerReport.Connections, Has.Count.EqualTo(1));
            });
        }
    }
}

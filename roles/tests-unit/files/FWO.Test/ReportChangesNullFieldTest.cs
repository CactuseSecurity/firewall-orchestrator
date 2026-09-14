using FWO.Basics;
using FWO.Data;
using FWO.Data.Report;
using FWO.Report;
using FWO.Report.Filter;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Covers change reports containing objects whose nullable database columns
    /// (name, comment, uid, member names) are not set.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class ReportChangesNullFieldTest
    {
        private const string NewObjectName = "ObjectWithFilledFields";
        private const string NewObjectComment = "CommentAfterChange";
        private const string NewObjectUid = "UidAfterChange";
        private const string NewServiceName = "ServiceWithFilledFields";
        private const string NewServiceComment = "ServiceCommentAfterChange";
        private const string NewServiceUid = "ServiceUidAfterChange";

        private readonly SimulatedUserConfig userConfig = new();
        private readonly DynGraphqlQuery query = new("TestFilter")
        {
            ReportTimeString = "2023-04-20T17:50:04",
        };
        private readonly TimeFilter timeFilter = new()
        {
            TimeRangeType = TimeRangeType.Fixeddates,
            StartTime = DateTime.Parse("2023-04-19T17:00:04"),
            EndTime = DateTime.Parse("2023-04-20T17:00:04")
        };

        [Test]
        public void ExportToHtml_RendersChangedObject_WhenPreviousValuesAreNull()
        {
            ReportChanges reportChanges = new(query, userConfig, ReportType.Changes, timeFilter, true)
            {
                ReportData = ConstructChangeReportWithNullFields()
            };

            string report = reportChanges.ExportToHtml();

            Assert.Multiple(() =>
            {
                Assert.That(report, Does.Contain(NewObjectName));
                Assert.That(report, Does.Contain(NewObjectComment));
                Assert.That(report, Does.Contain(NewObjectUid));
                Assert.That(report, Does.Contain(NewServiceName));
                Assert.That(report, Does.Contain(NewServiceComment));
                Assert.That(report, Does.Contain(NewServiceUid));
            });
        }

        [Test]
        public void ExportToCsv_RendersChangedObject_WhenPreviousValuesAreNull()
        {
            ReportChanges reportChanges = new(query, userConfig, ReportType.ResolvedChanges, timeFilter, true)
            {
                ReportData = ConstructChangeReportWithNullFields()
            };

            string report = reportChanges.ExportToCsv();

            Assert.Multiple(() =>
            {
                Assert.That(report, Does.Contain(NewObjectName));
                Assert.That(report, Does.Contain(NewObjectComment));
                Assert.That(report, Does.Contain(NewServiceName));
                Assert.That(report, Does.Contain(NewServiceComment));
            });
        }

        [Test]
        public void ExportToJson_RendersChangedObject_WhenPreviousValuesAreNull()
        {
            ReportChanges reportChanges = new(query, userConfig, ReportType.ResolvedChanges, timeFilter, true)
            {
                ReportData = ConstructChangeReportWithNullFields()
            };

            string report = reportChanges.ExportToJson();

            Assert.Multiple(() =>
            {
                Assert.That(report, Does.Contain(NewObjectName));
                Assert.That(report, Does.Contain(NewObjectComment));
                Assert.That(report, Does.Contain(NewServiceName));
                Assert.That(report, Does.Contain(NewServiceComment));
            });
        }

        [Test]
        public void ExportToHtml_RendersChangedObject_WhenAllValuesStayNull()
        {
            ReportChanges reportChanges = new(query, userConfig, ReportType.Changes, timeFilter, true)
            {
                ReportData = ConstructChangeReportWithoutAnyValues()
            };

            string report = reportChanges.ExportToHtml();

            // nothing was filled, so no value may be marked as added or deleted
            Assert.Multiple(() =>
            {
                Assert.That(report, Does.Contain("network_object_modified"));
                Assert.That(report, Does.Not.Contain(GlobalConst.kStyleDeleted));
                Assert.That(report, Does.Not.Contain(GlobalConst.kStyleAdded));
            });
        }

        private static ReportData ConstructChangeReportWithNullFields()
        {
            return ConstructChangeReport(BuildObjectWithNullFields(1), BuildFilledObject(2), BuildServiceWithNullFields(1), BuildFilledService(2));
        }

        private static ReportData ConstructChangeReportWithoutAnyValues()
        {
            return ConstructChangeReport(BuildObjectWithNullFields(1), BuildObjectWithNullFields(2), BuildServiceWithNullFields(1), BuildServiceWithNullFields(2));
        }

        private static ReportData ConstructChangeReport(NetworkObject oldObject, NetworkObject newObject, NetworkService oldService, NetworkService newService)
        {
            ObjectChange objectChange = new()
            {
                ChangeAction = 'C',
                ChangeImport = new ChangeImport() { Time = new DateTime(2023, 04, 05, 12, 0, 0) },
                OldObject = oldObject,
                NewObject = newObject
            };
            ServiceChange serviceChange = new()
            {
                ChangeAction = 'C',
                ChangeImport = new ChangeImport() { Time = new DateTime(2023, 04, 05, 12, 0, 0) },
                OldService = oldService,
                NewService = newService
            };

            return new ReportData()
            {
                ManagementData =
                [
                    new ()
                    {
                        Name = "TestMgt",
                        Devices = [ new () { Name = "TestDev" } ],
                        RuleChanges = [],
                        ObjectChanges = [objectChange],
                        ServiceChanges = [serviceChange]
                    }
                ]
            };
        }

        /// <summary>
        /// Builds an object as it arrives from the database when the nullable columns are empty.
        /// </summary>
        /// <param name="id">Object id.</param>
        /// <returns>The object without any optional value.</returns>
        private static NetworkObject BuildObjectWithNullFields(int id)
        {
            return new NetworkObject
            {
                Id = id,
                Name = null!,
                Comment = null!,
                Uid = null!,
                MemberNames = null!,
                IP = "1.2.3.4/32",
                IpEnd = "1.2.3.4/32",
                Type = new NetworkObjectType() { Name = ObjectType.Network }
            };
        }

        private static NetworkObject BuildFilledObject(int id)
        {
            return new NetworkObject
            {
                Id = id,
                Name = NewObjectName,
                Comment = NewObjectComment,
                Uid = NewObjectUid,
                MemberNames = "MemberAfterChange",
                IP = "1.2.3.4/32",
                IpEnd = "1.2.3.4/32",
                Type = new NetworkObjectType() { Name = ObjectType.Network }
            };
        }

        /// <summary>
        /// Builds a service as it arrives from the database when the nullable columns are empty.
        /// </summary>
        /// <param name="id">Service id.</param>
        /// <returns>The service without any optional value.</returns>
        private static NetworkService BuildServiceWithNullFields(int id)
        {
            return new NetworkService
            {
                Id = id,
                Name = null!,
                Comment = null!,
                Uid = null!,
                MemberNames = null!,
                DestinationPort = 443,
                DestinationPortEnd = 443,
                Protocol = new NetworkProtocol { Id = 6, Name = "TCP" }
            };
        }

        private static NetworkService BuildFilledService(int id)
        {
            return new NetworkService
            {
                Id = id,
                Name = NewServiceName,
                Comment = NewServiceComment,
                Uid = NewServiceUid,
                MemberNames = "ServiceMemberAfterChange",
                DestinationPort = 443,
                DestinationPortEnd = 443,
                Protocol = new NetworkProtocol { Id = 6, Name = "TCP" }
            };
        }
    }
}

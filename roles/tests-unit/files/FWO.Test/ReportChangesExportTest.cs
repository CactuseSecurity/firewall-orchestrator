using FWO.Basics;
using FWO.Data;
using FWO.Data.Report;
using FWO.Report;
using FWO.Report.Filter;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Covers the user changes and the empty object sections of the changes report exports.
    /// </summary>
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class ReportChangesExportTest
    {
        private const string kUserName = "changed user";
        private const string kUserComment = "user comment";

        private readonly SimulatedUserConfig userConfig = new();

        private ReportChanges CreateReport(ReportType reportType, bool includeObjects, params ManagementReport[] managements)
        {
            DynGraphqlQuery query = new("") { ReportTimeString = "2026-01-15T12:00:00" };
            TimeFilter timeFilter = new() { TimeRangeType = TimeRangeType.Fixeddates, StartTime = new DateTime(2026, 1, 1), EndTime = new DateTime(2026, 2, 1) };
            ReportChanges report = new(query, userConfig, reportType, timeFilter, includeObjects);
            report.ReportData.ManagementData = [.. managements];
            return report;
        }

        private static ManagementReport ManagementWithUserChange()
        {
            return new ManagementReport
            {
                Id = 1,
                Name = "management",
                RuleChanges = [],
                UserChanges =
                [
                    new UserChange
                    {
                        ChangeAction = 'I',
                        ChangeImport = new ChangeImport { Time = new DateTime(2026, 1, 10, 8, 0, 0) },
                        NewUser = new NetworkUser { Id = 7, Name = kUserName, Comment = kUserComment }
                    }
                ]
            };
        }

        [Test]
        public void TheCsvOfAResolvedChangesReportListsTheUserChanges()
        {
            ReportChanges report = CreateReport(ReportType.ResolvedChanges, includeObjects: true, ManagementWithUserChange());

            string csv = report.ExportToCsv();

            Assert.That(csv, Does.Contain("\"User objects\""));
            Assert.That(csv, Does.Contain("\"management-name\",\"change-time\",\"change-type\",\"user-name\",\"user-comment\""));
            string userRow = csv.Split('\n').Single(line => line.Contains(kUserName));
            Assert.That(userRow, Does.StartWith("\"management\""));
            Assert.That(userRow, Does.Contain(kUserComment));
        }

        [Test]
        public void WithoutUserChangesTheCsvHasNoUserSection()
        {
            ManagementReport management = ManagementWithUserChange();
            management.UserChanges = [];
            ReportChanges report = CreateReport(ReportType.ResolvedChanges, includeObjects: true, management);

            Assert.That(report.ExportToCsv(), Does.Not.Contain("\"User objects\""));
        }

        [Test]
        public void OnlyResolvedChangesReportsCanBeExportedAsCsv()
        {
            ReportChanges report = CreateReport(ReportType.Changes, includeObjects: false, ManagementWithUserChange());

            Assert.Throws<NotImplementedException>(() => report.ExportToCsv());
        }

        [Test]
        public void TheHtmlListsTheUserChanges()
        {
            ReportChanges report = CreateReport(ReportType.Changes, includeObjects: true, ManagementWithUserChange());

            string html = report.ExportToHtml();

            Assert.That(html, Does.Contain("User objects</h4>"));
            Assert.That(html, Does.Contain(kUserName));
            Assert.That(html, Does.Contain(kUserComment));
        }

        [Test]
        public void TheHtmlSaysSoWhenNoObjectsOrServicesChanged()
        {
            ManagementReport management = ManagementWithUserChange();
            management.ObjectChanges = null;
            management.ServiceChanges = [];
            management.UserChanges = null;
            ReportChanges report = CreateReport(ReportType.Changes, includeObjects: true, management);

            string html = report.ExportToHtml();

            // rules, network objects and services each say so; without user changes there is no user section at all
            Assert.That(html.Split(userConfig.GetText("no_changes_found")).Length - 1, Is.EqualTo(3));
            Assert.That(html, Does.Not.Contain("User objects"));
        }

        [Test]
        public void TheJsonListsTheUserChanges()
        {
            ReportChanges report = CreateReport(ReportType.Changes, includeObjects: true, ManagementWithUserChange());

            JObject json = JObject.Parse(report.ExportToJson());

            JArray userChanges = (JArray)json["managements"]![0]!["management"]!["User Object changes"]!;
            Assert.That(userChanges, Has.Count.EqualTo(1));
            Assert.That(userChanges[0].ToString(), Does.Contain(kUserName));
            Assert.That(json["managements"]![0]!["management"]!["Network Object changes"], Is.Null);
        }

        [Test]
        public void WithoutObjectsTheJsonHasNoUserChanges()
        {
            ReportChanges report = CreateReport(ReportType.Changes, includeObjects: false, ManagementWithUserChange());

            JObject json = JObject.Parse(report.ExportToJson());

            Assert.That(json["managements"]![0]!["management"]!["User Object changes"], Is.Null);
        }
    }
}

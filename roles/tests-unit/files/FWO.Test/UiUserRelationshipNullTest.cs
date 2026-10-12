using FWO.Data;
using FWO.Data.Report;
using FWO.Data.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// A UI role sees the users of its own tenant only (SEC-19), so the API returns null for a user
    /// relationship pointing to a user of another tenant. The models holding such a relationship keep
    /// an empty user instead, for both JSON serializers the API clients use.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class UiUserRelationshipNullTest
    {
        private const string kScheduleWithHiddenOwner = "{\"report_schedule_id\": 1, \"report_schedule_owner_user\": null}";
        private const string kReportWithHiddenOwner = "{\"report_id\": 1, \"uiuser\": null}";
        private const string kCommentWithHiddenCreator = "{\"comment_text\": \"text\", \"creator\": null}";
        private const string kScheduleWithVisibleOwner = "{\"report_schedule_owner_user\": {\"uiuser_id\": 5, \"uiuser_username\": \"owner\"}}";

        [Test]
        public void NewtonsoftKeepsEmptyUserForHiddenRelationships()
        {
            ReportSchedule? schedule = Newtonsoft.Json.JsonConvert.DeserializeObject<ReportSchedule>(kScheduleWithHiddenOwner);
            ReportFile? report = Newtonsoft.Json.JsonConvert.DeserializeObject<ReportFile>(kReportWithHiddenOwner);
            WfCommentBase? comment = Newtonsoft.Json.JsonConvert.DeserializeObject<WfCommentBase>(kCommentWithHiddenCreator);

            AssertEmptyUsers(schedule, report, comment);
        }

        [Test]
        public void SystemTextJsonKeepsEmptyUserForHiddenRelationships()
        {
            ReportSchedule? schedule = System.Text.Json.JsonSerializer.Deserialize<ReportSchedule>(kScheduleWithHiddenOwner);
            ReportFile? report = System.Text.Json.JsonSerializer.Deserialize<ReportFile>(kReportWithHiddenOwner);
            WfCommentBase? comment = System.Text.Json.JsonSerializer.Deserialize<WfCommentBase>(kCommentWithHiddenCreator);

            AssertEmptyUsers(schedule, report, comment);
        }

        [Test]
        public void VisibleRelationshipIsKept()
        {
            ReportSchedule? schedule = Newtonsoft.Json.JsonConvert.DeserializeObject<ReportSchedule>(kScheduleWithVisibleOwner);

            Assert.That(schedule?.ScheduleOwningUser.DbId, Is.EqualTo(5));
            Assert.That(schedule?.ScheduleOwningUser.Name, Is.EqualTo("owner"));
        }

        private static void AssertEmptyUsers(ReportSchedule? schedule, ReportFile? report, WfCommentBase? comment)
        {
            Assert.Multiple(() =>
            {
                Assert.That(schedule?.ScheduleOwningUser, Is.Not.Null);
                Assert.That(schedule?.ScheduleOwningUser.DbId, Is.EqualTo(0));
                Assert.That(report?.ReportOwningUser, Is.Not.Null);
                Assert.That(report?.ReportOwningUser.Name, Is.EqualTo(""));
                Assert.That(comment?.Creator, Is.Not.Null);
                Assert.That(comment?.Creator.DbId, Is.EqualTo(0));
            });
        }
    }
}

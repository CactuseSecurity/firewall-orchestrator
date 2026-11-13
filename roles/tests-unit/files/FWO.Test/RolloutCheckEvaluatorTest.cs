using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class RolloutCheckEvaluatorTest
    {
        private static readonly DateTime kStateEntry = new(2026, 11, 11, 10, 0, 0);
        private static readonly DateTime kImplementationDone = new(2026, 11, 11, 9, 30, 0);
        private static readonly List<int> kManagementsOneAndTwo = [1, 2];
        private static readonly List<int> kManagementsOneTwoFiveSeven = [1, 2, 5, 7];
        private static readonly List<int> kManagementOne = [1];
        private static readonly List<int> kManagementTwo = [2];
        private static readonly List<int> kManagementsFiveAndSeven = [5, 7];
        private static readonly List<int> kNoManagementIds = [];
        private static readonly List<ImportControl> kNoImports = [];
        private static readonly List<Management> kNoManagements = [];

        private static WfTicket CreateTicket(params WfReqTask[] reqTasks)
        {
            WfTicket ticket = new() { Id = 42, StateId = 249 };
            ticket.Tasks.AddRange(reqTasks);
            return ticket;
        }

        private static WfReqTask CreateReqTask(int? managementId, DateTime? stop, params WfImplTask[] implTasks)
        {
            WfReqTask reqTask = new() { ManagementId = managementId, Stop = stop };
            reqTask.ImplementationTasks.AddRange(implTasks);
            return reqTask;
        }

        private static WfImplTask CreateImplTask(int? managementId, DateTime? stop)
        {
            return new WfImplTask { ManagementId = managementId, Stop = stop };
        }

        private static ImportControl CreateImport(int managementId, DateTime startTime)
        {
            return new ImportControl { MgmId = managementId, StartTime = startTime, StopTime = startTime.AddMinutes(1), SuccessfulImport = true };
        }

        [Test]
        public void GetInvolvedManagementIds_UsesImplementationTasksOrRequestTask()
        {
            WfTicket ticket = CreateTicket(
                CreateReqTask(9, null, CreateImplTask(2, null), CreateImplTask(1, null), CreateImplTask(2, null)),
                CreateReqTask(5, null),
                CreateReqTask(null, null),
                CreateReqTask(7, null, CreateImplTask(null, null)));

            List<int> managementIds = RolloutCheckEvaluator.GetInvolvedManagementIds(ticket);

            Assert.That(managementIds, Is.EqualTo(kManagementsOneTwoFiveSeven));
        }

        [Test]
        public void GetInvolvedManagementIds_IgnoresMissingManagements()
        {
            WfTicket ticket = CreateTicket(CreateReqTask(null, null, CreateImplTask(null, null)), CreateReqTask(0, null));

            Assert.That(RolloutCheckEvaluator.GetInvolvedManagementIds(ticket), Is.Empty);
        }

        [Test]
        public void GetReviewStart_ReturnsLatestImplementationStop()
        {
            WfTicket ticket = CreateTicket(
                CreateReqTask(1, kImplementationDone.AddHours(-5), CreateImplTask(1, kImplementationDone.AddMinutes(-10)), CreateImplTask(2, kImplementationDone)),
                CreateReqTask(3, kImplementationDone.AddMinutes(-20)));

            Assert.That(RolloutCheckEvaluator.GetReviewStart(ticket, kStateEntry), Is.EqualTo(kImplementationDone));
        }

        [Test]
        public void GetReviewStart_UsesStateEntryWhenAStopDateIsMissing()
        {
            WfTicket ticket = CreateTicket(CreateReqTask(1, null, CreateImplTask(1, kImplementationDone), CreateImplTask(2, null)));

            Assert.That(RolloutCheckEvaluator.GetReviewStart(ticket, kStateEntry), Is.EqualTo(kStateEntry));
        }

        [Test]
        public void GetReviewStart_UsesStateEntryForTicketWithoutTasks()
        {
            Assert.That(RolloutCheckEvaluator.GetReviewStart(CreateTicket(), kStateEntry), Is.EqualTo(kStateEntry));
        }

        [Test]
        public void GetReviewStart_WithoutStopDateAndStateEntry_IsUnknown()
        {
            WfTicket ticket = CreateTicket(CreateReqTask(1, null));

            Assert.That(RolloutCheckEvaluator.GetReviewStart(ticket, null), Is.Null);
        }

        [Test]
        public void ImportData_ResolvesSuperManagementAndNames()
        {
            List<Management> managements =
            [
                new() { Id = 1, Name = "MDS" },
                new() { Id = 2, Name = "Domain A", SuperManagerId = 1 },
                new() { Id = 3, Name = "" }
            ];
            List<ImportControl> imports = [CreateImport(1, kImplementationDone.AddMinutes(5))];
            RolloutCheckImportData importData = new(managements, imports);

            Assert.Multiple(() =>
            {
                Assert.That(importData.GetImportManagementId(2), Is.EqualTo(1));
                Assert.That(importData.GetImportManagementId(1), Is.EqualTo(1));
                Assert.That(importData.GetImportManagementId(99), Is.EqualTo(99));
                Assert.That(importData.IsImportedAfter(2, kImplementationDone), Is.True);
                Assert.That(importData.IsImportedAfter(3, kImplementationDone), Is.False);
                Assert.That(importData.GetManagementName(2), Is.EqualTo("Domain A"));
                Assert.That(importData.GetManagementName(3), Is.EqualTo("3"));
                Assert.That(importData.GetManagementName(99), Is.EqualTo("99"));
            });
        }

        [Test]
        public void ImportData_IgnoresImportsWithoutManagementOrStartAndUsesLatestStart()
        {
            List<ImportControl> imports =
            [
                new() { MgmId = null, StartTime = kImplementationDone.AddHours(1) },
                new() { MgmId = 4, StartTime = null },
                CreateImport(5, kImplementationDone.AddMinutes(-1)),
                CreateImport(5, kImplementationDone.AddMinutes(1))
            ];
            RolloutCheckImportData importData = new(kNoManagements, imports);

            Assert.That(importData.IsImportedAfter(4, kImplementationDone), Is.False);
            Assert.That(importData.IsImportedAfter(5, kImplementationDone), Is.True);
        }

        [Test]
        public void ImportData_ImportStartedBeforeOrAtReviewStartDoesNotCount()
        {
            List<ImportControl> imports = [CreateImport(1, kImplementationDone), CreateImport(2, kImplementationDone.AddSeconds(-1))];
            RolloutCheckImportData importData = new(kNoManagements, imports);

            Assert.That(importData.IsImportedAfter(1, kImplementationDone), Is.False);
            Assert.That(importData.IsImportedAfter(2, kImplementationDone), Is.False);
        }

        [Test]
        public void Evaluate_AllManagementsImported_Passes()
        {
            WfTicket ticket = CreateTicket(CreateReqTask(1, null, CreateImplTask(1, kImplementationDone), CreateImplTask(2, kImplementationDone)));
            List<ImportControl> imports = [CreateImport(1, kImplementationDone.AddMinutes(1)), CreateImport(2, kImplementationDone.AddMinutes(2))];

            RolloutCheckResult result = RolloutCheckEvaluator.Evaluate(ticket, new RolloutCheckImportData(kNoManagements, imports), kStateEntry);

            Assert.Multiple(() =>
            {
                Assert.That(result.Passed, Is.True);
                Assert.That(result.ReviewStart, Is.EqualTo(kImplementationDone));
                Assert.That(result.InvolvedManagementIds, Is.EqualTo(kManagementsOneAndTwo));
                Assert.That(result.MissingManagementIds, Is.Empty);
            });
        }

        [Test]
        public void Evaluate_MissingImport_DoesNotPassAndListsManagement()
        {
            WfTicket ticket = CreateTicket(CreateReqTask(1, null, CreateImplTask(1, kImplementationDone), CreateImplTask(2, kImplementationDone)));
            List<ImportControl> imports = [CreateImport(1, kImplementationDone.AddMinutes(1)), CreateImport(2, kImplementationDone.AddMinutes(-1))];

            RolloutCheckResult result = RolloutCheckEvaluator.Evaluate(ticket, new RolloutCheckImportData(kNoManagements, imports), kStateEntry);

            Assert.That(result.Passed, Is.False);
            Assert.That(result.MissingManagementIds, Is.EqualTo(kManagementTwo));
        }

        [Test]
        public void Evaluate_NoImports_ListsAllManagementsAsMissing()
        {
            WfTicket ticket = CreateTicket(CreateReqTask(5, kImplementationDone), CreateReqTask(7, kImplementationDone));

            RolloutCheckResult result = RolloutCheckEvaluator.Evaluate(ticket, new RolloutCheckImportData(kNoManagements, kNoImports), kStateEntry);

            Assert.That(result.Passed, Is.False);
            Assert.That(result.MissingManagementIds, Is.EqualTo(kManagementsFiveAndSeven));
        }

        [Test]
        public void Evaluate_UnknownReviewStart_DoesNotPassEvenWithImports()
        {
            WfTicket ticket = CreateTicket(CreateReqTask(1, null));
            List<ImportControl> imports = [CreateImport(1, kImplementationDone.AddMinutes(1))];

            RolloutCheckResult result = RolloutCheckEvaluator.Evaluate(ticket, new RolloutCheckImportData(kNoManagements, imports), null);

            Assert.Multiple(() =>
            {
                Assert.That(result.Passed, Is.False);
                Assert.That(result.ReviewStart, Is.Null);
                Assert.That(result.MissingManagementIds, Is.EqualTo(kManagementOne));
            });
        }

        [Test]
        public void Evaluate_TicketWithoutManagements_Passes()
        {
            WfTicket ticket = CreateTicket(CreateReqTask(null, null));

            RolloutCheckResult result = RolloutCheckEvaluator.Evaluate(ticket, new RolloutCheckImportData(kNoManagements, kNoImports), null);

            Assert.That(result.Passed, Is.True);
            Assert.That(result.InvolvedManagementIds, Is.EqualTo(kNoManagementIds));
        }

        [Test]
        public void Evaluate_SubManagementCoveredBySuperManagementImport_Passes()
        {
            WfTicket ticket = CreateTicket(CreateReqTask(2, null, CreateImplTask(2, kImplementationDone)));
            List<Management> managements = [new() { Id = 2, Name = "Domain A", SuperManagerId = 1 }];
            List<ImportControl> imports = [CreateImport(1, kImplementationDone.AddMinutes(3))];

            RolloutCheckResult result = RolloutCheckEvaluator.Evaluate(ticket, new RolloutCheckImportData(managements, imports), kStateEntry);

            Assert.That(result.Passed, Is.True);
        }

        [Test]
        public void Comment_Passed_ListsManagementsAndReviewStart()
        {
            RolloutCheckResult result = new(true, kImplementationDone, kManagementsOneAndTwo, kNoManagementIds);

            string comment = RolloutCheckComment.Build(new RolloutCheckTextConfig(), result, CreateNamedImportData());

            Assert.That(comment, Is.EqualTo("ok 2026-11-11 09:30:00: MDS, 2"));
        }

        [Test]
        public void Comment_PassedWithoutManagements_UsesOwnText()
        {
            RolloutCheckResult result = new(true, null, kNoManagementIds, kNoManagementIds);

            string comment = RolloutCheckComment.Build(new RolloutCheckTextConfig(), result, CreateNamedImportData());

            Assert.That(comment, Is.EqualTo("ok without managements"));
        }

        [Test]
        public void Comment_Missing_ListsMissingManagements()
        {
            RolloutCheckResult result = new(false, kImplementationDone, kManagementsOneAndTwo, kManagementTwo);

            string comment = RolloutCheckComment.Build(new RolloutCheckTextConfig(), result, CreateNamedImportData());

            Assert.That(comment, Is.EqualTo("missing 2026-11-11 09:30:00: 2"));
        }

        [Test]
        public void Comment_MissingWithUnknownReviewStart_ShowsPlaceholder()
        {
            RolloutCheckResult result = new(false, null, kManagementTwo, kManagementTwo);

            string comment = RolloutCheckComment.Build(new RolloutCheckTextConfig(), result, CreateNamedImportData());

            Assert.That(comment, Is.EqualTo("missing -: 2"));
        }

        private static RolloutCheckImportData CreateNamedImportData()
        {
            List<Management> managements = [new() { Id = 1, Name = "MDS" }];
            return new RolloutCheckImportData(managements, kNoImports);
        }

        private sealed class RolloutCheckTextConfig : SimulatedUserConfig
        {
            public override string GetText(string key)
            {
                return key switch
                {
                    RolloutCheckComment.kPassedTextKey => "ok {0}: {1}",
                    RolloutCheckComment.kPassedWithoutManagementsTextKey => "ok without managements",
                    RolloutCheckComment.kMissingTextKey => "missing {0}: {1}",
                    _ => base.GetText(key)
                };
            }
        }
    }
}

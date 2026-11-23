using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using FWO.Ui.Services;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class UiApprovalTaskComplianceServiceTest
    {
        private static readonly List<string> kReasons = ["forbidden service"];
        private static readonly List<string> kFirstAndSecondReason = ["first", "second"];

        [Test]
        public async Task UpdateAsync_ChecksAgainOnlyWhenCheckedContentChanges()
        {
            WfReqTask task = ApprovalComplianceTestHelper.CheckableAccessTask(41, "access");
            WfHandler handler = new() { ActTicket = new WfTicket { Id = 401, Tasks = [task] } };
            FixedResultPolicyChecker checker = new(new Dictionary<long, RequestTaskComplianceResult> { [41] = new(41, false, kReasons) });
            await ApprovalComplianceTestHelper.AttachPolicyCheck(handler, new SimulatedApiConnection(), checker);
            ApprovalTaskComplianceService service = new();

            bool first = await service.UpdateAsync(handler);
            task.Title = "renamed";
            bool afterTitleChange = await service.UpdateAsync(handler);
            task.Elements.Single(element => element.Field == ElemFieldType.service.ToString()).Port = 8443;
            bool afterServiceChange = await service.UpdateAsync(handler);

            Assert.Multiple(() =>
            {
                Assert.That(first, Is.True);
                Assert.That(afterTitleChange, Is.False);
                Assert.That(afterServiceChange, Is.True);
                Assert.That(checker.CheckCount, Is.EqualTo(2));
                Assert.That(service.IsConfigured, Is.True);
                Assert.That(service.IsLoading, Is.False);
                Assert.That(service.ComplianceOf(task)!.Reasons, Is.EqualTo(kReasons));
            });
        }

        [Test]
        public async Task UpdateAsync_AfterResetReusesTheLastCheckOfUnchangedContent()
        {
            WfReqTask task = ApprovalComplianceTestHelper.CheckableAccessTask(42, "access");
            WfHandler handler = new() { ActTicket = new WfTicket { Id = 402, Tasks = [task] } };
            FixedResultPolicyChecker checker = new(new Dictionary<long, RequestTaskComplianceResult> { [42] = new(42, false, kReasons) });
            await ApprovalComplianceTestHelper.AttachPolicyCheck(handler, new SimulatedApiConnection(), checker);
            ApprovalTaskComplianceService service = new();

            await service.UpdateAsync(handler);
            service.Reset();
            bool changed = await service.UpdateAsync(handler);

            Assert.Multiple(() =>
            {
                Assert.That(changed, Is.True);
                Assert.That(checker.CheckCount, Is.EqualTo(1));
                Assert.That(service.ComplianceOf(task)!.Reasons, Is.EqualTo(kReasons));
            });
        }

        [Test]
        public async Task UpdateAsync_ShowsStoredPositiveResultWithoutCheck()
        {
            WfReqTask task = ApprovalComplianceTestHelper.CheckableAccessTask(45, "access");
            task.SetAddInfo(AdditionalInfoKeys.ComplianceOk, "true");
            WfHandler handler = new() { ActTicket = new WfTicket { Id = 405, Tasks = [task] } };
            FixedResultPolicyChecker checker = new(new Dictionary<long, RequestTaskComplianceResult> { [45] = new(45, false, kReasons) });
            await ApprovalComplianceTestHelper.AttachPolicyCheck(handler, new SimulatedApiConnection(), checker);
            ApprovalTaskComplianceService service = new();

            await service.UpdateAsync(handler);

            Assert.Multiple(() =>
            {
                Assert.That(checker.CheckCount, Is.Zero);
                Assert.That(service.ComplianceOf(task)!.IsCompliant, Is.True);
            });
        }

        [Test]
        public async Task RecheckAsync_ChecksStoredPositiveResultAgainAndStoresTheChange()
        {
            WfReqTask task = ApprovalComplianceTestHelper.CheckableAccessTask(46, "access");
            task.TicketId = 406;
            task.SetAddInfo(AdditionalInfoKeys.ComplianceOk, "true");
            WfHandler handler = new() { ActTicket = new WfTicket { Id = 406, Tasks = [task] } };
            FixedResultPolicyChecker checker = new(new Dictionary<long, RequestTaskComplianceResult> { [46] = new(46, false, kReasons) });
            await ApprovalComplianceTestHelper.AttachPolicyCheck(handler, new SimulatedApiConnection(), checker);
            ApprovalTaskComplianceService service = new();
            await service.UpdateAsync(handler);

            await service.RecheckAsync(handler);

            Assert.Multiple(() =>
            {
                Assert.That(checker.CheckCount, Is.EqualTo(1));
                Assert.That(service.ComplianceOf(task)!.Reasons, Is.EqualTo(kReasons));
                Assert.That(task.GetAddInfoValue(AdditionalInfoKeys.ComplianceOk), Is.EqualTo("false"));
            });
        }

        [Test]
        public async Task RecheckAsync_DoesNotStoreForReadOnlyUsers()
        {
            WfReqTask task = ApprovalComplianceTestHelper.CheckableAccessTask(47, "access");
            task.TicketId = 407;
            task.SetAddInfo(AdditionalInfoKeys.ComplianceOk, "true");
            WfHandler handler = new() { ActTicket = new WfTicket { Id = 407, Tasks = [task] }, ReadOnlyMode = true };
            FixedResultPolicyChecker checker = new(new Dictionary<long, RequestTaskComplianceResult> { [47] = new(47, false, kReasons) });
            await ApprovalComplianceTestHelper.AttachPolicyCheck(handler, new SimulatedApiConnection(), checker);
            ApprovalTaskComplianceService service = new();
            await service.UpdateAsync(handler);

            await service.RecheckAsync(handler);

            Assert.Multiple(() =>
            {
                Assert.That(service.ComplianceOf(task)!.IsCompliant, Is.False);
                Assert.That(task.GetAddInfoValue(AdditionalInfoKeys.ComplianceOk), Is.EqualTo("true"));
            });
        }

        [Test]
        public async Task RecheckAsync_WithoutConditionalPolicyCheckDoesNothing()
        {
            WfHandler handler = new() { ActTicket = new WfTicket { Id = 408, Tasks = [ApprovalComplianceTestHelper.CheckableAccessTask(48, "access")] } };
            ApprovalTaskComplianceService service = new();

            await service.RecheckAsync(handler);

            Assert.Multiple(() =>
            {
                Assert.That(service.IsLoading, Is.False);
                Assert.That(service.Results, Is.Null);
            });
        }

        [Test]
        public async Task UpdateAsync_NotConfiguredWithoutConditionalPolicyCheck()
        {
            WfHandler handler = new() { ActTicket = new WfTicket { Id = 403, Tasks = [ApprovalComplianceTestHelper.CheckableAccessTask(43, "access")] } };
            ApprovalTaskComplianceService service = new();

            await service.UpdateAsync(handler);

            Assert.Multiple(() =>
            {
                Assert.That(service.IsConfigured, Is.False);
                Assert.That(service.Results, Is.Null);
            });
        }

        [Test]
        public void Compliance_StatusTextAndTooltip()
        {
            SimulatedUserConfig userConfig = new();
            ApprovalTaskCompliance notAllowed = new(false, kFirstAndSecondReason);
            ApprovalTaskCompliance ok = new(true, ApprovalComplianceTestHelper.NoReasons);

            Assert.Multiple(() =>
            {
                Assert.That(notAllowed.StatusText(userConfig), Is.EqualTo("approval_compliance_not_allowed"));
                Assert.That(ok.StatusText(userConfig), Is.EqualTo("approval_compliance_ok"));
                Assert.That(notAllowed.Tooltip, Is.EqualTo("first\nsecond"));
            });
        }
    }
}

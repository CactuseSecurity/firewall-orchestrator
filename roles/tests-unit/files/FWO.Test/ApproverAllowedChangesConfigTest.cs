using FWO.Data.Workflow;
using NUnit.Framework;
using System.Text.Json;

namespace FWO.Test
{
    [TestFixture]
    public class ApproverAllowedChangesConfigTest
    {
        [Test]
        public void Parse_ReturnsDefaults_WhenConfigIsEmpty()
        {
            ApproverAllowedChangesConfig config = ApproverAllowedChangesConfig.Parse("");

            Assert.That(config.TicketFields, Is.Empty);
            Assert.That(config.TaskTypeFields.ContainsKey(WfTaskType.access.ToString()), Is.True);
            Assert.That(config.TaskTypeFields[WfTaskType.access.ToString()], Is.Empty);
        }

        [Test]
        public void SetFieldAndSerialize_RoundTripsSelections()
        {
            ApproverAllowedChangesConfig config = new();
            config.SetTicketField(WorkflowEditableFieldKeys.Title, true);
            config.SetTaskField(WfTaskType.access, WorkflowEditableFieldKeys.Services, true);

            string serialized = config.ToConfigValue();
            ApproverAllowedChangesConfig parsed = ApproverAllowedChangesConfig.Parse(serialized);

            Assert.That(parsed.IsTicketFieldAllowed(WorkflowEditableFieldKeys.Title), Is.True);
            Assert.That(parsed.IsTaskFieldAllowed(WfTaskType.access, WorkflowEditableFieldKeys.Services), Is.True);
        }

        [Test]
        public void Parse_AddsMissingTaskTypeEntries()
        {
            string serialized = JsonSerializer.Serialize(new ApproverAllowedChangesConfig()
            {
                TicketFields = [WorkflowEditableFieldKeys.Reason],
                TaskTypeFields = new Dictionary<string, List<string>>
                {
                    [WfTaskType.generic.ToString()] = [WorkflowEditableFieldKeys.FreeText]
                }
            });

            ApproverAllowedChangesConfig parsed = ApproverAllowedChangesConfig.Parse(serialized);

            Assert.That(parsed.IsTicketFieldAllowed(WorkflowEditableFieldKeys.Reason), Is.True);
            Assert.That(parsed.IsTaskFieldAllowed(WfTaskType.generic, WorkflowEditableFieldKeys.FreeText), Is.True);
            Assert.That(parsed.TaskTypeFields.ContainsKey(WfTaskType.new_interface.ToString()), Is.True);
        }

        [Test]
        public void AccessHelper_OnlyAllowsConfiguredTicketFieldsInApprovalPhase()
        {
            ApproverAllowedChangesConfig config = new();
            config.SetTicketField(WorkflowEditableFieldKeys.Title, true);

            Assert.That(ApproverAllowedChangesAccess.CanEditTicketField(config, true, false, false, WorkflowEditableFieldKeys.Title), Is.True);
            Assert.That(ApproverAllowedChangesAccess.CanEditTicketField(config, true, false, false, WorkflowEditableFieldKeys.Reason), Is.False);
            Assert.That(ApproverAllowedChangesAccess.CanEditTicketField(config, false, false, false, WorkflowEditableFieldKeys.Title), Is.False);
            Assert.That(ApproverAllowedChangesAccess.CanEditTicketField(config, true, false, true, WorkflowEditableFieldKeys.Title), Is.False);
        }

        [Test]
        public void AccessHelper_OnlyAllowsConfiguredTaskFieldsWhileApproving()
        {
            ApproverAllowedChangesConfig config = new();
            config.SetTaskField(WfTaskType.access, WorkflowEditableFieldKeys.Services, true);
            ApproverAllowedChangesAccess.TaskFieldEditContext approvalContext = new()
            {
                IsApprovalPhase = true,
                ApproveReqTaskMode = true,
                TaskType = WfTaskType.access
            };
            ApproverAllowedChangesAccess.TaskFieldEditContext noApproveContext = new()
            {
                IsApprovalPhase = true,
                TaskType = WfTaskType.access
            };
            ApproverAllowedChangesAccess.TaskFieldEditContext planningContext = new()
            {
                ApproveReqTaskMode = true,
                TaskType = WfTaskType.access
            };

            Assert.That(ApproverAllowedChangesAccess.CanEditTaskField(config, approvalContext, WorkflowEditableFieldKeys.Services), Is.True);
            Assert.That(ApproverAllowedChangesAccess.CanEditTaskField(config, noApproveContext, WorkflowEditableFieldKeys.Services), Is.False);
            Assert.That(ApproverAllowedChangesAccess.CanEditTaskField(config, approvalContext, WorkflowEditableFieldKeys.Source), Is.False);
            Assert.That(ApproverAllowedChangesAccess.CanEditTaskField(config, planningContext, WorkflowEditableFieldKeys.Services), Is.False);
        }

        [Test]
        public void AccessHelper_BlocksCopiedTaskFields_WhenImplementationTasksAlreadyExist()
        {
            ApproverAllowedChangesConfig config = new();
            config.SetTaskField(WfTaskType.access, WorkflowEditableFieldKeys.Services, true);
            config.SetTaskField(WfTaskType.access, WorkflowEditableFieldKeys.Reason, true);
            ApproverAllowedChangesAccess.TaskFieldEditContext context = new()
            {
                IsApprovalPhase = true,
                ApproveReqTaskMode = true,
                TaskType = WfTaskType.access,
                HasImplementationTasks = true
            };

            Assert.That(ApproverAllowedChangesAccess.CanEditTaskField(config, context, WorkflowEditableFieldKeys.Services), Is.False);
            Assert.That(ApproverAllowedChangesAccess.CanEditTaskField(config, context, WorkflowEditableFieldKeys.Reason), Is.True);
        }
    }
}

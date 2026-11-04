using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Data.Workflow;
using FWO.Ui.Pages.Request;
using FWO.Ui.Pages.Settings;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using FWO.Services.Workflow;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test;

[TestFixture]
internal class UiSharedEditorTest : UiSharedEditorTestBase
{
    [Test]
    public async Task OwnerEditor_SavesSelectedOwner()
    {
        await using BunitContext context = CreateContext();
        FwoOwner? saved = null;
        FwoOwner owner = new() { Id = 7, Name = "Owner" };

        IRenderedComponent<OwnerEditor> cut = context.Render<OwnerEditor>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.SelectedOwner, owner)
            .Add(p => p.Owners, [owner])
            .Add(p => p.Saved, value => saved = value));

        cut.Find("button.btn-primary").Click();

        Assert.That(saved?.Id, Is.EqualTo(7));
    }

    [Test]
    public async Task OwnerEditor_OwnerChangedUpdatesDraft()
    {
        OwnerEditor component = new();
        FwoOwner owner = new() { Id = 8, Name = "Changed" };

        await UiRequestWorkflowTest.InvokePrivateTask(component, "OwnerChanged", owner);

        Assert.That(UiRequestWorkflowTest.GetMember<FwoOwner>(component, "selectedOwner"), Is.SameAs(owner));
    }

    [Test]
    public async Task RuleActionEditor_SavesSelectedAction()
    {
        await using BunitContext context = CreateContext();
        RuleAction? saved = null;
        RuleAction action = new() { Id = 3, Name = "allow" };

        IRenderedComponent<RuleActionEditor> cut = context.Render<RuleActionEditor>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.SelectedRuleAction, action)
            .Add(p => p.RuleActions, [action])
            .Add(p => p.Saved, value => saved = value));

        cut.Find("button.btn-primary").Click();

        Assert.That(saved?.Id, Is.EqualTo(3));
    }

    [Test]
    public async Task RuleActionEditor_RuleActionChangedUpdatesDraft()
    {
        RuleActionEditor component = new();
        RuleAction action = new() { Id = 9, Name = "deny" };

        await UiRequestWorkflowTest.InvokePrivateTask(component, "RuleActionChanged", action);

        Assert.That(UiRequestWorkflowTest.GetMember<RuleAction?>(component, "selectedRuleAction"), Is.SameAs(action));
    }

    [Test]
    public async Task TrackingEditor_SavesSelectedTracking()
    {
        await using BunitContext context = CreateContext();
        Tracking? saved = null;
        Tracking tracking = new() { Id = 4, Name = "log" };

        IRenderedComponent<TrackingEditor> cut = context.Render<TrackingEditor>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.SelectedTracking, tracking)
            .Add(p => p.Trackings, [tracking])
            .Add(p => p.Saved, value => saved = value));

        cut.Find("button.btn-primary").Click();

        Assert.That(saved?.Id, Is.EqualTo(4));
    }

    [Test]
    public async Task TrackingEditor_TrackingChangedUpdatesDraft()
    {
        TrackingEditor component = new();
        Tracking tracking = new() { Id = 10, Name = "changed-tracking" };

        await UiRequestWorkflowTest.InvokePrivateTask(component, "TrackingChanged", tracking);

        Assert.That(UiRequestWorkflowTest.GetMember<Tracking?>(component, "selectedTracking"), Is.SameAs(tracking));
    }

    [Test]
    public async Task ManagementEditor_SavesSelectedManagement()
    {
        await using BunitContext context = CreateContext();
        Management? saved = null;
        Management management = new() { Id = 5, Name = "management" };

        IRenderedComponent<ManagementEditor> cut = context.Render<ManagementEditor>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.SelectedManagement, management)
            .Add(p => p.Managements, [management])
            .Add(p => p.Saved, value => saved = value));

        cut.Find("button.btn-primary").Click();

        Assert.That(saved?.Id, Is.EqualTo(5));
    }

    [Test]
    public async Task ManagementEditor_ManagementChangedUpdatesDraft()
    {
        ManagementEditor component = new();
        Management management = new() { Id = 11, Name = "changed-management" };

        await UiRequestWorkflowTest.InvokePrivateTask(component, "ManagementChanged", management);

        Assert.That(UiRequestWorkflowTest.GetMember<Management?>(component, "selectedManagement"), Is.SameAs(management));
    }

    [Test]
    public async Task TextEditor_SavesChangedMultilineText()
    {
        await using BunitContext context = CreateContext();
        string? saved = null;

        IRenderedComponent<TextEditor> cut = context.Render<TextEditor>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.Text, "before")
            .Add(p => p.TextKey, "reason")
            .Add(p => p.Multiline, true)
            .Add(p => p.Saved, value => saved = value));

        cut.Find("textarea").Change("after");
        cut.Find("button.btn-primary").Click();

        Assert.That(saved, Is.EqualTo("after"));
    }

    [Test]
    public async Task TimeObjectOriginSuffixes_SavesConfiguredValues()
    {
        await using BunitContext context = CreateContext();
        string? saved = null;

        IRenderedComponent<TimeObjectOriginSuffixes> cut = context.Render<TimeObjectOriginSuffixes>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.ConfigValue, "{\"requester\":\"_old\",\"approver\":\"\"}")
            .Add(p => p.ConfigValueChanged, value => saved = value));

        cut.FindAll("input")[0].Change("_user");
        cut.FindAll("input")[1].Change("_approver");
        cut.Find("button.btn-primary").Click();

        Assert.That(TimeObjectNamingHelper.ParseSuffixes(saved), Is.EqualTo(new Dictionary<string, string>
        {
            ["requester"] = "_user",
            ["approver"] = "_approver"
        }));
    }

    [Test]
    public async Task TimeObjectTaskEditor_SavesNamedDraft()
    {
        await using BunitContext context = CreateContext();
        TargetDatesEditorModel? saved = null;
        TargetDatesEditorModel model = new()
        {
            FlowTimeObjectName = "maintenance_approval",
            BeginDate = new DateTime(2026, 11, 1, 8, 0, 0),
            EndDate = new DateTime(2026, 11, 30, 18, 0, 0)
        };

        IRenderedComponent<TimeObjectTaskEditor> cut = context.Render<TimeObjectTaskEditor>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.Model, model)
            .Add(p => p.Saved, value => saved = value));

        cut.Find("button.btn-primary").Click();

        Assert.That(saved?.FlowTimeObjectName, Is.EqualTo("maintenance_approval"));
        Assert.That(saved?.BeginDate, Is.EqualTo(model.BeginDate));
    }

    [Test]
    public async Task TimeObjectTaskEditor_UsesSeparateRowsAndRespectsDateVisibility()
    {
        await using BunitContext context = CreateContext();
        TargetDatesEditorModel model = new()
        {
            FlowTimeObjectName = "maintenance",
            BeginDate = new DateTime(2026, 11, 1, 8, 0, 0),
            EndDate = new DateTime(2026, 11, 30, 18, 0, 0)
        };

        IRenderedComponent<TimeObjectTaskEditor> cut = context.Render<TimeObjectTaskEditor>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.Model, model)
            .Add(p => p.ShowBeginDate, false)
            .Add(p => p.ShowEndDate, true));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Markup, Does.Not.Contain("valid_from"));
            Assert.That(cut.Markup, Does.Contain("valid_to"));
            Assert.That(cut.FindAll("div.form-group.row"), Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task TimeObjectTaskEditor_DateCallbacksUpdateDraftAndValidityControlsSaving()
    {
        TargetDatesEditorModel model = new()
        {
            FlowTimeObjectName = "maintenance",
            BeginDate = new DateTime(2026, 11, 1, 8, 0, 0),
            EndDate = new DateTime(2026, 11, 30, 18, 0, 0)
        };
        await using BunitContext context = CreateContext();
        IRenderedComponent<TimeObjectTaskEditor> cut = context.Render<TimeObjectTaskEditor>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.Model, model));

        MethodInfo validMethod = UiRequestWorkflowTest.GetPrivateMethod(typeof(TimeObjectTaskEditor), "SetTargetDatesValid");
        validMethod.Invoke(cut.Instance, [false]);
        Assert.That(UiRequestWorkflowTest.GetMember<bool>(cut.Instance, "CanSave"), Is.False);

        await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "OnTargetBeginDateChanged",
            new DateTime(2026, 12, 1, 7, 0, 0));
        await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "OnTargetEndDateChanged",
            new DateTime(2026, 12, 31, 19, 0, 0));
        validMethod.Invoke(cut.Instance, [true]);

        TargetDatesEditorModel editingModel = UiRequestWorkflowTest.GetMember<TargetDatesEditorModel>(cut.Instance, "editingModel");
        Assert.Multiple(() =>
        {
            Assert.That(UiRequestWorkflowTest.GetMember<bool>(cut.Instance, "CanSave"), Is.True);
            Assert.That(editingModel.BeginDate, Is.EqualTo(new DateTime(2026, 12, 1, 7, 0, 0)));
            Assert.That(editingModel.EndDate, Is.EqualTo(new DateTime(2026, 12, 31, 19, 0, 0)));
        });
    }

    [Test]
    public async Task TimeObjectTaskDeleteConfirmation_YesConfirmsAndCloses()
    {
        await using BunitContext context = CreateContext();
        bool confirmed = false;
        bool? displayChanged = null;

        IRenderedComponent<TimeObjectTaskDeleteConfirmation> cut = context.Render<TimeObjectTaskDeleteConfirmation>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.Title, "Delete time object")
            .Add(p => p.Message, "Delete this task?")
            .Add(p => p.DisplayChanged, value => displayChanged = value)
            .Add(p => p.Confirmed, () => confirmed = true));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Markup, Does.Contain("Delete this task?"));
            Assert.That(cut.FindAll("button.btn-danger"), Has.Count.EqualTo(1));
            Assert.That(cut.FindAll("button.btn-secondary"), Has.Count.EqualTo(1));
        });

        await cut.InvokeAsync(() => cut.Find("button.btn-danger").Click());

        Assert.Multiple(() =>
        {
            Assert.That(confirmed, Is.True);
            Assert.That(displayChanged, Is.False);
            Assert.That(cut.Instance.Display, Is.False);
        });
    }

    [Test]
    public async Task TimeObjectTaskDeleteConfirmation_NoClosesWithoutConfirming()
    {
        await using BunitContext context = CreateContext();
        bool confirmed = false;
        bool? displayChanged = null;

        IRenderedComponent<TimeObjectTaskDeleteConfirmation> cut = context.Render<TimeObjectTaskDeleteConfirmation>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.Message, "Delete this task?")
            .Add(p => p.DisplayChanged, value => displayChanged = value)
            .Add(p => p.Confirmed, () => confirmed = true));

        await cut.InvokeAsync(() => cut.Find("button.btn-secondary").Click());

        Assert.Multiple(() =>
        {
            Assert.That(confirmed, Is.False);
            Assert.That(displayChanged, Is.False);
            Assert.That(cut.Instance.Display, Is.False);
        });
    }

    [Test]
    public async Task TargetDatesEditor_RendersTimeObjectSelectorAndDateEditor()
    {
        await using BunitContext context = CreateContext();
        TargetDatesEditorModel model = new()
        {
            BeginDate = new DateTime(2026, 8, 1, 8, 0, 0),
            EndDate = new DateTime(2026, 8, 1, 17, 0, 0),
            FlowTimeObjectId = 12,
            FlowTimeObjectName = "Business hours"
        };

        IRenderedComponent<TargetDatesEditor> cut = context.Render<TargetDatesEditor>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.Model, model));

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindComponent<TimeObjectSelector>(), Is.Not.Null);
            Assert.That(cut.FindComponent<DisplayTaskTargetDates>(), Is.Not.Null);
        });
    }

    [Test]
    public async Task TargetDatesEditor_CreateTimeObjectTaskButtonRaisesCallback()
    {
        await using BunitContext context = CreateContext();
        bool requested = false;
        IRenderedComponent<TargetDatesEditor> cut = context.Render<TargetDatesEditor>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.AllowCreateTimeObjectTask, true)
            .Add(p => p.CreateTimeObjectTaskRequested,
                EventCallback.Factory.Create(this, () => requested = true)));

        cut.Find("button.btn-success").Click();

        Assert.That(requested, Is.True);
    }

    [Test]
    public async Task ApprovalTaskDetailsView_RunWithProgressResetsProgressAfterAction()
    {
        ApprovalTaskDetailsView component = new();
        bool wasInProgress = false;
        Func<Task> action = () =>
        {
            wasInProgress = UiRequestWorkflowTest.GetMember<bool>(component, "workInProgress");
            return Task.CompletedTask;
        };

        await UiRequestWorkflowTest.InvokePrivateTask(component, "RunWithProgress", action);

        Assert.Multiple(() =>
        {
            Assert.That(wasInProgress, Is.True);
            Assert.That(UiRequestWorkflowTest.GetMember<bool>(component, "workInProgress"), Is.False);
        });
    }

    [Test]
    public async Task TargetDatesEditor_DateChangeClearsSelectedObject()
    {
        await using BunitContext context = CreateContext();
        TargetDatesEditorModel model = new()
        {
            BeginDate = new DateTime(2026, 8, 1),
            EndDate = new DateTime(2026, 8, 2),
            FlowTimeObjectId = 12,
            FlowTimeObjectName = "Business hours"
        };
        IRenderedComponent<TargetDatesEditor> cut = context.Render<TargetDatesEditor>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.Model, model));

        DateTime changed = new(2026, 8, 3);
        await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "OnTargetBeginDateChanged", changed);
        TargetDatesEditorModel editingModel = UiRequestWorkflowTest.GetMember<TargetDatesEditorModel>(cut.Instance, "editingModel");

        Assert.Multiple(() =>
        {
            Assert.That(editingModel.BeginDate, Is.EqualTo(changed));
            Assert.That(editingModel.FlowTimeObjectId, Is.Null);
            Assert.That(editingModel.FlowTimeObjectName, Is.Null);
            Assert.That(editingModel.TicketElementId, Is.Null);
        });
    }

    [Test]
    public async Task TargetDatesEditor_SelectingTicketObjectSetsElementIdAndDates()
    {
        await using BunitContext context = CreateContext();
        FlowTimeObject ticketObject = new() { Id = -7, Name = "Ticket window" };
        TargetDatesEditorModel model = new()
        {
            TicketTimeObjects = [ticketObject],
            TicketTimeObjectElementIds = new Dictionary<long, long> { [-7] = 77 }
        };
        IRenderedComponent<TargetDatesEditor> cut = context.Render<TargetDatesEditor>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.Model, model));

        await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "OnSelectedTimeObjectIdChanged", -7L);
        await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "OnTimeObjectSelected",
            new FlowTimeObject
            {
                Id = -7,
                StartTime = new DateTime(2026, 8, 1, 8, 0, 0),
                EndTime = new DateTime(2026, 8, 1, 17, 0, 0)
            });
        TargetDatesEditorModel editingModel = UiRequestWorkflowTest.GetMember<TargetDatesEditorModel>(cut.Instance, "editingModel");

        Assert.Multiple(() =>
        {
            Assert.That(editingModel.FlowTimeObjectId, Is.EqualTo(-7));
            Assert.That(editingModel.TicketElementId, Is.EqualTo(77));
            Assert.That(editingModel.BeginDate, Is.EqualTo(new DateTime(2026, 8, 1, 8, 0, 0)));
            Assert.That(editingModel.EndDate, Is.EqualTo(new DateTime(2026, 8, 1, 17, 0, 0)));
        });
    }

    [Test]
    public async Task TargetDatesEditor_SavesSelectedTimeObjectModel()
    {
        await using BunitContext context = CreateContext();
        TargetDatesEditorModel? saved = null;
        IRenderedComponent<TargetDatesEditor> cut = context.Render<TargetDatesEditor>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.Model, new TargetDatesEditorModel { FlowTimeObjectId = 42 })
            .Add(p => p.Saved, value => saved = value));

        await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "Save");

        Assert.That(saved?.FlowTimeObjectId, Is.EqualTo(42));
    }

    [Test]
    public async Task TargetDatesEditor_ValidationAndNameAndEndDateCallbacksUpdateModel()
    {
        await using BunitContext context = CreateContext();
        IRenderedComponent<TargetDatesEditor> cut = context.Render<TargetDatesEditor>(parameters => parameters
            .Add(p => p.Display, true)
            .Add(p => p.Model, new TargetDatesEditorModel
            {
                FlowTimeObjectId = 12,
                FlowTimeObjectName = "old-name",
                EndDate = new DateTime(2026, 8, 1)
            }));

        UiRequestWorkflowTest.GetPrivateMethod(typeof(TargetDatesEditor), "SetTargetDatesValid")
            .Invoke(cut.Instance, [false]);
        await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "OnSelectedTimeObjectNameChanged", "new-name");
        DateTime end = new(2026, 8, 2);
        await UiRequestWorkflowTest.InvokePrivateTask(cut.Instance, "OnTargetEndDateChanged", end);

        TargetDatesEditorModel editingModel = UiRequestWorkflowTest.GetMember<TargetDatesEditorModel>(cut.Instance, "editingModel");
        Assert.Multiple(() =>
        {
            Assert.That(UiRequestWorkflowTest.GetMember<bool>(cut.Instance, "targetDatesValid"), Is.False);
            Assert.That(editingModel.FlowTimeObjectName, Is.Null);
            Assert.That(editingModel.EndDate, Is.EqualTo(end));
            Assert.That(editingModel.FlowTimeObjectId, Is.Null);
        });
    }

}

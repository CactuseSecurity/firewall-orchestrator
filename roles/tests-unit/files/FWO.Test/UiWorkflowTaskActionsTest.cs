using Bunit;
using AngleSharp.Dom;
using FWO.Api.Client;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.RulePositioning.CheckPoint;
using FWO.Data.Workflow;
using FWO.Services;
using FWO.Services.Workflow;
using FWO.Ui.Pages.Request;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test;

[TestFixture]
internal sealed class UiWorkflowTaskActionsTest
{
    private static BunitContext CreateContext()
    {
        BunitContext context = new();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
        context.Services.AddSingleton<DomEventService>();
        context.Services.AddSingleton<ApiConnection>(new SimulatedApiConnection());
        return context;
    }

    [Test]
    public async Task ConfiguredTaskActions_RendersAllowedActionsAndInvokesExternalCallback()
    {
        WfStateAction offeredAction = new() { ButtonText = "Offer" };
        WfStateAction hiddenAction = new() { ButtonText = "Hidden" };
        List<WfStateAction> actions = [offeredAction, hiddenAction];
        WfStateAction? clickedAction = null;

        await using BunitContext context = CreateContext();
        IRenderedComponent<ConfiguredTaskActions> cut = context.Render<ConfiguredTaskActions>(parameters => parameters
            .Add(parameter => parameter.OfferedActions, actions)
            .Add(parameter => parameter.ActionVisibility, action => action == offeredAction)
            .Add(parameter => parameter.ActionRequested, action =>
            {
                clickedAction = action;
                return Task.CompletedTask;
            }));

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("button"), Has.Count.EqualTo(1));
            Assert.That(cut.Markup, Does.Contain("Offer"));
            Assert.That(cut.Markup, Does.Not.Contain("Hidden"));
        });

        await cut.Find("button").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.That(clickedAction, Is.SameAs(offeredAction));
    }

    [Test]
    public async Task WorkflowTaskDetailsFooter_RendersPromotionAndCancelControls()
    {
        WfStateAction offeredAction = new() { ButtonText = "Offer" };
        bool promotionRequested = false;
        bool closeRequested = false;

        await using BunitContext context = CreateContext();
        IRenderedComponent<WorkflowTaskDetailsFooter> cut = context.Render<WorkflowTaskDetailsFooter>(parameters => parameters
            .Add(parameter => parameter.OfferedActions, new List<WfStateAction> { offeredAction })
            .Add(parameter => parameter.CanPromoteTicket, true)
            .Add(parameter => parameter.PromoteTicket, () =>
            {
                promotionRequested = true;
                return Task.CompletedTask;
            })
            .Add(parameter => parameter.ShowCancel, true)
            .Add(parameter => parameter.Close, () =>
            {
                closeRequested = true;
                return Task.CompletedTask;
            }));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("button.btn-dark").TextContent, Does.Contain("Offer"));
            Assert.That(cut.FindAll("button.btn-primary"), Has.Count.EqualTo(1));
            Assert.That(cut.Find("button.workflow-task-details-close").ClassList, Does.Contain("btn-secondary"));
        });

        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        await cut.Find("button.workflow-task-details-close").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Multiple(() =>
        {
            Assert.That(promotionRequested, Is.True);
            Assert.That(closeRequested, Is.True);
        });
    }

    [Test]
    public async Task WorkflowTaskDetailsFooter_RendersOkButtonAndCustomCloseClass()
    {
        await using BunitContext context = CreateContext();
        IRenderedComponent<WorkflowTaskDetailsFooter> cut = context.Render<WorkflowTaskDetailsFooter>(parameters => parameters
            .Add(parameter => parameter.ShowCancel, false)
            .Add(parameter => parameter.CloseButtonClass, "custom-close"));

        IElement closeButton = cut.Find("button.custom-close");

        Assert.Multiple(() =>
        {
            Assert.That(closeButton.ClassList, Does.Contain("btn-primary"));
            Assert.That(closeButton.TextContent, Does.Not.Empty);
            Assert.That(cut.FindAll("button.btn-dark"), Is.Empty);
            Assert.That(cut.FindAll("button.btn-primary"), Has.Count.EqualTo(1));
        });
    }

}

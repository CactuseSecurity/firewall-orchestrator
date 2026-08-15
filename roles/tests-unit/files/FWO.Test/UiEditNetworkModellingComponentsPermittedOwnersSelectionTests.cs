using System.Text.Json;
using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.Middleware.Client;
using FWO.Services.EventMediator;
using FWO.Services.EventMediator.Interfaces;
using FWO.Services.Modelling;
using FWO.Services.Workflow;
using FWO.Ui.Pages.NetworkModelling;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal partial class UiEditNetworkModellingComponentsTest
    {
        [Test]
        public async Task PermittedOwnersSelection_AddOwner_AddsSelectedOwnerAndClearsSelection()
        {
            await using BunitContext context = CreateContext(out _, out _);
            FwoOwner existingOwner = new() { Id = 11, Name = "existing" };
            FwoOwner selectedOwner = new() { Id = 12, Name = "selected" };
            List<FwoOwner> permittedOwners = new List<FwoOwner> { existingOwner };
            List<FwoOwner> ownersToAdd = new List<FwoOwner>();
            List<FwoOwner> ownersToDelete = new List<FwoOwner>();

            IRenderedComponent<PermittedOwnersSelection> component = RenderPermittedOwnersSelection(
                context,
                new List<FwoOwner> { existingOwner, selectedOwner },
                permittedOwners,
                ownersToAdd,
                ownersToDelete,
                readonlyMode: false);

            component.Find("input").TriggerEvent("onfocus", new FocusEventArgs());
            component.Find("input").Change("selected");
            component.WaitForAssertion(() => Assert.That(component.FindAll("button.dropdown-item"), Has.Count.GreaterThanOrEqualTo(2)));
            component.FindAll("button.dropdown-item").Single(button => button.TextContent.Contains(selectedOwner.Name, StringComparison.Ordinal)).Click();
            component.Find("button.btn-success").Click();

            Assert.Multiple(() =>
            {
                Assert.That(ownersToAdd.Select(owner => owner.Id), Is.EquivalentTo(new List<long> { selectedOwner.Id }));
            });
        }

        [Test]
        public async Task PermittedOwnersSelection_Readonly_RendersExistingOwners()
        {
            await using BunitContext context = CreateContext(out _, out _);
            FwoOwner owner = new() { Id = 13, Name = "readonly-owner" };

            IRenderedComponent<PermittedOwnersSelection> component = RenderPermittedOwnersSelection(
                context,
                new List<FwoOwner> { owner },
                new List<FwoOwner> { owner },
                new List<FwoOwner>(),
                new List<FwoOwner>(),
                readonlyMode: true);

            Assert.That(component.Markup, Does.Contain("readonly-owner"));
        }
    }
}

using Bunit;
using FWO.Config.Api;
using FWO.Data;
using FWO.Ui.Pages.Settings;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiEmailRecipientSelectionEditorTest : BunitContext
    {
        [Test]
        public void Render_OrdersResponsiblesOtherAddressesAndEnsureFlag()
        {
            SimulatedUserConfig.DummyTranslate.TryAdd(nameof(EmailRecipientOption.OtherAddresses), "Other addresses");
            SimulatedUserConfig.DummyTranslate.TryAdd("modEnsureAtLeastOneEmailNotification", "Ensure at least one notification");
            SimulatedUserConfig.DummyTranslate.TryAdd("Main responsible", "Main responsible");
            SimulatedUserConfig.DummyTranslate.TryAdd("Supporting responsible", "Supporting responsible");
            SimulatedUserConfig.DummyTranslate.TryAdd("Optional escalation responsible", "Optional escalation responsible");
            Services.AddSingleton<UserConfig>(new SimulatedUserConfig());

            IRenderedComponent<EmailRecipientSelectionEditor> component = Render<EmailRecipientSelectionEditor>(parameters => parameters
                .Add(p => p.Selection, new EmailRecipientSelection
                {
                    OtherAddresses = true,
                    EnsureAtLeastOneNotification = true,
                    OwnerResponsibleTypeIds = [1, 2, 3]
                })
                .Add(p => p.OwnerResponsibleTypes, new List<OwnerResponsibleType>
                {
                    new() { Id = 3, Name = "Optional escalation responsible", Active = true, SortOrder = 30 },
                    new() { Id = 2, Name = "Supporting responsible", Active = true, SortOrder = 20 },
                    new() { Id = 1, Name = "Main responsible", Active = true, SortOrder = 10 }
                })
                .Add(p => p.CheckboxIdPrefix, "test_recipients"));

            List<string> labels = component.FindAll("label.form-check-label")
                .Select(label => label.TextContent)
                .ToList();

            Assert.That(labels, Is.EqualTo(new[]
            {
                "Main responsible",
                "Supporting responsible",
                "Optional escalation responsible",
                "Other addresses",
                "Ensure at least one notification"
            }));
        }

        [Test]
        public void OtherEmailAddressesEditor_AddsSanitizedAddressToList()
        {
            SimulatedUserConfig.DummyTranslate.TryAdd("email_address", "Email address");
            SimulatedUserConfig.DummyTranslate.TryAdd("U0001", "Address shortened");
            Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            List<string> addresses = [];

            IRenderedComponent<OtherEmailAddressesEditor> component = Render<OtherEmailAddressesEditor>(parameters => parameters
                .Add(p => p.AddressList, addresses)
                .Add(p => p.AddressListChanged, EventCallback.Factory.Create<List<string>>(this, value => addresses = value)));

            component.Find("input").Change(" new@example.org ");
            component.Find("button.btn-primary").Click();

            Assert.That(addresses, Is.EqualTo(new[] { "new@example.org" }));
        }

        [Test]
        public void OtherEmailAddressesEditor_UpdatesDirectAddressValue()
        {
            Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            string addressValue = "old@example.org";

            IRenderedComponent<OtherEmailAddressesEditor> component = Render<OtherEmailAddressesEditor>(parameters => parameters
                .Add(p => p.AddressValue, addressValue)
                .Add(p => p.AddressValueChanged, EventCallback.Factory.Create<string>(this, value => addressValue = value))
                .Add(p => p.LabelText, "Recipient"));

            component.Find("input").Change("new@example.org");

            Assert.That(addressValue, Is.EqualTo("new@example.org"));
        }

        [Test]
        public void WorkflowEmailRecipientEditor_ShowsAddressEditorOnlyForOtherAddresses()
        {
            SimulatedUserConfig.DummyTranslate.TryAdd(nameof(EmailRecipientOption.None), "None");
            SimulatedUserConfig.DummyTranslate.TryAdd(nameof(EmailRecipientOption.OtherAddresses), "Other addresses");
            SimulatedUserConfig.DummyTranslate.TryAdd(nameof(EmailRecipientOption.CurrentHandler), "Current handler");
            SimulatedUserConfig.DummyTranslate.TryAdd(nameof(EmailRecipientOption.RecentHandler), "Recent handler");
            SimulatedUserConfig.DummyTranslate.TryAdd(nameof(EmailRecipientOption.AssignedGroup), "Assigned group");
            SimulatedUserConfig.DummyTranslate.TryAdd(nameof(EmailRecipientOption.Requester), "Requester");
            SimulatedUserConfig.DummyTranslate.TryAdd(nameof(EmailRecipientOption.Approver), "Approver");
            SimulatedUserConfig.DummyTranslate.TryAdd(nameof(EmailRecipientOption.LastCommenter), "Last commenter");
            Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            Services.AddSingleton(new DomEventService());

            IRenderedComponent<WorkflowEmailRecipientEditor> component = Render<WorkflowEmailRecipientEditor>(parameters => parameters
                .Add(p => p.LabelText, "Recipient")
                .Add(p => p.Recipient, EmailRecipientOption.OtherAddresses)
                .Add(p => p.AddressValue, "other@example.org"));

            Assert.That(component.FindComponents<OtherEmailAddressesEditor>(), Has.Count.EqualTo(1));

            IRenderedComponent<WorkflowEmailRecipientEditor> requesterComponent = Render<WorkflowEmailRecipientEditor>(parameters => parameters
                .Add(p => p.LabelText, "Recipient")
                .Add(p => p.Recipient, EmailRecipientOption.Requester));

            Assert.That(requesterComponent.FindComponents<OtherEmailAddressesEditor>(), Is.Empty);
        }
    }
}

using Bunit;
using FWO.Config.Api;
using FWO.Data;
using FWO.Ui.Pages.Settings;
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
    }
}

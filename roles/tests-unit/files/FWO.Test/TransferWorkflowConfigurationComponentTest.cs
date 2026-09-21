using FWO.Data.Workflow;
using FWO.Ui.Pages.Settings;
using NUnit.Framework;
using System.Reflection;
using System.Text.Json;
using static FWO.Test.WorkflowConfigurationComponentTestSupport;

namespace FWO.Test
{
    [TestFixture]
    internal class TransferWorkflowConfigurationComponentTest
    {
        [Test]
        public void CanImport_RequiresUniqueNonEmptyNameAndLoadedPackage()
        {
            TransferWorkflowConfiguration component = new();
            SetProperty(component, "ExistingConfigurations", new List<WorkflowConfiguration> { new() { Name = "Existing" } });
            SetField(component, "importPackage", new WorkflowConfigurationTransferPackage());

            SetField(component, "importName", "Existing");
            Assert.That(GetProperty<bool>(component, "CanImport"), Is.False);
            SetField(component, "importName", "New");
            Assert.That(GetProperty<bool>(component, "CanImport"), Is.True);
            SetField(component, "importPackage", null);
            Assert.That(GetProperty<bool>(component, "CanImport"), Is.False);
        }

        [Test]
        public void CanImport_TrimsNameBeforeCheckingExistingConfiguration()
        {
            TransferWorkflowConfiguration component = new();
            SetProperty(component, "ExistingConfigurations", new List<WorkflowConfiguration> { new() { Name = "Shared" } });
            SetField(component, "importPackage", new WorkflowConfigurationTransferPackage());

            SetField(component, "importName", "  shared  ");

            Assert.That(GetProperty<bool>(component, "CanImport"), Is.False);
        }

        [Test]
        public void SuggestedImportName_AddsFirstAvailableSuffix()
        {
            TransferWorkflowConfiguration component = new();
            SetProperty(component, "ExistingConfigurations", new List<WorkflowConfiguration>
            {
                new() { Name = "Shared" },
                new() { Name = "Shared-2" }
            });

            Assert.That(Invoke(component, "SuggestedImportName", "Shared"), Is.EqualTo("Shared-3"));
            Assert.That(Invoke(component, "SuggestedImportName", "Unique"), Is.EqualTo("Unique"));
        }

        [Test]
        public void FileName_ReplacesInvalidCharactersAndUsesPackageSuffix()
        {
            MethodInfo method = typeof(TransferWorkflowConfiguration).GetMethod("FileName", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(typeof(TransferWorkflowConfiguration).FullName, "FileName");

            Assert.That(method.Invoke(null, ["Shared:Config"]), Is.EqualTo("Shared_Config.fwo-workflow.json"));
        }

        [Test]
        public void FileName_ReplacesControlCharacters()
        {
            MethodInfo method = typeof(TransferWorkflowConfiguration).GetMethod("FileName", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(typeof(TransferWorkflowConfiguration).FullName, "FileName");

            Assert.That(method.Invoke(null, ["Shared\u0001Config"]), Is.EqualTo("Shared_Config.fwo-workflow.json"));
        }

        [Test]
        public void OnParametersSet_ResetsTransferStateOnlyWhenOpening()
        {
            TransferWorkflowConfiguration component = new();
            SetProperty(component, "Display", true);
            SetField(component, "importName", "Old");
            SetField(component, "includeVisibilityGroups", true);

            Invoke(component, "OnParametersSet");
            Assert.Multiple(() =>
            {
                Assert.That(GetField<string>(component, "importName"), Is.Empty);
                Assert.That(GetField<bool>(component, "includeVisibilityGroups"), Is.False);
            });

            SetField(component, "importName", "Pending");
            Invoke(component, "OnParametersSet");
            Assert.That(GetField<string>(component, "importName"), Is.EqualTo("Pending"));
        }

        [Test]
        public async Task Close_HidesComponent()
        {
            TransferWorkflowConfiguration component = new();
            SetProperty(component, "Display", true);

            await InvokeAsync(component, "Close");

            Assert.That(component.Display, Is.False);
        }

        [Test]
        public async Task ImportAndExport_ReturnWithoutRequiredInput()
        {
            TransferWorkflowConfiguration component = new();
            SetProperty(component, "Display", true);

            await InvokeAsync(component, "Import");
            await InvokeAsync(component, "Export");

            Assert.That(component.Display, Is.True);
        }

        [Test]
        public void WorkflowPackageJson_PreservesTicketStateVisibilityMode()
        {
            WorkflowConfigurationTransferPackage package = new()
            {
                Configuration = new()
                {
                    Phases =
                    [
                        new() { VisibilityMode = PhaseVisibilityMode.TicketState }
                    ]
                }
            };

            string json = JsonSerializer.Serialize(package);
            WorkflowConfigurationTransferPackage restored = JsonSerializer.Deserialize<WorkflowConfigurationTransferPackage>(json)
                ?? throw new InvalidDataException("Workflow package did not deserialize.");

            Assert.Multiple(() =>
            {
                Assert.That(json, Does.Contain("\"phase_visibility_mode\":\"TicketState\""));
                Assert.That(restored.Configuration.Phases[0].VisibilityMode, Is.EqualTo(PhaseVisibilityMode.TicketState));
            });
        }

        [Test]
        public void WorkflowPackageJson_DefaultsMissingVisibilityModeToAnyTask()
        {
            WorkflowConfigurationTransferPackage restored = JsonSerializer.Deserialize<WorkflowConfigurationTransferPackage>(
                "{\"configuration\":{\"phases\":[{}]},\"transition_groups\":[]}")
                ?? throw new InvalidDataException("Workflow package did not deserialize.");

            Assert.That(restored.Configuration.Phases[0].VisibilityMode, Is.EqualTo(PhaseVisibilityMode.AnyTask));
        }
    }
}

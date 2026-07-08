using FWO.Data.Workflow;
using FWO.Ui.Pages.Settings;
using NUnit.Framework;
using System.Reflection;
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
    }
}

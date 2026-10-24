using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Ui.Pages.Settings;
using Bunit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using FWO.Basics;
using FWO.Config.Api;
using NUnit.Framework;
using System.Reflection;
using System.Text;
using System.Text.Json;
using static FWO.Test.WorkflowConfigurationComponentTestSupport;

namespace FWO.Test
{
    [TestFixture]
    internal class TransferWorkflowConfigurationComponentTest
    {
        private static readonly object[] kFileNameArguments = ["Shared:Config", "Fallback"];
        private static readonly object[] kControlCharacterFileNameArguments = ["Shared\u0001Config", "Fallback"];
        private static readonly object[] kFileNameWithExtensionArguments = ["Shared.fwo-workflow.json", "Fallback"];
        private static readonly object[] kBlankFileNameArguments = ["   ", "Fallback"];

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
        public void CanImport_AllowsSelectingAnExistingTargetConfiguration()
        {
            TransferWorkflowConfiguration component = new();
            SetProperty(component, "ExistingConfigurations", new List<WorkflowConfiguration> { new() { Id = 7, Name = "Existing" } });
            SetField(component, "importPackage", new WorkflowConfigurationTransferPackage());
            SetField(component, "targetConfigurationId", 7);

            Assert.That(GetProperty<bool>(component, "CanImport"), Is.True);
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
        public void SuggestedImportName_UsesLocalizedFallbackForBlankPackageName()
        {
            SimulatedUserConfig userConfig = new();
            string fallbackName = userConfig.GetText("imported_configuration");
            TransferWorkflowConfiguration component = new();
            SetProperty(component, "userConfig", userConfig);
            SetProperty(component, "ExistingConfigurations", new List<WorkflowConfiguration>
            {
                new() { Name = fallbackName }
            });

            Assert.That(Invoke(component, "SuggestedImportName", "   "),
                Is.EqualTo($"{fallbackName}-2"));
        }

        [Test]
        public void FileName_ReplacesInvalidCharactersAndUsesPackageSuffix()
        {
            MethodInfo method = typeof(TransferWorkflowConfiguration).GetMethod("FileName", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(typeof(TransferWorkflowConfiguration).FullName, "FileName");

            Assert.That(method.Invoke(null, kFileNameArguments), Is.EqualTo("Shared_Config.fwo-workflow.json"));
        }

        [Test]
        public void FileName_ReplacesControlCharacters()
        {
            MethodInfo method = typeof(TransferWorkflowConfiguration).GetMethod("FileName", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(typeof(TransferWorkflowConfiguration).FullName, "FileName");

            Assert.That(method.Invoke(null, kControlCharacterFileNameArguments), Is.EqualTo("Shared_Config.fwo-workflow.json"));
        }

        [Test]
        public void FileName_DoesNotDuplicatePackageExtension()
        {
            MethodInfo method = typeof(TransferWorkflowConfiguration).GetMethod("FileName", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(typeof(TransferWorkflowConfiguration).FullName, "FileName");

            Assert.That(method.Invoke(null, kFileNameWithExtensionArguments), Is.EqualTo("Shared.fwo-workflow.json"));
        }

        [Test]
        public void FileName_UsesFallbackForBlankCustomName()
        {
            MethodInfo method = typeof(TransferWorkflowConfiguration).GetMethod("FileName", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(typeof(TransferWorkflowConfiguration).FullName, "FileName");

            Assert.That(method.Invoke(null, kBlankFileNameArguments), Is.EqualTo("Fallback.fwo-workflow.json"));
        }

        [Test]
        public void PhaseSelection_TogglesIndividualAndAllPhases()
        {
            TransferWorkflowConfiguration component = new();
            List<WorkflowConfigurationPhase> phases =
            [
                new() { TaskType = "master", Phase = "request" },
                new() { TaskType = "master", Phase = "approval" }
            ];
            SetField(component, "exportPhases", phases);
            SetField(component, "selectedPhaseKeys", new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            Invoke(component, "SetPhaseSelected", "master:request", true);
            Assert.That(GetProperty<bool>(component, "AllPhasesSelected"), Is.False);

            Invoke(component, "ToggleAllPhases");
            Assert.That(GetProperty<bool>(component, "AllPhasesSelected"), Is.True);

            Invoke(component, "ToggleAllPhases");
            Assert.That(GetProperty<bool>(component, "AllPhasesSelected"), Is.False);
        }

        [Test]
        public void PhaseSelection_ClearsAnIndividualPhase()
        {
            TransferWorkflowConfiguration component = new();
            SetField(component, "exportPhases", new List<WorkflowConfigurationPhase>
            {
                new() { TaskType = "master", Phase = "request" }
            });
            SetField(component, "selectedPhaseKeys", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "master:request" });

            Invoke(component, "SetPhaseSelected", "master:request", false);

            Assert.That(GetProperty<bool>(component, "AllPhasesSelected"), Is.False);
        }

        [Test]
        public void ImportStatisticsMessage_ContainsAllCounters()
        {
            TransferWorkflowConfiguration component = new();
            SetProperty(component, "userConfig", new SimulatedUserConfig());
            WorkflowConfigurationImportStatistics statistics = new()
            {
                CreatedStates = 1,
                CreatedTransitionGroups = 2,
                CreatedVisibilityGroups = 3,
                AddedPhases = 4,
                MergedPhases = 5,
                AddedDerivedStates = 6,
                AddedTransitionGroupLinks = 7
            };

            string message = (string)(Invoke(component, "ImportStatisticsMessage", statistics)
                ?? throw new InvalidOperationException("Statistics message was not returned."));

            Assert.That(message, Does.Contain("1").And.Contain("2").And.Contain("3").And.Contain("4")
                .And.Contain("5").And.Contain("6").And.Contain("7"));
        }

        [Test]
        public void OnParametersSet_ResetsTransferStateOnlyWhenOpening()
        {
            TransferWorkflowConfiguration component = new();
            SetProperty(component, "Display", true);
            SetField(component, "importName", "Old");
            SetField(component, "includeVisibilityGroups", true);
            SetField(component, "targetConfigurationId", 7);

            Invoke(component, "OnParametersSet");
            Assert.Multiple(() =>
            {
                Assert.That(GetField<string>(component, "importName"), Is.Empty);
                Assert.That(GetField<bool>(component, "includeVisibilityGroups"), Is.False);
                Assert.That(GetField<int>(component, "targetConfigurationId"), Is.Zero);
                Assert.That(GetField<string>(component, "exportFileName"), Is.Empty);
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
        public void OnAfterRender_LoadsExportPhasesAndSelectsAllPhases()
        {
            RecordingWorkflowApiConnection api = new();
            api.Respond(RequestQueries.getWorkflowConfigurationPhaseMappings, new List<WorkflowConfigurationPhase>
            {
                new() { TaskType = "master", Phase = "request" }
            });

            using BunitContext context = CreateRenderContext(api);
            IRenderedComponent<TransferWorkflowConfiguration> component = context.Render<TransferWorkflowConfiguration>(parameters => parameters
                .Add(parameter => parameter.Display, true)
                .Add(parameter => parameter.SelectedConfigurationId, 5)
                .Add(parameter => parameter.ExistingConfigurations, new List<WorkflowConfiguration>
                {
                    new() { Id = 5, Name = "Shared" }
                }));

            Assert.Multiple(() =>
            {
                Assert.That(GetField<List<WorkflowConfigurationPhase>>(component.Instance, "exportPhases"), Has.Count.EqualTo(1));
                Assert.That(GetProperty<bool>(component.Instance, "AllPhasesSelected"), Is.True);
            });
        }

        [Test]
        public async Task OnAfterRender_ReportsPhaseLoadingErrors()
        {
            RecordingWorkflowApiConnection api = new();
            TransferWorkflowConfiguration component = new();
            Exception? reportedException = null;
            SetProperty(component, "apiConnection", api);
            SetProperty(component, "userConfig", new SimulatedUserConfig());
            SetProperty(component, "Display", true);
            SetProperty(component, "ImportMode", false);
            SetProperty(component, "SelectedConfigurationId", 5);
            SetProperty(component, "ExistingConfigurations", new List<WorkflowConfiguration> { new() { Id = 5, Name = "Shared" } });
            SetProperty(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((exception, _, _, _) => reportedException = exception));

            await InvokeAsync(component, "OnAfterRenderAsync", true);

            Assert.Multiple(() =>
            {
                Assert.That(GetField<bool>(component, "exportOptionsLoaded"), Is.True);
                Assert.That(reportedException, Is.Not.Null);
            });
        }

        [Test]
        public async Task OnAfterRender_SkipsPhaseLoadingInImportMode()
        {
            RecordingWorkflowApiConnection api = new();
            TransferWorkflowConfiguration component = new();
            SetProperty(component, "apiConnection", api);
            SetProperty(component, "Display", true);
            SetProperty(component, "ImportMode", true);
            SetProperty(component, "SelectedConfigurationId", 5);
            SetProperty(component, "ExistingConfigurations", new List<WorkflowConfiguration> { new() { Id = 5, Name = "Shared" } });

            await InvokeAsync(component, "OnAfterRenderAsync", true);

            Assert.Multiple(() =>
            {
                Assert.That(GetField<bool>(component, "exportOptionsLoaded"), Is.False);
                Assert.That(api.Calls, Is.Empty);
            });
        }

        [Test]
        public void ImportDialog_RendersPackageDetailsAndConfigurationTargets()
        {
            using BunitContext context = CreateRenderContext(new RecordingWorkflowApiConnection());
            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<TransferWorkflowConfiguration>(component => component
                    .Add(parameter => parameter.Display, true)
                    .Add(parameter => parameter.ImportMode, true)
                    .Add(parameter => parameter.ExistingConfigurations, new List<WorkflowConfiguration>
                    {
                        new() { Id = 5, Name = "Shared" }
                    })));
            IRenderedComponent<TransferWorkflowConfiguration> component = wrapper.FindComponent<TransferWorkflowConfiguration>();

            SetField(component.Instance, "importPackage", ImportPackageWithoutGroups());
            component.Render();

            Assert.Multiple(() =>
            {
                Assert.That(component.Find("#workflow-configuration-file"), Is.Not.Null);
                Assert.That(component.Markup, Does.Contain("Shared"));
                Assert.That(component.Markup, Does.Contain("import_as_new_configuration"));
                Assert.That(component.FindAll("select option"), Has.Count.EqualTo(2));
                Assert.That(component.Find("input.form-control"), Is.Not.Null);
            });
        }

        [Test]
        public async Task LoadImportFile_DeserializesValidPackageAndSuggestsName()
        {
            TransferWorkflowConfiguration component = new();
            SetProperty(component, "userConfig", new SimulatedUserConfig());
            string json = JsonSerializer.Serialize(ImportPackageWithoutGroups());

            await InvokeAsync(component, "LoadImportFile", CreateInputFileChangeEventArgs("workflow.json", Encoding.UTF8.GetBytes(json)));

            Assert.Multiple(() =>
            {
                Assert.That(GetField<WorkflowConfigurationTransferPackage>(component, "importPackage").Configuration.Name, Is.EqualTo("Shared"));
                Assert.That(GetField<string>(component, "importName"), Is.EqualTo("Shared"));
            });
        }

        [Test]
        public async Task LoadImportFile_ClearsInvalidPackageAndReportsError()
        {
            TransferWorkflowConfiguration component = new();
            Exception? reportedException = null;
            SetProperty(component, "userConfig", new SimulatedUserConfig());
            SetField(component, "importPackage", ImportPackageWithoutGroups());
            SetField(component, "importName", "Old");
            SetProperty(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((exception, _, _, _) => reportedException = exception));

            await InvokeAsync(component, "LoadImportFile", CreateInputFileChangeEventArgs("workflow.json", Encoding.UTF8.GetBytes("not-json")));

            Assert.Multiple(() =>
            {
                Assert.That(GetFieldValue(component, "importPackage"), Is.Null);
                Assert.That(GetField<string>(component, "importName"), Is.Empty);
                Assert.That(reportedException, Is.Not.Null);
            });
        }

        [Test]
        public async Task LoadImportFile_ReportsEmptyPackage()
        {
            TransferWorkflowConfiguration component = new();
            Exception? reportedException = null;
            SetProperty(component, "userConfig", new SimulatedUserConfig());
            SetProperty(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((exception, _, _, _) => reportedException = exception));

            await InvokeAsync(component, "LoadImportFile", CreateInputFileChangeEventArgs("workflow.json", Encoding.UTF8.GetBytes("null")));

            Assert.Multiple(() =>
            {
                Assert.That(GetFieldValue(component, "importPackage"), Is.Null);
                Assert.That(reportedException, Is.TypeOf<InvalidDataException>());
            });
        }

        [Test]
        public async Task Export_SerializesPackageAndDownloadsIt()
        {
            RecordingWorkflowApiConnection api = new();
            api.Respond(RequestQueries.getWorkflowConfigurations, new List<WorkflowConfiguration>
            {
                new() { Id = 5, Name = "Shared" }
            });
            api.Respond(RequestQueries.getWorkflowConfigurationPhaseMappings, new List<WorkflowConfigurationPhase>
            {
                new()
                {
                    TaskType = "master", Phase = "request", PhaseMatrix = new()
                    {
                        LowestInputState = 1, LowestStartState = 2, LowestEndState = 3,
                        TransitionGroups = []
                    }
                }
            });
            api.Respond(RequestQueries.getStateMatrixTransitionGroups, new List<StateMatrixTransitionGroup>());
            api.Respond(RequestQueries.getStates, new List<WfState>
            {
                new() { Id = 1, Name = "Open" },
                new() { Id = 2, Name = "Review" },
                new() { Id = 3, Name = "Closed" }
            });
            TransferWorkflowConfiguration component = new();
            RecordingJsRuntime jsRuntime = new();
            SetProperty(component, "apiConnection", api);
            SetProperty(component, "jsRuntime", jsRuntime);
            SetProperty(component, "userConfig", new SimulatedUserConfig());
            SetProperty(component, "Display", true);
            SetProperty(component, "ExistingConfigurations", new List<WorkflowConfiguration> { new() { Id = 5, Name = "Shared" } });
            SetProperty(component, "SelectedConfigurationId", 5);
            SetField(component, "exportOptionsLoaded", true);
            SetField(component, "exportPhases", new List<WorkflowConfigurationPhase>
            {
                new() { TaskType = "master", Phase = "request" }
            });
            SetField(component, "selectedPhaseKeys", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "master:request" });

            await InvokeAsync(component, "Export");

            Assert.Multiple(() =>
            {
                Assert.That(component.Display, Is.False);
                Assert.That(jsRuntime.Identifier, Is.EqualTo("DownloadFile"));
                Assert.That(jsRuntime.Arguments?[0]?.ToString(), Is.EqualTo("Shared.fwo-workflow.json"));
            });
        }

        [Test]
        public async Task Export_ReportsApiErrorsAndResetsWorkingState()
        {
            TransferWorkflowConfiguration component = new();
            Exception? reportedException = null;
            SetProperty(component, "apiConnection", new RecordingWorkflowApiConnection());
            SetProperty(component, "userConfig", new SimulatedUserConfig());
            SetProperty(component, "Display", true);
            SetProperty(component, "ExistingConfigurations", new List<WorkflowConfiguration> { new() { Id = 5, Name = "Shared" } });
            SetProperty(component, "SelectedConfigurationId", 5);
            SetField(component, "exportOptionsLoaded", true);
            SetField(component, "selectedPhaseKeys", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "master:request" });
            SetProperty(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((exception, _, _, _) => reportedException = exception));

            await InvokeAsync(component, "Export");

            Assert.Multiple(() =>
            {
                Assert.That(component.Display, Is.True);
                Assert.That(reportedException, Is.Not.Null);
                Assert.That(GetField<bool>(component, "working"), Is.False);
            });
        }

        [Test]
        public async Task Import_CreatesConfigurationAndDisplaysStatistics()
        {
            RecordingWorkflowApiConnection api = new();
            api.Respond(RequestQueries.getWorkflowConfigurations, new List<WorkflowConfiguration>());
            api.Respond(RequestQueries.getStateMatrixTransitionGroups, new List<StateMatrixTransitionGroup>());
            api.Respond(RequestQueries.getWorkflowVisibilityGroups, new List<WorkflowVisibilityGroup>());
            api.Respond(RequestQueries.getStates, new List<WfState>
            {
                new() { Id = 1, Name = "Open" },
                new() { Id = 2, Name = "Review" },
                new() { Id = 3, Name = "Closed" }
            });
            api.Respond(RequestQueries.createWorkflowConfiguration, new ReturnId { NewId = 9 });
            TransferWorkflowConfiguration component = new();
            string message = "";
            SetProperty(component, "apiConnection", api);
            SetProperty(component, "userConfig", new SimulatedUserConfig());
            SetProperty(component, "Display", true);
            SetField(component, "importPackage", ImportPackageWithoutGroups());
            SetField(component, "importName", "Imported");
            SetProperty(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((_, _, text, _) => message = text));

            await InvokeAsync(component, "Import");

            Assert.Multiple(() =>
            {
                Assert.That(component.Display, Is.False);
                Assert.That(message, Does.Contain("added_phases"));
                Assert.That(api.Calls.Any(call => call.Query == RequestQueries.createWorkflowConfiguration), Is.True);
            });
        }

        [Test]
        public async Task Import_ReportsApiErrorsAndKeepsDialogOpen()
        {
            TransferWorkflowConfiguration component = new();
            Exception? reportedException = null;
            SetProperty(component, "apiConnection", new RecordingWorkflowApiConnection());
            SetProperty(component, "userConfig", new SimulatedUserConfig());
            SetProperty(component, "Display", true);
            SetField(component, "importPackage", ImportPackageWithoutGroups());
            SetField(component, "importName", "Imported");
            SetProperty(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((exception, _, _, _) => reportedException = exception));

            await InvokeAsync(component, "Import");

            Assert.Multiple(() =>
            {
                Assert.That(component.Display, Is.True);
                Assert.That(reportedException, Is.Not.Null);
                Assert.That(GetField<bool>(component, "working"), Is.False);
            });
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

        private static WorkflowConfigurationTransferPackage ImportPackageWithoutGroups() => new()
        {
            Configuration = new()
            {
                Name = "Shared",
                Phases =
                [
                    new()
                    {
                        TaskType = "master", Phase = "request", Active = true,
                        LowestInputState = 1, LowestStartState = 2, LowestEndState = 3,
                        DerivedStates = [new() { FromStateId = 1, DerivedStateId = 3 }],
                        TransitionGroups = []
                    }
                ]
            },
            States =
            [
                new() { Id = 1, Name = "Open" },
                new() { Id = 2, Name = "Review" },
                new() { Id = 3, Name = "Closed" }
            ],
            TransitionGroups = [],
            VisibilityGroups = []
        };

        private static BunitContext CreateRenderContext(ApiConnection apiConnection)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            SimulatedUserConfig userConfig = new()
            {
                User = { Roles = [Roles.Admin] }
            };
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddLocalization();
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddSingleton<GlobalConfig>(new SimulatedGlobalConfig());
            context.Services.AddSingleton<AuthenticationStateProvider>(new SettingsStatesAuthStateProvider(userConfig.User.Roles));
            return context;
        }

        private static InputFileChangeEventArgs CreateInputFileChangeEventArgs(string fileName, ReadOnlyMemory<byte> content)
        {
            return new InputFileChangeEventArgs(new List<IBrowserFile>
            {
                new TestBrowserFile(fileName, content)
            });
        }

        private sealed class TestBrowserFile : IBrowserFile
        {
            private readonly ReadOnlyMemory<byte> content;

            public TestBrowserFile(string name, ReadOnlyMemory<byte> content)
            {
                Name = name;
                this.content = content;
            }

            public string Name { get; }
            public DateTimeOffset LastModified { get; } = DateTimeOffset.UtcNow;
            public long Size => content.Length;
            public string ContentType => "application/json";

            public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
            {
                return new MemoryStream(content.ToArray(), writable: false);
            }
        }

        private sealed class RecordingJsRuntime : IJSRuntime
        {
            public string? Identifier { get; private set; }
            public object?[]? Arguments { get; private set; }

            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            {
                Identifier = identifier;
                Arguments = args;
                return ValueTask.FromResult(default(TValue)!);
            }

            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            {
                return InvokeAsync<TValue>(identifier, args);
            }
        }
    }
}

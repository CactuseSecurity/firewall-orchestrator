using System.Reflection;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Workflow;
using FWO.ExternalSystems.Tufin.SecureChange;
using FWO.Middleware.Server;
using FWO.Services.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// The external request handler takes the SecureChange ticket templates from the device specific settings of the
    /// management of the request task, and bundles tasks of different managements only if their templates are equal.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    internal class ExtTicketHandlerSCTemplateTest
    {
        private const int kTufinSystemId = 1;
        private const int kStartManagementId = 1;
        private const int kFurtherManagementId = 2;
        private const string kManagementWorkflow = "Management Workflow";
        private const int kDoneStateId = 631;
        // all request tasks of the test ticket belong to the start management
        private static readonly List<List<int>> kLoadedManagementIds = [[kStartManagementId]];

        private static readonly ExternalTicketSystem TufinSystem = new()
        {
            Id = kTufinSystemId,
            TypeId = BuiltInExternalTicketSystemTypes.TufinSecureChangeId,
            Name = "Tufin",
            Url = "https://tufin-test.xxx.de/securechangeworkflow/api/securechange/"
        };

        private static List<ExternalTicketTemplate> GlobalTemplates => ExtTicketHandlerTest.ticketSystem.Templates;

        /// <summary>The global templates with another workflow name in the access request ticket.</summary>
        private static List<ExternalTicketTemplate> ManagementTemplates() =>
        [
            .. GlobalTemplates.Select(template => new ExternalTicketTemplate
            {
                TaskType = template.TaskType,
                TicketTemplate = template.TicketTemplate.Replace("Standard Firewall Request", kManagementWorkflow),
                TasksTemplate = template.TasksTemplate,
                ObjectTemplate = template.ObjectTemplate,
                ObjectTemplateShort = template.ObjectTemplateShort,
                IpTemplate = template.IpTemplate,
                NwObjGroupTemplate = template.NwObjGroupTemplate,
                ServiceTemplate = template.ServiceTemplate,
                IcmpTemplate = template.IcmpTemplate,
                IpProtocolTemplate = template.IpProtocolTemplate
            })
        ];

        private static SimulatedUserConfig UserConfig(ExternalTicketSystem system) => new()
        {
            ExternalRequestWaitCycles = 3,
            ReqPriorities = ExtTicketHandlerTest.reqPrios,
            ModNamingConvention = ExtTicketHandlerTest.namingConvention,
            ModRolloutBundleTasks = true
        };

        [Test]
        public async Task CreatedRequestUsesTheTemplatesOfTheTaskManagement()
        {
            ExtTicketHandlerTestApiConn apiConnection = new();
            FixedSCTemplateSource templateSource = new(GlobalTemplates,
                new Dictionary<int, List<ExternalTicketTemplate>> { [kStartManagementId] = ManagementTemplates() });
            using ExternalRequestHandler handler = new ExternalRequestHandler(UserConfig(TufinSystem), apiConnection, null)
            {
                SCTemplateSource = templateSource
            }.WithProvisioningSettings(ExtTicketHandlerTest.ExternalTicketSystemSettings(TufinSystem));

            // the request of task 1 is finished already
            ExtTicketHandlerTestApiConn.SetReqTaskState(1, kDoneStateId);
            await handler.HandleStateChange(new ExternalRequest
            {
                Id = 2,
                TicketId = 123,
                TaskNumber = 2,
                ExtRequestState = ExtStates.ExtReqDone.ToString(),
                ExtTicketId = "4712",
                ExtQueryVariables = "{\"BundledTasks\":[2,3]}"
            });

            ExternalTicketSystem storedSystem = System.Text.Json.JsonSerializer.Deserialize<ExternalTicketSystem>(apiConnection.AddedExtTicketSystem ?? "")
                ?? throw new AssertionException("No external request created");
            using (Assert.EnterMultipleScope())
            {
                Assert.That(apiConnection.AddExtRequestVars, Does.Contain(kManagementWorkflow));
                Assert.That(storedSystem.Templates.Single(template => template.TaskType == nameof(SCTaskType.AccessRequest)).TicketTemplate,
                    Does.Contain(kManagementWorkflow));
                Assert.That(templateSource.LoadedManagementIds, Is.EqualTo(kLoadedManagementIds));
            }
        }

        [Test]
        public async Task SecureChangeTasksAreOnlyBundledWithEqualTemplates()
        {
            SCTemplateLookup differentTemplates = new(GlobalTemplates,
                new Dictionary<int, List<ExternalTicketTemplate>> { [kFurtherManagementId] = ManagementTemplates() });
            SCTemplateLookup equalTemplates = new(GlobalTemplates,
                new Dictionary<int, List<ExternalTicketTemplate>> { [kFurtherManagementId] = [.. GlobalTemplates] });

            using (Assert.EnterMultipleScope())
            {
                Assert.That(await CanBundle(differentTemplates), Is.False);
                Assert.That(await CanBundle(equalTemplates), Is.True);
            }
        }

        [Test]
        public async Task TasksForAnotherSecureChangeServerAreNotBundled()
        {
            SCTemplateLookup equalTemplates = new(GlobalTemplates,
                new Dictionary<int, List<ExternalTicketTemplate>> { [kFurtherManagementId] = [.. GlobalTemplates] });

            Assert.That(await CanBundle(equalTemplates, "https://other-securechange.example/"), Is.False);
        }

        /// <summary>Whether an access task of the further management joins the request of an access task of the start management.</summary>
        /// <param name="templates">Ticket templates of the managements.</param>
        /// <param name="startUrl">URL of the request of the start task; null for the URL configured for both managements.</param>
        private static async Task<bool> CanBundle(SCTemplateLookup templates, string? startUrl = null)
        {
            GlobalDeviceSpecificSettings settings = ExtTicketHandlerTest.ExternalTicketSystemSettings(TufinSystem);
            using ExternalRequestHandler handler = new ExternalRequestHandler(UserConfig(TufinSystem), new ExtTicketHandlerTestApiConn(), null)
                .WithProvisioningSettings(settings);
            SetField(handler, "scTemplates", templates);
            ExternalTicketSystem startSystem = ExternalTicketSystem.FromSecureChangeSettings(settings);
            startSystem.Url = startUrl ?? startSystem.Url;
            SetField(handler, "actSystem", startSystem);
            MethodInfo method = typeof(ExternalRequestHandler).GetMethod("CanBundle", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(ExternalRequestHandler).FullName, "CanBundle");
            WfReqTask startTask = new() { TaskType = WfTaskType.access.ToString(), ManagementId = kStartManagementId };
            WfReqTask furtherTask = new() { TaskType = WfTaskType.access.ToString(), ManagementId = kFurtherManagementId };
            ProvisioningWorkItem start = new(ProvisioningWay.ExternalTicketSystem, startTask, null, ExternalTicketSystemKind.TufinSecureChange, false);
            ProvisioningWorkItem further = new(ProvisioningWay.ExternalTicketSystem, furtherTask, null, ExternalTicketSystemKind.TufinSecureChange, false);
            List<object?> arguments = [further, start];
            return await (Task<bool>)method.Invoke(handler, arguments.ToArray())!;
        }

        private static void SetField(ExternalRequestHandler handler, string name, object value)
        {
            FieldInfo field = typeof(ExternalRequestHandler).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(typeof(ExternalRequestHandler).FullName, name);
            field.SetValue(handler, value);
        }
    }
}

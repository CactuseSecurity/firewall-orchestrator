using System.Reflection;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.ExternalSystems.Tufin.SecureChange;
using FWO.Middleware.Server;
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
        private const int kCheckPointSystemId = 3;
        private const int kCheckPointTypeId = 9;
        private const int kStartManagementId = 1;
        private const int kFurtherManagementId = 2;
        private const string kManagementWorkflow = "Management Workflow";
        private static readonly List<int> kRoutedManagementIds = [kStartManagementId, kFurtherManagementId];
        // all request tasks of the test ticket belong to the start management
        private static readonly List<List<int>> kLoadedManagementIds = [[kStartManagementId]];

        private static readonly ExternalTicketSystem TufinSystem = new()
        {
            Id = kTufinSystemId,
            TypeId = BuiltInExternalTicketSystemTypes.TufinSecureChangeId,
            Name = "Tufin",
            Url = "https://tufin-test.xxx.de/securechangeworkflow/api/securechange/"
        };

        private static readonly ExternalTicketSystem CheckPointSystem = new()
        {
            Id = kCheckPointSystemId,
            TypeId = kCheckPointTypeId,
            Name = "CheckPoint",
            Url = "https://checkpoint-test.xxx.de/web_api/"
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

        private static List<ManagementFwConfigChangeState> RoutesTo(int systemId) =>
        [
            .. kRoutedManagementIds.Select(managementId => new ManagementFwConfigChangeState
            {
                Id = managementId,
                Name = $"m{managementId}",
                Enabled = true,
                SelectedChanges = new()
                {
                    [ManagementFwConfigChangeCategories.ObjectChanges] = systemId.ToString(),
                    [ManagementFwConfigChangeCategories.RuleChanges] = systemId.ToString()
                }
            })
        ];

        private static SimulatedUserConfig UserConfig(ExternalTicketSystem system) => new()
        {
            ExternalRequestWaitCycles = 3,
            ExtTicketSystems = System.Text.Json.JsonSerializer.Serialize(new List<ExternalTicketSystem> { system }),
            FwConfigChangeMgmSettings = System.Text.Json.JsonSerializer.Serialize(RoutesTo(system.Id)),
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
            using ExternalRequestHandler handler = new(UserConfig(TufinSystem), apiConnection, null) { SCTemplateSource = templateSource };

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
        public void SecureChangeTasksAreOnlyBundledWithEqualTemplates()
        {
            SCTemplateLookup differentTemplates = new(GlobalTemplates,
                new Dictionary<int, List<ExternalTicketTemplate>> { [kFurtherManagementId] = ManagementTemplates() });
            SCTemplateLookup equalTemplates = new(GlobalTemplates,
                new Dictionary<int, List<ExternalTicketTemplate>> { [kFurtherManagementId] = [.. GlobalTemplates] });

            using (Assert.EnterMultipleScope())
            {
                Assert.That(CanBundle(TufinSystem, differentTemplates), Is.False);
                Assert.That(CanBundle(TufinSystem, equalTemplates), Is.True);
                // the templates only matter for SecureChange
                Assert.That(CanBundle(CheckPointSystem, differentTemplates), Is.True);
            }
        }

        /// <summary>Whether an access task of the further management joins the request of an access task of the start management.</summary>
        private static bool CanBundle(ExternalTicketSystem system, SCTemplateLookup templates)
        {
            using ExternalRequestHandler handler = new(UserConfig(system), new ExtTicketHandlerTestApiConn(), null);
            FieldInfo templatesField = typeof(ExternalRequestHandler).GetField("scTemplates", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(typeof(ExternalRequestHandler).FullName, "scTemplates");
            templatesField.SetValue(handler, templates);
            MethodInfo method = typeof(ExternalRequestHandler).GetMethod("CanBundleWithStartTask", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(ExternalRequestHandler).FullName, "CanBundleWithStartTask");
            WfReqTask startTask = new() { TaskType = WfTaskType.access.ToString(), ManagementId = kStartManagementId };
            WfReqTask furtherTask = new() { TaskType = WfTaskType.access.ToString(), ManagementId = kFurtherManagementId };
            List<ExternalTicketSystem> systems = [system];
            List<object?> arguments = [furtherTask, startTask, system.Id, RoutesTo(system.Id), systems];
            return (bool)method.Invoke(handler, arguments.ToArray())!;
        }
    }
}

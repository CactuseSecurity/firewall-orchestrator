using FWO.Basics.Exceptions;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.ExternalSystems.Tufin.SecureChange;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// A SecureChange ticket is only created for a task type whose resolved template has ticket and task text; otherwise
    /// the task is refused with a configuration error instead of sending an empty request. Service group tasks are refused
    /// as well, because SecureChange only modifies groups of network objects.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class SCTicketTemplateCheckTest
    {
        private const string kGroupName = "ARxx12345-100";
        private const string kTicketTemplate = "{\"subject\":\"@@TICKET_SUBJECT@@\",\"tasks\":[@@TASKS@@]}";
        private const string kTasksTemplate = "{\"group\":\"@@GROUPNAME@@\"}";
        private static readonly ExternalCertificateChecks CertificateChecks = new(FirewallConnections: true, TicketSystems: true);
        private static readonly List<IpProtocol> IpProtos = [new() { Id = 6, Name = "TCP" }];
        private static readonly ModellingNamingConvention NamingConvention = new();

        private static List<WfReqTask> NetworkGroupCreateTasks() =>
        [
            new()
            {
                Id = 1,
                TaskNumber = 1,
                TaskType = WfTaskType.group_create.ToString(),
                RequestAction = RequestAction.create.ToString(),
                AdditionalInfo = $"{{\"GrpName\":\"{kGroupName}\"}}",
                OnManagement = new() { Id = 1, Name = "Checkpoint", ExtMgtData = "{\"id\":\"2\",\"name\":\"CheckpointExt\"}" },
                ManagementId = 1,
                Elements = [new() { Id = 1, TaskId = 1, IpString = "123.0.0.1/32", Name = "AppServerX", Field = ElemFieldType.source.ToString() }]
            }
        ];

        /// <summary>A group task without IP elements changes a service group.</summary>
        private static List<WfReqTask> ServiceGroupTasks(WfTaskType taskType) =>
        [
            new()
            {
                Id = 2,
                TaskNumber = 2,
                TaskType = taskType.ToString(),
                RequestAction = RequestAction.create.ToString(),
                AdditionalInfo = "{\"GrpName\":\"SVC-Group\"}",
                OnManagement = new() { Id = 1, Name = "Checkpoint", ExtMgtData = "{\"id\":\"2\",\"name\":\"CheckpointExt\"}" },
                ManagementId = 1,
                Elements = [new() { Id = 2, TaskId = 2, Name = "tcp_443", Port = 443, ProtoId = 6, Field = ElemFieldType.service.ToString() }]
            }
        ];

        private static SCTicket TicketWith(string ticketTemplate, string tasksTemplate)
        {
            GlobalDeviceSpecificSettings settings = new();
            SecureChangeTemplateSettingKeys.Keys[(SCTaskType.NetworkObjectModify, SecureChangeTemplatePart.Ticket)].DtoSetter!(settings, ticketTemplate);
            SecureChangeTemplateSettingKeys.Keys[(SCTaskType.NetworkObjectModify, SecureChangeTemplatePart.Tasks)].DtoSetter!(settings, tasksTemplate);
            ExternalTicketSystem system = new()
            {
                Id = 1,
                TypeId = BuiltInExternalTicketSystemTypes.TufinSecureChangeId,
                Name = "Tufin",
                Url = "https://tufin-test.xxx.de/securechangeworkflow/api/securechange/",
                Templates = SCTemplateLookup.FromSettings(settings)
            };
            return new SCTicket(system, CertificateChecks);
        }

        [TestCase("", "")]
        [TestCase(kTicketTemplate, "")]
        [TestCase("", kTasksTemplate)]
        public void TaskTypeWithoutTicketOrTaskText_IsRefused(string ticketTemplate, string tasksTemplate)
        {
            SCTicket ticket = TicketWith(ticketTemplate, tasksTemplate);

            ConfigException? exception = Assert.ThrowsAsync<ConfigException>(() => ticket.CreateRequestString(NetworkGroupCreateTasks(), IpProtos, NamingConvention));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(exception?.Message, Does.Contain(nameof(SCTaskType.NetworkObjectModify)));
                Assert.That(ticket.TicketText, Is.Empty);
            }
        }

        /// <summary>SecureChange only modifies groups of network objects; a service group task must not create a ticket without tasks.</summary>
        [TestCase(WfTaskType.group_create)]
        [TestCase(WfTaskType.group_modify)]
        public void ServiceGroupTask_IsRefused(WfTaskType taskType)
        {
            SCTicket ticket = TicketWith(kTicketTemplate, kTasksTemplate);

            ConfigException? exception = Assert.ThrowsAsync<ConfigException>(() => ticket.CreateRequestString(ServiceGroupTasks(taskType), IpProtos, NamingConvention));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(exception?.Message, Does.Contain("service groups"));
                Assert.That(ticket.TicketText, Is.Empty);
            }
        }

        [Test]
        public async Task TaskTypeWithTicketAndTaskText_CreatesTheTicket()
        {
            SCTicket ticket = TicketWith(kTicketTemplate, kTasksTemplate);

            await ticket.CreateRequestString(NetworkGroupCreateTasks(), IpProtos, NamingConvention);

            Assert.That(ticket.TicketText, Does.Contain(kGroupName));
        }
    }
}

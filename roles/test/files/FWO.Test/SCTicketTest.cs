using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Api.Data;
using FWO.Tufin.SecureChange;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class SCTicketTest
    {
        readonly SimulatedUserConfig userConfig = new();

        readonly ExternalTicketSystem ticketSystem = new()
        {
            Id = 1,
            Type = ExternalTicketSystemType.TufinSecureChange,
            Authorization = "xyz",
            Name = "Tufin",
            Url = "https://tufin-test.xxx.de/securechangeworkflow/api/securechange/",
            Templates =
            [
                new()
                {
                    TaskType = SCTaskType.NetworkObjectCreate.ToString(),
                    TicketTemplate = "{\"ticket\":{\"subject\":\"@@TICKET_SUBJECT@@\",\"priority\":\"@@PRIORITY@@\",\"requester\":\"@@ONBEHALF@@\",\"domain_name\":\"\",\"workflow\":{\"name\":\"@@WORKFLOW_NAME@@\"},\"steps\":{\"step\":[{\"name\":\"Erfassung des Antrags\",\"tasks\":{\"task\":{\"fields\":{\"field\":[@@TASKS@@]}}}}]}}}",
                    TasksTemplate = "{\"@xsi.type\": \"multi_group_change\",\"name\": \"Modify network object group\",\"group_change\": {\"name\": \"@@GROUPNAME@@\",\"management_id\": @@MANAGEMENT_ID@@,\"management_name\": \"@@MANAGEMENT_NAME@@\",\"members\": {\"member\": @@MEMBERS@@},\"change_action\": \"CREATE\"}}"
                }
            ]
        };

        readonly List<WfReqTask> reqTasks = 
        [
            new()
            {
                Id = 1,
                Title = "new Group",
                TaskNumber = 1,
                TaskType = WfTaskType.group_create.ToString(),
                TicketId = 123,
                RequestAction = RequestAction.create.ToString(),
                Reason = "need it for connection",
                AdditionalInfo = "{\"GrpName\":\"ARxx12345-100\"}",
                OnManagement = new(){ Id = 1, Name = "Checkpoint", ExtMgtData = "{\"id\":\"2\",\"name\":\"CheckpointExt\"}"},
                ManagementId = 1,
                Elements = 
                [
                    new()
                    {
                        Id = 1,
                        TaskId = 1,
                        RequestAction = RequestAction.create.ToString(),
                        IpString = "123.0.0.1/32",
                        GroupName = "ARxx12345-100",
                        Name = "AppServerX",
                        Field = ElemFieldType.source.ToString()
                    }
                ]
            }
        ];

        readonly static string FilledTicketText =
            "{\"ticket\":{\"subject\":\"\",\"priority\":\"Normal\",\"requester\":\"\",\"domain_name\":\"\",\"workflow\":{\"name\":\"Automatische Gruppenerstellung\"}," + 
            "\"steps\":{\"step\":[{\"name\":\"Erfassung des Antrags\",\"tasks\":{\"task\":{\"fields\":{\"field\":[{\"@xsi.type\": \"multi_group_change\",\"name\": \"Modify network object group\",\"group_change\": " +
            "{\"name\": \"ARxx12345-100\",\"management_id\": 2,\"management_name\": \"CheckpointExt\",\"members\": " +
            "{\"member\": [{\"@type\": \"host\", \"name\": \"AppServerX\", \"object_type\": \"host\", \"object_details\": \"123.0.0.1/32\", \"management_id\": 2, \"status\": \"ADDED\", \"comment\": \"\", \"object_updated_status\": \"NEW\"}]}," +
            "\"change_action\": \"CREATE\"}}]}}}}]}}}";


        [SetUp]
        public void Initialize()
        {

        }

        [Test]
        public void TestSCTicket()
        {
            SCTicket ticket = new (ticketSystem);
            ticket.CreateRequestString(reqTasks);

            ClassicAssert.AreEqual(FilledTicketText, ticket.TicketText);
        }
    }
}

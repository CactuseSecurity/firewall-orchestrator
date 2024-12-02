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
        static readonly ModellingNamingConvention NamingConvention = new()
        {
            NetworkAreaRequired = true, UseAppPart = false, FixedPartLength = 2, FreePartLength = 5, NetworkAreaPattern = "NA", AppRolePattern = "AR", AppServerPrefix = "net_"
        };
        static readonly List<IpProtocol> ipProtos = [ new(){ Id = 6, Name = "TCP" }];


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
                    TaskType = SCTaskType.NetworkObjectModify.ToString(),
                    TicketTemplate = "{\"ticket\":{\"subject\":\"@@TICKET_SUBJECT@@\",\"priority\":\"@@PRIORITY@@\",\"requester\":\"@@ONBEHALF@@\",\"domain_name\":\"\",\"workflow\":{\"name\":\"Automatische Gruppenerstellung\"},\"steps\":{\"step\":[{\"name\":\"Erfassung des Antrags\",\"tasks\":{\"task\":{\"fields\":{\"field\":[@@TASKS@@]}}}}]}}}",
                    TasksTemplate = "{\"@xsi.type\": \"multi_group_change\",\"name\": \"Modify network object group\",\"group_change\": {\"name\": \"@@GROUPNAME@@\",\"management_name\": \"@@MANAGEMENT_NAME@@\",\"members\": {\"member\": @@MEMBERS@@},\"change_action\": \"CREATE\"}}",
                    ObjectTemplate = "{\"@type\": \"@@TYPE@@\", \"name\": \"@@OBJECTNAME@@\", \"object_type\": \"@@OBJECT_TYPE@@\", \"object_details\": \"@@OBJECT_DETAILS@@\", \"status\": \"@@STATUS@@\", \"comment\": \"@@COMMENT@@\", \"object_updated_status\": \"@@OBJUPDSTATUS@@\", \"management_id\": @@MANAGEMENT_ID@@}",
                    ObjectTemplateShort = "{\"@type\": \"Object\", \"name\": \"@@OBJECTNAME@@\", \"status\": \"@@STATUS@@\", \"object_updated_status\": \"@@OBJUPDSTATUS@@\"}"
                },
                new()
                {
                    TaskType = SCTaskType.AccessRequest.ToString(),
                    TicketTemplate = "{\"ticket\":{\"subject\":\"@@TICKET_SUBJECT@@\",\"priority\":\"@@PRIORITY@@\",\"requester\":\"@@ONBEHALF@@\",\"domain_name\":\"\",\"workflow\":{\"name\":\"Standard Firewall Request\"},\"steps\":{\"step\":[{\"name\":\"Erfassung des Antrags\",\"tasks\":{\"task\":{\"fields\":{\"field\":[{\"@xsi.type\": \"multi_access_request\",\"name\": \"Zugang\",\"read_only\": false,\"access_request\":[@@TASKS@@]},{\"@xsi.type\": \"text_area\",\"name\": \"Grund für den Antrag\",\"read_only\": false,\"text\": \"@@REASON@@\"},{\"@xsi.type\": \"text_field\",\"name\": \"Anwendungs-ID\",\"text\": \"@@APPID@@\"},{\"@xsi.type\": \"checkbox\",\"name\": \"hinterlegt\",\"value\": true}]}}}}]}}}}",
                    TasksTemplate = "{\"order\": \"@@ORDERNAME@@\",\"verifier_result\": {\"status\": \"not run\"},\"use_topology\": true,\"targets\": {\"target\": {\"@type\": \"ANY\"}},\"action\": \"accept\",\"sources\":{\"source\":@@SOURCES@@},\"destinations\":{\"destination\":@@DESTINATIONS@@},\"services\":{\"service\":@@SERVICES@@},\"labels\":\"\",\"comment\": \"@@TASKCOMMENT@@\"}",
                    IpTemplate = "{\"@type\": \"IP\", \"ip_address\": \"@@IP@@\", \"netmask\": \"255.255.255.255\", \"cidr\": 32}",
                    NwObjGroupTemplate = "{\"@type\": \"Object\", \"object_name\": \"@@GROUPNAME@@\", \"management_name\": \"@@MANAGEMENT_NAME@@\"}",
                    ServiceTemplate = "{\"@type\": \"PROTOCOL\", \"protocol\": \"@@PROTOCOLNAME@@\", \"port\": @@PORT@@, \"name\": \"@@SERVICENAME@@\"}"
                }

            ]
        };

        readonly List<WfReqTask> grpCreateReqTasks = 
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

        readonly static string GrpCreateFilledTicketText =
            "{\"ticket\":{\"subject\":\"\",\"priority\":\"Normal\",\"requester\":\"\",\"domain_name\":\"\",\"workflow\":{\"name\":\"Automatische Gruppenerstellung\"}," + 
            "\"steps\":{\"step\":[{\"name\":\"Erfassung des Antrags\",\"tasks\":{\"task\":{\"fields\":{\"field\":[{\"@xsi.type\": \"multi_group_change\",\"name\": \"Modify network object group\",\"group_change\": " +
            "{\"name\": \"ARxx12345-100\",\"management_name\": \"CheckpointExt\",\"members\": " +
            "{\"member\": [{\"@type\": \"host\", \"name\": \"AppServerX\", \"object_type\": \"host\", \"object_details\": \"123.0.0.1/32\", \"status\": \"ADDED\", \"comment\": \"\", \"object_updated_status\": \"NEW\", \"management_id\": 2}]}," +
            "\"change_action\": \"CREATE\"}}]}}}}]}}}";

        readonly List<WfReqTask> accessReqTasks = 
        [
            new()
            {
                Id = 1,
                Title = "new Connection",
                TaskNumber = 1,
                TaskType = WfTaskType.access.ToString(),
                TicketId = 123,
                RequestAction = RequestAction.create.ToString(),
                Reason = "connection needed",
                AdditionalInfo = "{\"ConnId\":\"1\"}",
                OnManagement = new(){ Id = 1, Name = "Checkpoint", ExtMgtData = "{\"id\":\"2\",\"name\":\"CheckpointExt\"}"},
                ManagementId = 1,
                Elements = 
                [
                    new()
                    {
                        Id = 1,
                        TaskId = 1,
                        RequestAction = RequestAction.create.ToString(),
                        GroupName = "ARxx12345-100",
                        Field = ElemFieldType.source.ToString()
                    },
                    new()
                    {
                        Id = 2,
                        TaskId = 1,
                        RequestAction = RequestAction.create.ToString(),
                        GroupName = "ARxx12345-101",
                        Field = ElemFieldType.destination.ToString()
                    },
                    new()
                    {
                        Id = 3,
                        TaskId = 1,
                        RequestAction = RequestAction.create.ToString(),
                        Name = "Svc1",
                        Port = 1000,
                        ProtoId = 6,
                        Field = ElemFieldType.service.ToString()
                    }
                ]
            }
        ];

        readonly static string AccessFilledTicketText =
            "{\"ticket\":{\"subject\":\"\",\"priority\":\"Normal\",\"requester\":\"\",\"domain_name\":\"\",\"workflow\":{\"name\":\"Standard Firewall Request\"}," + 
            "\"steps\":{\"step\":[{\"name\":\"Erfassung des Antrags\",\"tasks\":{\"task\":{\"fields\":{\"field\":[{\"@xsi.type\": \"multi_access_request\",\"name\": \"Zugang\",\"read_only\": false,\"access_request\":[" +
            "{\"order\": \"AR1\",\"verifier_result\": {\"status\": \"not run\"},\"use_topology\": true,\"targets\": {\"target\": {\"@type\": \"ANY\"}},\"action\": \"accept\"," +
            "\"sources\":{\"source\":[{\"@type\": \"Object\", \"object_name\": \"ARxx12345-100\", \"management_name\": \"CheckpointExt\"}]}," +
            "\"destinations\":{\"destination\":[{\"@type\": \"Object\", \"object_name\": \"ARxx12345-101\", \"management_name\": \"CheckpointExt\"}]}," +
            "\"services\":{\"service\":[{\"@type\": \"PROTOCOL\", \"protocol\": \"TCP\", \"port\": 1000, \"name\": \"Svc1\"}]},\"labels\":\"\",\"comment\": \"\"}]}," +
            "{\"@xsi.type\": \"text_area\",\"name\": \"Grund für den Antrag\",\"read_only\": false,\"text\": \"connection needed\"},{\"@xsi.type\": \"text_field\",\"name\": \"Anwendungs-ID\",\"text\": \"\"},{\"@xsi.type\": \"checkbox\",\"name\": \"hinterlegt\",\"value\": true}]}}}}]}}}}";



        [SetUp]
        public void Initialize()
        {

        }

        [Test]
        public async Task TestSCGrpCreateTicket()
        {
            SCTicket ticket = new (ticketSystem);
            await ticket.CreateRequestString(grpCreateReqTasks, ipProtos, NamingConvention);

            ClassicAssert.AreEqual(GrpCreateFilledTicketText, ticket.TicketText);
        }

        [Test]
        public async Task TestSCAccessTicket()
        {
            SCTicket ticket = new (ticketSystem);
            await ticket.CreateRequestString(accessReqTasks, ipProtos, NamingConvention);

            ClassicAssert.AreEqual(AccessFilledTicketText, ticket.TicketText);
        }
    }
}

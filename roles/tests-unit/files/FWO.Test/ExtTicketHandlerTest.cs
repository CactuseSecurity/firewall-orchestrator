using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Data.Modelling;
using FWO.ExternalSystems.Tufin.SecureChange;
using FWO.Middleware.Server;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ExtTicketHandlerTest
    {
        readonly static ExternalTicketSystem ticketSystem = new()
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
                    TasksTemplate = "{\"order\": \"@@ORDERNAME@@\",\"verifier_result\": {\"status\": \"not run\"},\"use_topology\": true,\"targets\": {\"target\": {\"@type\": \"ANY\"}},\"action\": \"@@ACTION@@\",\"sources\":{\"source\":@@SOURCES@@},\"destinations\":{\"destination\":@@DESTINATIONS@@},\"services\":{\"service\":@@SERVICES@@},\"labels\":\"\",\"comment\": \"@@TASKCOMMENT@@\"}",
                    IpTemplate = "{\"@type\": \"IP\", \"ip_address\": \"@@IP@@\", \"netmask\": \"255.255.255.255\", \"cidr\": 32}",
                    NwObjGroupTemplate = "{\"@type\": \"Object\", \"object_name\": \"@@GROUPNAME@@\", \"management_name\": \"@@MANAGEMENT_NAME@@\"}",
                    ServiceTemplate = "{\"@type\": \"PROTOCOL\", \"protocol\": \"@@PROTOCOLNAME@@\", \"port\": @@PORT@@, \"name\": \"@@SERVICENAME@@\"}",
                    IcmpTemplate = "{\"@type\": \"PROTOCOL\", \"protocol\": \"ICMP\", \"type\": 8, \"name\": \"@@SERVICENAME@@\"}"
                }
            ]
        };
        readonly static string reqPrios = "[{\"numeric_prio\":1,\"name\":\"Highest\",\"ticket_deadline\":1,\"approval_deadline\":1},{\"numeric_prio\":2,\"name\":\"High\",\"ticket_deadline\":3,\"approval_deadline\":2},{\"numeric_prio\":3,\"name\":\"Medium\",\"ticket_deadline\":7,\"approval_deadline\":3},{\"numeric_prio\":4,\"name\":\"Low\",\"ticket_deadline\":14,\"approval_deadline\":7},{\"numeric_prio\":5,\"name\":\"Lowest\",\"ticket_deadline\":30,\"approval_deadline\":14}]";
        readonly static string namingConvention = "{\"networkAreaRequired\":true,\"useAppPart\":true,\"fixedPartLength\":4,\"freePartLength\":3,\"networkAreaPattern\":\"NA\",\"appRolePattern\":\"AR\",\"applicationZone\":\"AZ\",\"appServerPrefix\":\"host_\",\"networkPrefix\":\"net_\",\"ipRangePrefix\":\"range_\"}";

        readonly static List<ExternalTicketSystem> ticketSystemList = [ticketSystem];
        readonly SimulatedUserConfig userConfig = new(){ ExternalRequestWaitCycles = 3,
            ExtTicketSystems = System.Text.Json.JsonSerializer.Serialize(ticketSystemList),
            ReqPriorities = reqPrios,
            ModNamingConvention = namingConvention,
            ModRolloutBundleTasks = true };
        readonly ExtTicketHandlerTestApiConn apiConnection = new();
        static readonly ModellingNamingConvention NamingConvention = new()
        {
            NetworkAreaRequired = true, UseAppPart = false, FixedPartLength = 2, FreePartLength = 5, NetworkAreaPattern = "NA", AppRolePattern = "AR", AppServerPrefix = "net_"
        };
        static readonly List<IpProtocol> ipProtos = [ new(){ Id = 6, Name = "TCP" }];

        readonly static WfReqElement srcASElem = new()
        {
            Id = 1,
            TaskId = 1,
            RequestAction = RequestAction.create.ToString(),
            IpString = "123.0.0.1/32",
            GroupName = "ARxx12345-100",
            Name = "AppServerX",
            Field = ElemFieldType.source.ToString()
        };

        readonly static List<WfReqTask> grpCreateReqTasks = 
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
                Elements = [ srcASElem ]
            }
        ];

        readonly static List<WfReqTask> accessReqTasks = 
        [
            new()
            {
                Id = 2,
                Title = "new Connection",
                TaskNumber = 2,
                TaskType = WfTaskType.access.ToString(),
                TicketId = 123,
                RequestAction = RequestAction.create.ToString(),
                Reason = "connection needed",
                AdditionalInfo = "{\"ConnId\":\"1\"}",
                OnManagement = new(){ Id = 1, Name = "Checkpoint", ExtMgtData = "{\"id\":\"2\",\"name\":\"CheckpointExt\"}"},
                ManagementId = 1,
                Comments = [ new() { Comment = new() { CommentText = "FWOC1, NAT: To: 4.7.1.1" }}],
                Elements = [  ],
                SelectedDevices = "[1]"
            }
        ];


        [SetUp]
        public void Initialize()
        {

        }

        [Test]
        public async Task TestGetWaitCycles()
        {
            SCTicket ticket = new (ticketSystem);
            await ticket.CreateRequestString(grpCreateReqTasks, ipProtos, NamingConvention);
            ExternalRequestHandler extReqHandler = new(userConfig, apiConnection, null);
            ExternalRequest oldRquestGrp = new(){ ExtRequestType = ticket.GetTaskTypeAsString(grpCreateReqTasks[0]), ExtRequestContent = ticket.TicketText};
            ExternalRequest oldRquestAcc = new(){ ExtRequestType = ticket.GetTaskTypeAsString(accessReqTasks[0]), ExtRequestContent = ticket.TicketText};

            ClassicAssert.AreEqual(0, extReqHandler.GetWaitCycles(WfTaskType.access.ToString(), oldRquestAcc));
            ClassicAssert.AreEqual(0, extReqHandler.GetWaitCycles(WfTaskType.group_modify.ToString(), oldRquestAcc));
            ClassicAssert.AreEqual(0, extReqHandler.GetWaitCycles("any", oldRquestAcc));
            ClassicAssert.AreEqual(3, extReqHandler.GetWaitCycles(WfTaskType.access.ToString(), oldRquestGrp));
            ClassicAssert.AreEqual(3, extReqHandler.GetWaitCycles(WfTaskType.group_modify.ToString(), oldRquestGrp));
            ClassicAssert.AreEqual(3, extReqHandler.GetWaitCycles("any", oldRquestGrp));
        }

        [Test]
        public void TestGetLastTaskNumber()
        {
            string extQueryVars = "{\"BundledTasks\":[1,2,3]}";

            ClassicAssert.AreEqual(3, ExternalRequestHandler.GetLastTaskNumber(extQueryVars, 0));
        }

        [Test]
        public async Task TestHandleStateChange()
        {
            ExternalRequestHandler externalRequestHandler = new (userConfig, apiConnection, null);
            ExternalRequest externalRequest = new()
            {
                Id = 1,
                TicketId = 1,
                TaskNumber = 1,
                ExtRequestState = ExtStates.ExtReqRequested.ToString(),
                ExtQueryVariables = ""
            };
            await externalRequestHandler.HandleStateChange(externalRequest);

            ClassicAssert.AreEqual(0, apiConnection.History.Count);

            externalRequest.ExtRequestState = ExtStates.ExtReqDone.ToString();
            externalRequest.ExtTicketId = "4711";
            await externalRequestHandler.HandleStateChange(externalRequest);

            // { appId = , changeType = 11, objectType = 1, objectId = 1, changeText = Implemented Task1 on , changer = Tufin, changeSource = manual }
            ClassicAssert.AreEqual(3, apiConnection.History.Count);
            ClassicAssert.AreEqual(true, apiConnection.History[0].Contains("changeType = 11"));
            ClassicAssert.AreEqual(true, apiConnection.History[0].Contains("Task1"));

            // { appId = , changeType = 10, objectType = 1, objectId = 1, changeText = Requested Task2 on , changer = , changeSource = manual }
            ClassicAssert.AreEqual(true, apiConnection.History[1].Contains("changeType = 10"));
            ClassicAssert.AreEqual(true, apiConnection.History[1].Contains("Task2"));
            ClassicAssert.AreEqual(true, apiConnection.History[2].Contains("changeType = 10"));
            ClassicAssert.AreEqual(true, apiConnection.History[2].Contains("Task3"));

            // { ownerId = , ticketId = 123, taskNumber = 2, extTicketSystem = {"Id":1,"ExternalTicketSystemType":1,"Authorization":"xyz","Name":"Tufin","Url":"https://tufin-test.xxx.de/securechangeworkflow/api/securechange/",
            // "LookupRequesterId":false,"Templates":[{"TaskType":"NetworkObjectModify","TicketTemplate":"{\u0022ticket\u0022:{\u0022subject\u0022:\u0022@@TICKET_SUBJECT@@\u0022,\u0022priority\u0022:\u0022@@PRIORITY@@\u0022,
            // \u0022requester\u0022:\u0022@@ONBEHALF@@\u0022,\u0022domain_name\u0022:\u0022\u0022,\u0022workflow\u0022:{\u0022name\u0022:\u0022Automatische Gruppenerstellung\u0022},\u0022steps\u0022:{\u0022step\u0022:
            // [{\u0022name\u0022:\u0022Erfassung des Antrags\u0022,\u0022tasks\u0022:{\u0022task\u0022:{\u0022fields\u0022:{\u0022field\u0022:[@@TASKS@@]}}}}]}}}","TasksTemplate":"{\u0022@xsi.type\u0022: \u0022multi_group_change\u0022,
            // \u0022name\u0022: \u0022Modify network object group\u0022,\u0022group_change\u0022: {\u0022name\u0022: \u0022@@GROUPNAME@@\u0022,\u0022management_name\u0022: \u0022@@MANAGEMENT_NAME@@\u0022,\u0022members\u0022: {\u0022member\u0022: @@MEMBERS@@},
            // \u0022change_action\u0022: \u0022CREATE\u0022}}","ObjectTemplate":"{\u0022@type\u0022: \u0022@@TYPE@@\u0022, \u0022name\u0022: \u0022@@OBJECTNAME@@\u0022, \u0022object_type\u0022: \u0022@@OBJECT_TYPE@@\u0022, \u0022object_details\u0022: \u0022@@OBJECT_DETAILS@@\u0022, 
            // \u0022status\u0022: \u0022@@STATUS@@\u0022, \u0022comment\u0022: \u0022@@COMMENT@@\u0022, \u0022object_updated_status\u0022: \u0022@@OBJUPDSTATUS@@\u0022, \u0022management_id\u0022: @@MANAGEMENT_ID@@}","ObjectTemplateShort":"{\u0022@type\u0022: \u0022Object\u0022, 
            // \u0022name\u0022: \u0022@@OBJECTNAME@@\u0022, \u0022status\u0022: \u0022@@STATUS@@\u0022, \u0022object_updated_status\u0022: \u0022@@OBJUPDSTATUS@@\u0022}","IpTemplate":"","NwObjGroupTemplate":"","ServiceTemplate":"","IcmpTemplate":""},{"TaskType":"AccessRequest",
            // "TicketTemplate":"{\u0022ticket\u0022:{\u0022subject\u0022:\u0022@@TICKET_SUBJECT@@\u0022,\u0022priority\u0022:\u0022@@PRIORITY@@\u0022,\u0022requester\u0022:\u0022@@ONBEHALF@@\u0022,\u0022domain_name\u0022:\u0022\u0022,\u0022workflow\u0022:
            // {\u0022name\u0022:\u0022Standard Firewall Request\u0022},\u0022steps\u0022:{\u0022step\u0022:[{\u0022name\u0022:\u0022Erfassung des Antrags\u0022,\u0022tasks\u0022:{\u0022task\u0022:{\u0022fields\u0022:{\u0022field\u0022:[{\u0022@xsi.type\u0022: 
            // \u0022multi_access_request\u0022,\u0022name\u0022: \u0022Zugang\u0022,\u0022read_only\u0022: false,\u0022access_request\u0022:[@@TASKS@@]},{\u0022@xsi.type\u0022: \u0022text_area\u0022,\u0022name\u0022: \u0022Grund f\u00FCr den Antrag\u0022,\u0022read_only\u0022: false,
            // \u0022text\u0022: \u0022@@REASON@@\u0022},{\u0022@xsi.type\u0022: \u0022text_field\u0022,\u0022name\u0022: \u0022Anwendungs-ID\u0022,\u0022text\u0022: \u0022@@APPID@@\u0022},{\u0022@xsi.type\u0022: \u0022checkbox\u0022,\u0022name\u0022: \u0022hinterlegt\u0022,\u0022value\u0022: true}]}}}}]}}}}",
            // "TasksTemplate":"{\u0022order\u0022: \u0022@@ORDERNAME@@\u0022,\u0022verifier_result\u0022: {\u0022status\u0022: \u0022not run\u0022},\u0022use_topology\u0022: true,\u0022targets\u0022: {\u0022target\u0022: {\u0022@type\u0022: \u0022ANY\u0022}},\u0022action\u0022: \u0022@@ACTION@@\u0022,
            // \u0022sources\u0022:{\u0022source\u0022:@@SOURCES@@},\u0022destinations\u0022:{\u0022destination\u0022:@@DESTINATIONS@@},\u0022services\u0022:{\u0022service\u0022:@@SERVICES@@},\u0022labels\u0022:\u0022\u0022,\u0022comment\u0022: \u0022@@TASKCOMMENT@@\u0022}","ObjectTemplate":"","ObjectTemplateShort":"",
            // "IpTemplate":"{\u0022@type\u0022: \u0022IP\u0022, \u0022ip_address\u0022: \u0022@@IP@@\u0022, \u0022netmask\u0022: \u0022255.255.255.255\u0022, \u0022cidr\u0022: 32}","NwObjGroupTemplate":"{\u0022@type\u0022: \u0022Object\u0022, \u0022object_name\u0022: \u0022@@GROUPNAME@@\u0022, 
            // \u0022management_name\u0022: \u0022@@MANAGEMENT_NAME@@\u0022}","ServiceTemplate":"{\u0022@type\u0022: \u0022PROTOCOL\u0022, \u0022protocol\u0022: \u0022@@PROTOCOLNAME@@\u0022, \u0022port\u0022: @@PORT@@, \u0022name\u0022: \u0022@@SERVICENAME@@\u0022}","IcmpTemplate":"{\u0022@type\u0022: \u0022PROTOCOL\u0022, 
            // \u0022protocol\u0022: \u0022ICMP\u0022, \u0022type\u0022: 8, \u0022name\u0022: \u0022@@SERVICENAME@@\u0022}"}],"TicketTemplate":"","TasksTemplate":"","ResponseTimeout":300,"MaxAttempts":3,"CyclesBetweenAttempts":5}, extTaskType = (AccessRequest, CREATE), 
            // extTaskContent = {"ticketText":"{\u0022ticket\u0022:{\u0022subject\u0022:\u0022Create Ruleon()\u0022,\u0022priority\u0022:\u0022Low\u0022,\u0022requester\u0022:\u0022\u0022,\u0022domain_name\u0022:\u0022\u0022,\u0022workflow\u0022:{\u0022name\u0022:\u0022Standard Firewall Request\u0022},
            // \u0022steps\u0022:{\u0022step\u0022:[{\u0022name\u0022:\u0022Erfassung des Antrags\u0022,\u0022tasks\u0022:{\u0022task\u0022:{\u0022fields\u0022:{\u0022field\u0022:[{\u0022@xsi.type\u0022: \u0022multi_access_request\u0022,\u0022name\u0022: \u0022Zugang\u0022,\u0022read_only\u0022: false,
            // \u0022access_request\u0022:[{\u0022order\u0022: \u0022AR2\u0022,\u0022verifier_result\u0022: {\u0022status\u0022: \u0022not run\u0022},\u0022use_topology\u0022: true,\u0022targets\u0022: {\u0022target\u0022: {\u0022@type\u0022: \u0022ANY\u0022}},\u0022action\u0022: \u0022accept\u0022,
            // \u0022sources\u0022:{\u0022source\u0022:[{\u0022@type\u0022: \u0022Object\u0022, \u0022object_name\u0022: \u0022ARxx12345-100\u0022, \u0022management_name\u0022: \u0022\u0022}]},\u0022destinations\u0022:{\u0022destination\u0022:[{\u0022@type\u0022: \u0022Object\u0022, 
            // \u0022object_name\u0022: \u0022ARxx12345-101\u0022, \u0022management_name\u0022: \u0022\u0022}]},\u0022services\u0022:{\u0022service\u0022:[{\u0022@type\u0022: \u0022PROTOCOL\u0022, \u0022protocol\u0022: \u0022TCP\u0022, \u0022port\u0022: 1000, \u0022name\u0022: \u0022Svc1\u0022}]},
            // \u0022labels\u0022:\u0022\u0022,\u0022comment\u0022: \u0022\u0022}]},{\u0022@xsi.type\u0022: \u0022text_area\u0022,\u0022name\u0022: \u0022Grund f\u00FCr den Antrag\u0022,\u0022read_only\u0022: false,\u0022text\u0022: \u0022Kommunikationsprofil der Anwendung\u0022},{\u0022@xsi.type\u0022: \u0022text_field\u0022,
            // \u0022name\u0022: \u0022Anwendungs-ID\u0022,\u0022text\u0022: \u0022\u0022},{\u0022@xsi.type\u0022: \u0022checkbox\u0022,\u0022name\u0022: \u0022hinterlegt\u0022,\u0022value\u0022: true}]}}}}]}}}}","TicketId":""}, extQueryVariables = , extRequestState = ExtReqInitialized, waitCycles = 0 }
            ClassicAssert.AreEqual(true, apiConnection.AddExtRequestVars != null);
            ClassicAssert.AreEqual(true, apiConnection.AddExtRequestVars?.Contains("taskNumber = 2"));
            ClassicAssert.AreEqual(true, apiConnection.AddExtRequestVars?.Contains("extQueryVariables = {\"BundledTasks\":[2,3]}"));

            externalRequest.Id = 2;
            externalRequest.TaskNumber = 2;
            externalRequest.ExtQueryVariables = "{\"BundledTasks\":[2,3]}";
            await externalRequestHandler.HandleStateChange(externalRequest);

            ClassicAssert.AreEqual(7, apiConnection.History.Count);
            ClassicAssert.AreEqual(true, apiConnection.History[3].Contains("changeType = 11"));
            ClassicAssert.AreEqual(true, apiConnection.History[3].Contains("Task2"));
            ClassicAssert.AreEqual(true, apiConnection.History[4].Contains("changeType = 11"));
            ClassicAssert.AreEqual(true, apiConnection.History[4].Contains("Task3"));
            ClassicAssert.AreEqual(true, apiConnection.History[5].Contains("changeType = 10"));
            ClassicAssert.AreEqual(true, apiConnection.History[5].Contains("Task4"));
            ClassicAssert.AreEqual(true, apiConnection.History[6].Contains("changeType = 10"));
            ClassicAssert.AreEqual(true, apiConnection.History[6].Contains("Task5"));
            ClassicAssert.AreEqual(true, apiConnection.AddExtRequestVars?.Contains("taskNumber = 4"));
            ClassicAssert.AreEqual(true, apiConnection.AddExtRequestVars?.Contains("extQueryVariables = {\"BundledTasks\":[4,5]}"));
            ClassicAssert.AreEqual(true, apiConnection.AddExtRequestVars?.Contains("AR4"));
            ClassicAssert.AreEqual(false, apiConnection.AddExtRequestVars?.Contains("AR5"));
 
            externalRequest.Id = 3;
            externalRequest.TaskNumber = 4;
            externalRequest.ExtQueryVariables = "{\"BundledTasks\":[4,5]}";
            await externalRequestHandler.HandleStateChange(externalRequest);

            ClassicAssert.AreEqual(12, apiConnection.History.Count);
            ClassicAssert.AreEqual(true, apiConnection.History[7].Contains("changeType = 11"));
            ClassicAssert.AreEqual(true, apiConnection.History[7].Contains("Task4"));
            ClassicAssert.AreEqual(true, apiConnection.History[8].Contains("changeType = 11"));
            ClassicAssert.AreEqual(true, apiConnection.History[8].Contains("Task5"));
            ClassicAssert.AreEqual(true, apiConnection.History[9].Contains("changeType = 10"));
            ClassicAssert.AreEqual(true, apiConnection.History[9].Contains("Task6"));
            ClassicAssert.AreEqual(true, apiConnection.History[10].Contains("changeType = 10"));
            ClassicAssert.AreEqual(true, apiConnection.History[10].Contains("Task7"));
            ClassicAssert.AreEqual(true, apiConnection.History[11].Contains("changeType = 10"));
            ClassicAssert.AreEqual(true, apiConnection.History[11].Contains("Task8"));
            ClassicAssert.AreEqual(true, apiConnection.AddExtRequestVars?.Contains("taskNumber = 6"));
            ClassicAssert.AreEqual(true, apiConnection.AddExtRequestVars?.Contains("extQueryVariables = {\"BundledTasks\":[6,7,8]}"));
            ClassicAssert.AreEqual(true, apiConnection.AddExtRequestVars?.Contains("AR6"));
            ClassicAssert.AreEqual(false, apiConnection.AddExtRequestVars?.Contains("AR7"));
            ClassicAssert.AreEqual(true, apiConnection.AddExtRequestVars?.Contains("AR8"));

            userConfig.ModRolloutBundleTasks = false;
            await externalRequestHandler.HandleStateChange(externalRequest);
            ClassicAssert.AreEqual(15, apiConnection.History.Count);
            ClassicAssert.AreEqual(true, apiConnection.History[12].Contains("changeType = 11"));
            ClassicAssert.AreEqual(true, apiConnection.History[12].Contains("Task4"));
            ClassicAssert.AreEqual(true, apiConnection.History[13].Contains("changeType = 11"));
            ClassicAssert.AreEqual(true, apiConnection.History[13].Contains("Task5"));
            ClassicAssert.AreEqual(true, apiConnection.History[14].Contains("changeType = 10"));
            ClassicAssert.AreEqual(true, apiConnection.History[14].Contains("Task5"));
        }
    }
}

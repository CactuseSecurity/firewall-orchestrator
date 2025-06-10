using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Basics;
using FWO.Data;
using FWO.Middleware.Server;
using FWO.Tufin.SecureChange;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ExtRequestSenderTest
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
        readonly SimulatedGlobalConfig globalConfig = new()
        {
            ExternalRequestWaitCycles = 3,
            ExtTicketSystems = System.Text.Json.JsonSerializer.Serialize(ticketSystemList),
            ReqPriorities = reqPrios,
            ModNamingConvention = namingConvention
        };
        readonly ExtRequestSenderTestApiConn apiConnection = new();

        private string ExceptionMessage = "";


        [SetUp]
        public void Initialize()
        {

        }

		[Test]
		public async Task TestExternalRequestSender()
		{
			try
			{
				SimulatedSCClient simulatedSCClient = new(ticketSystem);
				ExternalRequestSender externalRequestSender = new(apiConnection, globalConfig, simulatedSCClient);
				List<string> FailedRequests = await externalRequestSender.Run();
				if (FailedRequests.Count > 0)
				{
					throw new ProcessingFailedException($"External Request(s) failed: {string.Join(". ", FailedRequests)}.");
				}
			}
			catch (Exception exc)
			{
				ExceptionMessage = exc.Message;
			}

			ClassicAssert.AreEqual(true, ExceptionMessage.Contains("External Request(s) failed:"));
			ClassicAssert.AreEqual(true, ExceptionMessage.Contains("Request Id: 4"));
			ClassicAssert.AreEqual(true, ExceptionMessage.Contains("Request Id: 5"));
			ClassicAssert.AreEqual(3, apiConnection.UpdateExtRequestCreation.Count);
			ClassicAssert.AreEqual(false, apiConnection.UpdateExtRequestCreation[0].Contains("id = 1"));
			ClassicAssert.AreEqual(true, apiConnection.UpdateExtRequestCreation[0].Contains("id = 2"));
			ClassicAssert.AreEqual(true, apiConnection.UpdateExtRequestCreation[1].Contains("id = 3"));
			ClassicAssert.AreEqual(true, apiConnection.UpdateExtRequestCreation[2].Contains("id = 3"));
			ClassicAssert.AreEqual(2, apiConnection.UpdateExtRequestProcess.Count);
			ClassicAssert.AreEqual(2, apiConnection.UpdateExtRequestProcess.Count);
			ClassicAssert.AreEqual(true, apiConnection.UpdateExtRequestProcess[0].Contains("id = 4"));
			ClassicAssert.AreEqual(true, apiConnection.UpdateExtRequestProcess[1].Contains("id = 5"));
			ClassicAssert.AreEqual(3, apiConnection.TriedToGetLdapsForHandleStateChange);
        }
    }
}


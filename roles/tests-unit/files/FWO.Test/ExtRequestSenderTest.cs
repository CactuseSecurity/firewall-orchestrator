using FWO.Basics.Exceptions;
using FWO.Config.Api;
using FWO.Data;
using FWO.ExternalSystems.Tufin.SecureChange;
using FWO.Middleware.Server;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using RestSharp;
using System.Net;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ExtRequestSenderTest
    {
        readonly static ExternalTicketSystem ticketSystem = new()
        {
            Id = 1,
            TypeId = BuiltInExternalTicketSystemTypes.TufinSecureChangeId,
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
            // The fixture owns immutable test data; each test creates its own sender instance.
        }

        [Test]
        public async Task TestExternalRequestSender()
        {
            try
            {
                SimulatedSCClient simulatedSCClient = new(ticketSystem);
                simulatedSCClient.EnqueueResponse("tickets.json", new(new()) { StatusCode = HttpStatusCode.OK, Content = "{\"ticket\": {\"id\": 1, \"status\": \"In Progress\" } }" });
                simulatedSCClient.EnqueueResponse("tickets.json", new(new()) { StatusCode = HttpStatusCode.OK, Content = "{\"ticket\": {\"id\": 2, \"status\": \"In Progress\" } }" });

                simulatedSCClient.EnqueueResponse("tickets/4711", new(new()) { StatusCode = HttpStatusCode.BadRequest, ErrorMessage = "poll failed 4711", Content = "{}" });
                simulatedSCClient.EnqueueResponse("tickets/4712", new(new()) { StatusCode = HttpStatusCode.BadRequest, ErrorMessage = "poll failed 4712", Content = "{}" });

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

            ClassicAssert.IsTrue(ExceptionMessage.Contains("External Request(s) failed:"));
            ClassicAssert.IsTrue(ExceptionMessage.Contains("Request Id: 4"));
            ClassicAssert.IsTrue(ExceptionMessage.Contains("Request Id: 5"));
            ClassicAssert.AreEqual(2, apiConnection.UpdateExtRequestCreation.Count);
            ClassicAssert.IsFalse(apiConnection.UpdateExtRequestCreation[0].Contains("id = 1"));
            ClassicAssert.IsTrue(apiConnection.UpdateExtRequestCreation[0].Contains("id = 2"));
            ClassicAssert.IsTrue(apiConnection.UpdateExtRequestCreation[0].Contains("extTicketId = 1"));
            ClassicAssert.IsTrue(apiConnection.UpdateExtRequestCreation[1].Contains("id = 3"));
            ClassicAssert.IsTrue(apiConnection.UpdateExtRequestCreation[1].Contains("extTicketId = 2"));
            ClassicAssert.AreEqual(2, apiConnection.UpdateExtRequestProcess.Count);
            ClassicAssert.IsTrue(apiConnection.UpdateExtRequestProcess[0].Contains("id = 4"));
            ClassicAssert.IsTrue(apiConnection.UpdateExtRequestProcess[1].Contains("id = 5"));
            // Successful creates are now synced to the workflow immediately so the external ticket id/state is visible on the task.
            ClassicAssert.AreEqual(2, apiConnection.TriedToGetLdapsForHandleStateChange);
        }

        [Test]
        public async Task SendDefaultRequest_SuccessfulCreateTriggersWorkflowSync()
        {
            ExtRequestSenderTestApiConn localApiConnection = new();
            SimulatedSCClient simulatedSCClient = new(ticketSystem);
            simulatedSCClient.EnqueueResponse("tickets.json", new(new())
            {
                StatusCode = HttpStatusCode.OK,
                Content = "{\"ticket\": {\"id\": 1, \"status\": \"In Progress\" } }"
            });
            simulatedSCClient.EnqueueResponse("tickets.json", new(new())
            {
                StatusCode = HttpStatusCode.OK,
                Content = "{\"ticket\": {\"id\": 2, \"status\": \"In Progress\" } }"
            });

            ExternalRequestSender sender = new(localApiConnection, globalConfig, simulatedSCClient);

            await sender.Run();

            ClassicAssert.AreEqual(2, localApiConnection.TriedToGetLdapsForHandleStateChange);
        }

        [Test]
        public void BuildInternalCheckPointTicketNumber_UsesInternalTicketAndTaskReferences()
        {
            UserConfig userConfig = new SimulatedUserConfig();
            ExternalRequest request = new()
            {
                TicketId = 123,
                TaskNumber = 4
            };

            string result = InvokePrivateStatic<string>("BuildInternalCheckPointTicketNumber", userConfig, request);

            ClassicAssert.AreEqual("Internal (Ticket Id: 123, Task Number: 4)", result);
        }

        [Test]
        public async Task TestExternalRequestSenderRejectsUnsupportedCustomSystems()
        {
            ExtRequestSenderTestApiConn localApiConnection = new()
            {
                ManualRequestsOnly = true
            };
            ExternalRequestSender externalRequestSender = new(localApiConnection, globalConfig);

            List<string> FailedRequests = await externalRequestSender.Run();

            ClassicAssert.AreEqual(1, FailedRequests.Count);
            ClassicAssert.AreEqual(0, localApiConnection.UpdateExtRequestCreation.Count);
            ClassicAssert.AreEqual(0, localApiConnection.UpdateExtRequestProcess.Count);
            ClassicAssert.AreEqual(0, localApiConnection.TriedToGetLdapsForHandleStateChange);
        }

        [TestCase("", null)]
        [TestCase("{}", null)]
        [TestCase("{\"ManagementId\":[]}", null)]
        [TestCase("{\"ManagementId\":[23,42]}", 23)]
        public void GetManagementId_ReturnsTheFirstConfiguredManagement(string queryVariables, int? expectedId)
        {
            int? managementId = InvokePrivateStatic<int?>("GetManagementId", queryVariables);

            ClassicAssert.AreEqual(expectedId, managementId);
        }

        [TestCase(null, false)]
        [TestCase("", false)]
        [TestCase("GENERAL_ERROR", true)]
        [TestCase("GENERAL_ERROR Unable to rollback against JDBC Connection", false)]
        [TestCase("ILLEGAL_ARGUMENT_ERROR", true)]
        [TestCase("FIELD_VALIDATION_ERROR", true)]
        [TestCase("WEB_APPLICATION_ERROR", true)]
        [TestCase("implementation failure", true)]
        [TestCase("Check Point rule change tasks are not yet supported.", true)]
        public void AnalyseForRejected_RecognizesPermanentFailures(string? content, bool expected)
        {
            RestResponse<int>? response = content == null ? null : new(new()) { Content = content };

            bool rejected = InvokePrivateStatic<bool>("AnalyseForRejected", response);

            ClassicAssert.AreEqual(expected, rejected);
        }

        [TestCase("{\"ticket\": {\"id\": 4711, \"status\": \"In Progress\" } }", "4711")]
        [TestCase("{\"id\": 4712}", "4712")]
        [TestCase(null, null)]
        [TestCase("", null)]
        [TestCase("{}", null)]
        [TestCase("not-json", null)]
        [TestCase("[1]", null)]
        [TestCase("123", null)]
        [TestCase("\"ok\"", null)]
        [TestCase("true", null)]
        [TestCase("null", null)]
        [TestCase("{\"ticket\": 123}", null)]
        [TestCase("{\"ticket\": [1]}", null)]
        [TestCase("{\"ticket\": \"ok\"}", null)]
        public void ExtractExternalTicketIdFromBody_ReturnsTicketIdWhenPresent(string? content, string? expectedId)
        {
            string? ticketId = InvokePrivateStatic<string?>("ExtractExternalTicketIdFromBody", content);

            ClassicAssert.AreEqual(expectedId, ticketId);
        }

        [Test]
        public void ExtractExternalTicketId_FallsBackToLocationHeader()
        {
            RestResponse<int> response = new(new())
            {
                Content = "{}",
                Headers =
                [
                    new HeaderParameter("location", "https://tufin.example/securechangeworkflow/api/securechange/tickets/4713")
                ]
            };

            string? ticketId = InvokePrivateStatic<string?>("ExtractExternalTicketId", response);

            ClassicAssert.AreEqual("4713", ticketId);
        }

        [Test]
        public void ExtractExternalTicketId_ReturnsNullWithoutBodyTicketIdOrLocationHeader()
        {
            RestResponse<int> response = new(new())
            {
                Content = "{}"
            };

            string? ticketId = InvokePrivateStatic<string?>("ExtractExternalTicketId", response);

            ClassicAssert.IsNull(ticketId);
        }

        [Test]
        public void GetManagementId_RejectsMalformedJson()
        {
            Assert.Throws<TargetInvocationException>(() => InvokePrivateStatic<int?>("GetManagementId", "not-json"));
        }

        private static T InvokePrivateStatic<T>(string methodName, params object?[] parameters)
        {
            MethodInfo method = typeof(ExternalRequestSender).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException($"{methodName} not found.");
            return (T)method.Invoke(null, parameters)!;
        }
    }
}

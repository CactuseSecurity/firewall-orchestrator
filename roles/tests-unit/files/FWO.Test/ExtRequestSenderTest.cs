using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Api.Client.Queries;
using FWO.Basics.Exceptions;
using FWO.Data;
using FWO.Middleware.Server;
using FWO.ExternalSystems.Tufin.SecureChange;
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
            ExceptionMessage = "";
            apiConnection.UpdateExtRequestCreation.Clear();
            apiConnection.UpdateExtRequestProcess.Clear();
            apiConnection.LockOperations.Clear();
            apiConnection.TryLockStatesById.Clear();
            apiConnection.Alerts.Clear();
            apiConnection.AcknowledgedAlertIds.Clear();
            apiConnection.OperationHistory.Clear();
            apiConnection.TriedToGetLdapsForHandleStateChange = 0;
            apiConnection.FailUpdateExtRequestProcessIds.Clear();
            apiConnection.InitiallyLockedRequestIds.Clear();
            apiConnection.ExpiredLockRequestIds.Clear();
            apiConnection.ClosedBeforeLockIds.Clear();
            apiConnection.UnavailableLockIds.Clear();
            apiConnection.OverdueRequestIds.Clear();
            apiConnection.ExistingOpenAlerts.Clear();
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
            ClassicAssert.AreEqual(false, ExceptionMessage.Contains("Request Id: 3"));
            ClassicAssert.AreEqual(true, ExceptionMessage.Contains("Request Id: 4"));
            ClassicAssert.AreEqual(true, ExceptionMessage.Contains("Request Id: 5"));
            ClassicAssert.AreEqual(3, apiConnection.UpdateExtRequestCreation.Count);
            ClassicAssert.AreEqual(false, apiConnection.UpdateExtRequestCreation.Any(update => update.Contains("id = 1")));
            ClassicAssert.AreEqual(true, apiConnection.UpdateExtRequestCreation.Any(update => update.Contains("id = 2")));
            ClassicAssert.AreEqual(2, apiConnection.UpdateExtRequestCreation.Count(update => update.Contains("id = 3")));
            ClassicAssert.AreEqual(2, apiConnection.UpdateExtRequestProcess.Count);
            ClassicAssert.AreEqual(2, apiConnection.UpdateExtRequestProcess.Count);
            ClassicAssert.AreEqual(true, apiConnection.UpdateExtRequestProcess[0].Contains("id = 4"));
            ClassicAssert.AreEqual(true, apiConnection.UpdateExtRequestProcess[1].Contains("id = 5"));
            ClassicAssert.AreEqual(3, apiConnection.TriedToGetLdapsForHandleStateChange);
        }

        [Test]
        public async Task TestExternalRequestSenderDoesNotAdvanceStateWhenPersistFails()
        {
            apiConnection.FailUpdateExtRequestProcessIds = [5];
            SimulatedSCClient simulatedSCClient = new(ticketSystem);
            ExternalRequestSender externalRequestSender = new(apiConnection, globalConfig, simulatedSCClient);

            List<string> failedRequests = await externalRequestSender.Run();

            ClassicAssert.AreEqual(true, failedRequests.Any(req => req.Contains("Request Id: 5")));
            ClassicAssert.AreEqual(false, apiConnection.UpdateExtRequestProcess.Any(update => update.Contains("id = 5") && update.Contains("ExtReqDone")));
        }

        [Test]
        public async Task TestExternalRequestSenderSkipsActiveLockedRequests()
        {
            apiConnection.InitiallyLockedRequestIds = [1];
            SimulatedSCClient simulatedSCClient = new(ticketSystem);
            ExternalRequestSender externalRequestSender = new(apiConnection, globalConfig, simulatedSCClient);

            await externalRequestSender.Run();

            ClassicAssert.AreEqual(false, apiConnection.LockOperations.Any(op => op.Contains("id = 1")));
            ClassicAssert.AreEqual(true, apiConnection.LockOperations.Any(op => op.Contains("id = 2")));
            ClassicAssert.AreEqual(true, apiConnection.LockOperations.Any(op => op.Contains("id = 5")));
        }

        [Test]
        public async Task TestExternalRequestSenderReclaimsExpiredLockedRequests()
        {
            apiConnection.InitiallyLockedRequestIds = [1];
            apiConnection.ExpiredLockRequestIds = [1];
            SimulatedSCClient simulatedSCClient = new(ticketSystem);
            ExternalRequestSender externalRequestSender = new(apiConnection, globalConfig, simulatedSCClient);

            await externalRequestSender.Run();

            ClassicAssert.AreEqual(true, apiConnection.LockOperations.Any(op => op.Contains("id = 1")));
            ClassicAssert.AreEqual(true, apiConnection.LockOperations.Any(op => op.Contains("id = 1") && op.Contains("lockOwner")));
        }

        [Test]
        public async Task TestExternalRequestSenderAlertsOnOverdueRequest()
        {
            apiConnection.OverdueRequestIds = [4];
            SimulatedSCClient simulatedSCClient = new(ticketSystem);
            ExternalRequestSender externalRequestSender = new(apiConnection, globalConfig, simulatedSCClient);

            await externalRequestSender.Run();

            ClassicAssert.AreEqual(true, apiConnection.Alerts.Any(alert => alert.Contains("External request overdue") && alert.Contains("Request Id: 4")));
        }

        [Test]
        public async Task TestExternalRequestSenderDoesNotDuplicateExistingOverdueAlert()
        {
            apiConnection.OverdueRequestIds = [4];
            apiConnection.ExistingOpenAlerts =
            [
                new()
                {
                    Id = 17,
                    AlertCode = AlertCode.ExternalRequest,
                    Title = "External request overdue",
                    Description = "Request Id: 4, Internal TicketId: 4, TaskNo: 0 has been open since 2000-01-01T00:00:00.0000000 in state ExtReqInProgress."
                }
            ];
            SimulatedSCClient simulatedSCClient = new(ticketSystem);
            ExternalRequestSender externalRequestSender = new(apiConnection, globalConfig, simulatedSCClient);

            await externalRequestSender.Run();

            ClassicAssert.AreEqual(0, apiConnection.Alerts.Count);
        }

        [Test]
        public async Task TestExternalRequestSenderAcknowledgesDuplicateExistingOverdueAlerts()
        {
            apiConnection.OverdueRequestIds = [4];
            apiConnection.ExistingOpenAlerts =
            [
                new()
                {
                    Id = 17,
                    AlertCode = AlertCode.ExternalRequest,
                    Title = "External request overdue",
                    Description = "Request Id: 4, Internal TicketId: 4, TaskNo: 0 has been open since 2000-01-01T00:00:00.0000000 in state ExtReqInProgress.",
                    Timestamp = new DateTime(2026, 1, 1)
                },
                new()
                {
                    Id = 18,
                    AlertCode = AlertCode.ExternalRequest,
                    Title = "External request overdue",
                    Description = "Request Id: 4, Internal TicketId: 4, TaskNo: 0 has been open since 2000-01-01T00:00:00.0000000 in state ExtReqInProgress.",
                    Timestamp = new DateTime(2026, 2, 1)
                }
            ];
            SimulatedSCClient simulatedSCClient = new(ticketSystem);
            ExternalRequestSender externalRequestSender = new(apiConnection, globalConfig, simulatedSCClient);

            await externalRequestSender.Run();

            ClassicAssert.AreEqual(0, apiConnection.Alerts.Count);
            ClassicAssert.AreEqual(1, apiConnection.AcknowledgedAlertIds.Count);
            ClassicAssert.AreEqual(17, apiConnection.AcknowledgedAlertIds[0]);
        }

        [Test]
        public async Task TestExternalRequestSenderDoesNotProcessRequestClosedBeforeLock()
        {
            apiConnection.ClosedBeforeLockIds = [2];
            SimulatedSCClient simulatedSCClient = new(ticketSystem);
            ExternalRequestSender externalRequestSender = new(apiConnection, globalConfig, simulatedSCClient);

            await externalRequestSender.Run();

            ClassicAssert.AreEqual(false, apiConnection.UpdateExtRequestCreation.Any(update => update.Contains("id = 2")));
            ClassicAssert.AreEqual(true, apiConnection.LockOperations.Any(op => op.Contains("id = 2")));
            ClassicAssert.AreEqual(true, apiConnection.TryLockStatesById.ContainsKey(2));
            ClassicAssert.AreEqual(true, apiConnection.TryLockStatesById[2].Contains(ExtStates.ExtReqInitialized.ToString()));
            ClassicAssert.AreEqual(true, apiConnection.TryLockStatesById[2].Contains(ExtStates.ExtReqInProgress.ToString()));
            ClassicAssert.AreEqual(false, apiConnection.TryLockStatesById[2].Contains(ExtStates.ExtReqAcknowledged.ToString()));
        }

        [Test]
        public void TestTryLockExternalRequestMutationFiltersByOpenStates()
        {
            string normalizedMutation = ExtRequestQueries.tryLockExternalRequest.ReplaceLineEndings(" ");
            ClassicAssert.AreEqual(true, normalizedMutation.Contains("ext_request_state: { _in: $states }"));
        }

        [Test]
        public void TestGetLockLeaseDurationFallsBackToDefaultOnInvalidTicketSystemJson()
        {
            ExternalRequest request = new()
            {
                Id = 99,
                TicketId = 123,
                ExtTicketSystem = "{invalid json"
            };

            MethodInfo? getLockLeaseDuration = typeof(ExternalRequestSender)
                .GetMethod("GetLockLeaseDuration", BindingFlags.NonPublic | BindingFlags.Static);

            TimeSpan leaseDuration = (TimeSpan)(getLockLeaseDuration?.Invoke(null, [request]) ?? TimeSpan.Zero);

            ClassicAssert.AreEqual(TimeSpan.FromMinutes(15), leaseDuration);
        }
    }
}

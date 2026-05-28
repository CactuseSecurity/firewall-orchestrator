using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.ExternalSystems.CheckPoint;
using FWO.ExternalSystems.Tufin.SecureChange;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using RestSharp;
using System.Net;
using System.Text.Json;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class CheckPointTicketTest
    {
        private readonly ExternalTicketSystem checkPointSystem = new()
        {
            Id = 1,
            TypeId = 9,
            Authorization = "X-chkp-sid: xyz",
            Name = "CheckPoint",
            Url = "https://checkpoint-test.xxx.de/web_api/",
            Templates =
            [
                new()
                {
                    TaskType = SCTaskType.AccessRequest.ToString(),
                    TicketTemplate = "@@TASKS@@",
                    TasksTemplate = "{\"source\":@@SOURCES@@,\"destination\":@@DESTINATIONS@@,\"service\":@@SERVICES@@,\"action\":\"@@ACTION@@\"}"
                },
                new()
                {
                    TaskType = CheckPointTaskTypes.Publish,
                    TicketTemplate = "{}"
                },
                new()
                {
                    TaskType = SCTaskType.NetworkServiceCreate.ToString(),
                    TicketTemplate = "@@TASKS@@",
                    TasksTemplate = "{\"name\":\"@@GROUPNAME@@\",\"members\":@@SERVICES@@}"
                }
            ]
        };

        [Test]
        public async Task CreateRequestStringUsesSecureChangeStyleTemplates()
        {
            CheckPointTicket ticket = new(checkPointSystem);

            await ticket.CreateRequestString([CreateAccessTask()], [new() { Id = 6, Name = "tcp" }], new ModellingNamingConvention());

            ClassicAssert.AreEqual(WfTaskType.access.ToString(), ticket.GetTaskTypeAsString(CreateAccessTask()));
            ClassicAssert.AreEqual("{\"source\":[\"src-group\"],\"destination\":[\"dst-group\"],\"service\":[\"https\"],\"action\":\"accept\"}", ticket.TicketText);
        }

        [Test]
        public async Task CreateRequestStringMapsServiceGroupCreateToServiceTemplate()
        {
            CheckPointTicket ticket = new(checkPointSystem);
            WfReqTask serviceTask = CreateServiceGroupTask();

            await ticket.CreateRequestString([serviceTask], [new() { Id = 6, Name = "tcp" }], new ModellingNamingConvention());

            ClassicAssert.AreEqual(SCTaskType.NetworkServiceCreate.ToString(), ticket.GetTaskTypeAsString(serviceTask));
            ClassicAssert.AreEqual("{\"name\":\"svc-group\",\"members\":[\"https\"]}", ticket.TicketText);
        }

        [Test]
        public async Task CreateExternalTicketSendsServiceGroupCreateToServiceGroupEndpoint()
        {
            SimulatedCheckPointClient checkPointClient = new(checkPointSystem);
            checkPointClient.EnqueueResponse("add-service-group", new(new()) { StatusCode = HttpStatusCode.OK, Content = "{\"uid\":\"svc-group\"}" });
            checkPointClient.EnqueueResponse("publish", new(new()) { StatusCode = HttpStatusCode.OK, Content = "{\"task-id\":\"publish-task\"}" });
            CheckPointTicket ticket = new(checkPointSystem, checkPointClient)
            {
                TicketText = "{\"name\":\"svc-group\",\"members\":[\"https\"]}",
                RequestTaskType = SCTaskType.NetworkServiceCreate.ToString()
            };

            await ticket.CreateExternalTicket();

            ClassicAssert.AreEqual(new List<string> { "add-service-group", "publish" }, checkPointClient.CalledEndpoints);
        }

        [Test]
        public async Task CreateExternalTicketSendsPublishAndInstallPolicyForTaskDevices()
        {
            SimulatedCheckPointClient checkPointClient = new(checkPointSystem);
            checkPointClient.EnqueueResponse("add-access-rule", new(new()) { StatusCode = HttpStatusCode.OK, Content = "{\"uid\":\"rule-1\"}" });
            checkPointClient.EnqueueResponse("publish", new(new()) { StatusCode = HttpStatusCode.OK, Content = "{\"task-id\":\"publish-task\"}" });
            checkPointClient.EnqueueResponse("install-policy", new(new()) { StatusCode = HttpStatusCode.OK, Content = "{\"task-id\":\"install-task\"}" });
            CheckPointTicket ticket = new(checkPointSystem, checkPointClient)
            {
                TicketText = "{\"source\":[\"src-group\"]}",
                RequestTaskType = WfTaskType.access.ToString(),
                ExtQueryVariables = JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    [ExternalVarKeys.CheckPointInstallPolicyTargets] = new List<CheckPointInstallPolicyTarget>
                    {
                        new() { PolicyPackage = "Standard", Targets = ["gw1"] }
                    }
                })
            };

            RestResponse<int> response = await ticket.CreateExternalTicket();

            ClassicAssert.AreEqual(new List<string> { "add-access-rule", "publish", "install-policy" }, checkPointClient.CalledEndpoints);
            ClassicAssert.IsTrue(checkPointClient.RequestBodies.Last().Contains("\"policy-package\":\"Standard\""));
            ClassicAssert.IsTrue(checkPointClient.RequestBodies.Last().Contains("\"gw1\""));
            ClassicAssert.AreEqual(1, checkPointClient.LogoutCalls);
            ClassicAssert.AreEqual("install-task", ticket.GetExternalTicketId(response));
        }

        [Test]
        public async Task CreateExternalTicketPublishesWithoutPublishTemplate()
        {
            ExternalTicketSystem checkPointSystemWithoutSystemTemplates = new()
            {
                Id = 2,
                TypeId = 9,
                Authorization = "X-chkp-sid: xyz",
                Name = "CheckPoint",
                Url = "https://checkpoint-test.xxx.de/web_api/"
            };
            SimulatedCheckPointClient checkPointClient = new(checkPointSystemWithoutSystemTemplates);
            checkPointClient.EnqueueResponse("add-access-rule", new(new()) { StatusCode = HttpStatusCode.OK, Content = "{\"uid\":\"rule-1\"}" });
            checkPointClient.EnqueueResponse("publish", new(new()) { StatusCode = HttpStatusCode.OK, Content = "{\"task-id\":\"publish-task\"}" });
            CheckPointTicket ticket = new(checkPointSystemWithoutSystemTemplates, checkPointClient)
            {
                TicketText = "{\"source\":[\"src-group\"]}",
                RequestTaskType = WfTaskType.access.ToString()
            };

            RestResponse<int> response = await ticket.CreateExternalTicket();

            ClassicAssert.AreEqual(new List<string> { "add-access-rule", "publish" }, checkPointClient.CalledEndpoints);
            ClassicAssert.AreEqual(1, checkPointClient.LogoutCalls);
            ClassicAssert.AreEqual("publish-task", ticket.GetExternalTicketId(response));
        }

        [Test]
        public async Task CreateExternalTicketAcceptsAlreadyExistsAsDone()
        {
            SimulatedCheckPointClient checkPointClient = new(checkPointSystem);
            checkPointClient.EnqueueResponse("add-access-rule", new(new()) { StatusCode = HttpStatusCode.BadRequest, Content = "{\"message\":\"Object already exists\"}" });
            CheckPointTicket ticket = new(checkPointSystem, checkPointClient)
            {
                TicketText = "{\"source\":[\"src-group\"]}",
                RequestTaskType = WfTaskType.access.ToString()
            };

            RestResponse<int> response = await ticket.CreateExternalTicket();

            ClassicAssert.AreEqual(new List<string> { "add-access-rule" }, checkPointClient.CalledEndpoints);
            ClassicAssert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            ClassicAssert.AreEqual(1, checkPointClient.LogoutCalls);
        }

        [Test]
        public async Task GetNewStateMapsSucceededCheckPointTaskToDone()
        {
            SimulatedCheckPointClient checkPointClient = new(checkPointSystem);
            checkPointClient.EnqueueResponse("show-task", new(new()) { StatusCode = HttpStatusCode.OK, Content = "{\"tasks\":[{\"status\":\"succeeded\"}]}" });
            CheckPointTicket ticket = new(checkPointSystem, checkPointClient)
            {
                TicketId = "install-task"
            };

            (string state, string? message) = await ticket.GetNewState(ExtStates.ExtReqRequested.ToString());

            ClassicAssert.AreEqual(ExtStates.ExtReqDone.ToString(), state);
            ClassicAssert.IsTrue(message?.Contains("succeeded"));
            ClassicAssert.AreEqual(1, checkPointClient.LogoutCalls);
        }

        private static WfReqTask CreateAccessTask()
        {
            return new()
            {
                Id = 1,
                TaskNumber = 1,
                TaskType = WfTaskType.access.ToString(),
                Elements =
                [
                    new()
                    {
                        GroupName = "src-group",
                        Field = ElemFieldType.source.ToString()
                    },
                    new()
                    {
                        GroupName = "dst-group",
                        Field = ElemFieldType.destination.ToString()
                    },
                    new()
                    {
                        Name = "https",
                        ProtoId = 6,
                        Port = 443,
                        Field = ElemFieldType.service.ToString()
                    }
                ]
            };
        }

        private static WfReqTask CreateServiceGroupTask()
        {
            return new()
            {
                Id = 2,
                TaskNumber = 2,
                TaskType = WfTaskType.group_create.ToString(),
                AdditionalInfo = "{\"GrpName\":\"svc-group\"}",
                Elements =
                [
                    new()
                    {
                        Name = "https",
                        ProtoId = 6,
                        Port = 443,
                        Field = ElemFieldType.service.ToString()
                    }
                ]
            };
        }
    }
}

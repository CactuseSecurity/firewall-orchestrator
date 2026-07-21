using FWO.Basics.Exceptions;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.ExternalSystems.CheckPoint;
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
        private static readonly string[] kExpectedExistingHostEndpoints = ["add-host", "show-host", "publish"];
        private static readonly string[] kExpectedRetriedHostEndpoints = ["add-host", "show-host", "add-host"];
        private static readonly string[] kExpectedExistingNetworkEndpoints = ["add-network", "show-network", "publish"];

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
                    TaskType = WfTaskType.group_create.ToString(),
                    TicketTemplate = "@@TASKS@@",
                    TasksTemplate = "{\"name\":\"@@GROUPNAME@@\",\"members\":@@MEMBERS@@}"
                },
                new()
                {
                    TaskType = CheckPointTaskTypes.Publish,
                    TicketTemplate = "{}"
                }
            ]
        };

        [Test]
        public async Task CreateRequestStringForGroupCreateBuildsDeltaExecutionPlan()
        {
            CheckPointTicket ticket = new(checkPointSystem);

            await ticket.CreateRequestString([CreateGroupCreateTaskWithNewHostMember()], [], new ModellingNamingConvention());

            using JsonDocument document = JsonDocument.Parse(ticket.TicketText);
            List<JsonElement> planSteps = [.. document.RootElement.GetProperty("Steps").EnumerateArray()];

            ClassicAssert.AreEqual(4, planSteps.Count);

            ClassicAssert.AreEqual(CheckPointTaskTypes.GroupCreate, planSteps[0].GetProperty("TaskType").GetString());
            ClassicAssert.AreEqual(CheckPointTaskTypes.HostCreate, planSteps[1].GetProperty("TaskType").GetString());
            ClassicAssert.AreEqual(CheckPointTaskTypes.GroupAddMembers, planSteps[2].GetProperty("TaskType").GetString());
            ClassicAssert.AreEqual(CheckPointTaskTypes.Publish, planSteps[3].GetProperty("TaskType").GetString());

            JsonElement createGroupBody = planSteps[0].GetProperty("Body");
            ClassicAssert.AreEqual("cp-group", createGroupBody.GetProperty("name").GetString());
            ClassicAssert.IsFalse(createGroupBody.TryGetProperty("members", out _));

            JsonElement hostBody = planSteps[1].GetProperty("Body");
            ClassicAssert.AreEqual("member-host", hostBody.GetProperty("name").GetString());
            ClassicAssert.AreEqual("10.0.0.1", hostBody.GetProperty("ip-address").GetString());

            JsonElement addMemberBody = planSteps[2].GetProperty("Body");
            ClassicAssert.AreEqual("cp-group", addMemberBody.GetProperty("name").GetString());
            JsonElement members = addMemberBody.GetProperty("members");
            ClassicAssert.AreEqual("member-host", members.GetProperty("add")[0].GetString());
        }

        [Test]
        public async Task CreateExternalTicketRetriesGroupMemberObjectCreationWithIgnoreWarnings()
        {


            ExternalTicketSystem retryCheckPointSystem = new()
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
                TaskType = WfTaskType.group_create.ToString(),
                TicketTemplate = "@@TASKS@@",
                TasksTemplate = "{\"name\":\"@@GROUPNAME@@\",\"members\":@@MEMBERS@@}"
            },
            new()
            {
                TaskType = CheckPointTaskTypes.Publish,
                TicketTemplate = "{}"
            }
                ]
            };

            Management management = new()
            {
                Id = 1,
                Name = "cp-mgmt",
                Hostname = "checkpoint-test.xxx.de",
                Port = 443,
                ExportCredential = new ImportCredential("tester", "secret")
            };

            SimulatedCheckPointClient checkPointClient = new(retryCheckPointSystem, management);
            checkPointClient.EnqueueResponse("add-group", new(new())
            {
                StatusCode = HttpStatusCode.OK,
                Content = "{\"uid\":\"group-1\"}"
            });
            checkPointClient.EnqueueResponse("publish", new(new())
            {
                StatusCode = HttpStatusCode.OK,
                Content = "{}"
            });
            checkPointClient.EnqueueResponse("add-host", new(new())
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = "{\"message\":\"multiple IP addresses are allowed only for DNS domains\"}"
            });
            checkPointClient.EnqueueResponse("add-host", new(new())
            {
                StatusCode = HttpStatusCode.OK,
                Content = "{\"uid\":\"host-1\"}"
            });
            checkPointClient.EnqueueResponse("publish", new(new())
            {
                StatusCode = HttpStatusCode.OK,
                Content = "{}"
            });
            checkPointClient.EnqueueResponse("set-group", new(new())
            {
                StatusCode = HttpStatusCode.OK,
                Content = "{\"uid\":\"group-1\"}"
            });
            checkPointClient.EnqueueResponse("publish", new(new())
            {
                StatusCode = HttpStatusCode.OK,
                Content = "{}"
            });
            checkPointClient.EnqueueResponse("publish", new(new())
            {
                StatusCode = HttpStatusCode.OK,
                Content = "{}"
            });

            CheckPointTicket ticket = new(retryCheckPointSystem, checkPointClient)
            {
                OnManagement = management
            };

            await ticket.CreateRequestString([CreateGroupCreateTaskWithNewHostMember()], [], new ModellingNamingConvention());

            RestResponse<int> response = await ticket.CreateExternalTicket();

            ClassicAssert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            CollectionAssert.AreEqual(
                ExpectedGroupCreateRetryEndpoints,
                checkPointClient.CalledEndpoints);

            StringAssert.Contains("\"ignore-warnings\":true", checkPointClient.RequestBodies[3] ?? "");
            StringAssert.Contains("\"name\":\"member-host\"", checkPointClient.RequestBodies[3] ?? "");
            ClassicAssert.AreEqual(1, checkPointClient.LogoutCalls);
        }

        [Test]
        public async Task CreateRequestStringForGroupModifyBuildsAddAndRemoveExecutionPlan()
        {
            ExternalTicketSystem groupModifyCheckPointSystem = new()
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
                TaskType = WfTaskType.group_modify.ToString(),
                TicketTemplate = "@@TASKS@@",
                TasksTemplate = "{\"name\":\"@@GROUPNAME@@\",\"members\":@@MEMBERS@@}"
            },
            new()
            {
                TaskType = CheckPointTaskTypes.Publish,
                TicketTemplate = "{}"
            }
                ]
            };

            CheckPointTicket ticket = new(groupModifyCheckPointSystem);

            await ticket.CreateRequestString([CreateGroupModifyTask()], [], new ModellingNamingConvention());

            using JsonDocument document = JsonDocument.Parse(ticket.TicketText);
            List<JsonElement> planSteps = [.. document.RootElement.GetProperty("Steps").EnumerateArray()];

            ClassicAssert.AreEqual(4, planSteps.Count);

            ClassicAssert.AreEqual(CheckPointTaskTypes.HostCreate, planSteps[0].GetProperty("TaskType").GetString());
            ClassicAssert.AreEqual(CheckPointTaskTypes.GroupAddMembers, planSteps[1].GetProperty("TaskType").GetString());
            ClassicAssert.AreEqual(CheckPointTaskTypes.GroupRemoveMembers, planSteps[2].GetProperty("TaskType").GetString());
            ClassicAssert.AreEqual(CheckPointTaskTypes.Publish, planSteps[3].GetProperty("TaskType").GetString());

            JsonElement addMemberBody = planSteps[1].GetProperty("Body");
            ClassicAssert.AreEqual("cp-group", addMemberBody.GetProperty("name").GetString());
            ClassicAssert.AreEqual("member-add", addMemberBody.GetProperty("members").GetProperty("add")[0].GetString());

            JsonElement removeMemberBody = planSteps[2].GetProperty("Body");
            ClassicAssert.AreEqual("cp-group", removeMemberBody.GetProperty("name").GetString());
            ClassicAssert.AreEqual("member-remove", removeMemberBody.GetProperty("members").GetProperty("remove")[0].GetString());
        }

        [Test]
        public async Task CreateExternalTicketLoadsPlanAndSkipsExistingGroup()
        {
            Management management = CreateManagement();
            SimulatedCheckPointClient checkPointClient = new(checkPointSystem, management);
            checkPointClient.EnqueueResponse("add-group", ErrorResponse("More than one object has the same name"));
            checkPointClient.EnqueueResponse("show-group", OkResponse("{\"name\":\"cp-group\"}"));
            checkPointClient.EnqueueResponse("publish", OkResponse("{}"));
            CheckPointTicket ticket = CreateTicketWithPlan(
                checkPointClient,
                management,
                "{\"Steps\":[" +
                $"{{\"TaskType\":\"{CheckPointTaskTypes.GroupCreate}\",\"Body\":{{\"name\":\"cp-group\"}}}}," +
                $"{{\"TaskType\":\"{CheckPointTaskTypes.Publish}\",\"Body\":{{}}}}" +
                "]}");

            RestResponse<int> response = await ticket.CreateExternalTicket();

            ClassicAssert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            CollectionAssert.AreEqual(ExpectedExistingGroupSkipEndpoints, checkPointClient.CalledEndpoints);
            ClassicAssert.AreEqual(1, checkPointClient.LogoutCalls);
        }

        [Test]
        public async Task CreateExternalTicketSkipsExistingNetworkWithSubnetMask()
        {
            Management management = CreateManagement();
            SimulatedCheckPointClient checkPointClient = new(checkPointSystem, management);
            checkPointClient.EnqueueResponse("add-network", ErrorResponse("More than one network has the same subnet"));
            checkPointClient.EnqueueResponse("show-network", OkResponse("{\"subnet4\":\"10.0.0.0\",\"subnet-mask\":\"255.255.255.0\"}"));
            checkPointClient.EnqueueResponse("publish", OkResponse("{}"));
            CheckPointTicket ticket = CreateTicketWithPlan(
                checkPointClient,
                management,
                "{\"Steps\":[" +
                $"{{\"TaskType\":\"{CheckPointTaskTypes.NetworkCreate}\",\"Body\":{{\"name\":\"net\",\"subnet\":\"10.0.0.0\",\"mask-length\":24}}}}," +
                $"{{\"TaskType\":\"{CheckPointTaskTypes.Publish}\",\"Body\":{{}}}}" +
                "]}");

            RestResponse<int> response = await ticket.CreateExternalTicket();

            ClassicAssert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            CollectionAssert.AreEqual(ExpectedExistingNetworkSkipEndpoints, checkPointClient.CalledEndpoints);
        }

        [Test]
        public async Task CreateExternalTicketSkipsExistingAddressRange()
        {
            Management management = CreateManagement();
            SimulatedCheckPointClient checkPointClient = new(checkPointSystem, management);
            checkPointClient.EnqueueResponse("add-address-range", ErrorResponse("More than one address range has the same IP"));
            checkPointClient.EnqueueResponse("show-address-range", OkResponse("{\"ipv4-address-first\":\"10.0.0.1\",\"ipv4-address-last\":\"10.0.0.10\"}"));
            checkPointClient.EnqueueResponse("publish", OkResponse("{}"));
            CheckPointTicket ticket = CreateTicketWithPlan(
                checkPointClient,
                management,
                "{\"Steps\":[" +
                $"{{\"TaskType\":\"{CheckPointTaskTypes.AddressRangeCreate}\",\"Body\":{{\"name\":\"range\",\"ip-address-first\":\"10.0.0.1\",\"ip-address-last\":\"10.0.0.10\"}}}}," +
                $"{{\"TaskType\":\"{CheckPointTaskTypes.Publish}\",\"Body\":{{}}}}" +
                "]}");

            RestResponse<int> response = await ticket.CreateExternalTicket();

            ClassicAssert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            CollectionAssert.AreEqual(ExpectedExistingAddressRangeSkipEndpoints, checkPointClient.CalledEndpoints);
        }

        [Test]
        public async Task CreateExternalTicketReturnsBadRequestForRuleChangeTask()
        {
            Management management = CreateManagement();
            SimulatedCheckPointClient checkPointClient = new(checkPointSystem, management);
            CheckPointTicket ticket = CreateTicketWithPlan(
                checkPointClient,
                management,
                """
                {"Steps":[{"TaskType":"access","Body":{}}]}
                """);

            RestResponse<int> response = await ticket.CreateExternalTicket();

            ClassicAssert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            ClassicAssert.AreEqual("Check Point rule change tasks are not yet supported.", response.Content);
            ClassicAssert.IsEmpty(checkPointClient.CalledEndpoints);
        }

        [Test]
        public void CreateExternalTicketRejectsInvalidExecutionPlan()
        {
            Management management = CreateManagement();
            CheckPointTicket ticket = CreateTicketWithPlan(
                new SimulatedCheckPointClient(checkPointSystem, management),
                management,
                "{not-json");

            ProcessingFailedException exception = Assert.ThrowsAsync<ProcessingFailedException>(ticket.CreateExternalTicket)!;

            ClassicAssert.AreEqual("Invalid CheckPoint request content format.", exception.Message);
        }

        [Test]
        public async Task CreateExternalTicketReturnsHardErrorResponse()
        {
            Management management = CreateManagement();
            SimulatedCheckPointClient checkPointClient = new(checkPointSystem, management);
            checkPointClient.EnqueueResponse("add-host", ErrorResponse("permission denied"));
            CheckPointTicket ticket = CreateTicketWithPlan(
                checkPointClient,
                management,
                "{\"Steps\":[" +
                $"{{\"TaskType\":\"{CheckPointTaskTypes.HostCreate}\",\"Body\":{{\"name\":\"host\",\"ip-address\":\"10.0.0.1\"}}}}" +
                "]}");

            RestResponse<int> response = await ticket.CreateExternalTicket();

            ClassicAssert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            CollectionAssert.AreEqual(ExpectedHardErrorEndpoints, checkPointClient.CalledEndpoints);
            ClassicAssert.AreEqual(1, checkPointClient.LogoutCalls);
        }

        [Test]
        public async Task CreateExternalTicketSkipsExistingHostWithMatchingAddress()
        {
            Management management = CreateManagement();
            SimulatedCheckPointClient checkPointClient = new(checkPointSystem, management);
            checkPointClient.EnqueueResponse("add-host", ErrorResponse("More than one host has the same IP"));
            checkPointClient.EnqueueResponse("show-host", OkResponse("{\"ipv4-address\":\"10.0.0.1\"}"));
            checkPointClient.EnqueueResponse("publish", OkResponse("{}"));
            CheckPointTicket ticket = CreateTicketWithPlan(checkPointClient, management,
                $"{{\"Steps\":[{{\"TaskType\":\"{CheckPointTaskTypes.HostCreate}\",\"Body\":{{\"name\":\"host\",\"ip-address\":\"10.0.0.1/32\"}}}},{{\"TaskType\":\"{CheckPointTaskTypes.Publish}\",\"Body\":{{}}}}]}}");

            RestResponse<int> response = await ticket.CreateExternalTicket();

            ClassicAssert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            CollectionAssert.AreEqual(kExpectedExistingHostEndpoints, checkPointClient.CalledEndpoints);
        }

        [Test]
        public async Task CreateExternalTicketRetriesWhenExistingObjectDoesNotMatch()
        {
            Management management = CreateManagement();
            SimulatedCheckPointClient checkPointClient = new(checkPointSystem, management);
            checkPointClient.EnqueueResponse("add-host", ErrorResponse("multiple IP addresses"));
            checkPointClient.EnqueueResponse("show-host", OkResponse("{\"ipv4-address\":\"10.0.0.9\"}"));
            checkPointClient.EnqueueResponse("add-host", OkResponse("{}"));
            CheckPointTicket ticket = CreateTicketWithPlan(checkPointClient, management,
                $"{{\"Steps\":[{{\"TaskType\":\"{CheckPointTaskTypes.HostCreate}\",\"Body\":{{\"name\":\"host\",\"ip-address\":\"10.0.0.1\"}}}}]}}");

            RestResponse<int> response = await ticket.CreateExternalTicket();

            ClassicAssert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            CollectionAssert.AreEqual(kExpectedRetriedHostEndpoints, checkPointClient.CalledEndpoints);
            StringAssert.Contains("\"ignore-warnings\":true", checkPointClient.RequestBodies[^1] ?? "");
        }

        [Test]
        public async Task CreateExternalTicketSkipsExistingIpv6NetworkWithNumericPrefix()
        {
            Management management = CreateManagement();
            SimulatedCheckPointClient checkPointClient = new(checkPointSystem, management);
            checkPointClient.EnqueueResponse("add-network", ErrorResponse("More than one network has the same subnet"));
            checkPointClient.EnqueueResponse("show-network", OkResponse("{\"subnet6\":\"2001:db8::\",\"mask-length6\":\"64\"}"));
            checkPointClient.EnqueueResponse("publish", OkResponse("{}"));
            CheckPointTicket ticket = CreateTicketWithPlan(checkPointClient, management,
                $"{{\"Steps\":[{{\"TaskType\":\"{CheckPointTaskTypes.NetworkCreate}\",\"Body\":{{\"name\":\"net6\",\"subnet\":\"2001:db8::\",\"mask-length\":64}}}},{{\"TaskType\":\"{CheckPointTaskTypes.Publish}\",\"Body\":{{}}}}]}}");

            RestResponse<int> response = await ticket.CreateExternalTicket();

            ClassicAssert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            CollectionAssert.AreEqual(kExpectedExistingNetworkEndpoints, checkPointClient.CalledEndpoints);
        }

        [Test]
        public void CreateExternalTicketRejectsEmptyAndUnknownExecutionPlans()
        {
            Management management = CreateManagement();
            CheckPointTicket emptyPlan = CreateTicketWithPlan(new SimulatedCheckPointClient(checkPointSystem, management), management, "{\"Steps\":[]}");
            CheckPointTicket missingContent = CreateTicketWithPlan(new SimulatedCheckPointClient(checkPointSystem, management), management, "");

            ProcessingFailedException emptyException = Assert.ThrowsAsync<ProcessingFailedException>(emptyPlan.CreateExternalTicket)!;
            ProcessingFailedException missingException = Assert.ThrowsAsync<ProcessingFailedException>(missingContent.CreateExternalTicket)!;

            ClassicAssert.AreEqual("CheckPoint request content has no executable steps.", emptyException.Message);
            ClassicAssert.AreEqual("CheckPoint request content missing.", missingException.Message);
        }

        private static WfReqTask CreateGroupModifyTask()
        {
            return new()
            {
                Id = 2,
                TaskNumber = 2,
                TaskType = WfTaskType.group_modify.ToString(),
                AdditionalInfo = "{\"GrpName\":\"cp-group\"}",
                Elements =
                [
                    new()
            {
                Name = "member-add",
                Field = ElemFieldType.source.ToString(),
                IpString = "10.0.0.2/32",
                RequestAction = RequestAction.create.ToString()
            },
            new()
            {
                Name = "member-remove",
                Field = ElemFieldType.source.ToString(),
                IpString = "10.0.0.3/32",
                RequestAction = RequestAction.delete.ToString()
            }
                ]
            };
        }

        private static WfReqTask CreateGroupCreateTaskWithNewHostMember()
        {
            return new()
            {
                Id = 1,
                TaskNumber = 1,
                TaskType = WfTaskType.group_create.ToString(),
                AdditionalInfo = "{\"GrpName\":\"cp-group\"}",
                Elements =
                [
                    new()
                    {
                        Name = "member-host",
                        Field = ElemFieldType.source.ToString(),
                        IpString = "10.0.0.1/32",
                        RequestAction = RequestAction.create.ToString()
                    }
                ]
            };
        }

        private static readonly string[] ExpectedGroupCreateRetryEndpoints =
            [
                "add-group",
                "add-host",
                "show-host",
                "add-host",
                "set-group",
                "publish"
            ];

        private static readonly string[] ExpectedExistingGroupSkipEndpoints =
            [
                "add-group",
                "show-group",
                "publish"
            ];

        private static readonly string[] ExpectedExistingNetworkSkipEndpoints =
            [
                "add-network",
                "show-network",
                "publish"
            ];

        private static readonly string[] ExpectedExistingAddressRangeSkipEndpoints =
            [
                "add-address-range",
                "show-address-range",
                "publish"
            ];

        private static readonly string[] ExpectedHardErrorEndpoints =
            [
                "add-host",
                "show-host"
            ];

        private CheckPointTicket CreateTicketWithPlan(SimulatedCheckPointClient checkPointClient, Management management, string ticketText)
        {
            return new CheckPointTicket(checkPointSystem, checkPointClient)
            {
                OnManagement = management,
                TicketText = ticketText
            };
        }

        private static Management CreateManagement()
        {
            return new()
            {
                Id = 1,
                Name = "cp-mgmt",
                Hostname = "checkpoint-test.xxx.de",
                Port = 443,
                ExportCredential = new ImportCredential("tester", "secret")
            };
        }

        private static RestResponse<int> OkResponse(string content)
        {
            return new(new())
            {
                StatusCode = HttpStatusCode.OK,
                ResponseStatus = ResponseStatus.Completed,
                Content = content
            };
        }

        private static RestResponse<int> ErrorResponse(string content)
        {
            return new(new())
            {
                StatusCode = HttpStatusCode.BadRequest,
                ResponseStatus = ResponseStatus.Completed,
                Content = content
            };
        }
    }
}

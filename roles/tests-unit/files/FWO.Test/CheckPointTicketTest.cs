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

            ClassicAssert.AreEqual(7, planSteps.Count);

            ClassicAssert.AreEqual(CheckPointTaskTypes.GroupCreate, planSteps[0].GetProperty("TaskType").GetString());
            ClassicAssert.AreEqual(CheckPointTaskTypes.Publish, planSteps[1].GetProperty("TaskType").GetString());
            ClassicAssert.AreEqual(CheckPointTaskTypes.HostCreate, planSteps[2].GetProperty("TaskType").GetString());
            ClassicAssert.AreEqual(CheckPointTaskTypes.Publish, planSteps[3].GetProperty("TaskType").GetString());
            ClassicAssert.AreEqual(CheckPointTaskTypes.GroupAddMembers, planSteps[4].GetProperty("TaskType").GetString());
            ClassicAssert.AreEqual(CheckPointTaskTypes.Publish, planSteps[5].GetProperty("TaskType").GetString());
            ClassicAssert.AreEqual(CheckPointTaskTypes.Publish, planSteps[6].GetProperty("TaskType").GetString());

            JsonElement createGroupBody = planSteps[0].GetProperty("Body");
            ClassicAssert.AreEqual("cp-group", createGroupBody.GetProperty("name").GetString());
            ClassicAssert.IsFalse(createGroupBody.TryGetProperty("members", out _));

            JsonElement hostBody = planSteps[2].GetProperty("Body");
            ClassicAssert.AreEqual("member-host", hostBody.GetProperty("name").GetString());
            ClassicAssert.AreEqual("10.0.0.1", hostBody.GetProperty("ip-address").GetString());

            JsonElement addMemberBody = planSteps[4].GetProperty("Body");
            ClassicAssert.AreEqual("cp-group", addMemberBody.GetProperty("name").GetString());
            JsonElement members = addMemberBody.GetProperty("members");
            ClassicAssert.AreEqual("member-host", members.GetProperty("add")[0].GetString());
        }
      
        [Test]
        public async Task CreateExternalTicketRetriesGroupMemberObjectCreationWithIgnoreWarnings()
        {
            ExternalTicketSystem checkPointSystem = new()
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

            SimulatedCheckPointClient checkPointClient = new(checkPointSystem, management);
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

            CheckPointTicket ticket = new(checkPointSystem, checkPointClient)
            {
                OnManagement = management
            };

            await ticket.CreateRequestString([CreateGroupCreateTaskWithNewHostMember()], [], new ModellingNamingConvention());

            RestResponse<int> response = await ticket.CreateExternalTicket();

            ClassicAssert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            CollectionAssert.AreEqual(
                new[] { "add-group", "publish", "add-host","show-host", "add-host", "publish", "set-group", "publish", "publish" },
                checkPointClient.CalledEndpoints);

            StringAssert.Contains("\"ignore-warnings\":true", checkPointClient.RequestBodies[4] ?? "");
            StringAssert.Contains("\"name\":\"member-host\"", checkPointClient.RequestBodies[4] ?? "");
            ClassicAssert.AreEqual(1, checkPointClient.LogoutCalls);
        }

        [Test]
        public async Task CreateRequestStringForGroupModifyBuildsAddAndRemoveExecutionPlan()
        {
            ExternalTicketSystem checkPointSystem = new()
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

            CheckPointTicket ticket = new(checkPointSystem);

            await ticket.CreateRequestString([CreateGroupModifyTask()], [], new ModellingNamingConvention());

            using JsonDocument document = JsonDocument.Parse(ticket.TicketText);
            List<JsonElement> planSteps = [.. document.RootElement.GetProperty("Steps").EnumerateArray()];

            ClassicAssert.AreEqual(CheckPointTaskTypes.HostCreate, planSteps[0].GetProperty("TaskType").GetString());
            ClassicAssert.AreEqual(CheckPointTaskTypes.Publish, planSteps[1].GetProperty("TaskType").GetString());
            ClassicAssert.AreEqual(CheckPointTaskTypes.GroupAddMembers, planSteps[2].GetProperty("TaskType").GetString());
            ClassicAssert.AreEqual(CheckPointTaskTypes.Publish, planSteps[3].GetProperty("TaskType").GetString());
            ClassicAssert.AreEqual(CheckPointTaskTypes.GroupRemoveMembers, planSteps[4].GetProperty("TaskType").GetString());
            ClassicAssert.AreEqual(CheckPointTaskTypes.Publish, planSteps[5].GetProperty("TaskType").GetString());

            JsonElement addMemberBody = planSteps[2].GetProperty("Body");
            ClassicAssert.AreEqual("cp-group", addMemberBody.GetProperty("name").GetString());
            ClassicAssert.AreEqual("member-add", addMemberBody.GetProperty("members").GetProperty("add")[0].GetString());

            JsonElement removeMemberBody = planSteps[4].GetProperty("Body");
            ClassicAssert.AreEqual("cp-group", removeMemberBody.GetProperty("name").GetString());
            ClassicAssert.AreEqual("member-remove", removeMemberBody.GetProperty("members").GetProperty("remove")[0].GetString());
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
    }
}

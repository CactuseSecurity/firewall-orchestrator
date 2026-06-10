using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.ExternalSystems.CheckPoint;
using NUnit.Framework;
using NUnit.Framework.Legacy;
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
        [Ignore("temp disabled")]
        public async Task CreateRequestStringForGroupCreateBuildsHostAndGroupExecutionPlan()
        {
            CheckPointTicket ticket = new(checkPointSystem);

            await ticket.CreateRequestString([CreateGroupCreateTaskWithNewHostMember()], [], new ModellingNamingConvention());

            using JsonDocument document = JsonDocument.Parse(ticket.TicketText);
            JsonElement.ArrayEnumerator steps = document.RootElement.GetProperty("Steps").EnumerateArray();
            List<JsonElement> planSteps = [.. steps];

            ClassicAssert.AreEqual(4, planSteps.Count);
            ClassicAssert.AreEqual(CheckPointTaskTypes.HostCreate, planSteps[0].GetProperty("TaskType").GetString());
            ClassicAssert.AreEqual(CheckPointTaskTypes.Publish, planSteps[1].GetProperty("TaskType").GetString());
            ClassicAssert.AreEqual(WfTaskType.group_create.ToString(), planSteps[2].GetProperty("TaskType").GetString());
            ClassicAssert.AreEqual(CheckPointTaskTypes.Publish, planSteps[3].GetProperty("TaskType").GetString());

            JsonElement memberBody = planSteps[0].GetProperty("Body");
            ClassicAssert.AreEqual("member-host", memberBody.GetProperty("name").GetString());
            ClassicAssert.AreEqual("10.0.0.1", memberBody.GetProperty("ip-address").GetString());

            JsonElement groupBody = planSteps[2].GetProperty("Body");
            ClassicAssert.AreEqual("cp-group", groupBody.GetProperty("name").GetString());
            ClassicAssert.AreEqual(1, groupBody.GetProperty("members").GetArrayLength());
            ClassicAssert.AreEqual("member-host", groupBody.GetProperty("members")[0].GetString());
        }

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
        [Ignore("temp disabled")]
        public async Task CreateExternalTicketRetriesHostCreateWithIgnoreWarningsForMultipleIpResponse()
        {
            //SimulatedCheckPointClient checkPointClient = new(checkPointSystem);
            //checkPointClient.EnqueueResponse("add-host", new(new())
            //{
            //    StatusCode = HttpStatusCode.BadRequest,
            //    Content = "{\"message\":\"multiple IP addresses are allowed only for DNS domains\"}"
            //});
            //checkPointClient.EnqueueResponse("add-host", new(new()) { StatusCode = HttpStatusCode.OK, Content = "{\"uid\":\"host-1\"}" });
            //checkPointClient.EnqueueResponse("publish", new(new()) { StatusCode = HttpStatusCode.OK, Content = "{}" });
            //checkPointClient.EnqueueResponse("add-group", new(new()) { StatusCode = HttpStatusCode.OK, Content = "{\"uid\":\"group-1\"}" });
            //checkPointClient.EnqueueResponse("publish", new(new()) { StatusCode = HttpStatusCode.OK, Content = "{}" });

            //CheckPointTicket ticket = new(checkPointSystem, checkPointClient);
            //await ticket.CreateRequestString([CreateGroupCreateTaskWithNewHostMember()], [], new ModellingNamingConvention());

            //await ticket.CreateExternalTicket();

            //ClassicAssert.AreEqual(new List<string> { "add-host", "add-host", "publish", "add-group", "publish" }, checkPointClient.CalledEndpoints);
            //ClassicAssert.AreEqual("{\"name\":\"member-host\",\"ip-address\":\"10.0.0.1\"}", checkPointClient.RequestBodies[0]);
            //StringAssert.Contains("\"ignore-warnings\":true", checkPointClient.RequestBodies[1]);
            //StringAssert.Contains("\"name\":\"member-host\"", checkPointClient.RequestBodies[1]);
            //StringAssert.Contains("\"ip-address\":\"10.0.0.1\"", checkPointClient.RequestBodies[1]);
            //ClassicAssert.AreEqual(1, checkPointClient.LogoutCalls);
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

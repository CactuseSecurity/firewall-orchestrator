using FWO.Basics;
using FWO.Basics.Exceptions;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.ExternalSystems.CheckPoint;
using NUnit.Framework;
using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class CheckPointTicketTaskTest
    {
        private static readonly string[] kExpectedAddedMembers = ["A-host", "z-host"];
        private static readonly string[] kExpectedRemovedMembers = ["delete-host"];
        private static readonly string[] kExpectedRequiredMemberObjectNames = ["z-host"];
        private static readonly string[] kExpectedRequiredMemberNames = ["host", "range"];

        [Test]
        public void FillTaskText_UsesExternalManagementDataAndTaskPlaceholders()
        {
            WfReqTask task = CreateTask(WfTaskType.group_create, "create");
            task.TaskNumber = 7;
            task.Reason = "Requested change";
            task.OnManagement = new Management
            {
                Name = "local-management",
                ExtMgtData = "{\"id\":\"external-12\",\"name\":\"external-management\"}"
            };
            CheckPointTicketTask ticketTask = new(task, [], new ModellingNamingConvention());
            ExternalTicketTemplate template = new()
            {
                TasksTemplate = "{\"order\":\"@@ORDERNAME@@\",\"comment\":\"@@TASKCOMMENT@@\",\"reason\":\"@@REASON@@\",\"action\":\"@@ACTION@@\",\"change\":\"@@CHANGEACTION@@\",\"group\":\"@@GROUPNAME@@\",\"managementId\":\"@@MANAGEMENT_ID@@\",\"managementName\":\"@@MANAGEMENT_NAME@@\",\"members\":@@MEMBERS@@}"
            };

            ticketTask.FillTaskText(template);

            using JsonDocument document = JsonDocument.Parse(ticketTask.TaskText);
            Assert.Multiple(() =>
            {
                Assert.That(document.RootElement.GetProperty("order").GetString(), Is.EqualTo("AR7"));
                Assert.That(document.RootElement.GetProperty("action").GetString(), Is.EqualTo(RuleActions.Accept));
                Assert.That(document.RootElement.GetProperty("change").GetString(), Is.EqualTo("CREATE"));
                Assert.That(document.RootElement.GetProperty("managementId").GetString(), Is.EqualTo("external-12"));
                Assert.That(document.RootElement.GetProperty("managementName").GetString(), Is.EqualTo("external-management"));
                Assert.That(document.RootElement.GetProperty("members").GetArrayLength(), Is.Zero);
            });
        }

        [TestCase(WfTaskType.rule_delete, RuleActions.Drop, "UPDATE")]
        [TestCase(WfTaskType.group_delete, RuleActions.Accept, "DELETE")]
        [TestCase(WfTaskType.group_modify, RuleActions.Accept, "UPDATE")]
        public void FillTaskText_MapsTaskActions(WfTaskType taskType, string expectedAction, string expectedChange)
        {
            CheckPointTicketTask ticketTask = new(CreateTask(taskType, "create"), [], null);
            ExternalTicketTemplate template = new() { TicketTemplate = "{\"action\":\"@@ACTION@@\",\"change\":\"@@CHANGEACTION@@\"}" };

            ticketTask.FillTaskText(template);

            Assert.That(ticketTask.TaskBody["action"]!.GetValue<string>(), Is.EqualTo(expectedAction));
            Assert.That(ticketTask.TaskBody["change"]!.GetValue<string>(), Is.EqualTo(expectedChange));
        }

        [Test]
        public void FillTaskText_UsesFallbackManagementDataAndRejectsInvalidTemplates()
        {
            WfReqTask task = CreateTask(WfTaskType.group_modify, "create");
            task.ManagementId = 42;
            task.OnManagement = new Management { Name = "fallback-management", ExtMgtData = "invalid" };
            CheckPointTicketTask ticketTask = new(task, [], null);

            ticketTask.FillTaskText(new() { TicketTemplate = "{\"id\":\"@@MANAGEMENT_ID@@\",\"name\":\"@@MANAGEMENT_NAME@@\"}" });

            Assert.Multiple(() =>
            {
                Assert.That(ticketTask.TaskBody["id"]!.GetValue<string>(), Is.EqualTo("42"));
                Assert.That(ticketTask.TaskBody["name"]!.GetValue<string>(), Is.EqualTo("fallback-management"));
                Assert.Throws<ConfigException>(() => ticketTask.FillTaskText(new()));
                Assert.Throws<ConfigException>(() => ticketTask.FillTaskText(new() { TicketTemplate = "not-json" }));
            });
        }

        [Test]
        public void MemberRendering_FiltersNonMembersAndCreatesSortedDistinctDeltas()
        {
            WfReqTask task = CreateTask(WfTaskType.group_create, "create");
            task.Elements =
            [
                Element("z-host", "10.0.0.1/32", "create"),
                Element("A-host", "10.0.0.2/32", "unchanged"),
                Element("a-host", "10.0.0.2/32", "addAfterCreation"),
                Element("delete-host", "10.0.0.3/32", "delete"),
                new WfReqElement { Name = "ignored-service", Field = ElemFieldType.service.ToString(), RequestAction = "create" },
                new WfReqElement { Name = "ignored-rule", Field = ElemFieldType.rule.ToString(), RequestAction = "create" }
            ];
            CheckPointTicketTask ticketTask = new(task, [], null);

            List<string> additions = InvokeStringList(ticketTask, "GetMembersToAdd");
            List<string> removals = InvokeStringList(ticketTask, "GetMembersToRemove");
            List<object> objects = InvokeObjectList(ticketTask, "GetRequiredMemberObjectSteps");

            Assert.Multiple(() =>
            {
                Assert.That(additions, Is.EqualTo(kExpectedAddedMembers));
                Assert.That(removals, Is.EqualTo(kExpectedRemovedMembers));
                Assert.That(objects.Select(GetObjectName), Is.EqualTo(kExpectedRequiredMemberObjectNames));
                Assert.That(InvokeJson(ticketTask, "RenderEmptyGroupCreateBody")["name"]!.GetValue<string>(), Is.EqualTo("cp-group"));
                Assert.That(InvokeJson(ticketTask, "RenderGroupMembersAddBody", additions)["members"]!["add"]!.AsArray().Count, Is.EqualTo(2));
                Assert.That(InvokeJson(ticketTask, "RenderGroupMembersRemoveBody", removals)["members"]!["remove"]![0]!.GetValue<string>(), Is.EqualTo("delete-host"));
            });
        }

        [Test]
        public void RequiredMemberObjectSteps_CreateHostNetworkAndRangeAndRejectMissingAddress()
        {
            WfReqTask task = CreateTask(WfTaskType.group_modify, "create");
            task.Elements =
            [
                Element("host", "10.0.0.1/32", "create"),
                Element("network", "10.0.1.0/24", "modify"),
                new WfReqElement { Name = "range", Field = ElemFieldType.source.ToString(), IpString = "10.0.2.1", IpEnd = "10.0.2.9", RequestAction = "create" }
            ];
            CheckPointTicketTask ticketTask = new(task, [], null);

            List<object> objects = InvokeObjectList(ticketTask, "GetRequiredMemberObjectSteps");

            Assert.Multiple(() =>
            {
                Assert.That(objects.Select(GetObjectType), Is.EqualTo(new[] { ObjectType.Host, ObjectType.Network, ObjectType.IPRange }));
                Assert.That(InvokeStringList(ticketTask, "GetMembersToAdd"), Is.EqualTo(kExpectedRequiredMemberNames));
            });

            task.Elements = [new WfReqElement { Name = "missing", Field = ElemFieldType.source.ToString(), RequestAction = "create" }];
            Assert.Throws<TargetInvocationException>(() => InvokeObjectList(ticketTask, "GetRequiredMemberObjectSteps"));
        }

        private static WfReqTask CreateTask(WfTaskType taskType, string action)
        {
            return new WfReqTask
            {
                TaskType = taskType.ToString(),
                TaskNumber = 1,
                AdditionalInfo = "{\"GrpName\":\"cp-group\"}",
                Elements = [Element("host", "10.0.0.1/32", action)]
            };
        }

        private static WfReqElement Element(string name, string ip, string action)
        {
            return new WfReqElement
            {
                Name = name,
                Field = ElemFieldType.source.ToString(),
                IpString = ip,
                RequestAction = action
            };
        }

        private static List<string> InvokeStringList(CheckPointTicketTask ticketTask, string methodName)
        {
            return ((IEnumerable)Invoke(ticketTask, methodName)).Cast<object>().Cast<string>().ToList();
        }

        private static List<object> InvokeObjectList(CheckPointTicketTask ticketTask, string methodName)
        {
            return ((IEnumerable)Invoke(ticketTask, methodName)).Cast<object>().ToList();
        }

        private static JsonNode InvokeJson(CheckPointTicketTask ticketTask, string methodName, params object[] parameters)
        {
            return (JsonNode)Invoke(ticketTask, methodName, parameters);
        }

        private static object Invoke(CheckPointTicketTask ticketTask, string methodName, params object[] parameters)
        {
            MethodInfo method = typeof(CheckPointTicketTask).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"{methodName} not found.");
            return method.Invoke(ticketTask, parameters)!;
        }

        private static string GetObjectName(object request)
        {
            return (string)request.GetType().GetProperty("Name")!.GetValue(request)!;
        }

        private static string GetObjectType(object request)
        {
            return (string)request.GetType().GetProperty("NetworkObjectType")!.GetValue(request)!;
        }
    }
}

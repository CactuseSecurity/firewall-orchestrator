using FWO.Basics;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.ExternalSystems.CheckPoint;
using FWO.ExternalSystems.Tufin.SecureChange;
using NUnit.Framework;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FWO.Test
{
    /// <summary>
    /// Requester text placed into external ticket templates must stay a JSON string value: it may neither break the
    /// request nor add fields to it.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class ExternalTicketJsonEscapingTest
    {
        private const string kInjectedKey = "injected";
        // closes the string, adds a field and carries a backslash, a tab and another control character
        private const string kPayload = "x\", \"injected\": \"1 \\ \t\u0001 end";
        private const string kMultiLinePayload = "line one\r\nline \"two\"";
        private static readonly ExternalCertificateChecks CertificateChecks = new(FirewallConnections: true, TicketSystems: true);
        private static readonly List<IpProtocol> kIpProtos = [new() { Id = 6, Name = "TCP" }];
        private static readonly List<IpProtocol> kNoIpProtos = [];
        private static readonly ModellingNamingConvention kNamingConvention = new() { AppServerPrefix = "net_" };

        [Test]
        public void ToJsonStringContent_EscapesQuotesBackslashesAndControlCharacters()
        {
            string json = "{\"value\":\"" + kPayload.ToJsonStringContent() + "\"}";

            JsonNode root = JsonNode.Parse(json)!;

            Assert.That(root["value"]!.GetValue<string>(), Is.EqualTo(kPayload));
            Assert.That(root.AsObject().ContainsKey(kInjectedKey), Is.False);
        }

        [Test]
        public void ToJsonStringContent_TurnsLineBreaksIntoBlanksAndKeepsNonAsciiText()
        {
            Assert.That(kMultiLinePayload.ToJsonStringContent(), Is.EqualTo("line one  line \\\"two\\\""));
            Assert.That("Grund für den Antrag".ToJsonStringContent(), Is.EqualTo("Grund für den Antrag"));
            Assert.That(((string?)null).ToJsonStringContent(), Is.Empty);
        }

        [Test]
        public void ToJsonStringContent_EscapesAtSignsSoValuesCarryNoPlaceholders()
        {
            Assert.That("a@@TASKS@@b".ToJsonStringContent(), Is.EqualTo("a\\u0040\\u0040TASKS\\u0040\\u0040b"));
        }

        [Test]
        public async Task SCAccessTicket_KeepsPlaceholderTokensInRequesterTextAsText()
        {
            const string kTokens = "see @@TASKS@@ and @@SOURCES@@";
            WfReqTask task = CreateAccessTask();
            task.Reason = kTokens;
            task.Comments = [new() { Comment = new() { CommentText = kTokens } }];
            List<WfReqTask> tasks = [task];
            SCTicket ticket = new(CreateSecureChangeSystem(), CertificateChecks);

            await ticket.CreateRequestString(tasks, kIpProtos, kNamingConvention);

            JsonNode root = JsonNode.Parse(ticket.TicketText)!;
            JsonArray fields = root["ticket"]!["steps"]!["step"]![0]!["tasks"]!["task"]!["fields"]!["field"]!.AsArray();
            Assert.That(fields[1]!["text"]!.GetValue<string>(), Is.EqualTo(kTokens));
            Assert.That(fields[0]!["access_request"]![0]!["comment"]!.GetValue<string>(), Is.EqualTo(kTokens));
        }

        [Test]
        public void ReplaceJsonStringPlaceholder_InsertsEscapedValue()
        {
            string json = "{\"a\":\"@@REASON@@\",\"b\":\"@@REASON@@\"}".ReplaceJsonStringPlaceholder(Placeholder.REASON, kPayload);

            JsonNode root = JsonNode.Parse(json)!;

            Assert.That(root["a"]!.GetValue<string>(), Is.EqualTo(kPayload));
            Assert.That(root["b"]!.GetValue<string>(), Is.EqualTo(kPayload));
        }

        [Test]
        public async Task SCAccessTicket_KeepsRequesterTextInsideItsStrings()
        {
            SCTicket ticket = new(CreateSecureChangeSystem(), CertificateChecks)
            {
                Subject = kPayload,
                Priority = kPayload,
                Requester = kPayload
            };

            List<WfReqTask> tasks = [CreateAccessTask()];

            await ticket.CreateRequestString(tasks, kIpProtos, kNamingConvention);

            JsonNode root = JsonNode.Parse(ticket.TicketText)!;
            JsonNode ticketNode = root["ticket"]!;
            JsonArray fields = ticketNode["steps"]!["step"]![0]!["tasks"]!["task"]!["fields"]!["field"]!.AsArray();
            JsonNode accessRequest = fields[0]!["access_request"]![0]!;
            Assert.Multiple(() =>
            {
                AssertNoInjectedProperty(root);
                Assert.That(ticketNode["subject"]!.GetValue<string>(), Is.EqualTo(kPayload));
                Assert.That(ticketNode["priority"]!.GetValue<string>(), Is.EqualTo(kPayload));
                Assert.That(ticketNode["requester"]!.GetValue<string>(), Is.EqualTo(kPayload));
                Assert.That(fields[1]!["text"]!.GetValue<string>(), Is.EqualTo(kPayload));
                Assert.That(accessRequest["comment"]!.GetValue<string>(), Is.EqualTo(kPayload));
                Assert.That(accessRequest["sources"]!["source"]![0]!["object_name"]!.GetValue<string>(), Is.EqualTo(kPayload));
                Assert.That(accessRequest["services"]!["service"]![0]!["name"]!.GetValue<string>(), Is.EqualTo(kPayload));
                Assert.That(accessRequest["services"]!["service"]![1]!["name"]!.GetValue<string>(), Is.EqualTo(kPayload));
            });
        }

        [Test]
        public async Task SCGroupCreateTicket_KeepsGroupAndObjectNamesInsideTheirStrings()
        {
            SCTicket ticket = new(CreateSecureChangeSystem(), CertificateChecks);

            List<WfReqTask> tasks = [CreateGroupCreateTask()];

            await ticket.CreateRequestString(tasks, kIpProtos, kNamingConvention);

            JsonNode root = JsonNode.Parse(ticket.TicketText)!;
            JsonNode groupChange = root["ticket"]!["steps"]!["step"]![0]!["tasks"]!["task"]!["fields"]!["field"]![0]!["group_change"]!;
            JsonNode member = groupChange["members"]!["member"]![0]!;
            Assert.Multiple(() =>
            {
                AssertNoInjectedProperty(root);
                Assert.That(groupChange["name"]!.GetValue<string>(), Does.Contain("\"injected\""));
                Assert.That(member["name"]!.GetValue<string>(), Does.Contain("\"injected\""));
            });
        }

        [Test]
        public void CheckPointTask_KeepsRequesterTextInsideItsStrings()
        {
            WfReqTask task = CreateGroupCreateTask();
            CheckPointTicketTask ticketTask = new(task, kNoIpProtos, kNamingConvention);
            ExternalTicketTemplate template = new()
            {
                TasksTemplate = "{\"comment\":\"@@TASKCOMMENT@@\",\"reason\":\"@@REASON@@\",\"group\":\"@@GROUPNAME@@\",\"managementName\":\"@@MANAGEMENT_NAME@@\",\"members\":@@MEMBERS@@}"
            };

            ticketTask.FillTaskText(template);

            Assert.Multiple(() =>
            {
                AssertNoInjectedProperty(ticketTask.TaskBody);
                Assert.That(ticketTask.TaskBody["comment"]!.GetValue<string>(), Is.EqualTo(kPayload));
                Assert.That(ticketTask.TaskBody["reason"]!.GetValue<string>(), Is.EqualTo(kPayload));
                Assert.That(ticketTask.TaskBody["group"]!.GetValue<string>(), Is.EqualTo(kPayload));
                Assert.That(ticketTask.TaskBody["managementName"]!.GetValue<string>(), Is.EqualTo(kPayload));
            });
        }

        [Test]
        public async Task CheckPointTicket_KeepsRequesterTextInsideItsStrings()
        {
            CheckPointTicket ticket = new(CreateCheckPointSystem(), CertificateChecks)
            {
                Subject = kPayload,
                Requester = kPayload
            };

            List<WfReqTask> tasks = [CreateGroupCreateTask()];

            await ticket.CreateRequestString(tasks, kNoIpProtos, kNamingConvention);

            JsonNode root = JsonNode.Parse(ticket.TicketText)!;
            JsonNode publishBody = FindObjectWithProperty(root, "publishReason")
                ?? throw new AssertionException($"publish body not found in {ticket.TicketText}");
            Assert.Multiple(() =>
            {
                AssertNoInjectedProperty(root);
                Assert.That(publishBody["publishReason"]!.GetValue<string>(), Is.EqualTo(kPayload));
                Assert.That(publishBody["subject"]!.GetValue<string>(), Is.EqualTo(kPayload));
                Assert.That(publishBody["requester"]!.GetValue<string>(), Is.EqualTo(kPayload));
                Assert.That(publishBody["group"]!.GetValue<string>(), Is.EqualTo(kPayload));
            });
        }

        private static ExternalTicketSystem CreateSecureChangeSystem()
        {
            return new()
            {
                Id = 1,
                TypeId = BuiltInExternalTicketSystemTypes.TufinSecureChangeId,
                Name = "Tufin",
                Url = "https://tufin-test.example/securechangeworkflow/api/securechange/",
                Templates =
                [
                    new()
                    {
                        TaskType = SCTaskType.NetworkObjectModify.ToString(),
                        TicketTemplate = "{\"ticket\":{\"subject\":\"@@TICKET_SUBJECT@@\",\"priority\":\"@@PRIORITY@@\",\"requester\":\"@@ONBEHALF@@\",\"steps\":{\"step\":[{\"tasks\":{\"task\":{\"fields\":{\"field\":[@@TASKS@@]}}}}]}}}",
                        TasksTemplate = "{\"group_change\": {\"name\": \"@@GROUPNAME@@\",\"management_name\": \"@@MANAGEMENT_NAME@@\",\"members\": {\"member\": @@MEMBERS@@},\"change_action\": \"@@CHANGEACTION@@\"}}",
                        ObjectTemplate = "{\"@type\": \"@@TYPE@@\", \"name\": \"@@OBJECTNAME@@\", \"object_type\": \"@@OBJECT_TYPE@@\", \"object_details\": \"@@OBJECT_DETAILS@@\", \"status\": \"@@STATUS@@\", \"comment\": \"@@COMMENT@@\", \"object_updated_status\": \"@@OBJUPDSTATUS@@\", \"management_id\": @@MANAGEMENT_ID@@}",
                        ObjectTemplateShort = "{\"name\": \"@@OBJECTNAME@@\", \"status\": \"@@STATUS@@\"}"
                    },
                    new()
                    {
                        TaskType = SCTaskType.AccessRequest.ToString(),
                        TicketTemplate = "{\"ticket\":{\"subject\":\"@@TICKET_SUBJECT@@\",\"priority\":\"@@PRIORITY@@\",\"requester\":\"@@ONBEHALF@@\",\"steps\":{\"step\":[{\"tasks\":{\"task\":{\"fields\":{\"field\":[{\"access_request\":[@@TASKS@@]},{\"text\": \"@@REASON@@\"},{\"text\": \"@@APPID@@\"}]}}}}]}}}",
                        TasksTemplate = "{\"order\": \"@@ORDERNAME@@\",\"action\": \"@@ACTION@@\",\"sources\":{\"source\":@@SOURCES@@},\"destinations\":{\"destination\":@@DESTINATIONS@@},\"services\":{\"service\":@@SERVICES@@},\"comment\": \"@@TASKCOMMENT@@\"}",
                        IpTemplate = "{\"@type\": \"IP\", \"ip_address\": \"@@IP@@\"}",
                        NwObjGroupTemplate = "{\"@type\": \"Object\", \"object_name\": \"@@GROUPNAME@@\", \"management_name\": \"@@MANAGEMENT_NAME@@\"}",
                        ServiceTemplate = "{\"@type\": \"PROTOCOL\", \"protocol\": \"@@PROTOCOLNAME@@\", \"port\": @@PORT@@, \"name\": \"@@SERVICENAME@@\"}",
                        IcmpTemplate = "{\"@type\": \"PROTOCOL\", \"protocol\": \"ICMP\", \"name\": \"@@SERVICENAME@@\"}"
                    }
                ]
            };
        }

        private static ExternalTicketSystem CreateCheckPointSystem()
        {
            return new()
            {
                Id = 2,
                TypeId = 9,
                Name = "CheckPoint",
                Url = "https://checkpoint-test.example/web_api/",
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
                        TicketTemplate = "{\"publishReason\":\"@@REASON@@\",\"subject\":\"@@TICKET_SUBJECT@@\",\"requester\":\"@@REQUESTER@@\",\"group\":\"@@GROUPNAME@@\"}"
                    }
                ]
            };
        }

        private static WfReqTask CreateAccessTask()
        {
            return new()
            {
                Id = 1,
                TaskNumber = 1,
                TaskType = WfTaskType.access.ToString(),
                RequestAction = RequestAction.create.ToString(),
                Reason = kPayload,
                AdditionalInfo = "{\"ConnId\":\"1\"}",
                OnManagement = new() { Id = 1, Name = "Checkpoint", ExtMgtData = "{\"id\":\"2\",\"name\":\"CheckpointExt\"}" },
                Comments = [new() { Comment = new() { CommentText = kPayload } }],
                Elements =
                [
                    new() { Id = 1, RequestAction = RequestAction.create.ToString(), GroupName = kPayload, Field = ElemFieldType.source.ToString() },
                    new() { Id = 2, RequestAction = RequestAction.create.ToString(), IpString = "10.0.0.1/32", Field = ElemFieldType.destination.ToString() },
                    new() { Id = 3, RequestAction = RequestAction.create.ToString(), Name = kPayload, Port = 1000, ProtoId = 6, Field = ElemFieldType.service.ToString() },
                    new() { Id = 4, RequestAction = RequestAction.create.ToString(), Name = kPayload, ProtoId = 1, Field = ElemFieldType.service.ToString() }
                ]
            };
        }

        private static WfReqTask CreateGroupCreateTask()
        {
            return new()
            {
                Id = 2,
                TaskNumber = 2,
                TaskType = WfTaskType.group_create.ToString(),
                RequestAction = RequestAction.create.ToString(),
                Reason = kPayload,
                AdditionalInfo = JsonSerializer.Serialize(new Dictionary<string, string> { ["GrpName"] = kPayload }),
                OnManagement = new() { Id = 1, Name = kPayload, ExtMgtData = JsonSerializer.Serialize(new Dictionary<string, string> { ["id"] = "2", ["name"] = kPayload }) },
                Comments = [new() { Comment = new() { CommentText = kPayload } }],
                Elements =
                [
                    new()
                    {
                        Id = 5,
                        RequestAction = RequestAction.create.ToString(),
                        Name = "host" + kPayload,
                        IpString = "10.0.0.1/32",
                        Field = ElemFieldType.source.ToString()
                    }
                ]
            };
        }

        private static void AssertNoInjectedProperty(JsonNode? node)
        {
            if (node is JsonObject jsonObject)
            {
                Assert.That(jsonObject.ContainsKey(kInjectedKey), Is.False, jsonObject.ToJsonString());
                foreach (KeyValuePair<string, JsonNode?> property in jsonObject)
                {
                    AssertNoInjectedProperty(property.Value);
                }
            }
            else if (node is JsonArray jsonArray)
            {
                foreach (JsonNode? element in jsonArray)
                {
                    AssertNoInjectedProperty(element);
                }
            }
        }

        private static JsonNode? FindObjectWithProperty(JsonNode? node, string propertyName)
        {
            if (node is JsonObject jsonObject)
            {
                if (jsonObject.ContainsKey(propertyName))
                {
                    return jsonObject;
                }
                return jsonObject.Select(property => FindObjectWithProperty(property.Value, propertyName)).FirstOrDefault(found => found != null);
            }
            if (node is JsonArray jsonArray)
            {
                return jsonArray.Select(element => FindObjectWithProperty(element, propertyName)).FirstOrDefault(found => found != null);
            }
            return null;
        }
    }
}

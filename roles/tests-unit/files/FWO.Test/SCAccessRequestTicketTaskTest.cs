using FWO.Data;
using FWO.Data.Workflow;
using FWO.ExternalSystems.Tufin.SecureChange;
using FWO.Basics;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class SCAccessRequestTicketTaskTest
    {
        private static readonly List<IpProtocol> kIpProtos =
        [
            new() { Id = 0, Name = "HOPOPT" },
            new() { Id = 1, Name = "ICMP" },
            new() { Id = 6, Name = "TCP" },
            new() { Id = 17, Name = "UDP" },
            new() { Id = 47, Name = "GRE" }
        ];

        private static ExternalTicketTemplate Template() => new()
        {
            TasksTemplate = "{\"order\":\"@@ORDERNAME@@\",\"action\":\"@@ACTION@@\",\"sources\":@@SOURCES@@,\"destinations\":@@DESTINATIONS@@,\"services\":@@SERVICES@@,\"comment\":\"@@TASKCOMMENT@@\"}",
            IpTemplate = "{\"ip\":\"@@IP@@\"}",
            NwObjGroupTemplate = "{\"group\":\"@@GROUPNAME@@\",\"mgt\":\"@@MANAGEMENT_NAME@@\"}",
            ServiceTemplate = "{\"proto\":\"@@PROTOCOLNAME@@\",\"port\":\"@@PORT@@\",\"name\":\"@@SERVICENAME@@\"}",
            IcmpTemplate = "{\"icmp\":\"@@SERVICENAME@@\"}",
            IpProtocolTemplate = "{\"proto\":\"@@PROTOCOLNAME@@\",\"id\":\"@@PROTOCOLID@@\",\"name\":\"@@SERVICENAME@@\"}"
        };

        /// <summary>
        /// Verifies network elements are sorted, groups are deduplicated and single IPs use the IP template.
        /// </summary>
        [Test]
        public void FillTaskTextSortsNetworkElementsAndDeduplicatesGroups()
        {
            WfReqTask reqTask = new()
            {
                TaskNumber = 7,
                TaskType = WfTaskType.access.ToString(),
                OnManagement = new() { Id = 1, Name = "Mgt", ExtMgtData = "{\"id\":\"2\",\"name\":\"MgtExt\"}" },
                Comments = [new() { Comment = new() { CommentText = "task comment" } }],
                Elements =
                [
                    new() { Id = 1, Field = ElemFieldType.source.ToString(), GroupName = "GroupB" },
                    new() { Id = 2, Field = ElemFieldType.source.ToString(), GroupName = "GroupB" },
                    new() { Id = 3, Field = ElemFieldType.source.ToString(), Name = "alpha", Cidr = new Cidr("10.0.0.2/32") },
                    new() { Id = 4, Field = ElemFieldType.source.ToString(), Cidr = new Cidr("10.0.0.1/32") },
                    new() { Id = 5, Field = ElemFieldType.destination.ToString(), Cidr = new Cidr("198.51.100.20/32") }
                ]
            };
            SCAccessRequestTicketTask ticketTask = new(reqTask, kIpProtos);

            ticketTask.FillTaskText(Template());

            int ipIndex = ticketTask.TaskText.IndexOf("10.0.0.1");
            int namedIndex = ticketTask.TaskText.IndexOf("10.0.0.2");
            int groupIndex = ticketTask.TaskText.IndexOf("GroupB");
            Assert.Multiple(() =>
            {
                Assert.That(ticketTask.TaskText, Does.Contain("\"order\":\"AR7\""));
                Assert.That(ticketTask.TaskText, Does.Contain("\"action\":\"accept\""));
                Assert.That(ticketTask.TaskText, Does.Contain("\"comment\":\"task comment\""));
                Assert.That(ticketTask.TaskText, Does.Contain("\"mgt\":\"MgtExt\""));
                Assert.That(ipIndex, Is.LessThan(namedIndex), "single IP sorts before named object");
                Assert.That(namedIndex, Is.LessThan(groupIndex), "named object sorts before group");
                Assert.That(CountOccurrences(ticketTask.TaskText, "GroupB"), Is.EqualTo(1), "duplicate group is converted once");
            });
        }

        /// <summary>
        /// Verifies each protocol kind is converted with its dedicated service template.
        /// </summary>
        [Test]
        public void FillTaskTextConvertsAllServiceProtocolVariants()
        {
            WfReqTask reqTask = new()
            {
                TaskNumber = 8,
                TaskType = WfTaskType.rule_modify.ToString(),
                Elements =
                [
                    new() { Id = 1, Field = ElemFieldType.service.ToString(), Name = "ping", ProtoId = 1 },
                    new() { Id = 2, Field = ElemFieldType.service.ToString(), Name = "https", ProtoId = 6, Port = 443, PortEnd = 443 },
                    new() { Id = 3, Field = ElemFieldType.service.ToString(), Name = "dns-range", ProtoId = 17, Port = 100, PortEnd = 200 },
                    new() { Id = 4, Field = ElemFieldType.service.ToString(), Name = "tunnel", ProtoId = 47 },
                    new() { Id = 5, Field = ElemFieldType.service.ToString(), Name = "unknown-proto", ProtoId = 99 },
                    new() { Id = 6, Field = ElemFieldType.service.ToString(), Name = "hopopt", ProtoId = 0 },
                    new() { Id = 7, Field = ElemFieldType.service.ToString(), Name = "any-service", ProtoId = null }
                ]
            };
            SCAccessRequestTicketTask ticketTask = new(reqTask, kIpProtos);

            ticketTask.FillTaskText(Template());

            Assert.Multiple(() =>
            {
                Assert.That(ticketTask.TaskText, Does.Contain("\"action\":\"accept\""));
                Assert.That(ticketTask.TaskText, Does.Contain("{\"icmp\":\"ping\"}"));
                Assert.That(ticketTask.TaskText, Does.Contain("{\"proto\":\"TCP\",\"port\":\"443\",\"name\":\"https\"}"));
                Assert.That(ticketTask.TaskText, Does.Contain("{\"proto\":\"UDP\",\"port\":\"100-200\",\"name\":\"dns-range\"}"));
                Assert.That(ticketTask.TaskText, Does.Contain("{\"proto\":\"GRE\",\"id\":\"47\",\"name\":\"tunnel\"}"));
                Assert.That(ticketTask.TaskText, Does.Contain("{\"proto\":\"99\",\"id\":\"99\",\"name\":\"unknown-proto\"}"));
                Assert.That(ticketTask.TaskText, Does.Contain("{\"proto\":\"HOPOPT\",\"id\":\"0\",\"name\":\"hopopt\"}"));
                Assert.That(ticketTask.TaskText, Does.Contain(SCConstants.SCAnyServiceJson));
            });
        }

        /// <summary>
        /// Verifies rule deletion requests use modelled elements and the remove action.
        /// </summary>
        [Test]
        public void FillTaskTextUsesModelledElementsForRuleDeletion()
        {
            WfReqTask reqTask = new()
            {
                TaskNumber = 9,
                TaskType = WfTaskType.rule_delete.ToString(),
                OnManagement = new() { Id = 1, Name = "Mgt", ExtMgtData = "{\"id\":\"2\",\"name\":\"MgtExt\"}" },
                Elements =
                [
                    new() { Id = 1, Field = ElemFieldType.modelled_source.ToString(), GroupName = "ModelledSrc" },
                    new() { Id = 2, Field = ElemFieldType.modelled_destination.ToString(), GroupName = "ModelledDst" },
                    new() { Id = 3, Field = ElemFieldType.source.ToString(), GroupName = "PlainSrc" }
                ]
            };
            SCAccessRequestTicketTask ticketTask = new(reqTask, kIpProtos);

            ticketTask.FillTaskText(Template());

            Assert.Multiple(() =>
            {
                Assert.That(ticketTask.TaskText, Does.Contain("\"action\":\"remove\""));
                Assert.That(ticketTask.TaskText, Does.Contain("ModelledSrc"));
                Assert.That(ticketTask.TaskText, Does.Contain("ModelledDst"));
                Assert.That(ticketTask.TaskText, Does.Not.Contain("PlainSrc"));
            });
        }

        /// <summary>
        /// Verifies unknown task types map to an empty action and missing management data to an empty name.
        /// </summary>
        [Test]
        public void FillTaskTextWithUnknownTaskTypeAndWithoutManagement()
        {
            WfReqTask reqTask = new()
            {
                TaskNumber = 10,
                TaskType = WfTaskType.generic.ToString(),
                Elements = [new() { Id = 1, Field = ElemFieldType.source.ToString(), GroupName = "GroupA" }]
            };
            SCAccessRequestTicketTask ticketTask = new(reqTask, kIpProtos);

            ticketTask.FillTaskText(Template());

            Assert.Multiple(() =>
            {
                Assert.That(ticketTask.TaskText, Does.Contain("\"action\":\"\""));
                Assert.That(ticketTask.TaskText, Does.Contain("\"mgt\":\"\""));
            });
        }

        /// <summary>
        /// Verifies port ranges collapse to a single port where start and end match.
        /// </summary>
        [TestCase(443, null, "443")]
        [TestCase(443, 0, "443")]
        [TestCase(443, 443, "443")]
        [TestCase(100, 200, "100-200")]
        public void DisplayPortRangeFormatsPorts(int port, int? portEnd, string expected)
        {
            Assert.That(SCAccessRequestTicketTask.DisplayPortRange(port, portEnd), Is.EqualTo(expected));
        }

        private static int CountOccurrences(string text, string value)
        {
            int count = 0;
            int index = text.IndexOf(value);
            while (index >= 0)
            {
                count++;
                index = text.IndexOf(value, index + value.Length);
            }
            return count;
        }
    }
}

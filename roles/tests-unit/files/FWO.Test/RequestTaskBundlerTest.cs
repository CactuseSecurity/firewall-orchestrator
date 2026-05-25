using FWO.Data.Workflow;
using FWO.Services.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    internal class RequestTaskBundlerTest
    {
        [Test]
        public void BuildBundleAssignments_AssignsCommonIdForTwoOutOfThreeMatches()
        {
            WfReqTask first = CreateTask(1, "10.0.0.1", "10.0.1.1", 443);
            WfReqTask second = CreateTask(2, "10.0.0.1", "10.0.1.2", 443);
            WfReqTask third = CreateTask(3, "10.0.0.2", "10.0.1.3", 8443);

            Dictionary<long, string> assignments = new RequestTaskBundler().BuildBundleAssignments([first, second, third], BundleTaskType.TwoOutOfThree);

            Assert.That(assignments.Keys, Is.EquivalentTo(new long[] { 1, 2 }));
            Assert.That(assignments[1], Is.EqualTo(assignments[2]));
        }

        [Test]
        public void BuildBundleAssignments_KeepsTasksSeparateWhenOnlyOneDimensionMatches()
        {
            WfReqTask first = CreateTask(1, "10.0.0.1", "10.0.1.1", 443);
            WfReqTask second = CreateTask(2, "10.0.0.1", "10.0.1.2", 8443);

            Dictionary<long, string> assignments = new RequestTaskBundler().BuildBundleAssignments([first, second], BundleTaskType.TwoOutOfThree);

            Assert.That(assignments, Is.Empty);
        }

        private static WfReqTask CreateTask(long taskId, string sourceIp, string destinationIp, int port)
        {
            return new()
            {
                Id = taskId,
                TicketId = 7,
                TaskType = WfTaskType.access.ToString(),
                RequestAction = RequestAction.create.ToString(),
                RuleAction = 1,
                ManagementId = 2,
                Elements =
                [
                    CreateNetworkElement(taskId, ElemFieldType.source, sourceIp),
                    CreateNetworkElement(taskId, ElemFieldType.destination, destinationIp),
                    new()
                    {
                        TaskId = taskId,
                        Field = ElemFieldType.service.ToString(),
                        ProtoId = 6,
                        Port = port,
                        PortEnd = port,
                        RequestAction = RequestAction.create.ToString()
                    }
                ]
            };
        }

        private static WfReqElement CreateNetworkElement(long taskId, ElemFieldType field, string ip)
        {
            return new()
            {
                TaskId = taskId,
                Field = field.ToString(),
                Cidr = new(ip),
                CidrEnd = new(ip),
                IpString = ip,
                IpEnd = ip,
                RequestAction = RequestAction.create.ToString()
            };
        }
    }
}

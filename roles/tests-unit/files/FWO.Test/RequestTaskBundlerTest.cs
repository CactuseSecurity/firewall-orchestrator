using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using NetTools;
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

        [Test]
        public void BuildBundleAssignments_KeepsTasksSeparateForDifferentOwners()
        {
            WfReqTask first = CreateTask(1, "10.0.0.1", "10.0.1.1", 443, ownerId: 5);
            WfReqTask second = CreateTask(2, "10.0.0.1", "10.0.1.2", 443, ownerId: 6);

            Dictionary<long, string> assignments = new RequestTaskBundler().BuildBundleAssignments([first, second], BundleTaskType.TwoOutOfThree);

            Assert.That(assignments, Is.Empty);
        }

        [Test]
        public void BuildBundleAssignments_KeepsTasksSeparateForDifferentTimeObjects()
        {
            WfReqTask first = CreateTask(1, "10.0.0.1", "10.0.1.1", 443);
            WfReqTask second = CreateTask(2, "10.0.0.1", "10.0.1.2", 443);
            first.TargetBeginDate = new DateTime(2026, 06, 15, 08, 00, 00);
            first.TargetEndDate = new DateTime(2026, 06, 15, 18, 00, 00);
            second.TargetBeginDate = new DateTime(2026, 06, 16, 08, 00, 00);
            second.TargetEndDate = new DateTime(2026, 06, 16, 18, 00, 00);

            Dictionary<long, string> assignments = new RequestTaskBundler().BuildBundleAssignments([first, second], BundleTaskType.TwoOutOfThree);

            Assert.That(assignments, Is.Empty);
        }

        [Test]
        public void BuildBundleAssignments_AssignsCommonIdForIdenticalTimeObjects()
        {
            WfReqTask first = CreateTask(1, "10.0.0.1", "10.0.1.1", 443);
            WfReqTask second = CreateTask(2, "10.0.0.1", "10.0.1.2", 443);
            DateTime targetBeginDate = new(2026, 06, 15, 08, 00, 00);
            DateTime targetEndDate = new(2026, 06, 15, 18, 00, 00);
            first.TargetBeginDate = targetBeginDate;
            first.TargetEndDate = targetEndDate;
            second.TargetBeginDate = targetBeginDate;
            second.TargetEndDate = targetEndDate;

            Dictionary<long, string> assignments = new RequestTaskBundler().BuildBundleAssignments([first, second], BundleTaskType.TwoOutOfThree);

            Assert.That(assignments.Keys, Is.EquivalentTo(new long[] { 1, 2 }));
            Assert.That(assignments[1], Is.EqualTo(assignments[2]));
        }

        [Test]
        public void BuildBundleAssignments_AssignsCommonIdWhenCleanZonesMatch()
        {
            WfReqTask first = CreateTask(1, "10.0.0.1", "10.0.1.1", 443);
            WfReqTask second = CreateTask(2, "10.0.0.1", "10.0.1.2", 443);

            Dictionary<long, string> assignments = new RequestTaskBundler().BuildBundleAssignments([first, second],
                BundleTaskType.TwoOutOfThree, cleanZones: true, CreateComplianceZones());

            Assert.That(assignments.Keys, Is.EquivalentTo(new long[] { 1, 2 }));
            Assert.That(assignments[1], Is.EqualTo(assignments[2]));
        }

        [Test]
        public void BuildBundleAssignments_KeepsTasksSeparateWhenCleanZonesDiffer()
        {
            WfReqTask first = CreateTask(1, "10.0.0.1", "10.0.1.1", 443);
            WfReqTask second = CreateTask(2, "10.0.0.1", "10.0.2.1", 443);

            Dictionary<long, string> assignments = new RequestTaskBundler().BuildBundleAssignments([first, second],
                BundleTaskType.TwoOutOfThree, cleanZones: true, CreateComplianceZones());

            Assert.That(assignments, Is.Empty);
        }

        [Test]
        public void BuildBundleAssignments_KeepsTasksSeparateWhenCleanZonesHaveNoMatrixZones()
        {
            WfReqTask first = CreateTask(1, "10.0.0.1", "10.0.1.1", 443);
            WfReqTask second = CreateTask(2, "10.0.0.1", "10.0.1.2", 443);

            Dictionary<long, string> assignments = new RequestTaskBundler().BuildBundleAssignments([first, second],
                BundleTaskType.TwoOutOfThree, cleanZones: true);

            Assert.That(assignments, Is.Empty);
        }

        [Test]
        public void BuildBundleAssignments_IgnoresNonAccessTasks()
        {
            WfReqTask first = CreateGroupTask(1, "AR-First", "10.0.0.1");
            WfReqTask second = CreateGroupTask(2, "AR-Second", "10.0.0.2");

            Dictionary<long, string> assignments = new RequestTaskBundler().BuildBundleAssignments([first, second], BundleTaskType.TwoOutOfThree);

            Assert.That(assignments, Is.Empty);
        }

        [Test]
        public void BuildBundleAssignments_IgnoresAccessTasksWithMissingDimensions()
        {
            WfReqTask first = CreateTask(1, "10.0.0.1", "10.0.1.1", 443);
            WfReqTask second = CreateTask(2, "10.0.0.1", "10.0.1.2", 443);
            first.Elements.RemoveAll(element => element.Field == ElemFieldType.service.ToString());

            Dictionary<long, string> assignments = new RequestTaskBundler().BuildBundleAssignments([first, second], BundleTaskType.TwoOutOfThree);

            Assert.That(assignments, Is.Empty);
        }

        private static WfReqTask CreateTask(long taskId, string sourceIp, string destinationIp, int port, int ownerId = 5)
        {
            return new()
            {
                Id = taskId,
                TicketId = 7,
                TaskType = WfTaskType.access.ToString(),
                RequestAction = RequestAction.create.ToString(),
                RuleAction = 1,
                ManagementId = 2,
                Owners = [new FwoOwnerDataHelper { Owner = new FwoOwner { Id = ownerId } }],
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

        private static WfReqTask CreateGroupTask(long taskId, string groupName, string memberIp)
        {
            WfReqTask task = new()
            {
                Id = taskId,
                TicketId = 7,
                TaskType = WfTaskType.group_create.ToString(),
                RequestAction = RequestAction.create.ToString(),
                RuleAction = 1,
                ManagementId = 2,
                Owners = [new FwoOwnerDataHelper { Owner = new FwoOwner { Id = 5 } }],
                Elements =
                [
                    CreateNetworkElement(taskId, ElemFieldType.source, memberIp)
                ]
            };
            task.Elements[0].GroupName = groupName;
            task.SetAddInfo(AdditionalInfoKeys.GrpName, groupName);
            return task;
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

        private static List<ComplianceNetworkZone> CreateComplianceZones()
        {
            return
            [
                new()
                {
                    Id = 1,
                    Name = "Source Zone",
                    IPRanges = [IPAddressRange.Parse("10.0.0.0/24")]
                },
                new()
                {
                    Id = 2,
                    Name = "Destination Zone",
                    IPRanges = [IPAddressRange.Parse("10.0.1.0/24")]
                },
                new()
                {
                    Id = 3,
                    Name = "Other Destination Zone",
                    IPRanges = [IPAddressRange.Parse("10.0.2.0/24")]
                }
            ];
        }
    }
}

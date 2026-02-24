using FWO.Data;
using FWO.Data.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class WfWriterTest
    {
        [Test]
        public void Writers_FullyPopulate_FromTicket()
        {
            WfTicket ticket = new();
            WfReqTask reqTask = new()
            {
                RequestAction = "modify",
                Reason = "reason",
                AdditionalInfo = "{\"key\":\"value\"}",
                LastRecertDate = new DateTime(2024, 1, 2),
                ManagementId = 5
            };
            reqTask.SelectedDevices = "[1,2]";
            reqTask.Elements.Add(new WfReqElement
            {
                RequestAction = "create",
                DeviceId = 9,
                Cidr = new Cidr("10.0.0.1/32"),
                CidrEnd = new Cidr("10.0.0.2/32"),
                Port = 80,
                PortEnd = 81,
                ProtoId = 6,
                NetworkId = 12,
                ServiceId = 13,
                Field = ElemFieldType.source.ToString(),
                UserId = 14,
                OriginalNatId = 15,
                RuleUid = "uid",
                GroupName = "grp",
                Name = "obj"
            });
            reqTask.Approvals.Add(new WfApproval
            {
                ApprovalDate = new DateTime(2024, 3, 4),
                Deadline = new DateTime(2024, 5, 6),
                ApproverGroup = "cn=group",
                ApproverDn = "cn=approver",
                TenantId = 7,
                InitialApproval = false,
                AssignedGroup = "cn=assigned",
                CurrentHandler = new UiUser { DbId = 1 },
                RecentHandler = new UiUser { DbId = 2 }
            });
            reqTask.Owners.Add(new FwoOwnerDataHelper { Owner = new FwoOwner { Id = 77 } });
            ticket.Tasks.Add(reqTask);

            WfTicketWriter ticketWriter = new(ticket);

            Assert.That(ticketWriter.Tasks, Has.Count.EqualTo(1));
            WfReqTaskWriter reqWriter = ticketWriter.Tasks[0];
            Assert.That(reqWriter.RequestAction, Is.EqualTo(reqTask.RequestAction));
            Assert.That(reqWriter.Reason, Is.EqualTo(reqTask.Reason));
            Assert.That(reqWriter.AdditionalInfo, Is.EqualTo(reqTask.AdditionalInfo));
            Assert.That(reqWriter.LastRecertDate, Is.EqualTo(reqTask.LastRecertDate));
            Assert.That(reqWriter.ManagementId, Is.EqualTo(reqTask.ManagementId));
            Assert.That(reqWriter.GetDeviceList(), Is.EqualTo(new List<int> { 1, 2 }));
            Assert.That(reqWriter.Elements.WfElementList, Has.Count.EqualTo(1));

            WfReqElementWriter elemWriter = reqWriter.Elements.WfElementList[0];
            Assert.That(elemWriter.RequestAction, Is.EqualTo("create"));
            Assert.That(elemWriter.DeviceId, Is.EqualTo(9));
            Assert.That(elemWriter.IpString, Is.EqualTo("10.0.0.1/32"));
            Assert.That(elemWriter.IpEnd, Is.EqualTo("10.0.0.2/32"));
            Assert.That(elemWriter.Port, Is.EqualTo(80));
            Assert.That(elemWriter.PortEnd, Is.EqualTo(81));
            Assert.That(elemWriter.ProtoId, Is.EqualTo(6));
            Assert.That(elemWriter.NetworkId, Is.EqualTo(12));
            Assert.That(elemWriter.ServiceId, Is.EqualTo(13));
            Assert.That(elemWriter.Field, Is.EqualTo(ElemFieldType.source.ToString()));
            Assert.That(elemWriter.UserId, Is.EqualTo(14));
            Assert.That(elemWriter.OriginalNatId, Is.EqualTo(15));
            Assert.That(elemWriter.RuleUid, Is.EqualTo("uid"));
            Assert.That(elemWriter.GroupName, Is.EqualTo("grp"));
            Assert.That(elemWriter.Name, Is.EqualTo("obj"));

            Assert.That(reqWriter.Approvals.WfApprovalList, Has.Count.EqualTo(1));
            WfApprovalWriter approvalWriter = reqWriter.Approvals.WfApprovalList[0];
            Assert.That(approvalWriter.ApprovalDate, Is.EqualTo(new DateTime(2024, 3, 4)));
            Assert.That(approvalWriter.Deadline, Is.EqualTo(new DateTime(2024, 5, 6)));
            Assert.That(approvalWriter.ApproverGroup, Is.EqualTo("cn=group"));
            Assert.That(approvalWriter.ApproverDn, Is.EqualTo("cn=approver"));
            Assert.That(approvalWriter.TenantId, Is.EqualTo(7));
            Assert.That(approvalWriter.InitialApproval, Is.False);
            Assert.That(approvalWriter.AssignedGroup, Is.EqualTo("cn=assigned"));
            Assert.That(approvalWriter.CurrentHandler?.DbId, Is.EqualTo(1));
            Assert.That(approvalWriter.RecentHandler?.DbId, Is.EqualTo(2));

            Assert.That(reqWriter.Owners.WfOwnerList, Has.Count.EqualTo(1));
            Assert.That(reqWriter.Owners.WfOwnerList[0].OwnerId, Is.EqualTo(77));
        }
    }
}

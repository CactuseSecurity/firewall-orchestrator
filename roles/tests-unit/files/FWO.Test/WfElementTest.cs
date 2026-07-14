using FWO.Data;
using FWO.Data.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class WfElementTest
    {
        [Test]
        public void WfElementBase_ToNetworkObject_MapsFields()
        {
            WfElementBase element = new()
            {
                Name = "obj",
                IpString = "10.0.0.1/32",
                IpEnd = "10.0.0.2/32"
            };

            NetworkObject nwObj = WfElementBase.ToNetworkObject(element);

            Assert.That(nwObj.Name, Is.EqualTo("obj"));
            Assert.That(nwObj.IP, Is.EqualTo("10.0.0.1/32"));
            Assert.That(nwObj.IpEnd, Is.EqualTo("10.0.0.2/32"));
        }

        [Test]
        public void WfElementBase_Sanitize_RemovesInvalidCharacters()
        {
            WfElementBase element = new()
            {
                IpString = "10.0.0.1/32!",
                IpEnd = "10.0.0.2/32!",
                Field = "source!",
                RuleUid = "uid!",
                GroupName = "grp!",
                Name = "name!"
            };

            bool shortened = element.Sanitize();

            Assert.That(shortened, Is.True);
            Assert.That(element.IpString, Is.EqualTo("10.0.0.1/32"));
            Assert.That(element.IpEnd, Is.EqualTo("10.0.0.2/32"));
            Assert.That(element.Field, Is.EqualTo("source"));
            Assert.That(element.RuleUid, Is.EqualTo("uid"));
            Assert.That(element.GroupName, Is.EqualTo("grp"));
            Assert.That(element.Name, Is.EqualTo("name"));
        }

        [Test]
        public void WfElementBase_CopyConstructor_CopiesFields()
        {
            WfElementBase original = new()
            {
                IpString = "10.0.0.1/32",
                IpEnd = "10.0.0.2/32",
                Port = 80,
                PortEnd = 81,
                ProtoId = 6,
                NetworkId = 7,
                ServiceId = 8,
                Field = ElemFieldType.source.ToString(),
                UserId = 9,
                OriginalNatId = 10,
                RuleUid = "uid",
                GroupName = "grp",
                Name = "obj"
            };

            WfElementBase copy = new(original);

            Assert.That(copy.IpString, Is.EqualTo(original.IpString));
            Assert.That(copy.IpEnd, Is.EqualTo(original.IpEnd));
            Assert.That(copy.Port, Is.EqualTo(original.Port));
            Assert.That(copy.PortEnd, Is.EqualTo(original.PortEnd));
            Assert.That(copy.ProtoId, Is.EqualTo(original.ProtoId));
            Assert.That(copy.NetworkId, Is.EqualTo(original.NetworkId));
            Assert.That(copy.ServiceId, Is.EqualTo(original.ServiceId));
            Assert.That(copy.Field, Is.EqualTo(original.Field));
            Assert.That(copy.UserId, Is.EqualTo(original.UserId));
            Assert.That(copy.OriginalNatId, Is.EqualTo(original.OriginalNatId));
            Assert.That(copy.RuleUid, Is.EqualTo(original.RuleUid));
            Assert.That(copy.GroupName, Is.EqualTo(original.GroupName));
            Assert.That(copy.Name, Is.EqualTo(original.Name));
        }

        [Test]
        public void WfReqElement_CopyConstructor_CopiesFields()
        {
            WfReqElement original = new()
            {
                Id = 1,
                TaskId = 2,
                RequestAction = "modify",
                DeviceId = 3,
                Cidr = new Cidr("10.0.0.1/32"),
                CidrEnd = new Cidr("10.0.0.2/32")
            };
            original.Field = ElemFieldType.source.ToString();
            original.RuleUid = "uid";

            WfReqElement copy = new(original);

            Assert.That(copy.Id, Is.EqualTo(original.Id));
            Assert.That(copy.TaskId, Is.EqualTo(original.TaskId));
            Assert.That(copy.RequestAction, Is.EqualTo(original.RequestAction));
            Assert.That(copy.DeviceId, Is.EqualTo(original.DeviceId));
            Assert.That(copy.Cidr?.CidrString, Is.EqualTo(original.Cidr?.CidrString));
            Assert.That(copy.CidrEnd?.CidrString, Is.EqualTo(original.CidrEnd?.CidrString));
            Assert.That(copy.Field, Is.EqualTo(original.Field));
            Assert.That(copy.RuleUid, Is.EqualTo(original.RuleUid));
        }

        [Test]
        public void WfImplElement_CopyConstructor_CopiesFields()
        {
            WfImplElement original = new()
            {
                Id = 1,
                ImplTaskId = 2,
                ImplAction = "modify",
                Cidr = new Cidr("10.0.0.1/32"),
                CidrEnd = new Cidr("10.0.0.2/32")
            };
            original.Field = ElemFieldType.destination.ToString();
            original.RuleUid = "uid";

            WfImplElement copy = new(original);

            Assert.That(copy.Id, Is.EqualTo(original.Id));
            Assert.That(copy.ImplTaskId, Is.EqualTo(original.ImplTaskId));
            Assert.That(copy.ImplAction, Is.EqualTo(original.ImplAction));
            Assert.That(copy.Cidr?.CidrString, Is.EqualTo(original.Cidr?.CidrString));
            Assert.That(copy.CidrEnd?.CidrString, Is.EqualTo(original.CidrEnd?.CidrString));
            Assert.That(copy.Field, Is.EqualTo(original.Field));
            Assert.That(copy.RuleUid, Is.EqualTo(original.RuleUid));
        }

        [Test]
        public void WfImplElement_FromReqElement_CopiesCommonFields()
        {
            WfReqElement req = new()
            {
                RequestAction = "delete",
                Cidr = new Cidr("10.0.0.1/32"),
                CidrEnd = new Cidr("10.0.0.2/32"),
                Port = 80,
                PortEnd = 81,
                ProtoId = 6,
                NetworkId = 7,
                ServiceId = 8,
                Field = ElemFieldType.service.ToString(),
                UserId = 9,
                OriginalNatId = 10,
                RuleUid = "uid",
                GroupName = "grp"
            };

            WfImplElement impl = new(req);

            Assert.That(impl.ImplAction, Is.EqualTo("delete"));
            Assert.That(impl.Cidr?.CidrString, Is.EqualTo("10.0.0.1/32"));
            Assert.That(impl.CidrEnd?.CidrString, Is.EqualTo("10.0.0.2/32"));
            Assert.That(impl.Port, Is.EqualTo(80));
            Assert.That(impl.PortEnd, Is.EqualTo(81));
            Assert.That(impl.ProtoId, Is.EqualTo(6));
            Assert.That(impl.NetworkId, Is.EqualTo(7));
            Assert.That(impl.ServiceId, Is.EqualTo(8));
            Assert.That(impl.Field, Is.EqualTo(ElemFieldType.service.ToString()));
            Assert.That(impl.UserId, Is.EqualTo(9));
            Assert.That(impl.OriginalNatId, Is.EqualTo(10));
            Assert.That(impl.RuleUid, Is.EqualTo("uid"));
            Assert.That(impl.GroupName, Is.EqualTo("grp"));
        }
    }
}

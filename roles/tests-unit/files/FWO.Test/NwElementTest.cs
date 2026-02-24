using FWO.Data;
using FWO.Data.Workflow;
using NetTools;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class NwElementTest
    {
        [Test]
        public void NwObjectElement_CtorWithCidrString_SetsCidrAndTaskId()
        {
            NwObjectElement element = new("10.0.0.0/24", 12);

            Assert.That(element.TaskId, Is.EqualTo(12));
            Assert.That(element.Cidr.Valid, Is.True);
            Assert.That(element.Cidr.CidrString, Is.EqualTo("10.0.0.0/24"));
        }

        [Test]
        public void NwObjectElement_CtorWithIpRange_SetsCidrAndCidrEnd()
        {
            IPAddressRange range = new(System.Net.IPAddress.Parse("10.0.0.1"), System.Net.IPAddress.Parse("10.0.0.2"));

            NwObjectElement element = new(range, 5);

            Assert.That(element.TaskId, Is.EqualTo(5));
            Assert.That(element.Cidr.CidrString, Is.EqualTo("10.0.0.1/32"));
            Assert.That(element.CidrEnd.CidrString, Is.EqualTo("10.0.0.2/32"));
        }

        [Test]
        public void NwObjectElement_ToReqElement_CopiesFields()
        {
            NwObjectElement element = new()
            {
                ElemId = 1,
                TaskId = 2,
                Cidr = new Cidr("10.0.0.1/32"),
                CidrEnd = new Cidr("10.0.0.2/32"),
                NetworkId = 3,
                GroupName = "grp",
                RequestAction = "create",
                Name = "obj"
            };

            WfReqElement reqElement = element.ToReqElement(ElemFieldType.source);

            Assert.That(reqElement.Id, Is.EqualTo(1));
            Assert.That(reqElement.TaskId, Is.EqualTo(2));
            Assert.That(reqElement.Field, Is.EqualTo(ElemFieldType.source.ToString()));
            Assert.That(reqElement.Cidr?.CidrString, Is.EqualTo("10.0.0.1/32"));
            Assert.That(reqElement.CidrEnd?.CidrString, Is.EqualTo("10.0.0.2/32"));
            Assert.That(reqElement.NetworkId, Is.EqualTo(3));
            Assert.That(reqElement.GroupName, Is.EqualTo("grp"));
            Assert.That(reqElement.RequestAction, Is.EqualTo("create"));
            Assert.That(reqElement.Name, Is.EqualTo("obj"));
        }

        [Test]
        public void NwObjectElement_ToImplElement_CopiesFields()
        {
            NwObjectElement element = new()
            {
                ElemId = 1,
                TaskId = 2,
                Cidr = new Cidr("10.0.0.1/32"),
                CidrEnd = new Cidr("10.0.0.2/32"),
                NetworkId = 3,
                GroupName = "grp",
                Name = "obj"
            };

            WfImplElement implElement = element.ToImplElement(ElemFieldType.destination);

            Assert.That(implElement.Id, Is.EqualTo(1));
            Assert.That(implElement.ImplTaskId, Is.EqualTo(2));
            Assert.That(implElement.Field, Is.EqualTo(ElemFieldType.destination.ToString()));
            Assert.That(implElement.Cidr?.CidrString, Is.EqualTo("10.0.0.1/32"));
            Assert.That(implElement.CidrEnd?.CidrString, Is.EqualTo("10.0.0.2/32"));
            Assert.That(implElement.NetworkId, Is.EqualTo(3));
            Assert.That(implElement.GroupName, Is.EqualTo("grp"));
            Assert.That(implElement.Name, Is.EqualTo("obj"));
        }

        [Test]
        public void NwServiceElement_ToReqElement_CopiesFields()
        {
            NwServiceElement element = new()
            {
                ElemId = 1,
                TaskId = 2,
                Port = 80,
                PortEnd = 81,
                ProtoId = 6,
                ServiceId = 7,
                Name = "svc",
                RequestAction = "delete"
            };

            WfReqElement reqElement = element.ToReqElement();

            Assert.That(reqElement.Id, Is.EqualTo(1));
            Assert.That(reqElement.TaskId, Is.EqualTo(2));
            Assert.That(reqElement.Field, Is.EqualTo(ElemFieldType.service.ToString()));
            Assert.That(reqElement.Port, Is.EqualTo(80));
            Assert.That(reqElement.PortEnd, Is.EqualTo(81));
            Assert.That(reqElement.ProtoId, Is.EqualTo(6));
            Assert.That(reqElement.ServiceId, Is.EqualTo(7));
            Assert.That(reqElement.Name, Is.EqualTo("svc"));
            Assert.That(reqElement.RequestAction, Is.EqualTo("delete"));
        }

        [Test]
        public void NwServiceElement_ToImplElement_CopiesFields()
        {
            NwServiceElement element = new()
            {
                ElemId = 1,
                TaskId = 2,
                Port = 80,
                PortEnd = 81,
                ProtoId = 6,
                ServiceId = 7,
                Name = "svc"
            };

            WfImplElement implElement = element.ToImplElement();

            Assert.That(implElement.Id, Is.EqualTo(1));
            Assert.That(implElement.ImplTaskId, Is.EqualTo(2));
            Assert.That(implElement.Field, Is.EqualTo(ElemFieldType.service.ToString()));
            Assert.That(implElement.Port, Is.EqualTo(80));
            Assert.That(implElement.PortEnd, Is.EqualTo(81));
            Assert.That(implElement.ProtoId, Is.EqualTo(6));
            Assert.That(implElement.ServiceId, Is.EqualTo(7));
            Assert.That(implElement.Name, Is.EqualTo("svc"));
        }

        [Test]
        public void NwRuleElement_ToReqElement_CopiesFields()
        {
            NwRuleElement element = new()
            {
                ElemId = 1,
                TaskId = 2,
                RuleUid = "uid",
                Name = "rule"
            };

            WfReqElement reqElement = element.ToReqElement();

            Assert.That(reqElement.Id, Is.EqualTo(1));
            Assert.That(reqElement.TaskId, Is.EqualTo(2));
            Assert.That(reqElement.Field, Is.EqualTo(ElemFieldType.rule.ToString()));
            Assert.That(reqElement.RuleUid, Is.EqualTo("uid"));
            Assert.That(reqElement.Name, Is.EqualTo("rule"));
        }

        [Test]
        public void NwRuleElement_ToImplElement_CopiesFields()
        {
            NwRuleElement element = new()
            {
                ElemId = 1,
                TaskId = 2,
                RuleUid = "uid",
                Name = "rule"
            };

            WfImplElement implElement = element.ToImplElement();

            Assert.That(implElement.Id, Is.EqualTo(1));
            Assert.That(implElement.ImplTaskId, Is.EqualTo(2));
            Assert.That(implElement.Field, Is.EqualTo(ElemFieldType.rule.ToString()));
            Assert.That(implElement.RuleUid, Is.EqualTo("uid"));
            Assert.That(implElement.Name, Is.EqualTo("rule"));
        }
    }
}

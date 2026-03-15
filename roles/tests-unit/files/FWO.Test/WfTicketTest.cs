using FWO.Data;
using FWO.Data.Workflow;
using NUnit.Framework;
using System.Text.Json;

namespace FWO.Test
{
    [TestFixture]
    internal class WfTicketTest
    {
        [Test]
        public void HighestTaskNumber_ReturnsMax()
        {
            WfTicket ticket = new();
            ticket.Tasks.Add(new WfReqTask { TaskNumber = 1 });
            ticket.Tasks.Add(new WfReqTask { TaskNumber = 7 });
            ticket.Tasks.Add(new WfReqTask { TaskNumber = 3 });

            int highest = ticket.HighestTaskNumber();

            Assert.That(highest, Is.EqualTo(7));
        }

        [Test]
        public void NumberImplTasks_ReturnsSum()
        {
            WfTicket ticket = new();
            WfReqTask first = new();
            first.ImplementationTasks.Add(new WfImplTask());
            first.ImplementationTasks.Add(new WfImplTask());
            WfReqTask second = new();
            second.ImplementationTasks.Add(new WfImplTask());
            ticket.Tasks.Add(first);
            ticket.Tasks.Add(second);

            int count = ticket.NumberImplTasks();

            Assert.That(count, Is.EqualTo(3));
        }

        [Test]
        public void UpdateIpStringsFromCidrInTaskElements_SetsIpStrings()
        {
            WfTicket ticket = new();
            WfReqTask reqTask = new();
            WfReqElement element = new()
            {
                Cidr = new Cidr("10.0.0.1/32"),
                CidrEnd = new Cidr("10.0.0.2/32")
            };
            reqTask.Elements.Add(element);
            ticket.Tasks.Add(reqTask);

            ticket.UpdateIpStringsFromCidrInTaskElements();

            Assert.That(element.IpString, Is.EqualTo(element.Cidr?.CidrString));
            Assert.That(element.IpEnd, Is.EqualTo(element.CidrEnd?.CidrString));
        }

        [Test]
        public void UpdateCidrsInTaskElements_SetsCidrsForReqAndImpl()
        {
            WfTicket ticket = new();
            WfReqTask reqTask = new();
            WfReqElement reqElem = new()
            {
                IpString = "10.0.0.1/32",
                IpEnd = "10.0.0.2/32"
            };
            reqTask.Elements.Add(reqElem);
            WfImplTask implTask = new();
            WfImplElement implElem = new()
            {
                IpString = "10.0.1.1/32",
                IpEnd = "10.0.1.2/32"
            };
            implTask.ImplElements.Add(implElem);
            reqTask.ImplementationTasks.Add(implTask);
            ticket.Tasks.Add(reqTask);

            ticket.UpdateCidrsInTaskElements();

            Assert.That(reqElem.Cidr?.Valid, Is.True);
            Assert.That(reqElem.Cidr?.CidrString, Is.EqualTo("10.0.0.1/32"));
            Assert.That(reqElem.CidrEnd?.CidrString, Is.EqualTo("10.0.0.2/32"));
            Assert.That(implElem.Cidr?.CidrString, Is.EqualTo("10.0.1.1/32"));
            Assert.That(implElem.CidrEnd?.CidrString, Is.EqualTo("10.0.1.2/32"));
        }

        [Test]
        public void IsEditableForOwner_ReturnsTrue_WhenTicketIdMatches()
        {
            WfTicket ticket = new() { Id = 5 };

            bool editable = ticket.IsEditableForOwner([5], [1], 99);

            Assert.That(editable, Is.True);
        }

        [Test]
        public void IsEditableForOwner_ReturnsTrue_WhenOwnerMatches()
        {
            WfTicket ticket = new();
            ticket.Tasks.Add(new WfReqTask
            {
                Owners = [new FwoOwnerDataHelper { Owner = new FwoOwner { Id = 7 } }]
            });

            bool editable = ticket.IsEditableForOwner([], [7], 99);

            Assert.That(editable, Is.True);
        }

        [Test]
        public void IsEditableForOwner_ReturnsTrue_WhenRequesterMatches()
        {
            WfTicket ticket = new() { Requester = new UiUser { DbId = 42 } };

            bool editable = ticket.IsEditableForOwner([], [], 42);

            Assert.That(editable, Is.True);
        }

        [Test]
        public void IsEditableForOwner_ReturnsFalse_WhenNoMatch()
        {
            WfTicket ticket = new() { Id = 5, Requester = new UiUser { DbId = 42 } };

            bool editable = ticket.IsEditableForOwner([1], [2], 99);

            Assert.That(editable, Is.False);
        }

        [Test]
        public void IsVisibleForOwner_ReturnsTrue_WhenReqOwnerInAdditionalInfo()
        {
            WfTicket ticket = new();
            WfReqTask reqTask = new();
            reqTask.AdditionalInfo = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                { AdditionalInfoKeys.ReqOwner, "7" }
            });
            ticket.Tasks.Add(reqTask);

            bool visible = ticket.IsVisibleForOwner([], [7], 99);

            Assert.That(visible, Is.True);
        }

        [Test]
        public void Sanitize_RemovesInvalidCharacters_AndReturnsTrue()
        {
            WfTicketBase ticket = new()
            {
                Title = "Bad!",
                RequesterDn = "cn=bad!,ou=users,dc=example,dc=com",
                RequesterGroup = "cn=grp!,ou=groups,dc=example,dc=com",
                Reason = "why!",
                ExternalTicketId = "ext!"
            };
            ticket.AssignedGroup = "cn=grp!,ou=groups,dc=example,dc=com";
            ticket.SetOptComment("note!");

            bool shortened = ticket.Sanitize();

            Assert.That(shortened, Is.True);
            Assert.That(ticket.Title, Is.EqualTo("Bad"));
            Assert.That(ticket.RequesterDn, Is.EqualTo("cn=bad,ou=users,dc=example,dc=com"));
            Assert.That(ticket.RequesterGroup, Is.EqualTo("cn=grp,ou=groups,dc=example,dc=com"));
            Assert.That(ticket.Reason, Is.EqualTo("why"));
            Assert.That(ticket.ExternalTicketId, Is.EqualTo("ext"));
            Assert.That(ticket.AssignedGroup, Is.EqualTo("cn=grp,ou=groups,dc=example,dc=com"));
            Assert.That(ticket.OptComment(), Is.EqualTo("note"));
        }

        [Test]
        public void CopyConstructor_CopiesWfTicketBaseFields()
        {
            WfTicketBase original = new()
            {
                Id = 10,
                Title = "title",
                CreationDate = new DateTime(2024, 1, 2),
                CompletionDate = new DateTime(2024, 2, 3),
                Requester = new UiUser { DbId = 7, Name = "user" },
                RequesterDn = "cn=user,dc=example,dc=com",
                RequesterGroup = "cn=grp,dc=example,dc=com",
                TenantId = 2,
                Reason = "reason",
                ExternalTicketId = "ext",
                ExternalTicketSource = 3,
                Deadline = new DateTime(2024, 4, 5),
                Priority = 4
            };
            original.AssignedGroup = "cn=grp,dc=example,dc=com";
            original.SetOptComment("comment");

            WfTicketBase copy = new(original);

            Assert.That(copy.Id, Is.EqualTo(original.Id));
            Assert.That(copy.Title, Is.EqualTo(original.Title));
            Assert.That(copy.CreationDate, Is.EqualTo(original.CreationDate));
            Assert.That(copy.CompletionDate, Is.EqualTo(original.CompletionDate));
            Assert.That(copy.Requester, Is.EqualTo(original.Requester));
            Assert.That(copy.RequesterDn, Is.EqualTo(original.RequesterDn));
            Assert.That(copy.RequesterGroup, Is.EqualTo(original.RequesterGroup));
            Assert.That(copy.TenantId, Is.EqualTo(original.TenantId));
            Assert.That(copy.Reason, Is.EqualTo(original.Reason));
            Assert.That(copy.ExternalTicketId, Is.EqualTo(original.ExternalTicketId));
            Assert.That(copy.ExternalTicketSource, Is.EqualTo(original.ExternalTicketSource));
            Assert.That(copy.Deadline, Is.EqualTo(original.Deadline));
            Assert.That(copy.Priority, Is.EqualTo(original.Priority));
            Assert.That(copy.AssignedGroup, Is.EqualTo(original.AssignedGroup));
            Assert.That(copy.OptComment(), Is.EqualTo(original.OptComment()));
        }

        [Test]
        public void CopyConstructor_CopiesWfTicketFields()
        {
            WfTicket original = new()
            {
                Id = 5,
                Title = "title"
            };
            original.Tasks.Add(new WfReqTask { Id = 1 });
            original.Comments.Add(new WfCommentDataHelper(new WfComment { Id = 2 }));

            WfTicket copy = new(original);

            Assert.That(copy.Id, Is.EqualTo(original.Id));
            Assert.That(copy.Title, Is.EqualTo(original.Title));
            Assert.That(copy.Tasks, Is.EqualTo(original.Tasks));
            Assert.That(copy.Comments, Is.EqualTo(original.Comments));
        }
    }
}

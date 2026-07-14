using FWO.Data;
using FWO.Data.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class WfCommentApprovalTest
    {
        [Test]
        public void WfCommentBase_CopyConstructor_AndSanitize_Work()
        {
            WfCommentBase original = new()
            {
                RefId = 10,
                Scope = "scope!",
                CreationDate = new DateTime(2024, 1, 2),
                Creator = new UiUser { Name = "user" },
                CommentText = "text!"
            };

            WfCommentBase copy = new(original);
            bool shortened = copy.Sanitize();

            Assert.That(copy.RefId, Is.EqualTo(10));
            Assert.That(copy.Scope, Is.EqualTo("scope"));
            Assert.That(copy.CreationDate, Is.EqualTo(new DateTime(2024, 1, 2)));
            Assert.That(copy.Creator, Is.EqualTo(original.Creator));
            Assert.That(copy.CommentText, Is.EqualTo("text"));
            Assert.That(shortened, Is.True);
        }

        [Test]
        public void WfComment_CopyConstructor_CopiesBaseAndId()
        {
            WfComment original = new()
            {
                Id = 5,
                RefId = 11,
                Scope = "scope",
                CreationDate = new DateTime(2024, 2, 3),
                Creator = new UiUser { Name = "user" },
                CommentText = "text"
            };

            WfComment copy = new(original);

            Assert.That(copy.Id, Is.EqualTo(5));
            Assert.That(copy.RefId, Is.EqualTo(11));
            Assert.That(copy.Scope, Is.EqualTo("scope"));
            Assert.That(copy.CreationDate, Is.EqualTo(new DateTime(2024, 2, 3)));
            Assert.That(copy.Creator, Is.EqualTo(original.Creator));
            Assert.That(copy.CommentText, Is.EqualTo("text"));
        }

        [Test]
        public void WfApprovalBase_CopyConstructor_AndSanitize_Work()
        {
            WfApprovalBase original = new()
            {
                DateOpened = new DateTime(2024, 1, 2),
                ApprovalDate = new DateTime(2024, 3, 4),
                Deadline = new DateTime(2024, 5, 6),
                ApproverGroup = "cn=grp!",
                ApproverDn = "cn=user!",
                TenantId = 7,
                InitialApproval = false
            };
            original.AssignedGroup = "cn=assigned!";
            original.SetOptComment("note!");

            WfApprovalBase copy = new(original);
            bool shortened = copy.Sanitize();

            Assert.That(copy.DateOpened, Is.EqualTo(new DateTime(2024, 1, 2)));
            Assert.That(copy.ApprovalDate, Is.EqualTo(new DateTime(2024, 3, 4)));
            Assert.That(copy.Deadline, Is.EqualTo(new DateTime(2024, 5, 6)));
            Assert.That(copy.ApproverGroup, Is.EqualTo("cn=grp"));
            Assert.That(copy.ApproverDn, Is.EqualTo("cn=user"));
            Assert.That(copy.TenantId, Is.EqualTo(7));
            Assert.That(copy.InitialApproval, Is.False);
            Assert.That(copy.AssignedGroup, Is.EqualTo("cn=assigned"));
            Assert.That(copy.OptComment(), Is.EqualTo("note"));
            Assert.That(shortened, Is.True);
        }

        [Test]
        public void WfApproval_CopyConstructor_CopiesBaseAndId()
        {
            WfApproval original = new()
            {
                Id = 9,
                TaskId = 12,
                ApproverDn = "cn=user",
                TenantId = 3
            };
            original.Comments.Add(new WfCommentDataHelper(new WfComment { Id = 1 }));

            WfApproval copy = new(original);

            Assert.That(copy.Id, Is.EqualTo(9));
            Assert.That(copy.TaskId, Is.EqualTo(12));
            Assert.That(copy.ApproverDn, Is.EqualTo("cn=user"));
            Assert.That(copy.TenantId, Is.EqualTo(3));
            Assert.That(copy.Comments, Is.EqualTo(original.Comments));
        }
    }
}

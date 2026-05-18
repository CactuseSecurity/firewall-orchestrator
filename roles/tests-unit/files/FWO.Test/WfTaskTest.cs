using FWO.Data;
using FWO.Data.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class WfTaskTest
    {
        [Test]
        public void CopyConstructor_CopiesWfReqTaskBaseFields()
        {
            WfReqTaskBase original = new()
            {
                RequestAction = "modify",
                Reason = "reason",
                AdditionalInfo = "{\"key\":\"value\"}",
                LastRecertDate = new DateTime(2024, 1, 2),
                ManagementId = 5
            };
            original.SelectedDevices = "[1,2,3]";

            WfReqTaskBase copy = new(original);

            Assert.That(copy.RequestAction, Is.EqualTo(original.RequestAction));
            Assert.That(copy.Reason, Is.EqualTo(original.Reason));
            Assert.That(copy.AdditionalInfo, Is.EqualTo(original.AdditionalInfo));
            Assert.That(copy.LastRecertDate, Is.EqualTo(original.LastRecertDate));
            Assert.That(copy.ManagementId, Is.EqualTo(original.ManagementId));
            Assert.That(copy.GetDeviceList(), Is.EqualTo(original.GetDeviceList()));
        }

        [Test]
        public void CopyConstructor_CopiesWfReqTaskFields()
        {
            WfReqTask original = new()
            {
                Id = 10,
                TicketId = 20,
                OnManagement = new Management { Id = 3 }
            };
            original.Elements.Add(new WfReqElement { Id = 1 });
            original.ImplementationTasks.Add(new WfImplTask { Id = 2 });
            original.Approvals.Add(new WfApproval { Id = 3 });
            original.Owners.Add(new FwoOwnerDataHelper { Owner = new FwoOwner { Id = 4 } });
            original.Comments.Add(new WfCommentDataHelper(new WfComment { Id = 5 }));
            original.RemovedElements.Add(new WfReqElement { Id = 6 });
            original.NewOwners.Add(new FwoOwner { Id = 7 });
            original.RemovedOwners.Add(new FwoOwner { Id = 8 });

            WfReqTask copy = new(original);

            Assert.That(copy.Id, Is.EqualTo(original.Id));
            Assert.That(copy.TicketId, Is.EqualTo(original.TicketId));
            Assert.That(copy.OnManagement, Is.EqualTo(original.OnManagement));
            Assert.That(copy.Elements, Is.EqualTo(original.Elements));
            Assert.That(copy.ImplementationTasks, Is.EqualTo(original.ImplementationTasks));
            Assert.That(copy.Approvals, Is.EqualTo(original.Approvals));
            Assert.That(copy.Owners, Is.EqualTo(original.Owners));
            Assert.That(copy.Comments, Is.EqualTo(original.Comments));
            Assert.That(copy.RemovedElements, Is.EqualTo(original.RemovedElements));
            Assert.That(copy.NewOwners, Is.EqualTo(original.NewOwners));
            Assert.That(copy.RemovedOwners, Is.EqualTo(original.RemovedOwners));
        }

        [Test]
        public void CopyConstructor_CopiesWfImplTaskFields()
        {
            WfImplTask original = new()
            {
                Id = 11,
                ReqTaskId = 12,
                ImplAction = "delete",
                DeviceId = 3,
                TicketId = 99
            };
            original.ImplElements.Add(new WfImplElement { Id = 1 });
            original.Comments.Add(new WfCommentDataHelper(new WfComment { Id = 2 }));
            original.RemovedElements.Add(new WfImplElement { Id = 3 });

            WfImplTask copy = new(original);

            Assert.That(copy.Id, Is.EqualTo(original.Id));
            Assert.That(copy.ReqTaskId, Is.EqualTo(original.ReqTaskId));
            Assert.That(copy.ImplAction, Is.EqualTo(original.ImplAction));
            Assert.That(copy.DeviceId, Is.EqualTo(original.DeviceId));
            Assert.That(copy.TicketId, Is.EqualTo(original.TicketId));
            Assert.That(copy.ImplElements, Is.EqualTo(original.ImplElements));
            Assert.That(copy.Comments, Is.EqualTo(original.Comments));
            Assert.That(copy.RemovedElements, Is.EqualTo(original.RemovedElements));
        }

        [Test]
        public void WfReqTaskBase_AddInfoGetters_Work()
        {
            WfReqTaskBase task = new();
            task.SetAddInfo("key", "123");
            task.SetAddInfo("long", "9999999999");

            Assert.That(task.GetAddInfoValue("key"), Is.EqualTo("123"));
            Assert.That(task.GetAddInfoIntValue("key"), Is.EqualTo(123));
            Assert.That(task.GetAddInfoIntValueOrZero("missing"), Is.EqualTo(0));
            Assert.That(task.GetAddInfoLongValue("long"), Is.EqualTo(9999999999L));
        }

        [Test]
        public void WfReqTaskBase_SetDeviceList_PersistsIds()
        {
            WfReqTaskBase task = new();
            task.SetDeviceList([new Device { Id = 1 }, new Device { Id = 2 }]);

            Assert.That(task.GetDeviceList(), Is.EqualTo(new List<int> { 1, 2 }));
        }

        [Test]
        public void WfReqTaskBase_GetResolvedDeviceList_ExpandsAllMarker()
        {
            WfReqTaskBase task = new();
            task.SetDeviceList([WfReqTaskBase.kAllDevicesId]);

            List<int> deviceIds = task.GetResolvedDeviceList([new Device { Id = 4 }, new Device { Id = 7 }]);

            Assert.That(task.HasAllDevicesSelected(), Is.True);
            Assert.That(deviceIds, Is.EqualTo(new List<int> { 4, 7 }));
        }

        [Test]
        public void WfReqTask_OwnerList_JoinsNames()
        {
            WfReqTask task = new();
            task.Owners.Add(new FwoOwnerDataHelper { Owner = new FwoOwner { Name = "A" } });
            task.Owners.Add(new FwoOwnerDataHelper { Owner = new FwoOwner { Name = "B" } });

            Assert.That(task.OwnerList(), Is.EqualTo("A, B"));
        }

        [Test]
        public void WfReqTask_GetElements_FilterByField()
        {
            WfReqTask task = new();
            task.Elements.Add(new WfReqElement
            {
                Id = 1,
                TaskId = 10,
                Field = ElemFieldType.source.ToString(),
                Cidr = new Cidr("10.0.0.1/32"),
                CidrEnd = new Cidr("10.0.0.2/32"),
                IpString = "10.0.0.1/32",
                NetworkId = 12,
                RequestAction = "create",
                Name = "obj",
                GroupName = "grp"
            });
            task.Elements.Add(new WfReqElement { Id = 2, TaskId = 10, Field = ElemFieldType.service.ToString(), Port = 80, ProtoId = 6 });
            task.Elements.Add(new WfReqElement { Id = 3, TaskId = 10, Field = ElemFieldType.rule.ToString(), RuleUid = "uid", Name = "rule" });

            List<NwObjectElement> nwObjects = task.GetNwObjectElements(ElemFieldType.source);
            List<NwServiceElement> services = task.GetServiceElements();
            List<NwRuleElement> rules = task.GetRuleElements();

            Assert.That(nwObjects, Has.Count.EqualTo(1));
            Assert.That(nwObjects[0].ElemId, Is.EqualTo(1));
            Assert.That(nwObjects[0].TaskId, Is.EqualTo(10));
            Assert.That(nwObjects[0].Cidr.CidrString, Is.EqualTo("10.0.0.1/32"));
            Assert.That(nwObjects[0].CidrEnd.CidrString, Is.EqualTo("10.0.0.2/32"));
            Assert.That(nwObjects[0].IpString, Is.EqualTo("10.0.0.1/32"));
            Assert.That(nwObjects[0].NetworkId, Is.EqualTo(12));
            Assert.That(nwObjects[0].RequestAction, Is.EqualTo("create"));
            Assert.That(nwObjects[0].Name, Is.EqualTo("obj"));
            Assert.That(nwObjects[0].GroupName, Is.EqualTo("grp"));
            Assert.That(services, Has.Count.EqualTo(1));
            Assert.That(services[0].Port, Is.EqualTo(80));
            Assert.That(rules, Has.Count.EqualTo(1));
            Assert.That(rules[0].RuleUid, Is.EqualTo("uid"));
            Assert.That(rules[0].Name, Is.EqualTo("rule"));
        }

        [Test]
        public void WfReqTask_GetFirstCommentText_ReturnsFirstOrEmpty()
        {
            WfReqTask task = new();
            Assert.That(task.GetFirstCommentText(), Is.EqualTo(""));
            task.Comments.Add(new WfCommentDataHelper(new WfComment { CommentText = "first" }));
            task.Comments.Add(new WfCommentDataHelper(new WfComment { CommentText = "second" }));

            Assert.That(task.GetFirstCommentText(), Is.EqualTo("first"));
        }

        [Test]
        public void WfReqTask_GetRuleDeviceId_ReturnsFirstRuleDevice()
        {
            WfReqTask task = new();
            task.Elements.Add(new WfReqElement { Field = ElemFieldType.rule.ToString(), DeviceId = 5 });
            task.Elements.Add(new WfReqElement { Field = ElemFieldType.rule.ToString(), DeviceId = 7 });

            Assert.That(task.GetRuleDeviceId(), Is.EqualTo(5));
        }

        [Test]
        public void WfReqTask_GetDeviceList_UsesDeviceListOrRuleElements()
        {
            WfReqTask task = new();
            task.Elements.Add(new WfReqElement { Field = ElemFieldType.rule.ToString(), DeviceId = 5 });
            Assert.That(task.GetDeviceList(), Is.EqualTo(new List<int> { 5 }));

            task.SelectedDevices = "[1,2]";
            Assert.That(task.GetDeviceList(), Is.EqualTo(new List<int> { 1, 2 }));
        }

        [Test]
        public void WfReqTask_GetDeviceList_PreservesAllMarker()
        {
            WfReqTask task = new();
            task.SelectedDevices = "[-1]";

            Assert.That(task.HasAllDevicesSelected(), Is.True);
            Assert.That(task.GetDeviceList(), Is.EqualTo(new List<int> { WfReqTaskBase.kAllDevicesId }));
        }

        [Test]
        public void WfReqTask_IsNetworkFlavor_TrueWhenIpStringPresent()
        {
            WfReqTask task = new();
            Assert.That(task.IsNetworkFlavor(), Is.False);
            task.Elements.Add(new WfReqElement { IpString = "10.0.0.1/32" });

            Assert.That(task.IsNetworkFlavor(), Is.True);
        }

        [Test]
        public void WfImplTask_FromReqTask_CopiesElementsAndComments()
        {
            WfReqTask reqTask = new()
            {
                Id = 10,
                TicketId = 20,
                TaskType = WfTaskType.rule_delete.ToString(),
                RequestAction = "delete"
            };
            reqTask.Elements.Add(new WfReqElement { DeviceId = 9, Field = ElemFieldType.rule.ToString() });
            reqTask.Comments.Add(new WfCommentDataHelper(new WfComment { CommentText = "comment", Scope = WfObjectScopes.RequestTask.ToString() }));

            WfImplTask implTask = new(reqTask, copyComments: true);

            Assert.That(implTask.ReqTaskId, Is.EqualTo(10));
            Assert.That(implTask.TicketId, Is.EqualTo(20));
            Assert.That(implTask.DeviceId, Is.EqualTo(9));
            Assert.That(implTask.ImplElements, Has.Count.EqualTo(1));
            Assert.That(implTask.Comments, Has.Count.EqualTo(1));
            Assert.That(implTask.Comments[0].Comment.Scope, Is.EqualTo(WfObjectScopes.ImplementationTask.ToString()));
        }

        [Test]
        public void WfImplTask_FromReqTask_DoesNotCopyComments_WhenDisabled()
        {
            WfReqTask reqTask = new();
            reqTask.Comments.Add(new WfCommentDataHelper(new WfComment { CommentText = "comment" }));

            WfImplTask implTask = new(reqTask, copyComments: false);

            Assert.That(implTask.Comments, Is.Empty);
        }

        [Test]
        public void WfImplTask_GetElements_FilterByField()
        {
            WfImplTask task = new();
            task.ImplElements.Add(new WfImplElement
            {
                Id = 1,
                ImplTaskId = 10,
                Field = ElemFieldType.source.ToString(),
                Cidr = new Cidr("10.0.0.1/32"),
                IpString = "10.0.0.1/32",
                NetworkId = 12,
                Name = "obj"
            });
            task.ImplElements.Add(new WfImplElement { Id = 2, ImplTaskId = 10, Field = ElemFieldType.service.ToString(), Port = 80, ProtoId = 6 });
            task.ImplElements.Add(new WfImplElement { Id = 3, ImplTaskId = 10, Field = ElemFieldType.rule.ToString(), RuleUid = "uid", Name = "rule" });

            List<NwObjectElement> nwObjects = task.GetNwObjectElements(ElemFieldType.source);
            List<NwServiceElement> services = task.GetServiceElements();
            List<NwRuleElement> rules = task.GetRuleElements();

            Assert.That(nwObjects, Has.Count.EqualTo(1));
            Assert.That(nwObjects[0].ElemId, Is.EqualTo(1));
            Assert.That(nwObjects[0].TaskId, Is.EqualTo(10));
            Assert.That(nwObjects[0].Cidr.CidrString, Is.EqualTo("10.0.0.1/32"));
            Assert.That(nwObjects[0].IpString, Is.EqualTo("10.0.0.1/32"));
            Assert.That(nwObjects[0].NetworkId, Is.EqualTo(12));
            Assert.That(nwObjects[0].Name, Is.EqualTo("obj"));
            Assert.That(services, Has.Count.EqualTo(1));
            Assert.That(services[0].Port, Is.EqualTo(80));
            Assert.That(rules, Has.Count.EqualTo(1));
            Assert.That(rules[0].RuleUid, Is.EqualTo("uid"));
            Assert.That(rules[0].Name, Is.EqualTo("rule"));
        }

        [Test]
        public void WfTaskBase_Sanitize_RemovesInvalidCharacters()
        {
            WfTaskBase task = new()
            {
                Title = "Title!",
                FreeText = "Text!"
            };
            task.AssignedGroup = "cn=grp!,dc=example,dc=com";
            task.SetOptComment("note!");

            bool shortened = task.Sanitize();

            Assert.That(shortened, Is.True);
            Assert.That(task.Title, Is.EqualTo("Title"));
            Assert.That(task.FreeText, Is.EqualTo("Text"));
            Assert.That(task.AssignedGroup, Is.EqualTo("cn=grp,dc=example,dc=com"));
            Assert.That(task.OptComment(), Is.EqualTo("note"));
        }

        [Test]
        public void WfReqTaskBase_Sanitize_RemovesInvalidCharacters()
        {
            WfReqTaskBase task = new()
            {
                Reason = "reason!",
                Title = "Title!"
            };

            bool shortened = task.Sanitize();

            Assert.That(shortened, Is.True);
            Assert.That(task.Reason, Is.EqualTo("reason"));
            Assert.That(task.Title, Is.EqualTo("Title"));
        }

        [Test]
        public void WfStatefulObject_DisplayAllComments_FormatsText()
        {
            DateTime firstDate = new DateTime(2024, 1, 2);
            DateTime secondDate = new DateTime(2024, 2, 3);
            List<WfCommentDataHelper> comments =
            [
                new WfCommentDataHelper(new WfComment
                {
                    CreationDate = firstDate,
                    Creator = new UiUser { Name = "Alice" },
                    CommentText = "Hello"
                }),
                new WfCommentDataHelper(new WfComment
                {
                    CreationDate = secondDate,
                    Creator = new UiUser { Name = "Bob" },
                    CommentText = "World"
                })
            ];

            string text = WfStatefulObject.DisplayAllComments(comments);

            Assert.That(text, Does.Contain(firstDate.ToShortDateString()));
            Assert.That(text, Does.Contain("Alice: Hello"));
            Assert.That(text, Does.Contain(secondDate.ToShortDateString()));
            Assert.That(text, Does.Contain("Bob: World"));
            Assert.That(text, Does.Contain("\r\n"));

            string markup = WfStatefulObject.DisplayAllComments(comments, true);
            Assert.That(markup, Does.Contain("<br>"));
        }
    }
}

using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Ui.Pages.Settings;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.Reflection;
using static FWO.Test.WorkflowConfigurationComponentTestSupport;

namespace FWO.Test
{
    [TestFixture]
    internal class EditWorkflowVisibilityGroupsComponentTest
    {
        private static readonly string[] kSingleMemberDns = ["cn=a"];
        private static readonly string[] kNormalizedMemberDns = ["CN=User,DC=example", "CN=Second,DC=example", "CN=NestedGroup,DC=example"];

        [TestCase(null, null)]
        [TestCase("", null)]
        [TestCase("   ", null)]
        [TestCase("  Description  ", "Description")]
        public void NormalizeOptional_TrimsOrReturnsNull(string? input, string? expected)
        {
            Assert.That(InvokeStatic("NormalizeOptional", input), Is.EqualTo(expected));
        }

        [Test]
        public void Clone_CopiesMembersWithoutSharingMutableCollections()
        {
            WorkflowVisibilityGroup source = new()
            {
                Id = 7,
                Name = "Operators",
                Description = "Original",
                Members = [new() { VisibilityGroupId = 7, MemberDn = "cn=a" }]
            };

            WorkflowVisibilityGroup clone = (WorkflowVisibilityGroup)InvokeStatic("Clone", source)!;
            clone.Name = "Changed";
            clone.Members[0].MemberDn = "cn=b";
            clone.Members.Add(new() { MemberDn = "cn=c" });

            Assert.Multiple(() =>
            {
                Assert.That(clone.Id, Is.EqualTo(7));
                Assert.That(clone.Description, Is.EqualTo("Original"));
                Assert.That(source.Name, Is.EqualTo("Operators"));
                Assert.That(source.Members.Select(member => member.MemberDn), Is.EqualTo(kSingleMemberDns));
            });
        }

        [Test]
        public async Task MemberInputs_NormalizeAndDeduplicateUsersAndNestedGroups()
        {
            EditWorkflowVisibilityGroups component = new();

            Invoke(component, "AddMemberDn", " CN=User,DC=example ");
            Invoke(component, "AddMemberDn", "cn=user,dc=example");
            await InvokeAsync(component, "AddUserMember", new UiUser { Dn = "CN=Second,DC=example" });
            await InvokeAsync(component, "AddGroupMember", "CN=NestedGroup,DC=example");
            Invoke(component, "AddMemberDn", "   ");

            WorkflowVisibilityGroup group = GetField<WorkflowVisibilityGroup>(component, "editGroup");
            Assert.That(group.Members.Select(member => member.MemberDn), Is.EqualTo(kNormalizedMemberDns));
        }

        [Test]
        public void AddMember_UsesManualInputAndClearsIt()
        {
            EditWorkflowVisibilityGroups component = new();
            SetField(component, "newMemberDn", " cn=manual ");

            Invoke(component, "AddMember");

            Assert.Multiple(() =>
            {
                Assert.That(GetField<WorkflowVisibilityGroup>(component, "editGroup").Members[0].MemberDn, Is.EqualTo("cn=manual"));
                Assert.That(GetField<string>(component, "newMemberDn"), Is.Empty);
            });
        }

        [Test]
        public void CanSave_RequiresNameAndRejectsOtherGroupNameCaseInsensitively()
        {
            EditWorkflowVisibilityGroups component = new();
            SetField(component, "groups", new List<WorkflowVisibilityGroup>
            {
                new() { Id = 1, Name = "Existing" },
                new() { Id = 2, Name = "Current" }
            });
            WorkflowVisibilityGroup edit = new() { Id = 2, Name = "   " };
            SetField(component, "editGroup", edit);
            Assert.That(GetProperty<bool>(component, "CanSave"), Is.False);

            edit.Name = " existing ";
            Assert.That(GetProperty<bool>(component, "CanSave"), Is.False);

            edit.Name = " current ";
            Assert.That(GetProperty<bool>(component, "CanSave"), Is.True);

            edit.Name = "Unique";
            Assert.That(GetProperty<bool>(component, "CanSave"), Is.True);
        }

        [Test]
        public void AddAndEditGroup_InitializeIndependentEditorState()
        {
            EditWorkflowVisibilityGroups component = new();
            Invoke(component, "AddGroup");
            Assert.Multiple(() =>
            {
                Assert.That(GetField<bool>(component, "EditMode"), Is.True);
                Assert.That(GetField<WorkflowVisibilityGroup>(component, "editGroup").Id, Is.Zero);
            });

            WorkflowVisibilityGroup source = new() { Id = 4, Name = "Source", Members = [new() { MemberDn = "cn=a" }] };
            Invoke(component, "EditGroup", source);
            GetField<WorkflowVisibilityGroup>(component, "editGroup").Members.Clear();

            Assert.Multiple(() =>
            {
                Assert.That(source.Members, Has.Count.EqualTo(1));
                Assert.That(GetField<WorkflowVisibilityGroup>(component, "originalGroup").Members, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public void CloseEditor_ResetsTransientEditorState()
        {
            EditWorkflowVisibilityGroups component = new();
            SetField(component, "EditMode", true);
            SetField(component, "SearchMemberMode", true);
            SetField(component, "newMemberDn", "pending");

            Invoke(component, "CloseEditor");

            Assert.Multiple(() =>
            {
                Assert.That(GetField<bool>(component, "EditMode"), Is.False);
                Assert.That(GetField<bool>(component, "SearchMemberMode"), Is.False);
                Assert.That(GetField<string>(component, "newMemberDn"), Is.Empty);
            });
        }

        [Test]
        public async Task SaveMembers_SendsOnlyCaseInsensitiveSetDifferences()
        {
            RecordingWorkflowApiConnection api = new();
            api.Respond(RequestQueries.replaceWorkflowVisibilityGroupMembers, new object());
            EditWorkflowVisibilityGroups component = new();
            SetProperty(component, "apiConnection", api);
            SetField(component, "originalGroup", GroupWithMembers(8, "cn=keep", "cn=remove"));
            SetField(component, "editGroup", GroupWithMembers(8, "CN=KEEP", " cn=add "));

            await InvokeAsync(component, "SaveMembers", 8);

            JObject variables = JObject.FromObject(api.Calls.Single().Variables!);
            Assert.Multiple(() =>
            {
                Assert.That((string?)variables["removedMembers"]?[0]?["member_dn"]?["_eq"], Is.EqualTo("cn=remove"));
                Assert.That((int?)variables["removedMembers"]?[0]?["visibility_group_id"]?["_eq"], Is.EqualTo(8));
                Assert.That((string?)variables["members"]?[0]?["member_dn"], Is.EqualTo("cn=add"));
                Assert.That((int?)variables["members"]?[0]?["visibility_group_id"], Is.EqualTo(8));
            });
        }

        [Test]
        public async Task SaveMembers_DoesNotCallApiWhenSetsAreEqual()
        {
            RecordingWorkflowApiConnection api = new();
            EditWorkflowVisibilityGroups component = new();
            SetProperty(component, "apiConnection", api);
            SetField(component, "originalGroup", GroupWithMembers(3, "cn=a"));
            SetField(component, "editGroup", GroupWithMembers(3, " CN=A "));

            await InvokeAsync(component, "SaveMembers", 3);

            Assert.That(api.Calls, Is.Empty);
        }

        [TestCase(0, "createWorkflowVisibilityGroup", false)]
        [TestCase(12, "updateWorkflowVisibilityGroup", true)]
        public async Task Save_UsesCorrectCreateOrUpdateVariableShape(int groupId, string expectedOperation, bool expectsId)
        {
            RecordingWorkflowApiConnection api = new();
            string mutation = groupId == 0 ? RequestQueries.createWorkflowVisibilityGroup : RequestQueries.updateWorkflowVisibilityGroup;
            api.Respond(mutation, new ReturnId { NewId = 12, UpdatedId = groupId });
            api.Respond(RequestQueries.getWorkflowVisibilityGroups, new List<WorkflowVisibilityGroup>());
            EditWorkflowVisibilityGroups component = new();
            SetProperty(component, "apiConnection", api);
            SetField(component, "editGroup", new WorkflowVisibilityGroup
            {
                Id = groupId,
                Name = "  Group  ",
                Description = "   "
            });
            SetField(component, "originalGroup", new WorkflowVisibilityGroup { Id = groupId });

            await InvokeAsync(component, "Save");

            (string Query, object? Variables, Type ResponseType) mutationCall = api.Calls.First(call => call.Query == mutation);
            JObject variables = JObject.FromObject(mutationCall.Variables!);
            Assert.Multiple(() =>
            {
                Assert.That(mutationCall.Query, Does.Contain(expectedOperation));
                Assert.That((string?)variables["name"], Is.EqualTo("Group"));
                Assert.That(variables["description"]?.Type, Is.EqualTo(JTokenType.Null));
                Assert.That(variables["isActive"], Is.Null);
                Assert.That(variables["id"] != null, Is.EqualTo(expectsId));
            });
        }

        private static WorkflowVisibilityGroup GroupWithMembers(int id, params string[] dns) => new()
        {
            Id = id,
            Members = dns.Select(dn => new WorkflowVisibilityGroupMember { VisibilityGroupId = id, MemberDn = dn }).ToList()
        };

        private static object? InvokeStatic(string methodName, params object?[] parameters)
        {
            MethodInfo method = typeof(EditWorkflowVisibilityGroups).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(typeof(EditWorkflowVisibilityGroups).FullName, methodName);
            return method.Invoke(null, parameters);
        }
    }
}

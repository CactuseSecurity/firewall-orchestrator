using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Middleware.Client;
using FWO.Test.Mocks;
using FWO.Ui.Pages.Settings;
using FWO.Ui.Services;
using Bunit;
using NUnit.Framework;
using RestSharp;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace FWO.Test
{
    [TestFixture]
    internal class UiSettingsGroupsTest
    {
        [SetUp]
        public void SetUp()
        {
            SimulatedUserConfig.DummyTranslate["groups"] = "Groups";
            SimulatedUserConfig.DummyTranslate["U5214"] = "Group settings";
            SimulatedUserConfig.DummyTranslate["group_action"] = "Group action";
            SimulatedUserConfig.DummyTranslate["user_action"] = "User action";
            SimulatedUserConfig.DummyTranslate["add_new_group"] = "Add group";
            SimulatedUserConfig.DummyTranslate["edit_group"] = "Edit group";
            SimulatedUserConfig.DummyTranslate["delete_group"] = "Delete group";
            SimulatedUserConfig.DummyTranslate["assign_user"] = "Assign user";
            SimulatedUserConfig.DummyTranslate["remove_user"] = "Remove user";
            SimulatedUserConfig.DummyTranslate["name"] = "Name";
            SimulatedUserConfig.DummyTranslate["owner_group"] = "Owner group";
            SimulatedUserConfig.DummyTranslate["users"] = "Users";
            SimulatedUserConfig.DummyTranslate["roles"] = "Roles";
            SimulatedUserConfig.DummyTranslate["save_group"] = "Save group";
            SimulatedUserConfig.DummyTranslate["assign_user_to_group"] = "Assign user to group";
            SimulatedUserConfig.DummyTranslate["remove_user_from_group"] = "Remove user from group";
            SimulatedUserConfig.DummyTranslate["fetch_groups"] = "Fetch groups";
            SimulatedUserConfig.DummyTranslate["fetch_roles"] = "Fetch roles";
            SimulatedUserConfig.DummyTranslate["fetch_data"] = "Fetch data";
            SimulatedUserConfig.DummyTranslate["E5231"] = "Failed to fetch groups";
            SimulatedUserConfig.DummyTranslate["E5234"] = "Missing group name";
            SimulatedUserConfig.DummyTranslate["E5235"] = "Duplicate group name";
            SimulatedUserConfig.DummyTranslate["E5236"] = "Add group failed";
            SimulatedUserConfig.DummyTranslate["E5237"] = "Edit group failed";
            SimulatedUserConfig.DummyTranslate["E5238"] = "Group has users";
            SimulatedUserConfig.DummyTranslate["E5239"] = "Delete group failed";
            SimulatedUserConfig.DummyTranslate["E5240"] = "Missing user or group";
            SimulatedUserConfig.DummyTranslate["E5241"] = "Duplicate user";
            SimulatedUserConfig.DummyTranslate["E5242"] = "Add user failed";
            SimulatedUserConfig.DummyTranslate["E5243"] = "Remove user failed";
            SimulatedUserConfig.DummyTranslate["E5244"] = "Missing DN";
            SimulatedUserConfig.DummyTranslate["E5245"] = "Sample data still in use";
            SimulatedUserConfig.DummyTranslate["U5204"] = "Delete group ";
            SimulatedUserConfig.DummyTranslate["U5205"] = "Delete sample data";
            SimulatedUserConfig.DummyTranslate["E5251"] = "No roles found";
            SimulatedUserConfig.DummyTranslate["assign_user_group_to_role"] = "Assign user/group to role";
            SimulatedUserConfig.DummyTranslate["E5246"] = "Add group to role failed";
            SimulatedUserConfig.DummyTranslate["save"] = "Save";
            SimulatedUserConfig.DummyTranslate["cancel"] = "Cancel";
        }

        [Test]
        public async Task OnInitializedAsync_LoadsGroupsRolesAndSynchronizesMemberships()
        {
            SettingsGroups component = CreateComponent(
                new SettingsGroupsMiddlewareHandler
                {
                    GroupsJson = """
                        [
                          {
                            "GroupDn": "cn=DevTeam,ou=groups,dc=fworch,dc=internal",
                            "OwnerGroup": false,
                            "Members": [
                              "uid=alice,ou=users,dc=fworch,dc=internal"
                            ]
                          }
                        ]
                        """,
                    RolesJson = """
                        [
                          {
                            "Role": "cn=AppOwners,ou=roles,dc=fworch,dc=internal",
                            "Attributes": [
                              { "Key": "description", "Value": "Application owners" },
                              { "Key": "user", "Value": "cn=DevTeam,ou=groups,dc=fworch,dc=internal" }
                            ]
                          }
                        ]
                        """
                },
                out List<(Exception? Exception, string Title, string Message, bool IsError)> messages);

            await InvokePrivateTask(component, "OnInitializedAsync");

            Assert.Multiple(() =>
            {
                Assert.That(messages, Is.Empty);
                Assert.That(GetMember<List<UserGroup>>(component, "groups"), Has.Count.EqualTo(1));
                Assert.That(GetMember<List<Role>>(component, "roles"), Has.Count.EqualTo(1));
                Assert.That(GetMember<List<UserGroup>>(component, "groups")[0].Roles, Does.Contain("AppOwners"));
                Assert.That(GetMember<List<Role>>(component, "availableRoles"), Has.Count.EqualTo(1));
                Assert.That(GetMember<List<Role>>(component, "ownerGroupRoles"), Is.Empty);
            });
        }

        [Test]
        public async Task OnInitializedAsync_ShowsMessageWhenRoleLoadFails()
        {
            SettingsGroups component = CreateComponent(
                new SettingsGroupsMiddlewareHandler
                {
                    GroupsJson = "[]",
                    RolesJson = "[]",
                    RoleStatusCode = HttpStatusCode.BadGateway
                },
                out List<(Exception? Exception, string Title, string Message, bool IsError)> messages);

            await InvokePrivateTask(component, "OnInitializedAsync");

            Assert.That(messages, Has.Count.EqualTo(1));
            Assert.That(messages[0].Title, Is.EqualTo("Fetch roles"));
            Assert.That(messages[0].Message, Is.EqualTo("No roles found"));
            Assert.That(messages[0].IsError, Is.True);
        }

        [Test]
        public async Task OnInitializedAsync_ShowsMessageWhenGroupLoadFails()
        {
            SettingsGroups component = CreateComponent(
                new SettingsGroupsMiddlewareHandler
                {
                    GroupStatusCode = HttpStatusCode.BadGateway
                },
                out List<(Exception? Exception, string Title, string Message, bool IsError)> messages);

            await InvokePrivateTask(component, "OnInitializedAsync");

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(2));
                Assert.That(messages[0].Title, Is.EqualTo("Fetch groups"));
                Assert.That(messages[0].Message, Is.EqualTo("Failed to fetch groups"));
                Assert.That(messages[1].Title, Is.EqualTo("Fetch roles"));
                Assert.That(messages[1].Message, Is.EqualTo("No roles found"));
                Assert.That(messages[1].IsError, Is.True);
            });
        }

        [Test]
        public async Task Save_AddGroup_AddsGroupAndRoleAssignment()
        {
            SettingsGroups component = CreateComponent(
                new SettingsGroupsMiddlewareHandler
                {
                    GroupsJson = "[]",
                    RolesJson = """
                        [
                          {
                            "Role": "cn=AppOwners,ou=roles,dc=fworch,dc=internal",
                            "Attributes": [
                              { "Key": "user", "Value": "uid=alice,ou=users,dc=fworch,dc=internal" }
                            ]
                          }
                        ]
                        """,
                    AddGroupBody = "\"cn=NewGroup,ou=groups,dc=fworch,dc=internal\"",
                    AddUserToRoleBody = "true"
                },
                out List<(Exception? Exception, string Title, string Message, bool IsError)> messages);

            await InvokePrivateTask(component, "OnInitializedAsync");
            Role selectedRole = GetMember<List<Role>>(component, "roles")[0];
            SetMember(component, "AddGroupMode", true);
            SetMember(component, "EditGroupMode", true);
            SetMember(component, "actGroup", new UserGroup { OwnerGroup = false });
            SetMember(component, "newGroupName", "NewGroup");
            SetMember(component, "selectedRole", selectedRole);

            await InvokePrivateTask(component, "Save");

            Assert.Multiple(() =>
            {
                Assert.That(messages, Is.Empty);
                Assert.That(GetMember<List<UserGroup>>(component, "groups"), Has.Count.EqualTo(1));
                Assert.That(GetMember<List<UserGroup>>(component, "groups")[0].Dn, Is.EqualTo("cn=NewGroup,ou=groups,dc=fworch,dc=internal"));
                Assert.That(GetMember<List<UserGroup>>(component, "groups")[0].Roles, Does.Contain("AppOwners"));
                Assert.That(GetMember<bool>(component, "AddGroupMode"), Is.False);
                Assert.That(GetMember<bool>(component, "EditGroupMode"), Is.False);
            });
        }

        [Test]
        public async Task Save_ShowsValidationErrorsForMissingAndDuplicateGroupName()
        {
            SettingsGroups component = CreateComponent(
                new SettingsGroupsMiddlewareHandler
                {
                    GroupsJson = """
                        [
                          {
                            "GroupDn": "cn=DevTeam,ou=groups,dc=fworch,dc=internal",
                            "OwnerGroup": false,
                            "Members": []
                          }
                        ]
                        """,
                    RolesJson = """
                        [
                          {
                            "Role": "cn=AppOwners,ou=roles,dc=fworch,dc=internal",
                            "Attributes": [
                              { "Key": "user", "Value": "uid=alice,ou=users,dc=fworch,dc=internal" }
                            ]
                          }
                        ]
                        """
                },
                out List<(Exception? Exception, string Title, string Message, bool IsError)> messages);

            await InvokePrivateTask(component, "OnInitializedAsync");
            SetMember(component, "AddGroupMode", true);
            SetMember(component, "EditGroupMode", true);
            SetMember(component, "actGroup", new UserGroup { OwnerGroup = false });

            await InvokePrivateTask(component, "Save");
            SetMember(component, "newGroupName", "DevTeam");
            await InvokePrivateTask(component, "Save");

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(2));
                Assert.That(messages[0].Message, Is.EqualTo("Missing group name"));
                Assert.That(messages[1].Message, Is.EqualTo("Duplicate group name"));
                Assert.That(GetMember<bool>(component, "AddGroupMode"), Is.True);
                Assert.That(GetMember<bool>(component, "EditGroupMode"), Is.True);
            });
        }

        [Test]
        public async Task Save_ShowsMessageWhenAddGroupMiddlewareFails()
        {
            SettingsGroups component = CreateComponent(
                new SettingsGroupsMiddlewareHandler
                {
                    GroupsJson = "[]",
                    RolesJson = """
                        [
                          {
                            "Role": "cn=AppOwners,ou=roles,dc=fworch,dc=internal",
                            "Attributes": [
                              { "Key": "user", "Value": "uid=alice,ou=users,dc=fworch,dc=internal" }
                            ]
                          }
                        ]
                        """,
                    AddGroupStatusCode = HttpStatusCode.BadGateway
                },
                out List<(Exception? Exception, string Title, string Message, bool IsError)> messages);

            await InvokePrivateTask(component, "OnInitializedAsync");
            SetMember(component, "AddGroupMode", true);
            SetMember(component, "EditGroupMode", true);
            SetMember(component, "actGroup", new UserGroup { OwnerGroup = false });
            SetMember(component, "newGroupName", "NewGroup");

            await InvokePrivateTask(component, "Save");

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("Add group"));
                Assert.That(messages[0].Message, Is.EqualTo("Add group failed"));
                Assert.That(GetMember<List<UserGroup>>(component, "groups"), Is.Empty);
                Assert.That(GetMember<bool>(component, "AddGroupMode"), Is.True);
            });
        }

        [Test]
        public async Task Save_ShowsMessageWhenRoleAssignmentFails()
        {
            SettingsGroups component = CreateComponent(
                new SettingsGroupsMiddlewareHandler
                {
                    GroupsJson = "[]",
                    RolesJson = """
                        [
                          {
                            "Role": "cn=AppOwners,ou=roles,dc=fworch,dc=internal",
                            "Attributes": [
                              { "Key": "user", "Value": "uid=alice,ou=users,dc=fworch,dc=internal" }
                            ]
                          }
                        ]
                        """,
                    AddGroupBody = "\"cn=NewGroup,ou=groups,dc=fworch,dc=internal\"",
                    AddUserToRoleStatusCode = HttpStatusCode.BadGateway
                },
                out List<(Exception? Exception, string Title, string Message, bool IsError)> messages);

            await InvokePrivateTask(component, "OnInitializedAsync");
            SetMember(component, "AddGroupMode", true);
            SetMember(component, "EditGroupMode", true);
            SetMember(component, "actGroup", new UserGroup { OwnerGroup = false });
            SetMember(component, "newGroupName", "NewGroup");
            SetMember(component, "selectedRole", GetMember<List<Role>>(component, "roles")[0]);

            await InvokePrivateTask(component, "Save");

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("Assign user/group to role"));
                Assert.That(messages[0].Message, Is.EqualTo("Add group to role failed"));
                Assert.That(GetMember<List<UserGroup>>(component, "groups"), Has.Count.EqualTo(1));
                Assert.That(GetMember<List<UserGroup>>(component, "groups")[0].Roles, Does.Contain("AppOwners"));
                Assert.That(GetMember<bool>(component, "AddGroupMode"), Is.False);
                Assert.That(GetMember<bool>(component, "EditGroupMode"), Is.False);
            });
        }

        [Test]
        public async Task Save_ShowsMessageWhenUpdateMiddlewareFails()
        {
            SettingsGroups component = CreateComponent(
                new SettingsGroupsMiddlewareHandler
                {
                    GroupsJson = """
                        [
                          {
                            "GroupDn": "cn=DevTeam,ou=groups,dc=fworch,dc=internal",
                            "OwnerGroup": false,
                            "Members": []
                          }
                        ]
                        """,
                    RolesJson = """
                        [
                          {
                            "Role": "cn=AppOwners,ou=roles,dc=fworch,dc=internal",
                            "Attributes": [
                              { "Key": "user", "Value": "uid=alice,ou=users,dc=fworch,dc=internal" }
                            ]
                          }
                        ]
                        """,
                    UpdateGroupStatusCode = HttpStatusCode.BadGateway
                },
                out List<(Exception? Exception, string Title, string Message, bool IsError)> messages);

            await InvokePrivateTask(component, "OnInitializedAsync");
            UserGroup group = GetMember<List<UserGroup>>(component, "groups")[0];
            SetMember(component, "actGroup", group);
            SetMember(component, "newGroupName", "DevTeamRenamed");
            SetMember(component, "EditGroupMode", true);

            await InvokePrivateTask(component, "Save");

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("Edit group"));
                Assert.That(messages[0].Message, Is.EqualTo("Edit group failed"));
                Assert.That(GetMember<List<UserGroup>>(component, "groups")[0].Name, Is.EqualTo("DevTeam"));
                Assert.That(GetMember<bool>(component, "EditGroupMode"), Is.True);
            });
        }

        [Test]
        public async Task Save_UpdatesExistingGroupSuccessfully()
        {
            SettingsGroups component = CreateComponent(
                new SettingsGroupsMiddlewareHandler
                {
                    GroupsJson = """
                        [
                          {
                            "GroupDn": "cn=DevTeam,ou=groups,dc=fworch,dc=internal",
                            "OwnerGroup": false,
                            "Members": []
                          }
                        ]
                        """,
                    RolesJson = """
                        [
                          {
                            "Role": "cn=AppOwners,ou=roles,dc=fworch,dc=internal",
                            "Attributes": [
                              { "Key": "user", "Value": "uid=alice,ou=users,dc=fworch,dc=internal" }
                            ]
                          }
                        ]
                        """,
                    UpdateGroupBody = "\"cn=DevTeamRenamed,ou=groups,dc=fworch,dc=internal\""
                },
                out List<(Exception? Exception, string Title, string Message, bool IsError)> messages);

            await InvokePrivateTask(component, "OnInitializedAsync");
            UserGroup group = GetMember<List<UserGroup>>(component, "groups")[0];
            SetMember(component, "actGroup", group);
            SetMember(component, "newGroupName", "DevTeamRenamed");

            await InvokePrivateTask(component, "Save");

            Assert.Multiple(() =>
            {
                Assert.That(messages, Is.Empty);
                Assert.That(GetMember<List<UserGroup>>(component, "groups")[0].Name, Is.EqualTo("DevTeamRenamed"));
                Assert.That(GetMember<List<UserGroup>>(component, "groups")[0].Dn, Is.EqualTo("cn=DevTeamRenamed,ou=groups,dc=fworch,dc=internal"));
                Assert.That(GetMember<bool>(component, "EditGroupMode"), Is.False);
            });
        }

        [Test]
        public async Task RequestDeleteGroup_SetsGuardMessageForUsedAndEmptyGroups()
        {
            SettingsGroups component = CreateComponent(
                new SettingsGroupsMiddlewareHandler
                {
                    GroupsJson = """
                        [
                          {
                            "GroupDn": "cn=UsedGroup,ou=groups,dc=fworch,dc=internal",
                            "OwnerGroup": false,
                            "Members": [
                              "uid=alice,ou=users,dc=fworch,dc=internal"
                            ]
                          },
                          {
                            "GroupDn": "cn=EmptyGroup,ou=groups,dc=fworch,dc=internal",
                            "OwnerGroup": false,
                            "Members": []
                          }
                        ]
                        """,
                    RolesJson = """
                        [
                          {
                            "Role": "cn=AppOwners,ou=roles,dc=fworch,dc=internal",
                            "Attributes": [
                              { "Key": "user", "Value": "uid=alice,ou=users,dc=fworch,dc=internal" }
                            ]
                          }
                        ]
                        """
                },
                out List<(Exception? Exception, string Title, string Message, bool IsError)> messages);

            await InvokePrivateTask(component, "OnInitializedAsync");
            InvokePrivateVoid(component, "RequestDeleteGroup", GetMember<List<UserGroup>>(component, "groups")[0]);
            Assert.Multiple(() =>
            {
                Assert.That(messages, Is.Empty);
                Assert.That(GetMember<string>(component, "deleteGroupMessage"), Is.EqualTo("Group has users"));
                Assert.That(GetMember<bool>(component, "DeleteGroupAllowed"), Is.False);
                Assert.That(GetMember<bool>(component, "DeleteGroupMode"), Is.True);
            });

            UserGroup emptyGroup = new()
            {
                Name = "EmptyGroup",
                Dn = "cn=EmptyGroup,ou=groups,dc=fworch,dc=internal"
            };
            InvokePrivateVoid(component, "RequestDeleteGroup", emptyGroup);
            Assert.Multiple(() =>
            {
                Assert.That(messages, Is.Empty);
                Assert.That(GetMember<string>(component, "deleteGroupMessage"), Is.EqualTo("Delete group EmptyGroup?"));
                Assert.That(GetMember<bool>(component, "DeleteGroupAllowed"), Is.True);
            });
        }

        [Test]
        public async Task DeleteGroup_ShowsMessageWhenMiddlewareFails()
        {
            SettingsGroupsMiddlewareHandler handler = new()
            {
                GroupsJson = """
                    [
                      {
                        "GroupDn": "cn=Deletable,ou=groups,dc=fworch,dc=internal",
                        "OwnerGroup": false,
                        "Members": []
                      }
                    ]
                    """,
                RolesJson = """
                    [
                      {
                        "Role": "cn=AppOwners,ou=roles,dc=fworch,dc=internal",
                        "Attributes": [
                          { "Key": "user", "Value": "uid=alice,ou=users,dc=fworch,dc=internal" }
                        ]
                      }
                    ]
                    """,
                DeleteGroupStatusCode = HttpStatusCode.BadGateway
            };

            await using BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new AllowAllAuthStateProvider(Roles.Admin));
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton(typeof(IStringLocalizer<>), typeof(EmptyStringLocalizer<>));
            context.Services.AddSingleton<ApiConnection>(new SimulatedApiConnection());

            TestMiddlewareClient middlewareClient = new("https://middleware.example/");
            middlewareClient.UseHandler(handler);
            context.Services.AddSingleton<MiddlewareClient>(middlewareClient);
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());

            IRenderedComponent<CascadingAuthenticationState> rendered = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<SettingsGroups>());
            SettingsGroups component = rendered.FindComponent<SettingsGroups>().Instance;
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            SetMember(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((exception, title, message, isError) =>
            {
                messages.Add((exception, title, message, isError));
            }));
            SetMember(component, "actGroup", GetMember<List<UserGroup>>(component, "groups")[0]);

            await rendered.InvokeAsync(async () => await InvokePrivateTask(component, "DeleteGroup"));

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("Delete group"));
                Assert.That(messages[0].Message, Is.EqualTo("Delete group failed"));
                Assert.That(GetMember<List<UserGroup>>(component, "groups"), Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task RequestRemoveSampleData_ShowsGuardWhenRealUsersArePresent()
        {
            SettingsGroups component = CreateComponent(
                new SettingsGroupsMiddlewareHandler
                {
                    GroupsJson = """
                        [
                          {
                            "GroupDn": "cn=DevTeam_demo,ou=groups,dc=fworch,dc=internal",
                            "OwnerGroup": false,
                            "Members": [
                              "uid=alice,ou=users,dc=fworch,dc=internal"
                            ]
                          }
                        ]
                        """,
                    RolesJson = """
                        [
                          {
                            "Role": "cn=AppOwners,ou=roles,dc=fworch,dc=internal",
                            "Attributes": [
                              { "Key": "user", "Value": "uid=alice,ou=users,dc=fworch,dc=internal" }
                            ]
                          }
                        ]
                        """
                },
                out List<(Exception? Exception, string Title, string Message, bool IsError)> messages);

            await InvokePrivateTask(component, "OnInitializedAsync");
            Assert.That(GetMember<List<UserGroup>>(component, "sampleGroups"), Has.Count.EqualTo(1));
            InvokePrivateVoid(component, "RequestRemoveSampleData");

            Assert.Multiple(() =>
            {
                Assert.That(messages, Is.Empty);
                Assert.That(GetMember<string>(component, "sampleRemoveMessage"), Is.EqualTo("Sample data still in use"));
                Assert.That(GetMember<bool>(component, "sampleRemoveAllowed"), Is.False);
                Assert.That(GetMember<bool>(component, "SampleRemoveMode"), Is.True);
            });
        }

        [Test]
        public async Task RemoveSampleData_RemovesDemoGroups()
        {
            SettingsGroupsMiddlewareHandler handler = new()
            {
                GroupsJson = """
                    [
                      {
                        "GroupDn": "cn=DevTeam_demo,ou=groups,dc=fworch,dc=internal",
                        "OwnerGroup": false,
                        "Members": [
                          "uid=alice_demo,ou=users,dc=fworch,dc=internal"
                        ]
                      },
                      {
                        "GroupDn": "cn=ProdTeam,ou=groups,dc=fworch,dc=internal",
                        "OwnerGroup": false,
                        "Members": []
                      }
                    ]
                    """,
                RolesJson = """
                    [
                      {
                        "Role": "cn=AppOwners,ou=roles,dc=fworch,dc=internal",
                        "Attributes": [
                          { "Key": "user", "Value": "uid=alice_demo,ou=users,dc=fworch,dc=internal" }
                        ]
                      }
                    ]
                    """
            };

            await using BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new AllowAllAuthStateProvider(Roles.Admin));
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton(typeof(IStringLocalizer<>), typeof(EmptyStringLocalizer<>));
            context.Services.AddSingleton<ApiConnection>(new SimulatedApiConnection());

            TestMiddlewareClient middlewareClient = new("https://middleware.example/");
            middlewareClient.UseHandler(handler);
            context.Services.AddSingleton<MiddlewareClient>(middlewareClient);
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());

            IRenderedComponent<CascadingAuthenticationState> rendered = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<SettingsGroups>());
            SettingsGroups component = rendered.FindComponent<SettingsGroups>().Instance;
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            SetMember(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((exception, title, message, isError) =>
            {
                messages.Add((exception, title, message, isError));
            }));

            await InvokePrivateTask(component, "OnInitializedAsync");
            InvokePrivateVoid(component, "RequestRemoveSampleData");
            await rendered.InvokeAsync(async () => await InvokePrivateTask(component, "RemoveSampleData"));

            Assert.Multiple(() =>
            {
                Assert.That(messages, Is.Empty);
                Assert.That(GetMember<List<UserGroup>>(component, "groups"), Has.Count.EqualTo(1));
                Assert.That(GetMember<List<UserGroup>>(component, "groups")[0].Name, Is.EqualTo("ProdTeam"));
                Assert.That(GetMember<bool>(component, "showSampleRemoveButton"), Is.False);
                Assert.That(GetMember<bool>(component, "SampleRemoveMode"), Is.False);
                Assert.That(GetMember<bool>(component, "workInProgress"), Is.False);
            });
        }

        [Test]
        public async Task AddUserFromUiUsers_ShowsMessageWhenMiddlewareFails()
        {
            SettingsGroups component = CreateComponent(
                new SettingsGroupsMiddlewareHandler
                {
                    GroupsJson = """
                        [
                          {
                            "GroupDn": "cn=DevTeam,ou=groups,dc=fworch,dc=internal",
                            "OwnerGroup": false,
                            "Members": []
                          }
                        ]
                        """,
                    RolesJson = """
                        [
                          {
                            "Role": "cn=AppOwners,ou=roles,dc=fworch,dc=internal",
                            "Attributes": [
                              { "Key": "user", "Value": "uid=alice,ou=users,dc=fworch,dc=internal" }
                            ]
                          }
                        ]
                        """,
                    AddUserToGroupStatusCode = HttpStatusCode.BadGateway
                },
                out List<(Exception? Exception, string Title, string Message, bool IsError)> messages);

            await InvokePrivateTask(component, "OnInitializedAsync");
            UserGroup group = GetMember<List<UserGroup>>(component, "groups")[0];
            SetMember(component, "actGroup", group);

            await InvokePrivateTask(component, "AddUserFromUiUsers", new UiUser { Dn = "uid=bob,ou=users,dc=fworch,dc=internal", Name = "bob" });

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("Assign user to group"));
                Assert.That(messages[0].Message, Is.EqualTo("Add user failed"));
                Assert.That(GetMember<List<UserGroup>>(component, "groups")[0].Users, Is.Empty);
            });
        }

        [Test]
        public async Task RemoveUserFromGroup_ShowsMessageWhenMiddlewareFails()
        {
            SettingsGroups component = CreateComponent(
                new SettingsGroupsMiddlewareHandler
                {
                    GroupsJson = """
                        [
                          {
                            "GroupDn": "cn=DevTeam,ou=groups,dc=fworch,dc=internal",
                            "OwnerGroup": false,
                            "Members": [
                              "uid=alice,ou=users,dc=fworch,dc=internal"
                            ]
                          }
                        ]
                        """,
                    RolesJson = """
                        [
                          {
                            "Role": "cn=AppOwners,ou=roles,dc=fworch,dc=internal",
                            "Attributes": [
                              { "Key": "user", "Value": "uid=alice,ou=users,dc=fworch,dc=internal" }
                            ]
                          }
                        ]
                        """,
                    RemoveUserFromGroupStatusCode = HttpStatusCode.BadGateway
                },
                out List<(Exception? Exception, string Title, string Message, bool IsError)> messages);

            await InvokePrivateTask(component, "OnInitializedAsync");
            UserGroup group = GetMember<List<UserGroup>>(component, "groups")[0];
            SetMember(component, "actGroup", group);

            await InvokePrivateTask(component, "RemoveUserFromGroup", group.Users[0]);

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("Remove user from group"));
                Assert.That(messages[0].Message, Is.EqualTo("Remove user failed"));
                Assert.That(GetMember<List<UserGroup>>(component, "groups")[0].Users, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task AddUserFromUiUsers_ShowsDuplicateAndSuccessBranches()
        {
            SettingsGroups component = CreateComponent(
                new SettingsGroupsMiddlewareHandler
                {
                    GroupsJson = """
                        [
                          {
                            "GroupDn": "cn=DevTeam,ou=groups,dc=fworch,dc=internal",
                            "OwnerGroup": false,
                            "Members": [
                              "uid=alice,ou=users,dc=fworch,dc=internal"
                            ]
                          }
                        ]
                        """,
                    RolesJson = """
                        [
                          {
                            "Role": "cn=AppOwners,ou=roles,dc=fworch,dc=internal",
                            "Attributes": [
                              { "Key": "user", "Value": "uid=carol,ou=users,dc=fworch,dc=internal" }
                            ]
                          }
                        ]
                        """,
                    AddUserToGroupBody = "true"
                },
                out List<(Exception? Exception, string Title, string Message, bool IsError)> messages);

            await InvokePrivateTask(component, "OnInitializedAsync");
            UserGroup group = GetMember<List<UserGroup>>(component, "groups")[0];
            SetMember(component, "actGroup", group);

            await InvokePrivateTask(component, "AddUserFromUiUsers", new UiUser { Dn = "", Name = "" });
            await InvokePrivateTask(component, "AddUserFromUiUsers", group.Users[0]);
            await InvokePrivateTask(component, "AddUserFromUiUsers", new UiUser { Dn = "uid=bob,ou=users,dc=fworch,dc=internal", Name = "bob" });

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(2));
                Assert.That(messages[0].Message, Is.EqualTo("Missing user or group"));
                Assert.That(messages[1].Message, Is.EqualTo("Duplicate user"));
                Assert.That(GetMember<List<UserGroup>>(component, "groups")[0].Users, Has.Count.EqualTo(2));
                Assert.That(GetMember<bool>(component, "AddUserMode"), Is.False);
            });
        }

        [Test]
        public async Task RemoveUserFromGroup_ShowsGuardAndSuccessBranches()
        {
            SettingsGroups component = CreateComponent(
                new SettingsGroupsMiddlewareHandler
                {
                    GroupsJson = """
                        [
                          {
                            "GroupDn": "cn=DevTeam,ou=groups,dc=fworch,dc=internal",
                            "OwnerGroup": false,
                            "Members": [
                              "uid=alice,ou=users,dc=fworch,dc=internal"
                            ]
                          }
                        ]
                        """,
                    RolesJson = """
                        [
                          {
                            "Role": "cn=AppOwners,ou=roles,dc=fworch,dc=internal",
                            "Attributes": [
                              { "Key": "user", "Value": "uid=carol,ou=users,dc=fworch,dc=internal" }
                            ]
                          }
                        ]
                        """,
                    RemoveUserFromGroupBody = "true"
                },
                out List<(Exception? Exception, string Title, string Message, bool IsError)> messages);

            await InvokePrivateTask(component, "OnInitializedAsync");
            UserGroup group = GetMember<List<UserGroup>>(component, "groups")[0];
            SetMember(component, "actGroup", group);

            await InvokePrivateTask(component, "RemoveUserFromGroup", new UiUser());
            await InvokePrivateTask(component, "RemoveUserFromGroup", group.Users[0]);

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Message, Is.EqualTo("Missing DN"));
                Assert.That(GetMember<List<UserGroup>>(component, "groups")[0].Users, Is.Empty);
                Assert.That(GetMember<bool>(component, "RemoveUserMode"), Is.False);
            });
        }

        private static SettingsGroups CreateComponent(
            SettingsGroupsMiddlewareHandler handler,
            out List<(Exception? Exception, string Title, string Message, bool IsError)> messages)
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> capturedMessages = [];
            SettingsGroups component = new();
            SimulatedUserConfig userConfig = new();
            userConfig.User.Roles = [Roles.Admin];
            userConfig.User.Name = "group-test-user";
            userConfig.User.Dn = "uid=group-test-user,ou=users,dc=fworch,dc=internal";

            TestMiddlewareClient middlewareClient = new("https://middleware.example/");
            middlewareClient.UseHandler(handler);

            SetMember(component, "apiConnection", new SimulatedApiConnection());
            SetMember(component, "middlewareClient", middlewareClient);
            SetMember(component, "userConfig", userConfig);
            SetMember(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((exception, title, message, isError) =>
            {
                capturedMessages.Add((exception, title, message, isError));
            }));
            messages = capturedMessages;
            return component;
        }

        private static void SetMember<T>(object instance, string memberName, T value)
        {
            Type type = instance.GetType();
            PropertyInfo? property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
            {
                property.SetValue(instance, value);
                return;
            }

            FieldInfo? field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(instance, value);
                return;
            }

            throw new MissingMemberException(type.FullName, memberName);
        }

        private static T GetMember<T>(object instance, string memberName)
        {
            Type type = instance.GetType();
            PropertyInfo? property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
            {
                return (T)property.GetValue(instance)!;
            }

            FieldInfo? field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                return (T)field.GetValue(instance)!;
            }

            throw new MissingMemberException(type.FullName, memberName);
        }

        private static async Task InvokePrivateTask(object instance, string methodName, params object?[] args)
        {
            MethodInfo method = FindPrivateMethod(instance.GetType(), methodName, args);
            Task task = (Task)(method.Invoke(instance, args) ?? throw new InvalidOperationException($"{methodName} returned null task."));
            await task;
        }

        private static void InvokePrivateVoid(object instance, string methodName, params object?[] args)
        {
            MethodInfo method = FindPrivateMethod(instance.GetType(), methodName, args);
            method.Invoke(instance, args);
        }

        private static MethodInfo FindPrivateMethod(Type type, string methodName, object?[] args)
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                         .Where(m => m.Name == methodName))
            {
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != args.Length)
                {
                    continue;
                }

                bool matches = true;
                for (int i = 0; i < parameters.Length; i++)
                {
                    Type parameterType = parameters[i].ParameterType;
                    object? arg = args[i];

                    if (arg == null)
                    {
                        if (parameterType.IsValueType && Nullable.GetUnderlyingType(parameterType) == null)
                        {
                            matches = false;
                            break;
                        }

                        continue;
                    }

                    if (!parameterType.IsAssignableFrom(arg.GetType()))
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                {
                    return method;
                }
            }

            throw new MissingMethodException(type.FullName, methodName);
        }

        private sealed class EmptyStringLocalizer<T> : IStringLocalizer<T>
        {
            public LocalizedString this[string name] => new(name, name, resourceNotFound: true);

            public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments), resourceNotFound: true);

            public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];

            public EmptyStringLocalizer<T> WithCulture(System.Globalization.CultureInfo culture) => this;
        }

        private sealed class SettingsGroupsMiddlewareHandler : HttpMessageHandler
        {
            public string GroupsJson { get; set; } = "[]";
            public string RolesJson { get; set; } = "[]";
            public HttpStatusCode GroupStatusCode { get; set; } = HttpStatusCode.OK;
            public HttpStatusCode RoleStatusCode { get; set; } = HttpStatusCode.OK;
            public string AddGroupBody { get; set; } = "\"cn=NewGroup,ou=groups,dc=fworch,dc=internal\"";
            public HttpStatusCode AddGroupStatusCode { get; set; } = HttpStatusCode.OK;
            public string UpdateGroupBody { get; set; } = "\"cn=RenamedGroup,ou=groups,dc=fworch,dc=internal\"";
            public HttpStatusCode UpdateGroupStatusCode { get; set; } = HttpStatusCode.OK;
            public string AddUserToGroupBody { get; set; } = "true";
            public HttpStatusCode AddUserToGroupStatusCode { get; set; } = HttpStatusCode.OK;
            public string AddUserToRoleBody { get; set; } = "true";
            public HttpStatusCode AddUserToRoleStatusCode { get; set; } = HttpStatusCode.OK;
            public string RemoveUserFromGroupBody { get; set; } = "true";
            public HttpStatusCode RemoveUserFromGroupStatusCode { get; set; } = HttpStatusCode.OK;
            public string DeleteGroupBody { get; set; } = "true";
            public HttpStatusCode DeleteGroupStatusCode { get; set; } = HttpStatusCode.OK;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string path = request.RequestUri?.AbsolutePath ?? "";
                if (request.Method == HttpMethod.Get && path.EndsWith("/Group", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(JsonResponse(GroupStatusCode, GroupsJson));
                }

                if (request.Method == HttpMethod.Get && path.EndsWith("/Role", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(JsonResponse(RoleStatusCode, RolesJson));
                }

                if (request.Method == HttpMethod.Post && path.EndsWith("/Group", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(JsonResponse(AddGroupStatusCode, AddGroupBody));
                }

                if (request.Method == HttpMethod.Put && path.EndsWith("/Group", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(JsonResponse(UpdateGroupStatusCode, UpdateGroupBody));
                }

                if (request.Method == HttpMethod.Post && path.EndsWith("/Group/User", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(JsonResponse(AddUserToGroupStatusCode, AddUserToGroupBody));
                }

                if (request.Method == HttpMethod.Post && path.EndsWith("/Role/User", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(JsonResponse(AddUserToRoleStatusCode, AddUserToRoleBody));
                }

                if (request.Method == HttpMethod.Delete && path.EndsWith("/Group/User", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(JsonResponse(RemoveUserFromGroupStatusCode, RemoveUserFromGroupBody));
                }

                if (request.Method == HttpMethod.Delete && path.EndsWith("/Group", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(JsonResponse(DeleteGroupStatusCode, DeleteGroupBody));
                }

                throw new InvalidOperationException($"Unexpected middleware call: {request.Method} {path}");
            }

            private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string body)
            {
                HttpContent content = new StringContent(body, System.Text.Encoding.UTF8);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                return new HttpResponseMessage(statusCode)
                {
                    Content = content
                };
            }
        }
    }
}

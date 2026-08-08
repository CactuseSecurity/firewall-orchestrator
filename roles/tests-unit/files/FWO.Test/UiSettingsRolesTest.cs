using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Middleware.Client;
using FWO.Services.EventMediator.Events;
using FWO.Test.Mocks;
using FWO.Ui.Pages.Settings;
using NUnit.Framework;
using RestSharp;
using System.Net;
using System.Net.Http;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace FWO.Test
{
    [TestFixture]
    internal class UiSettingsRolesTest
    {
        [SetUp]
        public void SetUp()
        {
            SimulatedUserConfig.DummyTranslate["roles"] = "Roles";
            SimulatedUserConfig.DummyTranslate["U5215"] = "Role assignments";
            SimulatedUserConfig.DummyTranslate["actions"] = "Actions";
            SimulatedUserConfig.DummyTranslate["assign_user_group"] = "Assign user/group";
            SimulatedUserConfig.DummyTranslate["remove_user_group"] = "Remove user/group";
            SimulatedUserConfig.DummyTranslate["name"] = "Name";
            SimulatedUserConfig.DummyTranslate["description"] = "Description";
            SimulatedUserConfig.DummyTranslate["users_groups"] = "Users/groups";
            SimulatedUserConfig.DummyTranslate["assign_user_group_to_role"] = "Assign user/group to role";
            SimulatedUserConfig.DummyTranslate["remove_user_group_from_role"] = "Remove user/group from role";
            SimulatedUserConfig.DummyTranslate["user_group"] = "User/group";
            SimulatedUserConfig.DummyTranslate["fetch_roles"] = "Fetch roles";
            SimulatedUserConfig.DummyTranslate["fetch_data"] = "Fetch data";
            SimulatedUserConfig.DummyTranslate["E5251"] = "No roles found";
            SimulatedUserConfig.DummyTranslate["E5240"] = "Missing user or group";
            SimulatedUserConfig.DummyTranslate["E5254"] = "Duplicate assignment";
            SimulatedUserConfig.DummyTranslate["E5255"] = "Assignment failed";
            SimulatedUserConfig.DummyTranslate["E5256"] = "Admin role must keep one user";
            SimulatedUserConfig.DummyTranslate["E5257"] = "Removal failed";
            SimulatedUserConfig.DummyTranslate["E5258"] = "Missing DN";
        }

        [Test]
        public async Task OnInitializedAsync_LoadsRolesFromMiddleware()
        {
            RecordingRoleMiddlewareHandler handler = new("""
                [
                  {
                    "Role": "cn=AppOwners,ou=roles,dc=fworch,dc=internal",
                    "Attributes": [
                      { "Key": "description", "Value": "Application owners" },
                      { "Key": "user", "Value": "uid=alice,ou=users,dc=fworch,dc=internal" },
                      { "Key": "user", "Value": "uid=bob,ou=users,dc=fworch,dc=internal" }
                    ]
                  }
                ]
                """);
            SettingsRoles component = CreateComponent(handler, out List<(Exception? Exception, string Title, string Message, bool IsError)> messages, out RecordingEventMediator eventMediator);

            await InvokePrivateTask(component, "OnInitializedAsync");

            Assert.Multiple(() =>
            {
                Assert.That(messages, Is.Empty);
                Assert.That(GetMember<List<Role>>(component, "roles"), Has.Count.EqualTo(1));
                Assert.That(GetMember<List<Role>>(component, "roles")[0].Name, Is.EqualTo("AppOwners"));
                Assert.That(GetMember<List<Role>>(component, "roles")[0].UserList(), Is.EqualTo("alice, bob"));
                Assert.That(eventMediator.PublishedEvents, Is.Empty);
            });
        }

        [Test]
        public async Task OnInitializedAsync_ShowsMessageWhenNoRolesAreReturned()
        {
            RecordingRoleMiddlewareHandler handler = new("[]");
            SettingsRoles component = CreateComponent(handler, out List<(Exception? Exception, string Title, string Message, bool IsError)> messages, out _);

            await InvokePrivateTask(component, "OnInitializedAsync");

            Assert.That(messages, Has.Count.EqualTo(1));
            Assert.That(messages[0].Title, Is.EqualTo("Fetch roles"));
            Assert.That(messages[0].Message, Is.EqualTo("No roles found"));
            Assert.That(messages[0].IsError, Is.True);
        }

        [Test]
        public async Task AddUserFromLdap_AddsUserAndPublishesPermissionChange()
        {
            RecordingRoleMiddlewareHandler handler = new(rolesJson: """
                [
                  {
                    "Role": "cn=AppOwners,ou=roles,dc=fworch,dc=internal",
                    "Attributes": [
                      { "Key": "description", "Value": "Application owners" },
                      { "Key": "user", "Value": "uid=alice,ou=users,dc=fworch,dc=internal" }
                    ]
                  }
                ]
                """)
            {
                AddUserStatusCode = HttpStatusCode.OK,
                AddUserResponseBody = "true"
            };
            SettingsRoles component = CreateComponent(handler, out List<(Exception? Exception, string Title, string Message, bool IsError)> messages, out RecordingEventMediator eventMediator);

            await InvokePrivateTask(component, "OnInitializedAsync");
            SetMember(component, "actRole", GetMember<List<Role>>(component, "roles")[0]);
            SetMember(component, "AddMode", true);

            await InvokePrivateTask(component, "AddUserFromLdap", new UiUser { Dn = "uid=bob,ou=users,dc=fworch,dc=internal", Name = "bob" });

            Assert.Multiple(() =>
            {
                Assert.That(messages, Is.Empty);
                Assert.That(handler.AddUserCalls, Is.EqualTo(1));
                Assert.That(GetMember<bool>(component, "AddMode"), Is.False);
                Assert.That(GetMember<List<Role>>(component, "roles")[0].UserList(), Is.EqualTo("alice, bob"));
                Assert.That(eventMediator.PublishedEvents, Has.Count.EqualTo(1));
                Assert.That(eventMediator.PublishedEvents[0].Name, Is.EqualTo(nameof(PermissionChangedEvent)));
            });
        }

        [Test]
        public async Task AddGroupFromLdap_AddsGroupAndPublishesPermissionChange()
        {
            RecordingRoleMiddlewareHandler handler = new("""
                [
                  {
                    "Role": "cn=AppOwners,ou=roles,dc=fworch,dc=internal",
                    "Attributes": []
                  }
                ]
                """)
            {
                AddUserStatusCode = HttpStatusCode.OK,
                AddUserResponseBody = "true"
            };
            SettingsRoles component = CreateComponent(handler, out List<(Exception? Exception, string Title, string Message, bool IsError)> messages, out RecordingEventMediator eventMediator);

            await InvokePrivateTask(component, "OnInitializedAsync");
            SetMember(component, "actRole", GetMember<List<Role>>(component, "roles")[0]);
            SetMember(component, "AddMode", true);

            await InvokePrivateTask(component, "AddGroupFromLdap", "cn=AppAdmins,ou=groups,dc=fworch,dc=internal");

            Assert.Multiple(() =>
            {
                Assert.That(messages, Is.Empty);
                Assert.That(handler.AddUserCalls, Is.EqualTo(1));
                Assert.That(GetMember<bool>(component, "AddMode"), Is.False);
                Assert.That(GetMember<List<Role>>(component, "roles")[0].UserList(), Is.EqualTo("AppAdmins"));
                Assert.That(eventMediator.PublishedEvents, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task AddUserToRole_ShowsValidationAndDuplicateErrors()
        {
            RecordingRoleMiddlewareHandler handler = new("""
                [
                  {
                    "Role": "cn=AppOwners,ou=roles,dc=fworch,dc=internal",
                    "Attributes": [
                      { "Key": "user", "Value": "uid=alice,ou=users,dc=fworch,dc=internal" }
                    ]
                  }
                ]
                """);
            SettingsRoles component = CreateComponent(handler, out List<(Exception? Exception, string Title, string Message, bool IsError)> messages, out _);

            await InvokePrivateTask(component, "OnInitializedAsync");
            SetMember(component, "actRole", GetMember<List<Role>>(component, "roles")[0]);

            await InvokePrivateTask(component, "AddUserToRole", (UiUser?)null);
            await InvokePrivateTask(component, "AddUserToRole", new UiUser { Dn = "uid=alice,ou=users,dc=fworch,dc=internal", Name = "alice" });

            Assert.That(messages, Has.Count.EqualTo(2));
            Assert.That(messages[0].Title, Is.EqualTo("Assign user/group to role"));
            Assert.That(messages[0].Message, Is.EqualTo("Missing user or group"));
            Assert.That(messages[1].Message, Is.EqualTo("Duplicate assignment"));
            Assert.That(handler.AddUserCalls, Is.EqualTo(0));
        }

        [Test]
        public async Task AddUserToRole_ShowsMiddlewareErrorWhenAssignmentFails()
        {
            RecordingRoleMiddlewareHandler handler = new("""
                [
                  {
                    "Role": "cn=AppOwners,ou=roles,dc=fworch,dc=internal",
                    "Attributes": []
                  }
                ]
                """)
            {
                AddUserStatusCode = HttpStatusCode.InternalServerError,
                AddUserResponseBody = "false"
            };
            SettingsRoles component = CreateComponent(handler, out List<(Exception? Exception, string Title, string Message, bool IsError)> messages, out _);

            await InvokePrivateTask(component, "OnInitializedAsync");
            SetMember(component, "actRole", GetMember<List<Role>>(component, "roles")[0]);

            await InvokePrivateTask(component, "AddUserFromLdap", new UiUser { Dn = "uid=bob,ou=users,dc=fworch,dc=internal", Name = "bob" });

            Assert.That(messages, Has.Count.EqualTo(1));
            Assert.That(messages[0].Title, Is.EqualTo("Assign user/group to role"));
            Assert.That(messages[0].Message, Is.EqualTo("Assignment failed"));
            Assert.That(messages[0].IsError, Is.True);
            Assert.That(handler.AddUserCalls, Is.EqualTo(1));
        }

        [Test]
        public async Task RemoveUser_ShowsValidationAndGuardErrors()
        {
            RecordingRoleMiddlewareHandler handler = new("""
                [
                  {
                    "Role": "cn=Admin,ou=roles,dc=fworch,dc=internal",
                    "Attributes": [
                      { "Key": "user", "Value": "uid=alice,ou=users,dc=fworch,dc=internal" }
                    ]
                  }
                ]
                """);
            SettingsRoles component = CreateComponent(handler, out List<(Exception? Exception, string Title, string Message, bool IsError)> messages, out _);

            await InvokePrivateTask(component, "OnInitializedAsync");
            Role adminRole = GetMember<List<Role>>(component, "roles")[0];
            adminRole.Name = Roles.Admin;
            SetMember(component, "actRole", adminRole);

            await InvokePrivateTask(component, "RemoveUser", new UiUser());
            await InvokePrivateTask(component, "RemoveUser", new UiUser { Dn = "uid=alice,ou=users,dc=fworch,dc=internal", Name = "alice" });

            Assert.That(messages, Has.Count.EqualTo(2));
            Assert.That(messages[0].Title, Is.EqualTo("Remove user/group from role"));
            Assert.That(messages[0].Message, Is.EqualTo("Missing DN"));
            Assert.That(messages[1].Message, Is.EqualTo("Admin role must keep one user"));
            Assert.That(handler.RemoveUserCalls, Is.EqualTo(0));
        }

        [Test]
        public async Task RemoveUserFromRole_PublishesPermissionChange()
        {
            RecordingRoleMiddlewareHandler handler = new("""
                [
                  {
                    "Role": "cn=AppOwners,ou=roles,dc=fworch,dc=internal",
                    "Attributes": [
                      { "Key": "user", "Value": "uid=alice,ou=users,dc=fworch,dc=internal" }
                    ]
                  }
                ]
                """)
            {
                RemoveUserStatusCode = HttpStatusCode.OK,
                RemoveUserResponseBody = "true"
            };
            SettingsRoles component = CreateComponent(handler, out List<(Exception? Exception, string Title, string Message, bool IsError)> messages, out RecordingEventMediator eventMediator);

            await InvokePrivateTask(component, "OnInitializedAsync");
            SetMember(component, "actRole", GetMember<List<Role>>(component, "roles")[0]);
            SetMember(component, "RemoveUserMode", true);

            UiUser userToRemove = GetMember<List<Role>>(component, "roles")[0].Users[0];
            await InvokePrivateTask(component, "RemoveUser", userToRemove);

            Assert.Multiple(() =>
            {
                Assert.That(messages, Is.Empty);
                Assert.That(handler.RemoveUserCalls, Is.EqualTo(1));
                Assert.That(GetMember<bool>(component, "RemoveUserMode"), Is.False);
                Assert.That(GetMember<List<Role>>(component, "roles")[0].Users, Is.Empty);
                Assert.That(eventMediator.PublishedEvents, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task RemoveUserFromRole_ShowsMiddlewareErrorWhenRemovalFails()
        {
            RecordingRoleMiddlewareHandler handler = new("""
                [
                  {
                    "Role": "cn=AppOwners,ou=roles,dc=fworch,dc=internal",
                    "Attributes": [
                      { "Key": "user", "Value": "uid=alice,ou=users,dc=fworch,dc=internal" }
                    ]
                  }
                ]
                """)
            {
                RemoveUserStatusCode = HttpStatusCode.InternalServerError,
                RemoveUserResponseBody = "false"
            };
            SettingsRoles component = CreateComponent(handler, out List<(Exception? Exception, string Title, string Message, bool IsError)> messages, out _);

            await InvokePrivateTask(component, "OnInitializedAsync");
            SetMember(component, "actRole", GetMember<List<Role>>(component, "roles")[0]);

            await InvokePrivateTask(component, "RemoveUser", new UiUser { Dn = "uid=alice,ou=users,dc=fworch,dc=internal", Name = "alice" });

            Assert.That(messages, Has.Count.EqualTo(1));
            Assert.That(messages[0].Title, Is.EqualTo("Remove user/group from role"));
            Assert.That(messages[0].Message, Is.EqualTo("Removal failed"));
            Assert.That(messages[0].IsError, Is.True);
            Assert.That(handler.RemoveUserCalls, Is.EqualTo(1));
        }

        private static SettingsRoles CreateComponent(
            RecordingRoleMiddlewareHandler handler,
            out List<(Exception? Exception, string Title, string Message, bool IsError)> messages,
            out RecordingEventMediator eventMediator)
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> capturedMessages = [];
            eventMediator = new RecordingEventMediator();
            TestMiddlewareClient middlewareClient = new("https://middleware.example/");
            middlewareClient.UseHandler(handler);
            SettingsRoles component = new();
            SimulatedUserConfig userConfig = new();
            userConfig.User.Roles = [Roles.Admin];
            userConfig.User.Name = "role-test-user";
            userConfig.User.Dn = "uid=role-test-user,ou=users,dc=fworch,dc=internal";

            SetMember(component, "middlewareClient", middlewareClient);
            SetMember(component, "apiConnection", new SimulatedApiConnection());
            SetMember(component, "userConfig", userConfig);
            SetMember(component, "EventMediator", eventMediator);
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

        private sealed class RecordingRoleMiddlewareHandler : HttpMessageHandler
        {
            private readonly string rolesJson;
            public int AddUserCalls { get; private set; }
            public int RemoveUserCalls { get; private set; }
            public HttpStatusCode AddUserStatusCode { get; set; } = HttpStatusCode.OK;
            public string AddUserResponseBody { get; set; } = "true";
            public HttpStatusCode RemoveUserStatusCode { get; set; } = HttpStatusCode.OK;
            public string RemoveUserResponseBody { get; set; } = "true";

            public RecordingRoleMiddlewareHandler(string rolesJson)
            {
                this.rolesJson = rolesJson;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string path = request.RequestUri?.AbsolutePath ?? "";
                if (request.Method == HttpMethod.Get && path.EndsWith("/Role", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(JsonResponse(HttpStatusCode.OK, rolesJson));
                }

                if (request.Method == HttpMethod.Post && path.EndsWith("/Role/User", StringComparison.OrdinalIgnoreCase))
                {
                    AddUserCalls++;
                    return Task.FromResult(JsonResponse(AddUserStatusCode, AddUserResponseBody));
                }

                if (request.Method == HttpMethod.Delete && path.EndsWith("/Role/User", StringComparison.OrdinalIgnoreCase))
                {
                    RemoveUserCalls++;
                    return Task.FromResult(JsonResponse(RemoveUserStatusCode, RemoveUserResponseBody));
                }

                throw new InvalidOperationException($"Unexpected middleware call: {request.Method} {path}");
            }

            private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string body)
            {
                return new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };
            }
        }
    }
}

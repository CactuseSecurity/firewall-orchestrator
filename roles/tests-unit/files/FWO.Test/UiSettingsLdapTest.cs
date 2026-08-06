using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Middleware.Client;
using FWO.Test.Mocks;
using FWO.Ui.Pages.Settings;
using NUnit.Framework;
using RestSharp;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace FWO.Test
{
    [TestFixture]
    internal class UiSettingsLdapTest
    {
        private string? mainKeyFilePath;
        private bool mainKeyFileAvailable;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            mainKeyFilePath = GlobalConst.kMainKeyFile;
            try
            {
                string? keyDirectory = Path.GetDirectoryName(mainKeyFilePath);
                if (!string.IsNullOrWhiteSpace(keyDirectory))
                {
                    Directory.CreateDirectory(keyDirectory);
                }

                File.WriteAllText(mainKeyFilePath, "0123456789ABCDEF0123456789ABCDEF");
                mainKeyFileAvailable = true;
            }
            catch (UnauthorizedAccessException)
            {
                mainKeyFileAvailable = false;
            }
            catch (IOException)
            {
                mainKeyFileAvailable = false;
            }
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            if (!string.IsNullOrWhiteSpace(mainKeyFilePath) && File.Exists(mainKeyFilePath))
            {
                File.Delete(mainKeyFilePath);
            }
        }

        [SetUp]
        public void SetUp()
        {
            SimulatedUserConfig.DummyTranslate["ldap_conns"] = "LDAP connections";
            SimulatedUserConfig.DummyTranslate["U5211"] = "LDAP connections overview";
            SimulatedUserConfig.DummyTranslate["add_new_ldap"] = "Add LDAP";
            SimulatedUserConfig.DummyTranslate["actions"] = "Actions";
            SimulatedUserConfig.DummyTranslate["clone"] = "Clone";
            SimulatedUserConfig.DummyTranslate["edit"] = "Edit";
            SimulatedUserConfig.DummyTranslate["delete"] = "Delete";
            SimulatedUserConfig.DummyTranslate["edit_ldap"] = "Edit LDAP";
            SimulatedUserConfig.DummyTranslate["name"] = "Name";
            SimulatedUserConfig.DummyTranslate["address"] = "Address";
            SimulatedUserConfig.DummyTranslate["port"] = "Port";
            SimulatedUserConfig.DummyTranslate["tenant_level"] = "Tenant level";
            SimulatedUserConfig.DummyTranslate["type"] = "Type";
            SimulatedUserConfig.DummyTranslate["pattern_length"] = "Pattern length";
            SimulatedUserConfig.DummyTranslate["user_search_path"] = "User search path";
            SimulatedUserConfig.DummyTranslate["role_search_path"] = "Role search path";
            SimulatedUserConfig.DummyTranslate["group_search_path"] = "Group search path";
            SimulatedUserConfig.DummyTranslate["group_write_path"] = "Group write path";
            SimulatedUserConfig.DummyTranslate["search_user"] = "Search user";
            SimulatedUserConfig.DummyTranslate["search_user_pwd"] = "Search user password";
            SimulatedUserConfig.DummyTranslate["write_user"] = "Write user";
            SimulatedUserConfig.DummyTranslate["write_user_pwd"] = "Write user password";
            SimulatedUserConfig.DummyTranslate["tenant"] = "Tenant";
            SimulatedUserConfig.DummyTranslate["global_tenant_name"] = "Global tenant name";
            SimulatedUserConfig.DummyTranslate["active"] = "Active";
            SimulatedUserConfig.DummyTranslate["test_connection"] = "Test connection";
            SimulatedUserConfig.DummyTranslate["save"] = "Save";
            SimulatedUserConfig.DummyTranslate["cancel"] = "Cancel";
            SimulatedUserConfig.DummyTranslate["ok"] = "OK";
            SimulatedUserConfig.DummyTranslate["fetch_ldap_conn"] = "Fetch LDAP connections";
            SimulatedUserConfig.DummyTranslate["fetch_data"] = "Fetch data";
            SimulatedUserConfig.DummyTranslate["save_ldap_conn"] = "Save LDAP connection";
            SimulatedUserConfig.DummyTranslate["delete_ldap_conn"] = "Delete LDAP connection";
            SimulatedUserConfig.DummyTranslate["E5204"] = "LDAP load failed";
            SimulatedUserConfig.DummyTranslate["E5261"] = "Only one LDAP exists";
            SimulatedUserConfig.DummyTranslate["E5262"] = "Role handling prevents delete";
            SimulatedUserConfig.DummyTranslate["E5266"] = "LDAP is reachable";
            SimulatedUserConfig.DummyTranslate["E5267"] = "LDAP test failed";
            SimulatedUserConfig.DummyTranslate["E5268"] = "LDAP auth failed";
            SimulatedUserConfig.DummyTranslate["E5269"] = "LDAP bind failed";
            SimulatedUserConfig.DummyTranslate["E5270"] = "LDAP certificate failed";
            SimulatedUserConfig.DummyTranslate["E5102"] = "Missing required LDAP fields";
            SimulatedUserConfig.DummyTranslate["E5103"] = "Invalid LDAP port";
            SimulatedUserConfig.DummyTranslate["E5263"] = "Invalid pattern length";
            SimulatedUserConfig.DummyTranslate["E5264"] = "Duplicate LDAP endpoint";
            SimulatedUserConfig.DummyTranslate["E5265"] = "Role handling requires internal LDAP";
            SimulatedUserConfig.DummyTranslate["E5260"] = "Cannot deactivate the last active LDAP";
            SimulatedUserConfig.DummyTranslate["E5201"] = "Adding LDAP failed";
            SimulatedUserConfig.DummyTranslate["E5202"] = "Updating LDAP failed";
            SimulatedUserConfig.DummyTranslate["U0001"] = "Sanitized input";
        }

        [Test]
        public async Task OnInitializedAsync_LoadsLdapsAndTenants()
        {
            RecordingLdapApiConnection apiConnection = new();
            apiConnection.Tenants = [new Tenant { Id = 21, Name = "Tenant 21" }];

            TestMiddlewareClient middlewareClient = new("https://middleware.example/");
            middlewareClient.UseHandler(new SingleResponseHandler(HttpStatusCode.OK, """
                [
                  {
                    "Id": 7,
                    "Name": "Primary LDAP",
                    "Address": "ldap.example.org",
                    "Port": 636,
                    "Type": 1,
                    "PatternLength": 3,
                    "SearchUser": "cn=svc,ou=users,dc=fworch,dc=internal",
                    "Tls": true,
                    "TenantLevel": 1,
                    "SearchUserPwd": "ldap-secret",
                    "UserSearchPath": "ou=users,dc=fworch,dc=internal",
                    "RoleSearchPath": "",
                    "GroupSearchPath": "",
                    "GroupWritePath": "",
                    "WriteUser": "",
                    "WriteUserPwd": "",
                    "TenantId": 21,
                    "GlobalTenantName": "Global",
                    "Active": true
                  }
                ]
                """));

            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            SettingsLdap component = CreateBareComponent(apiConnection, middlewareClient, messages);

            await InvokePrivateTask(component, "OnInitializedAsync");

            Assert.Multiple(() =>
            {
                Assert.That(GetMember<List<UiLdapConnection>>(component, "connectedLdaps"), Has.Count.EqualTo(1));
                Assert.That(GetMember<List<Tenant>>(component, "tenants"), Has.Count.EqualTo(1));
                Assert.That(messages, Is.Empty);
            });
        }

        [Test]
        public async Task OnInitializedAsync_ShowsErrorWhenMiddlewareFails()
        {
            RecordingLdapApiConnection apiConnection = new();
            apiConnection.Tenants = [new Tenant { Id = 21, Name = "Tenant 21" }];

            TestMiddlewareClient middlewareClient = new("https://middleware.example/");
            middlewareClient.UseHandler(new SingleResponseHandler(HttpStatusCode.InternalServerError, "{}"));

            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            SettingsLdap component = CreateBareComponent(apiConnection, middlewareClient, messages);

            await InvokePrivateTask(component, "OnInitializedAsync");

            Assert.That(messages, Has.Count.EqualTo(1));
            Assert.That(messages[0].Title, Is.EqualTo("Fetch LDAP connections"));
            Assert.That(messages[0].Message, Is.EqualTo("LDAP load failed"));
            Assert.That(messages[0].IsError, Is.True);
        }

        [Test]
        public async Task OnInitializedAsync_ShowsErrorWhenTenantQueryFails()
        {
            RecordingLdapApiConnection apiConnection = new()
            {
                ThrowOnGetTenants = true
            };

            TestMiddlewareClient middlewareClient = new("https://middleware.example/");
            middlewareClient.UseHandler(new SingleResponseHandler(HttpStatusCode.OK, "[]"));

            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            SettingsLdap component = CreateBareComponent(apiConnection, middlewareClient, messages);

            await InvokePrivateTask(component, "OnInitializedAsync");

            Assert.That(messages, Has.Count.EqualTo(1));
            Assert.That(messages[0].Title, Is.EqualTo("Fetch data"));
            Assert.That(messages[0].Message, Is.Empty);
            Assert.That(messages[0].Exception, Is.TypeOf<InvalidOperationException>());
            Assert.That(messages[0].IsError, Is.True);
        }

        [Test]
        public void RequestDelete_ShowsErrorWhenOnlyOneLdapExists()
        {
            SettingsLdap component = CreateBareComponent(out List<(Exception? Exception, string Title, string Message, bool IsError)> messages);
            SetMember(component, "connectedLdaps", new List<UiLdapConnection>
            {
                BuildLdap(1, "ldap-one", "ldap.example.org", roleHandling: false, internalLdap: true)
            });

            InvokePrivateVoid(component, "RequestDelete", BuildLdap(1, "ldap-one", "ldap.example.org", roleHandling: false, internalLdap: true));

            Assert.That(messages, Has.Count.EqualTo(1));
            Assert.That(messages[0].Title, Is.EqualTo("Delete LDAP connection"));
            Assert.That(messages[0].Message, Is.EqualTo("Only one LDAP exists"));
            Assert.That(messages[0].IsError, Is.True);
        }

        [Test]
        public void RequestDelete_ShowsErrorWhenRoleHandlingBlocksDelete()
        {
            SettingsLdap component = CreateBareComponent(out List<(Exception? Exception, string Title, string Message, bool IsError)> messages);
            SetMember(component, "connectedLdaps", new List<UiLdapConnection>
            {
                BuildLdap(1, "ldap-one", "ldap.example.org", roleHandling: false, internalLdap: true),
                BuildLdap(2, "ldap-role", "ldap-role.example.org", roleHandling: true, internalLdap: true)
            });

            InvokePrivateVoid(component, "RequestDelete", BuildLdap(2, "ldap-role", "ldap-role.example.org", roleHandling: true, internalLdap: true));

            Assert.That(messages, Has.Count.EqualTo(1));
            Assert.That(messages[0].Title, Is.EqualTo("Delete LDAP connection"));
            Assert.That(messages[0].Message, Is.EqualTo("Role handling prevents delete"));
            Assert.That(messages[0].IsError, Is.True);
        }

        [Test]
        public void RequestDelete_AllowsDeleteWhenRoleRulesDoNotBlock()
        {
            SettingsLdap component = CreateBareComponent(out List<(Exception? Exception, string Title, string Message, bool IsError)> messages);
            UiLdapConnection removableLdap = BuildLdap(2, "ldap-remove", "ldap-remove.example.org", roleHandling: false, internalLdap: true);
            SetMember(component, "connectedLdaps", new List<UiLdapConnection>
            {
                BuildLdap(1, "ldap-one", "ldap.example.org", roleHandling: false, internalLdap: true),
                removableLdap
            });

            InvokePrivateVoid(component, "RequestDelete", removableLdap);

            Assert.Multiple(() =>
            {
                Assert.That(messages, Is.Empty);
                Assert.That(GetMember<bool>(component, "DeleteMode"), Is.True);
                Assert.That(GetMember<string>(component, "deleteMessage"), Does.Contain("ldap-remove.example.org"));
            });
        }

        [Test]
        public void CheckValues_ReportsValidationErrors()
        {
            AssertValidationFailure(
                expectedMessage: "Missing required LDAP fields",
                configure: component =>
                {
                    SetMember(component, "actLdapConnection", BuildLdap(0, "ldap-missing-fields", "", roleHandling: false, internalLdap: true));
                });

            AssertValidationFailure(
                expectedMessage: "Invalid LDAP port",
                configure: component =>
                {
                    SetMember(component, "actLdapConnection", BuildLdap(0, "ldap-invalid-port", "ldap.example.org", roleHandling: false, internalLdap: true, port: 0));
                });

            AssertValidationFailure(
                expectedMessage: "Invalid pattern length",
                configure: component =>
                {
                    SetMember(component, "actLdapConnection", BuildLdap(0, "ldap-negative-pattern", "ldap.example.org", roleHandling: false, internalLdap: true, patternLength: -1));
                });

            AssertValidationFailure(
                expectedMessage: "Duplicate LDAP endpoint",
                configure: component =>
                {
                    SetMember(component, "connectedLdaps", new List<UiLdapConnection>
                    {
                        BuildLdap(1, "ldap-existing", "ldap.example.org", roleHandling: false, internalLdap: true)
                    });
                    SetMember(component, "actLdapConnection", BuildLdap(2, "ldap-duplicate", "ldap.example.org", roleHandling: false, internalLdap: true));
                });

            AssertValidationFailure(
                expectedMessage: "Role handling requires internal LDAP",
                configure: component =>
                {
                    SetMember(component, "actLdapConnection", BuildLdap(0, "ldap-external", "ldap.example.org", roleHandling: true, internalLdap: false));
                });

            AssertValidationFailure(
                expectedMessage: "Cannot deactivate the last active LDAP",
                configure: component =>
                {
                    SetMember(component, "connectedLdaps", new List<UiLdapConnection>
                    {
                        BuildLdap(1, "ldap-active", "ldap.example.org", roleHandling: false, internalLdap: true)
                    });
                    SetMember(component, "actLdapConnection", BuildLdap(1, "ldap-active", "ldap.example.org", roleHandling: false, internalLdap: true, active: false));
                    SetMember(component, "AddMode", false);
                    SetMember(component, "wasActive", true);
                });
        }

        [Test]
        public async Task TestConnection_ShowsExpectedMessagesForResponseCodes()
        {
            foreach ((HttpStatusCode StatusCode, int ResponseCode, string ExpectedMessage) testCase in new[]
            {
                (HttpStatusCode.OK, 0, "LDAP is reachable"),
                (HttpStatusCode.OK, 1, "LDAP auth failed"),
                (HttpStatusCode.OK, 2, "LDAP bind failed"),
                (HttpStatusCode.OK, 3, "LDAP certificate failed"),
                (HttpStatusCode.OK, 9, "LDAP test failed"),
                (HttpStatusCode.BadGateway, 0, "LDAP test failed")
            })
            {
                RecordingLdapApiConnection apiConnection = new();
                TestMiddlewareClient middlewareClient = new("https://middleware.example/");
                middlewareClient.UseHandler(new SingleResponseHandler(testCase.StatusCode, testCase.ResponseCode.ToString()));
                List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
                SettingsLdap component = CreateBareComponent(apiConnection, middlewareClient, messages);
                SetMember(component, "actLdapConnection", BuildLdap(0, "ldap-test", "ldap.example.org", roleHandling: false, internalLdap: true));

                await InvokePrivateTask(component, "TestConnection");

                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("Test connection"));
                Assert.That(messages[0].Message, Is.EqualTo(testCase.ExpectedMessage));
                messages.Clear();
            }
        }

        [Test]
        public async Task TestConnection_ShowsExceptionWhenMiddlewareThrows()
        {
            RecordingLdapApiConnection apiConnection = new();
            TestMiddlewareClient middlewareClient = new("https://middleware.example/");
            middlewareClient.UseHandler(new SingleResponseHandler(HttpStatusCode.BadGateway, "0"));
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            SettingsLdap component = CreateBareComponent(apiConnection, middlewareClient, messages);
            SetMember(component, "actLdapConnection", BuildLdap(0, "ldap-test", "ldap.example.org", roleHandling: false, internalLdap: true));

            await InvokePrivateTask(component, "TestConnection");

            Assert.That(messages, Has.Count.EqualTo(1));
            Assert.That(messages[0].Title, Is.EqualTo("Test connection"));
            Assert.That(messages[0].Message, Is.EqualTo("LDAP test failed"));
            Assert.That(messages[0].Exception, Is.Null);
            Assert.That(messages[0].IsError, Is.True);
        }

        [Test]
        public async Task Save_AddModePersistsNewLdap()
        {
            Assume.That(mainKeyFileAvailable, "Requires a writable main key file path for password encryption.");
            RecordingLdapApiConnection apiConnection = new();
            TestMiddlewareClient middlewareClient = new("https://middleware.example/");
            middlewareClient.UseHandler(new SingleResponseHandler(HttpStatusCode.OK, "11"));

            SettingsLdap component = CreateBareComponent(apiConnection, middlewareClient, []);
            SetMember(component, "AddMode", true);
            SetMember(component, "EditMode", true);
            SetMember(component, "selectedType", LdapType.OpenLdap);
            SetMember(component, "selectedTenant", new Tenant { Id = 21, Name = "Tenant 21" });
            SetMember(component, "actLdapConnection", BuildLdap(0, "ldap-new", "ldap-new.example.org", roleHandling: false, internalLdap: true));
            SetMember(component, "connectedLdaps", new List<UiLdapConnection>());
            SetMember(component, "tenants", new List<Tenant> { new Tenant { Id = 21, Name = "Tenant 21" } });

            await InvokePrivateTask(component, "Save");

            List<UiLdapConnection> connectedLdaps = GetMember<List<UiLdapConnection>>(component, "connectedLdaps");
            Assert.Multiple(() =>
            {
                Assert.That(connectedLdaps, Has.Count.EqualTo(1));
                Assert.That(connectedLdaps[0].Id, Is.EqualTo(11));
                Assert.That(GetMember<bool>(component, "AddMode"), Is.False);
                Assert.That(GetMember<bool>(component, "EditMode"), Is.False);
            });
        }

        [Test]
        public async Task Save_AddModeShowsSanitizeWarningWhenInputIsTrimmed()
        {
            Assume.That(mainKeyFileAvailable, "Requires a writable main key file path for password encryption.");
            RecordingLdapApiConnection apiConnection = new();
            TestMiddlewareClient middlewareClient = new("https://middleware.example/");
            middlewareClient.UseHandler(new SingleResponseHandler(HttpStatusCode.OK, "12"));
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];

            SettingsLdap component = CreateBareComponent(apiConnection, middlewareClient, messages);
            SetMember(component, "AddMode", true);
            SetMember(component, "EditMode", true);
            SetMember(component, "selectedType", LdapType.OpenLdap);
            SetMember(component, "actLdapConnection", BuildLdap(0, " ldap-new ", "ldap-new.example.org", roleHandling: false, internalLdap: true));
            SetMember(component, "connectedLdaps", new List<UiLdapConnection>());
            SetMember(component, "tenants", new List<Tenant>());

            await InvokePrivateTask(component, "Save");

            Assert.That(messages, Has.Count.EqualTo(1));
            Assert.That(messages[0].Title, Is.EqualTo("Save LDAP connection"));
            Assert.That(messages[0].Message, Is.EqualTo("Sanitized input"));
            Assert.That(messages[0].IsError, Is.True);
            Assert.That(GetMember<List<UiLdapConnection>>(component, "connectedLdaps"), Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Save_UpdateModePersistsEditedLdap()
        {
            Assume.That(mainKeyFileAvailable, "Requires a writable main key file path for password encryption.");
            RecordingLdapApiConnection apiConnection = new();
            TestMiddlewareClient middlewareClient = new("https://middleware.example/");
            middlewareClient.UseHandler(new RoutingMiddlewareHandler
            {
                UpdateStatusCode = HttpStatusCode.OK,
                UpdateBody = "31"
            });

            UiLdapConnection existing = BuildLdap(31, "ldap-existing", "ldap-existing.example.org", roleHandling: false, internalLdap: true);
            SettingsLdap component = CreateBareComponent(apiConnection, middlewareClient, []);
            SetMember(component, "AddMode", false);
            SetMember(component, "EditMode", true);
            SetMember(component, "selectedType", LdapType.ActiveDirectory);
            SetMember(component, "actLdapConnection", existing);
            SetMember(component, "connectedLdaps", new List<UiLdapConnection> { existing });

            await InvokePrivateTask(component, "Save");

            Assert.Multiple(() =>
            {
                Assert.That(GetMember<List<UiLdapConnection>>(component, "connectedLdaps")[0].Id, Is.EqualTo(31));
                Assert.That(GetMember<bool>(component, "EditMode"), Is.False);
            });
        }

        [Test]
        public async Task Save_UpdateModeShowsMessageWhenUpdateFails()
        {
            Assume.That(mainKeyFileAvailable, "Requires a writable main key file path for password encryption.");
            RecordingLdapApiConnection apiConnection = new();
            TestMiddlewareClient middlewareClient = new("https://middleware.example/");
            middlewareClient.UseHandler(new RoutingMiddlewareHandler
            {
                UpdateStatusCode = HttpStatusCode.InternalServerError,
                UpdateBody = "0"
            });
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];

            UiLdapConnection existing = BuildLdap(31, "ldap-existing", "ldap-existing.example.org", roleHandling: false, internalLdap: true);
            SettingsLdap component = CreateBareComponent(apiConnection, middlewareClient, messages);
            SetMember(component, "AddMode", false);
            SetMember(component, "EditMode", true);
            SetMember(component, "selectedType", LdapType.ActiveDirectory);
            SetMember(component, "actLdapConnection", existing);
            SetMember(component, "connectedLdaps", new List<UiLdapConnection> { existing });

            await InvokePrivateTask(component, "Save");

            Assert.That(messages, Has.Count.EqualTo(1));
            Assert.That(messages[0].Title, Is.EqualTo("Save LDAP connection"));
            Assert.That(messages[0].Message, Is.EqualTo("Updating LDAP failed"));
            Assert.That(messages[0].IsError, Is.True);
        }

        [Test]
        public async Task Save_ShowsMessageWhenAddFails()
        {
            Assume.That(mainKeyFileAvailable, "Requires a writable main key file path for password encryption.");
            RecordingLdapApiConnection apiConnection = new();
            TestMiddlewareClient middlewareClient = new("https://middleware.example/");
            middlewareClient.UseHandler(new SingleResponseHandler(HttpStatusCode.InternalServerError, "0"));
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            SettingsLdap component = CreateBareComponent(apiConnection, middlewareClient, messages);
            SetMember(component, "AddMode", true);
            SetMember(component, "selectedType", LdapType.OpenLdap);
            SetMember(component, "actLdapConnection", BuildLdap(0, "ldap-fail", "ldap-fail.example.org", roleHandling: false, internalLdap: true));

            await InvokePrivateTask(component, "Save");

            Assert.That(messages, Has.Count.EqualTo(1));
            Assert.That(messages[0].Title, Is.EqualTo("Save LDAP connection"));
            Assert.That(messages[0].Message, Is.EqualTo("Adding LDAP failed"));
            Assert.That(messages[0].IsError, Is.True);
        }

        private static SettingsLdap CreateBareComponent(
            out List<(Exception? Exception, string Title, string Message, bool IsError)> messages)
        {
            messages = [];
            return CreateBareComponent(new RecordingLdapApiConnection(), new TestMiddlewareClient(), messages);
        }

        private static SettingsLdap CreateComponent(
            RecordingLdapApiConnection apiConnection,
            TestMiddlewareClient middlewareClient,
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages)
        {
            return CreateBareComponent(apiConnection, middlewareClient, messages);
        }

        private static SettingsLdap CreateBareComponent(
            RecordingLdapApiConnection apiConnection,
            TestMiddlewareClient middlewareClient,
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages)
        {
            SettingsLdap component = new();
            SimulatedUserConfig userConfig = new();
            userConfig.User.Roles = [Roles.Admin];
            userConfig.User.Name = "ldap-test-user";
            userConfig.User.Dn = "uid=ldap-test-user,ou=users,dc=fworch,dc=internal";
            SetMember(component, "apiConnection", apiConnection);
            SetMember(component, "middlewareClient", middlewareClient);
            SetMember(component, "userConfig", userConfig);
            SetMember(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((exception, title, message, isError) =>
            {
                messages.Add((exception, title, message, isError));
            }));
            return component;
        }

        private static UiLdapConnection BuildLdap(
            int id,
            string name,
            string address,
            bool roleHandling,
            bool internalLdap,
            int port = 636,
            int patternLength = 3,
            bool active = true)
        {
            return new UiLdapConnection
            {
                Id = id,
                Name = name,
                Address = address,
                Port = port,
                Type = (int)LdapType.OpenLdap,
                PatternLength = patternLength,
                SearchUser = "cn=svc,ou=users,dc=fworch,dc=internal",
                Tls = true,
                TenantLevel = 1,
                SearchUserPwd = "search-secret",
                UserSearchPath = internalLdap
                    ? "ou=users,dc=fworch,dc=internal"
                    : "ou=users,dc=example,dc=com",
                RoleSearchPath = roleHandling ? "ou=roles,dc=fworch,dc=internal" : "",
                GroupSearchPath = "",
                GroupWritePath = "",
                WriteUser = "cn=writer,ou=users,dc=fworch,dc=internal",
                WriteUserPwd = "write-secret",
                TenantId = 21,
                GlobalTenantName = "Global tenant",
                Active = active
            };
        }

        private static void AssertValidationFailure(
            string expectedMessage,
            Action<SettingsLdap> configure)
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            SettingsLdap component = CreateBareComponent(out messages);
            SetMember(component, "AddMode", false);
            SetMember(component, "connectedLdaps", new List<UiLdapConnection>());
            configure(component);

            bool result = InvokePrivate<bool>(component, "CheckValues");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("Save LDAP connection"));
                Assert.That(messages[0].Message, Is.EqualTo(expectedMessage));
                Assert.That(messages[0].IsError, Is.True);
            });
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

        private static T InvokePrivate<T>(object instance, string methodName, params object?[] args)
        {
            MethodInfo method = FindPrivateMethod(instance.GetType(), methodName, args);
            return (T)(method.Invoke(instance, args) ?? throw new InvalidOperationException($"{methodName} returned null result."));
        }

        private static void InvokePrivateVoid(object instance, string methodName, params object?[] args)
        {
            MethodInfo method = FindPrivateMethod(instance.GetType(), methodName, args);
            method.Invoke(instance, args);
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

        private sealed class RecordingLdapApiConnection : SimulatedApiConnection
        {
            public List<Tenant> Tenants { get; set; } = [];
            public bool ThrowOnGetTenants { get; set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == AuthQueries.getTenants && typeof(QueryResponseType) == typeof(List<Tenant>))
                {
                    if (ThrowOnGetTenants)
                    {
                        throw new InvalidOperationException("tenant query failed");
                    }
                    return Task.FromResult((QueryResponseType)(object)Tenants);
                }

                throw new InvalidOperationException($"Unexpected query: {query}");
            }
        }

        private sealed class RoutingMiddlewareHandler : HttpMessageHandler
        {
            public HttpStatusCode AddStatusCode { get; set; } = HttpStatusCode.OK;
            public string AddBody { get; set; } = "11";
            public HttpStatusCode UpdateStatusCode { get; set; } = HttpStatusCode.OK;
            public string UpdateBody { get; set; } = "31";

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string path = request.RequestUri?.AbsolutePath ?? "";
                if (request.Method == HttpMethod.Post && path.EndsWith("/AuthenticationServer", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(JsonResponse(AddStatusCode, AddBody));
                }

                if (request.Method == HttpMethod.Put && path.EndsWith("/AuthenticationServer", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(JsonResponse(UpdateStatusCode, UpdateBody));
                }

                if (request.Method == HttpMethod.Get && path.EndsWith("/AuthenticationServer", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(JsonResponse(HttpStatusCode.OK, "[]"));
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

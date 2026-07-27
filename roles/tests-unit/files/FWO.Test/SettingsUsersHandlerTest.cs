using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Middleware.Client;
using FWO.Ui.Services;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [NonParallelizable]
    internal class SettingsUsersHandlerTest
    {
        private static readonly string[] kExpectedAdminReporterRoles = ["admin", "reporter"];
        private static readonly string[] kExpectedTenantName = ["t2"];

        [Test]
        public void BuildUsersByNormalizedDnGroupsEquivalentDns()
        {
            UiUser upperCase = new() { Name = "alice", Dn = "CN=Alice,OU=T1,DC=fwo" };
            UiUser lowerCase = new() { Name = "alice2", Dn = "cn=alice,ou=t1,dc=fwo" };
            UiUser other = new() { Name = "bob", Dn = "cn=bob,ou=t1,dc=fwo" };
            UiUser empty = new() { Name = "ghost", Dn = "" };

            Dictionary<string, List<UiUser>> result =
                SettingsUsersHandler.BuildUsersByNormalizedDn([upperCase, lowerCase, other, empty]);

            // Equivalent DNs that differ only in casing collapse to a single key.
            Assert.That(result, Has.Count.EqualTo(2));
            string aliceKey = DistName.NormalizeDnForComparison(upperCase.Dn);
            Assert.That(result[aliceKey], Has.Count.EqualTo(2));
            Assert.That(result[aliceKey], Does.Contain(upperCase).And.Contain(lowerCase));
            // Users with an empty DN are skipped.
            Assert.That(result.Values.SelectMany(u => u), Does.Not.Contain(empty));
        }

        [Test]
        public void AssignGroupsToUsersAssignsMembershipAndResetsPrevious()
        {
            UiUser alice = new() { Name = "alice", Dn = "cn=alice,ou=t1", Groups = ["stale"] };
            UiUser bob = new() { Name = "bob", Dn = "cn=bob,ou=t1" };
            List<UiUser> users = [alice, bob];

            UserGroup admins = new() { Name = "admins", Users = [new UiUser { Dn = "CN=Alice,OU=T1" }] };
            UserGroup auditors = new() { Name = "auditors", Users = [new UiUser { Dn = "cn=bob,ou=t1" }] };

            SettingsUsersHandler.AssignGroupsToUsers(users, [admins, auditors]);

            // Stale membership is cleared and matching is case-insensitive on the DN.
            Assert.That(alice.Groups, Is.EqualTo(new List<string> { "admins" }));
            Assert.That(bob.Groups, Is.EqualTo(new List<string> { "auditors" }));
        }

        [Test]
        public void AssignRolesToUsersAssignsMembership()
        {
            UiUser alice = new() { Name = "alice", Dn = "cn=alice,ou=t1", Roles = ["stale"] };
            List<UiUser> users = [alice];
            Role admin = new() { Name = "admin", Users = [new UiUser { Dn = "cn=alice,ou=t1" }] };
            Role reporter = new() { Name = "reporter", Users = [new UiUser { Dn = "cn=nobody,ou=t1" }] };

            SettingsUsersHandler.AssignRolesToUsers(users, [admin, reporter]);

            Assert.That(alice.Roles, Is.EqualTo(new List<string> { "admin" }));
        }

        [Test]
        public void MapApiUsersToUiUsersResolvesLdapAndTenant()
        {
            UiLdapConnection internalLdap = new() { Id = 5, Name = "internal" };
            List<UiLdapConnection> ldaps = [internalLdap];
            List<Tenant> tenants = [new Tenant { Id = 3, Name = "tenant3" }];
            List<UserGetReturnParameters> apiUsers =
            [
                new() { Name = "alice", UserDn = "cn=alice", LdapId = 5, TenantId = 3 }
            ];

            List<UiUser> result = SettingsUsersHandler.MapApiUsersToUiUsers(apiUsers, ldaps, tenants);

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].LdapConnection, Is.SameAs(internalLdap));
            Assert.That(result[0].Tenant?.Name, Is.EqualTo("tenant3"));
        }

        [Test]
        public void MapApiUsersToUiUsersThrowsWhenLdapMissing()
        {
            List<UserGetReturnParameters> apiUsers =
            [
                new() { Name = "alice", UserDn = "cn=alice", LdapId = 99 }
            ];

            Assert.Throws<ArgumentNullException>(
                () => SettingsUsersHandler.MapApiUsersToUiUsers(apiUsers, [], []));
        }

        [Test]
        public void GetAvailableTenantsHonorsLdapConfiguration()
        {
            List<Tenant> tenants = [new Tenant { Id = 1, Name = "t1" }, new Tenant { Id = 2, Name = "t2" }];

            List<Tenant> fixedTenant = SettingsUsersHandler.GetAvailableTenants(
                new UiLdapConnection { TenantId = 2, TenantLevel = 0 }, tenants);
            Assert.That(fixedTenant.Select(t => t.Name), Is.EqualTo(kExpectedTenantName));

            List<Tenant> allTenants = SettingsUsersHandler.GetAvailableTenants(
                new UiLdapConnection { TenantId = null, TenantLevel = 1 }, tenants);
            Assert.That(allTenants, Has.Count.EqualTo(2));

            Assert.That(SettingsUsersHandler.GetAvailableTenants(null, tenants), Is.Empty);
        }

        [Test]
        public void BuildUserDnBuildsExpectedDistinguishedNames()
        {
            UiLdapConnection adLdap = new()
            {
                Type = (int)LdapType.ActiveDirectory,
                TenantLevel = 0,
                UserSearchPath = "ou=users,dc=fwo"
            };
            Assert.That(SettingsUsersHandler.BuildUserDn("alice", adLdap, null),
                Is.EqualTo("cn=alice,ou=users,dc=fwo"));

            UiLdapConnection openLdap = new()
            {
                Type = (int)LdapType.OpenLdap,
                TenantLevel = 1,
                UserSearchPath = "ou=users,dc=fwo"
            };
            Tenant tenant = new() { Id = 2, Name = "customer1" };
            Assert.That(SettingsUsersHandler.BuildUserDn("bob", openLdap, tenant),
                Is.EqualTo("uid=bob,ou=customer1,ou=users,dc=fwo"));
        }

        [Test]
        public void EditReplacesTenantZeroNameWithGlobalTenantName()
        {
            (SettingsUsersHandler handler, _, _, _) = CreateHandler();
            UiUser user = new()
            {
                Dn = "uid=alice,ou=users,dc=fworch,dc=internal",
                Tenant = new Tenant { Id = GlobalConst.kTenant0Id, Name = "tenant0" },
                LdapConnection = new UiLdapConnection { GlobalTenantName = "global-tenant" }
            };

            handler.Edit(user);

            Assert.Multiple(() =>
            {
                Assert.That(handler.EditMode, Is.True);
                Assert.That(handler.ActUser.Tenant?.Name, Is.EqualTo("global-tenant"));
            });
        }

        [Test]
        public void AddUserPreparesEditableState()
        {
            (SettingsUsersHandler handler, _, _, _) = CreateHandler();
            handler.Tenants = [new Tenant { Id = 1, Name = "t1" }];
            handler.WritableLdaps = [new UiLdapConnection { Id = 5, Name = "ldap", TenantLevel = 0 }];

            handler.AddUser();

            Assert.Multiple(() =>
            {
                Assert.That(handler.AddMode, Is.True);
                Assert.That(handler.EditMode, Is.True);
                Assert.That(handler.SelectedLdap?.Id, Is.EqualTo(5));
                Assert.That(handler.SelectedTenant, Is.Null);
                Assert.That(handler.SelectedGroup, Is.Null);
                Assert.That(handler.SelectedRole, Is.Null);
                Assert.That(handler.NewUser.Groups, Is.Empty);
                Assert.That(handler.NewUser.Roles, Is.Empty);
            });
        }

        [Test]
        public void CloneCopiesStateAndClearsMutableCollections()
        {
            (SettingsUsersHandler handler, _, _, _) = CreateHandler();
            UiLdapConnection ldap = new() { Id = 5, Name = "ldap", TenantLevel = 0 };
            handler.WritableLdaps = [ldap];
            handler.Groups = [new UserGroup { Name = "admins" }];
            handler.Roles = [new Role { Name = "auditor" }];
            UiUser source = new()
            {
                Name = "alice",
                Password = "secret",
                Firstname = "Alice",
                Lastname = "Example",
                Email = "alice@example.com",
                Tenant = new Tenant { Id = 7, Name = "tenant7" },
                LdapConnection = ldap,
                Groups = ["admins"],
                Roles = ["auditor"]
            };

            handler.Clone(source);

            Assert.Multiple(() =>
            {
                Assert.That(handler.AddMode, Is.True);
                Assert.That(handler.EditMode, Is.True);
                Assert.That(handler.NewUser.Password, Is.Empty);
                Assert.That(handler.NewUser.Firstname, Is.EqualTo("Alice"));
                Assert.That(handler.NewUser.Email, Is.EqualTo("alice@example.com"));
                Assert.That(handler.SelectedLdap?.Id, Is.EqualTo(ldap.Id));
                Assert.That(handler.SelectedLdap?.Name, Is.EqualTo(ldap.Name));
                Assert.That(handler.SelectedTenant?.Name, Is.EqualTo("tenant7"));
                Assert.That(handler.SelectedGroup?.Name, Is.EqualTo("admins"));
                Assert.That(handler.SelectedRole?.Name, Is.EqualTo("auditor"));
                Assert.That(handler.NewUser.Groups, Is.Empty);
                Assert.That(handler.NewUser.Roles, Is.Empty);
            });
        }

        [Test]
        public void RequestDeleteRejectsCurrentUserAndBuildsConfirmationForOthers()
        {
            List<(string Title, string Message)> messages = [];
            (SettingsUsersHandler handler, EchoUserConfig userConfig, _, _) = CreateHandler(messages: messages);
            userConfig.User.Dn = "uid=me,dc=fworch,dc=internal";

            handler.RequestDelete(new UiUser { Name = "me", Dn = "uid=me,dc=fworch,dc=internal" });

            Assert.That(messages, Has.Count.EqualTo(1));
            Assert.That(messages[0].Title, Is.EqualTo("delete_user"));
            Assert.That(messages[0].Message, Is.EqualTo("E5215"));

            messages.Clear();
            handler.RequestDelete(new UiUser { Name = "alice", Dn = "uid=alice,dc=example,dc=net" });

            Assert.Multiple(() =>
            {
                Assert.That(handler.DeleteMode, Is.True);
                Assert.That(handler.DeleteMessage, Is.EqualTo("U5201alice?U5202"));
                Assert.That(messages, Is.Empty);
            });
        }

        [Test]
        public void RequestResetPasswordRejectsExternalUsersAndOpensDialogForInternalUsers()
        {
            List<(string Title, string Message)> messages = [];
            (SettingsUsersHandler handler, _, _, _) = CreateHandler(messages: messages);

            handler.RequestResetPassword(new UiUser { Name = "ext", Dn = "uid=ext,dc=example,dc=net" });
            Assert.That(messages, Has.Count.EqualTo(1));
            Assert.That(messages[0].Title, Is.EqualTo("reset_password"));
            Assert.That(messages[0].Message, Is.EqualTo("E5217"));

            messages.Clear();
            UiUser internalUser = new() { Name = "int", Dn = "uid=int,ou=users,dc=fworch,dc=internal", Password = "keep" };
            handler.RequestResetPassword(internalUser);

            Assert.Multiple(() =>
            {
                Assert.That(handler.ResetPasswordMode, Is.True);
                Assert.That(handler.ActUser.Password, Is.Empty);
                Assert.That(handler.ActUser, Is.SameAs(internalUser));
            });
        }

        [Test]
        public void RequestRemoveSampleDataTracksWhetherCurrentUserIsSeeded()
        {
            (SettingsUsersHandler handler, EchoUserConfig userConfig, _, _) = CreateHandler();
            userConfig.User.DbId = 10;
            handler.SampleUsers = [new UiUser { DbId = 10 }, new UiUser { DbId = 20 }];

            handler.RequestRemoveSampleData();

            Assert.Multiple(() =>
            {
                Assert.That(handler.SampleRemoveMode, Is.True);
                Assert.That(handler.SampleRemoveAllowed, Is.False);
                Assert.That(handler.SampleRemoveMessage, Is.EqualTo("E5220"));
            });

            handler.SampleUsers = [new UiUser { DbId = 20 }];
            handler.RequestRemoveSampleData();

            Assert.Multiple(() =>
            {
                Assert.That(handler.SampleRemoveAllowed, Is.True);
                Assert.That(handler.SampleRemoveMessage, Is.EqualTo("U5203"));
            });
        }

        [Test]
        public void CancelClosesAllDialogs()
        {
            (SettingsUsersHandler handler, _, _, _) = CreateHandler();
            handler.AddMode = true;
            handler.EditMode = true;
            handler.DeleteMode = true;
            handler.SampleRemoveMode = true;
            handler.ResetPasswordMode = true;

            handler.Cancel();

            Assert.Multiple(() =>
            {
                Assert.That(handler.AddMode, Is.False);
                Assert.That(handler.EditMode, Is.False);
                Assert.That(handler.DeleteMode, Is.False);
                Assert.That(handler.SampleRemoveMode, Is.False);
                Assert.That(handler.ResetPasswordMode, Is.False);
            });
        }

        [Test]
        public async Task InitLoadsUsersTenantsGroupsAndRolesAndFlagsDemoAccounts()
        {
            await using TestMiddlewareServer server = new(request =>
            {
                if (request.Method == "GET" && request.Path == "/api/User")
                {
                    return new TestResponse(200, JsonSerializer.Serialize(new[]
                    {
                        new UserGetReturnParameters
                        {
                            Name = "alice_demo",
                            UserId = 11,
                            UserDn = "uid=alice,ou=users,dc=fworch,dc=internal",
                            Email = "alice@example.com",
                            TenantId = 1,
                            LdapId = 5
                        }
                    }));
                }

                if (request.Method == "GET" && request.Path == "/api/Group")
                {
                    return new TestResponse(200, JsonSerializer.Serialize(new[]
                    {
                        new GroupGetReturnParameters
                        {
                            GroupDn = "cn=admins,ou=groups,dc=fworch,dc=internal",
                            OwnerGroup = true,
                            Members = ["uid=alice,ou=users,dc=fworch,dc=internal"]
                        }
                    }));
                }

                if (request.Method == "GET" && request.Path == "/api/Role")
                {
                    return new TestResponse(200, JsonSerializer.Serialize(new[]
                    {
                        new RoleGetReturnParameters
                        {
                            Role = "cn=admin,ou=roles,dc=fworch,dc=internal",
                            Attributes = [new RoleAttribute { Key = "description", Value = "Admin" }, new RoleAttribute { Key = "user", Value = "uid=alice,ou=users,dc=fworch,dc=internal" }]
                        },
                        new RoleGetReturnParameters
                        {
                            Role = "cn=middleware-server,ou=roles,dc=fworch,dc=internal",
                            Attributes = [new RoleAttribute { Key = "user", Value = "uid=ignored,ou=users,dc=fworch,dc=internal" }]
                        },
                        new RoleGetReturnParameters
                        {
                            Role = "cn=anonymous,ou=roles,dc=fworch,dc=internal",
                            Attributes = []
                        },
                        new RoleGetReturnParameters
                        {
                            Role = "cn=reporter,ou=roles,dc=fworch,dc=internal",
                            Attributes = []
                        }
                    }));
                }

                return new TestResponse(404, "{}");
            });

            (SettingsUsersHandler handler, _, RecordingApiConnection apiConnection, _) = CreateHandler(middlewareClient: new MiddlewareClient(server.BaseUrl));
            apiConnection.ConnectedLdaps =
            [
                new UiLdapConnection
                {
                    Id = 5,
                    Name = "internal",
                    WriteUser = "cn=writer",
                    TenantLevel = 0,
                    UserSearchPath = "ou=users,dc=fworch,dc=internal"
                }
            ];
            apiConnection.Tenants = [new Tenant { Id = 1, Name = "tenant1" }];

            await handler.Init();

            Assert.Multiple(() =>
            {
                Assert.That(handler.ConnectedLdaps, Has.Count.EqualTo(1));
                Assert.That(handler.WritableLdaps, Has.Count.EqualTo(1));
                Assert.That(handler.InternalLdap.Id, Is.EqualTo(5));
                Assert.That(handler.UiUsers, Has.Count.EqualTo(1));
                Assert.That(handler.SampleUsers, Has.Count.EqualTo(1));
                Assert.That(handler.ShowSampleRemoveButton, Is.True);
                Assert.That(handler.UiUsers[0].Groups, Is.EqualTo(new List<string> { "admins" }));
                Assert.That(handler.UiUsers[0].Roles, Is.EqualTo(new List<string> { "admin" }));
                Assert.That(handler.AvailableRoles.Select(r => r.Name), Is.EqualTo(kExpectedAdminReporterRoles));
            });
        }

        [Test]
        public async Task ResynchronizeUpdatesMatchingUsersAndAddsNewLdapUsers()
        {
            await using TestMiddlewareServer server = new(request =>
            {
                if (request.Method == "GET" && request.Path == "/api/User")
                {
                    return new TestResponse(200, JsonSerializer.Serialize(new[]
                    {
                        new UserGetReturnParameters
                        {
                            Name = "alice",
                            UserId = 11,
                            UserDn = "uid=alice,ou=users,dc=fworch,dc=internal",
                            Email = "alice@old.example",
                            Firstname = "Alice",
                            Lastname = "Old",
                            TenantId = 1,
                            LdapId = 5
                        }
                    }));
                }

                if (request.Method == "POST" && request.Path == "/api/User/Get")
                {
                    return new TestResponse(200, JsonSerializer.Serialize(new[]
                    {
                        new LdapUserGetReturnParameters
                        {
                            UserDn = "uid=alice,ou=users,dc=fworch,dc=internal",
                            Email = "alice@new.example",
                            Firstname = "Alicia",
                            Lastname = "New"
                        },
                        new LdapUserGetReturnParameters
                        {
                            UserDn = "uid=bob,ou=users,dc=fworch,dc=internal",
                            Email = "bob@example.com",
                            Firstname = "Bob",
                            Lastname = "Builder"
                        }
                    }));
                }

                if (request.Method == "GET" && request.Path == "/api/Group")
                {
                    return new TestResponse(200, JsonSerializer.Serialize(new[]
                    {
                        new GroupGetReturnParameters
                        {
                            GroupDn = "cn=admins,ou=groups,dc=fworch,dc=internal",
                            Members = ["uid=alice,ou=users,dc=fworch,dc=internal", "uid=bob,ou=users,dc=fworch,dc=internal"]
                        }
                    }));
                }

                if (request.Method == "GET" && request.Path == "/api/Role")
                {
                    return new TestResponse(200, JsonSerializer.Serialize(new[]
                    {
                        new RoleGetReturnParameters
                        {
                            Role = "cn=reporter,ou=roles,dc=fworch,dc=internal",
                            Attributes = [new RoleAttribute { Key = "user", Value = "uid=alice,ou=users,dc=fworch,dc=internal" }, new RoleAttribute { Key = "user", Value = "uid=bob,ou=users,dc=fworch,dc=internal" }]
                        }
                    }));
                }

                if (request.Method == "PUT" && request.Path == "/api/User")
                {
                    return new TestResponse(200, JsonSerializer.Serialize(true));
                }

                if (request.Method == "POST" && request.Path == "/api/User")
                {
                    return new TestResponse(200, JsonSerializer.Serialize(77));
                }

                return new TestResponse(404, "{}");
            });

            (SettingsUsersHandler handler, _, RecordingApiConnection apiConnection, _) = CreateHandler(middlewareClient: new MiddlewareClient(server.BaseUrl));
            apiConnection.ConnectedLdaps =
            [
                new UiLdapConnection
                {
                    Id = 5,
                    Name = "internal",
                    WriteUser = "cn=writer",
                    TenantLevel = 0,
                    UserSearchPath = "ou=users,dc=fworch,dc=internal"
                }
            ];
            apiConnection.Tenants = [new Tenant { Id = 1, Name = "tenant1" }];

            await handler.Resynchronize();

            Assert.Multiple(() =>
            {
                Assert.That(handler.UiUsers, Has.Count.EqualTo(2));
                Assert.That(handler.UiUsers.Single(user => user.Name == "alice").Email, Is.EqualTo("alice@new.example"));
                Assert.That(handler.UiUsers.Single(user => user.Name == "alice").Firstname, Is.EqualTo("Alicia"));
                Assert.That(handler.UiUsers.Single(user => user.Name == "alice").Lastname, Is.EqualTo("New"));
                Assert.That(handler.UiUsers.Single(user => user.Name == "bob").DbId, Is.EqualTo(77));
                Assert.That(handler.UiUsers.Single(user => user.Name == "bob").Groups, Does.Contain("admins"));
                Assert.That(handler.UiUsers.Single(user => user.Name == "bob").Roles, Does.Contain("reporter"));
            });

            Assert.That(apiConnection.Queries.Count(entry => entry.Query == AuthQueries.updateUserEmail), Is.EqualTo(3));
            Assert.That(apiConnection.Queries.Count(entry => entry.Query == AuthQueries.upsertUiUser), Is.EqualTo(1));
        }

        [Test]
        public async Task SaveNewUserPersistsMembershipsAndResetsModes()
        {
            await using TestMiddlewareServer server = new(request =>
            {
                if (request.Method == "POST" && request.Path == "/api/User")
                {
                    return new TestResponse(200, JsonSerializer.Serialize(23));
                }

                if (request.Method == "POST" && request.Path == "/api/Group/User")
                {
                    return new TestResponse(200, JsonSerializer.Serialize(true));
                }

                if (request.Method == "POST" && request.Path == "/api/Role/User")
                {
                    return new TestResponse(200, JsonSerializer.Serialize(true));
                }

                return new TestResponse(404, "{}");
            });

            (SettingsUsersHandler handler, _, _, _) = CreateHandler(middlewareClient: new MiddlewareClient(server.BaseUrl));
            UiLdapConnection ldap = new()
            {
                Id = 5,
                Type = (int)LdapType.ActiveDirectory,
                TenantLevel = 0,
                UserSearchPath = "ou=users,dc=fworch,dc=internal"
            };
            handler.SelectedLdap = ldap;
            handler.SelectedGroup = new UserGroup { Name = "admins", Dn = "cn=admins,ou=groups,dc=fworch,dc=internal" };
            handler.SelectedRole = new Role { Name = Basics.Roles.Auditor, Dn = "cn=auditor,ou=roles,dc=fworch,dc=internal" };
            handler.Groups = [handler.SelectedGroup];
            handler.Roles = [handler.SelectedRole];
            handler.AddMode = true;
            handler.ActUser = new UiUser
            {
                Name = "alice",
                Password = "Password1!",
                Email = "alice@example.com",
                Firstname = "Alice",
                Lastname = "Example"
            };

            await handler.Save();

            Assert.Multiple(() =>
            {
                Assert.That(handler.UiUsers, Has.Count.EqualTo(1));
                Assert.That(handler.UiUsers[0].DbId, Is.EqualTo(23));
                Assert.That(handler.UiUsers[0].PasswordMustBeChanged, Is.False);
                Assert.That(handler.AddMode, Is.False);
                Assert.That(handler.EditMode, Is.False);
                Assert.That(handler.UiUsers[0].Groups, Is.EqualTo(new List<string> { "admins" }));
                Assert.That(handler.UiUsers[0].Roles, Is.EqualTo(new List<string> { "auditor" }));
            });
        }

        [Test]
        public async Task SaveExistingUserUpdatesOnlyTheEmail()
        {
            await using TestMiddlewareServer server = new(request =>
            {
                if (request.Method == "PUT" && request.Path == "/api/User")
                {
                    return new TestResponse(200, JsonSerializer.Serialize(true));
                }

                return new TestResponse(404, "{}");
            });

            (SettingsUsersHandler handler, _, _, _) = CreateHandler(middlewareClient: new MiddlewareClient(server.BaseUrl));
            UiUser existing = new()
            {
                DbId = 11,
                Name = "alice",
                Email = "alice@old.example",
                LdapConnection = new UiLdapConnection { Id = 5 }
            };
            handler.UiUsers = [existing];
            handler.ActUser = new UiUser(existing)
            {
                Email = "alice@new.example"
            };
            handler.EditMode = true;

            await handler.Save();

            Assert.Multiple(() =>
            {
                Assert.That(handler.UiUsers[0].Email, Is.EqualTo("alice@new.example"));
                Assert.That(handler.EditMode, Is.False);
            });
        }

        [Test]
        public async Task SaveNewUserRejectsMissingRequiredFields()
        {
            List<(string Title, string Message)> messages = [];
            (SettingsUsersHandler handler, _, _, _) = CreateHandler(messages: messages);
            handler.AddMode = true;
            handler.SelectedLdap = new UiLdapConnection
            {
                Id = 5,
                Type = (int)LdapType.ActiveDirectory,
                TenantLevel = 0,
                UserSearchPath = "ou=users,dc=fworch,dc=internal"
            };
            handler.ActUser = new UiUser
            {
                Name = "",
                Password = "",
                Email = "alice@example.com",
                LdapConnection = handler.SelectedLdap
            };

            await handler.Save();

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("add_user"));
                Assert.That(messages[0].Message, Is.EqualTo("E5211"));
                Assert.That(handler.UiUsers, Is.Empty);
            });
        }

        [Test]
        public async Task SaveNewUserRejectsInvalidTenant()
        {
            List<(string Title, string Message)> messages = [];
            (SettingsUsersHandler handler, _, _, _) = CreateHandler(messages: messages);
            handler.Tenants = [new Tenant { Id = 1, Name = "tenant1" }];
            handler.AddMode = true;
            handler.SelectedLdap = new UiLdapConnection
            {
                Id = 5,
                Type = (int)LdapType.OpenLdap,
                TenantLevel = 1,
                UserSearchPath = "ou=users,dc=fworch,dc=internal"
            };
            handler.SelectedTenant = new Tenant { Id = 99, Name = "unknown" };
            handler.SelectedRole = new Role { Name = "auditor", Dn = "cn=auditor,ou=roles,dc=fworch,dc=internal" };
            handler.ActUser = new UiUser
            {
                Name = "alice",
                Password = "Password1!",
                Email = "alice@example.com",
                LdapConnection = handler.SelectedLdap,
                Tenant = handler.SelectedTenant,
                Roles = [handler.SelectedRole.Name]
            };

            await handler.Save();

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("add_user"));
                Assert.That(messages[0].Message, Is.EqualTo("E5212"));
                Assert.That(handler.UiUsers, Is.Empty);
            });
        }

        [Test]
        public async Task SaveNewUserRejectsDuplicateDn()
        {
            List<(string Title, string Message)> messages = [];
            (SettingsUsersHandler handler, _, _, _) = CreateHandler(messages: messages);
            handler.AddMode = true;
            handler.SelectedLdap = new UiLdapConnection
            {
                Id = 5,
                Type = (int)LdapType.ActiveDirectory,
                TenantLevel = 0,
                UserSearchPath = "ou=users,dc=fworch,dc=internal"
            };
            handler.SelectedRole = new Role { Name = "auditor", Dn = "cn=auditor,ou=roles,dc=fworch,dc=internal" };
            handler.UiUsers = [new UiUser { Dn = "cn=alice,ou=users,dc=fworch,dc=internal" }];
            handler.ActUser = new UiUser
            {
                Name = "alice",
                Password = "Password1!",
                Email = "alice@example.com",
                LdapConnection = handler.SelectedLdap,
                Roles = [handler.SelectedRole.Name]
            };

            await handler.Save();

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("add_user"));
                Assert.That(messages[0].Message, Is.EqualTo("E5210"));
                Assert.That(handler.UiUsers, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task SaveNewUserRejectsPasswordPolicyViolation()
        {
            List<(string Title, string Message)> messages = [];
            SimulatedGlobalConfig globalConfig = new()
            {
                PwMinLength = 12,
                PwUpperCaseRequired = false,
                PwLowerCaseRequired = false,
                PwNumberRequired = false,
                PwSpecialCharactersRequired = false
            };
            (SettingsUsersHandler handler, _, _, _) = CreateHandler(messages: messages, globalConfig: globalConfig);
            handler.AddMode = true;
            handler.SelectedLdap = new UiLdapConnection
            {
                Id = 5,
                Type = (int)LdapType.ActiveDirectory,
                TenantLevel = 0,
                UserSearchPath = "ou=users,dc=fworch,dc=internal"
            };
            handler.SelectedRole = new Role { Name = "auditor", Dn = "cn=auditor,ou=roles,dc=fworch,dc=internal" };
            handler.ActUser = new UiUser
            {
                Name = "alice",
                Password = "short",
                Email = "alice@example.com",
                LdapConnection = handler.SelectedLdap,
                Roles = [handler.SelectedRole.Name]
            };

            await handler.Save();

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("add_user"));
                Assert.That(messages[0].Message, Is.EqualTo("E541112"));
                Assert.That(handler.UiUsers, Is.Empty);
            });
        }

        [Test]
        public async Task RemoveSampleDataKeepsButtonVisibleWhenCleanupFails()
        {
            await using TestMiddlewareServer server = new(request =>
            {
                if (request.Method == "DELETE" && request.Path == "/api/User/AllGroupsAndRoles")
                {
                    return new TestResponse(200, JsonSerializer.Serialize(false));
                }

                return new TestResponse(404, "{}");
            });

            (SettingsUsersHandler handler, _, _, List<(string Title, string Message)> messages) = CreateHandler(middlewareClient: new MiddlewareClient(server.BaseUrl));
            UiUser sample = new()
            {
                DbId = 11,
                Name = "alice_demo",
                Dn = "uid=alice,dc=fworch,dc=internal",
                LdapConnection = new UiLdapConnection { Id = 5 }
            };
            handler.SampleUsers = [sample];
            handler.UiUsers = [sample];
            handler.ShowSampleRemoveButton = true;

            await handler.RemoveSampleData();

            Assert.Multiple(() =>
            {
                Assert.That(handler.ShowSampleRemoveButton, Is.True);
                Assert.That(handler.UiUsers, Has.Count.EqualTo(1));
                Assert.That(handler.WorkInProgress, Is.False);
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("remove_sample_data"));
                Assert.That(messages[0].Message, Is.EqualTo("E5221"));
            });
        }

        [Test]
        public async Task SaveExistingUserReportsMiddlewareFailure()
        {
            await using TestMiddlewareServer server = new(request =>
            {
                if (request.Method == "PUT" && request.Path == "/api/User")
                {
                    return new TestResponse(200, JsonSerializer.Serialize(false));
                }

                return new TestResponse(404, "{}");
            });

            List<(string Title, string Message)> messages = [];
            (SettingsUsersHandler handler, _, _, _) = CreateHandler(messages: messages, middlewareClient: new MiddlewareClient(server.BaseUrl));
            handler.UiUsers = [new UiUser
            {
                DbId = 11,
                Name = "alice",
                Email = "alice@old.example",
                LdapConnection = new UiLdapConnection { Id = 5 }
            }];
            handler.ActUser = new UiUser(handler.UiUsers[0]) { Email = "alice@new.example" };
            handler.EditMode = true;

            await handler.Save();

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("update_user"));
                Assert.That(messages[0].Message, Is.EqualTo("E5214"));
                Assert.That(handler.EditMode, Is.True);
                Assert.That(handler.UiUsers[0].Email, Is.EqualTo("alice@old.example"));
            });
        }

        [Test]
        public async Task DeleteReportsMiddlewareFailure()
        {
            await using TestMiddlewareServer server = new(request =>
            {
                if (request.Method == "DELETE" && request.Path == "/api/User")
                {
                    return new TestResponse(200, JsonSerializer.Serialize(false));
                }

                return new TestResponse(404, "{}");
            });

            List<(string Title, string Message)> messages = [];
            (SettingsUsersHandler handler, _, _, _) = CreateHandler(messages: messages, middlewareClient: new MiddlewareClient(server.BaseUrl));
            handler.UiUsers = [new UiUser
            {
                DbId = 11,
                Name = "alice",
                Dn = "uid=alice,dc=fworch,dc=internal",
                LdapConnection = new UiLdapConnection { Id = 5 }
            }];
            handler.ActUser = handler.UiUsers[0];

            await handler.Delete();

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("delete_user"));
                Assert.That(messages[0].Message, Is.EqualTo("E5216"));
                Assert.That(handler.UiUsers, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task SaveNewUserReportsGroupAndRoleAssignmentFailures()
        {
            await using TestMiddlewareServer server = new(request =>
            {
                if (request.Method == "POST" && request.Path == "/api/User")
                {
                    return new TestResponse(200, JsonSerializer.Serialize(23));
                }

                if (request.Method == "POST" && request.Path == "/api/Group/User")
                {
                    return new TestResponse(200, JsonSerializer.Serialize(false));
                }

                if (request.Method == "POST" && request.Path == "/api/Role/User")
                {
                    return new TestResponse(200, JsonSerializer.Serialize(false));
                }

                return new TestResponse(404, "{}");
            });

            List<(string Title, string Message)> messages = [];
            (SettingsUsersHandler handler, _, _, _) = CreateHandler(messages: messages, middlewareClient: new MiddlewareClient(server.BaseUrl));
            handler.SelectedLdap = new UiLdapConnection
            {
                Id = 5,
                Type = (int)LdapType.ActiveDirectory,
                TenantLevel = 0,
                UserSearchPath = "ou=users,dc=fworch,dc=internal"
            };
            handler.SelectedGroup = new UserGroup { Name = "admins", Dn = "cn=admins,ou=groups,dc=fworch,dc=internal" };
            handler.SelectedRole = new Role { Name = "auditor", Dn = "cn=auditor,ou=roles,dc=fworch,dc=internal" };
            handler.Groups = [handler.SelectedGroup];
            handler.Roles = [handler.SelectedRole];
            handler.AddMode = true;
            handler.ActUser = new UiUser
            {
                Name = "alice",
                Password = "Password1!",
                Email = "alice@example.com",
                LdapConnection = handler.SelectedLdap,
                Roles = [handler.SelectedRole.Name]
            };

            await handler.Save();

            Assert.Multiple(() =>
            {
                Assert.That(messages.Count(message => message.Title == "assign_user_to_group" && message.Message == "E5242"), Is.EqualTo(1));
                Assert.That(messages.Count(message => message.Title == "assign_user_group_to_role" && message.Message == "E5255"), Is.EqualTo(1));
                Assert.That(handler.UiUsers, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task DeleteRemovesTheSelectedUser()
        {
            await using TestMiddlewareServer server = new(request =>
            {
                if (request.Method == "DELETE" && request.Path == "/api/User")
                {
                    return new TestResponse(200, JsonSerializer.Serialize(true));
                }

                return new TestResponse(404, "{}");
            });

            (SettingsUsersHandler handler, _, _, _) = CreateHandler(middlewareClient: new MiddlewareClient(server.BaseUrl));
            UiUser user = new()
            {
                DbId = 11,
                Name = "alice",
                Dn = "uid=alice,dc=fworch,dc=internal",
                LdapConnection = new UiLdapConnection { Id = 5 }
            };
            handler.UiUsers = [user];
            handler.ActUser = user;
            handler.DeleteMode = true;

            await handler.Delete();

            Assert.Multiple(() =>
            {
                Assert.That(handler.UiUsers, Is.Empty);
                Assert.That(handler.DeleteMode, Is.False);
            });
        }

        [Test]
        public async Task ResetPasswordAcceptsInternalUsersAndSurfacesMiddlewareWarnings()
        {
            await using TestMiddlewareServer server = new(request =>
            {
                if (request.Method == "PATCH" && request.Path == "/api/User/ResetPassword")
                {
                    return new TestResponse(200, JsonSerializer.Serialize("ldap warning"));
                }

                return new TestResponse(404, "{}");
            });

            (SettingsUsersHandler handler, _, _, List<(string Title, string Message)> messages) = CreateHandler(middlewareClient: new MiddlewareClient(server.BaseUrl));
            handler.ActUser = new UiUser
            {
                Name = "alice",
                Dn = "uid=alice,ou=users,dc=fworch,dc=internal",
                Password = "Password1!",
                DbId = 11,
                LdapConnection = new UiLdapConnection { Id = 5 }
            };

            await handler.ResetPassword();

            Assert.Multiple(() =>
            {
                Assert.That(handler.ResetPasswordMode, Is.False);
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("reset_password"));
                Assert.That(messages[0].Message, Is.EqualTo("ldap warning"));
            });
        }

        [Test]
        public async Task RemoveSampleDataDeletesAllSampleUsers()
        {
            await using TestMiddlewareServer server = new(request =>
            {
                if (request.Method == "DELETE" && request.Path == "/api/User/AllGroupsAndRoles")
                {
                    return new TestResponse(200, JsonSerializer.Serialize(true));
                }

                if (request.Method == "DELETE" && request.Path == "/api/User")
                {
                    return new TestResponse(200, JsonSerializer.Serialize(true));
                }

                return new TestResponse(404, "{}");
            });

            (SettingsUsersHandler handler, _, _, _) = CreateHandler(middlewareClient: new MiddlewareClient(server.BaseUrl));
            UiUser sample = new()
            {
                DbId = 11,
                Name = "alice_demo",
                Dn = "uid=alice,dc=fworch,dc=internal",
                LdapConnection = new UiLdapConnection { Id = 5 }
            };
            handler.SampleUsers = [sample];
            handler.UiUsers = [sample];
            handler.ShowSampleRemoveButton = true;

            await handler.RemoveSampleData();

            Assert.Multiple(() =>
            {
                Assert.That(handler.UiUsers, Is.Empty);
                Assert.That(handler.ShowSampleRemoveButton, Is.False);
                Assert.That(handler.WorkInProgress, Is.False);
            });
        }

        private static (SettingsUsersHandler Handler, EchoUserConfig UserConfig, RecordingApiConnection ApiConnection, List<(string Title, string Message)> Messages) CreateHandler(
            List<(string Title, string Message)>? messages = null,
            RecordingApiConnection? apiConnection = null,
            MiddlewareClient? middlewareClient = null,
            SimulatedGlobalConfig? globalConfig = null)
        {
            EchoUserConfig userConfig = new();
            userConfig.User.Name = "tester";
            userConfig.User.Dn = "uid=tester,dc=fworch,dc=internal";
            userConfig.User.DbId = 99;

            RecordingApiConnection effectiveApiConnection = apiConnection ?? new RecordingApiConnection();
            MiddlewareClient effectiveMiddlewareClient = middlewareClient ?? new MiddlewareClient("http://127.0.0.1:1/");
            SimulatedGlobalConfig effectiveGlobalConfig = globalConfig ?? new()
            {
                PwMinLength = 3,
                PwUpperCaseRequired = false,
                PwLowerCaseRequired = false,
                PwNumberRequired = false,
                PwSpecialCharactersRequired = false
            };
            List<(string Title, string Message)> effectiveMessages = messages ?? [];

            SettingsUsersHandler handler = new(
                effectiveApiConnection,
                effectiveMiddlewareClient,
                userConfig,
                effectiveGlobalConfig,
                (exception, title, message, _) => effectiveMessages.Add((title, message)));

            return (handler, userConfig, effectiveApiConnection, effectiveMessages);
        }

        private sealed class EchoUserConfig : SimulatedUserConfig
        {
            public override string GetText(string key)
            {
                return key;
            }
        }

        private sealed class RecordingApiConnection : SimulatedApiConnection
        {
            public List<(string Query, object? Variables)> Queries { get; } = [];
            public List<UiLdapConnection> ConnectedLdaps { get; set; } = [];
            public List<Tenant> Tenants { get; set; } = [];
            public int NextNewUserId { get; set; } = 77;

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                Queries.Add((query, variables));
                object result = query switch
                {
                    var value when value == AuthQueries.getLdapConnections => ConnectedLdaps,
                    var value when value == AuthQueries.getTenants => Tenants,
                    var value when value == AuthQueries.updateUserEmail => new ReturnId { UpdatedId = GetVariable<int>(variables, "id") },
                    var value when value == AuthQueries.upsertUiUser => new ReturnIdWrapper { ReturnIds = [new ReturnId { NewId = NextNewUserId }] },
                    var value when value == AuthQueries.deleteUser => new ReturnId { DeletedId = GetVariable<int>(variables, "id") },
                    _ => throw new AssertionException($"Unexpected query: {query}")
                };
                return Task.FromResult((QueryResponseType)result);
            }

            private static T GetVariable<T>(object? variables, string name)
            {
                object? value = variables?.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(variables);
                return value is T typed ? typed : default!;
            }
        }

        private sealed class TestMiddlewareServer : IAsyncDisposable
        {
            private readonly TcpListener listener;
            private readonly CancellationTokenSource cancellationTokenSource = new();
            private readonly Func<TestRequest, TestResponse> responder;
            private readonly Task worker;

            public string BaseUrl { get; }

            public TestMiddlewareServer(Func<TestRequest, TestResponse> responder)
            {
                this.responder = responder;
                listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                BaseUrl = $"http://127.0.0.1:{port}/";
                worker = Task.Run(ServeAsync);
            }

            private async Task ServeAsync()
            {
                try
                {
                    while (!cancellationTokenSource.IsCancellationRequested)
                    {
                        TcpClient client = await listener.AcceptTcpClientAsync();
                        _ = Task.Run(() => HandleClientAsync(client));
                    }
                }
                catch
                {
                    // Expected when the listener is stopped during cleanup.
                }
            }

            private async Task HandleClientAsync(TcpClient client)
            {
                using (client)
                {
                    try
                    {
                        NetworkStream stream = client.GetStream();
                        TestRequest request = await ReadRequestAsync(stream);
                        TestResponse response = responder(request);
                        await WriteResponseAsync(stream, response);
                    }
                    catch
                    {
                        // Keep test cleanup resilient.
                    }
                }
            }

            private static async Task<TestRequest> ReadRequestAsync(NetworkStream stream)
            {
                using StreamReader reader = new(stream, Encoding.ASCII, leaveOpen: true);
                string? requestLine = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(requestLine))
                {
                    throw new InvalidOperationException("Missing request line");
                }

                string[] requestParts = requestLine.Split(' ', 3);
                string method = requestParts[0];
                string path = requestParts[1];
                int contentLength = 0;
                string? headerLine;
                while (!string.IsNullOrEmpty(headerLine = await reader.ReadLineAsync()))
                {
                    if (headerLine.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    {
                        contentLength = int.Parse(headerLine["Content-Length:".Length..].Trim(), CultureInfo.InvariantCulture);
                    }
                }

                char[] bodyBuffer = new char[contentLength];
                int read = 0;
                while (read < contentLength)
                {
                    int chunk = await reader.ReadAsync(bodyBuffer.AsMemory(read, contentLength - read));
                    if (chunk == 0)
                    {
                        break;
                    }
                    read += chunk;
                }

                return new TestRequest(method, path, new string(bodyBuffer, 0, read));
            }

            private static async Task WriteResponseAsync(NetworkStream stream, TestResponse response)
            {
                byte[] body = Encoding.UTF8.GetBytes(response.Body);
                string reasonPhrase = response.StatusCode switch
                {
                    200 => "OK",
                    201 => "Created",
                    400 => "Bad Request",
                    404 => "Not Found",
                    500 => "Internal Server Error",
                    _ => "OK"
                };
                string header = $"HTTP/1.1 {response.StatusCode} {reasonPhrase}\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(header));
                await stream.WriteAsync(body);
            }

            public async ValueTask DisposeAsync()
            {
                cancellationTokenSource.Cancel();
                listener.Stop();
                try
                {
                    await worker;
                }
                catch
                {
                    // Ignore shutdown races.
                }
                cancellationTokenSource.Dispose();
            }
        }

        private sealed class TestRequest
        {
            public string Method { get; }
            public string Path { get; }
            public string Body { get; }

            public TestRequest(string method, string path, string body)
            {
                Method = method;
                Path = path;
                Body = body;
            }
        }

        private sealed class TestResponse
        {
            public int StatusCode { get; }
            public string Body { get; }

            public TestResponse(int statusCode, string body)
            {
                StatusCode = statusCode;
                Body = body;
            }
        }
    }
}

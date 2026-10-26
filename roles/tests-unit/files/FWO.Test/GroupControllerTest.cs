using FWO.Data;
using FWO.Basics;
using FWO.Data.Middleware;
using FWO.Middleware.Server.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using Novell.Directory.Ldap;
using MiddlewareLdap = FWO.Middleware.Server.Ldap;

namespace FWO.Test
{
    [TestFixture]
    internal class GroupControllerTest
    {
        private static readonly string kInternalUserSearchPath = "ou=users,dc=fworch,dc=internal";
        private static readonly string kGroupSearchPath = "ou=groups,dc=fworch,dc=internal";
        private static readonly string kGroupWritePath = "ou=groups,dc=fworch,dc=internal";
        private static readonly string kSearchUser = "cn=search,dc=fworch,dc=internal";
        private static readonly string kSearchPassword = LdapTestSupport.CreateEncryptedSecret("searchpwd");
        private static readonly string kWriteUser = "cn=write,dc=fworch,dc=internal";
        private static readonly string kGroupDn = "cn=AppOwners,ou=groups,dc=fworch,dc=internal";
        private static readonly string kOtherGroupDn = "cn=OtherGroup,ou=groups,dc=fworch,dc=internal";
        private static readonly string kGroupMemberDn = "uid=groupmember,ou=users,dc=fworch,dc=internal";
        private static readonly string kDirectUserDn = "uid=direct,ou=users,dc=fworch,dc=internal";
        private static readonly string kUserDn = "uid=user,ou=users,dc=fworch,dc=internal";
        private static readonly string kUserName = "user";
        private static readonly string[] kUserValues = { kUserName };
        private static readonly string[] kUniqueMemberValues = { kUserDn };
        private static readonly string[] kGroupMemberValues = { kGroupMemberDn };
        private static readonly string[] kOwnerGroupValues = { "ownergroup" };
        private static readonly string[] kMembersValues = { kUserDn, "", kOtherGroupDn };
        private static readonly List<string> kFirstMemberPage = [kOtherGroupDn];
        private static readonly List<string> kSecondMemberPage = [kUserDn];
        private static readonly List<string> kFirstResolvedPage = [kDirectUserDn];
        private static readonly List<string> kLdapIdAndLimitKeys = ["ldapId", "limit"];

        [Test]
        public async Task Get_ReturnsInternalGroupObjectsFromLdap()
        {
            RecordingLdapClient client = new()
            {
                SearchResults = LdapTestSupport.CreateSearchResults(
                    LdapTestSupport.CreateEntry(
                        kGroupDn,
                        new LdapAttribute("uniqueMember", kUniqueMemberValues),
                        new LdapAttribute("businessCategory", kOwnerGroupValues)))
            };
            MiddlewareLdap ldap = CreateInternalGroupLdap(client);
            GroupController controller = new(new List<MiddlewareLdap> { ldap }, new SessionRevocationApiConnection(), new SimulatedGlobalConfig());

            ActionResult<List<GroupGetReturnParameters>> result = await controller.Get(new PagingParameters());

            Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
            List<GroupGetReturnParameters> groups = (List<GroupGetReturnParameters>)((OkObjectResult)result.Result!).Value!;
            Assert.That(groups, Has.Count.EqualTo(1));
            Assert.That(groups[0].GroupDn, Is.EqualTo(kGroupDn));
            Assert.That(groups[0].Members, Is.EqualTo(new List<string> { kUserDn }));
            Assert.That(groups[0].OwnerGroup, Is.True);
            Assert.That(client.SearchCalls, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task GetMembers_ReturnsMembersFromMatchingGroupLdap()
        {
            RecordingLdapClient client = new()
            {
                ReadResult = LdapTestSupport.CreateEntry(
                    kGroupDn,
                    new LdapAttribute("uniqueMember", kMembersValues))
            };
            MiddlewareLdap ldap = CreateInternalGroupLdap(client);
            GroupController controller = new(new List<MiddlewareLdap> { ldap }, new SessionRevocationApiConnection(), new SimulatedGlobalConfig());

            List<string> result = (await controller.GetMembers(new GroupMemberGetParameters { GroupDn = kGroupDn })).Value!;

            // ordered by dn, so the members page deterministically
            Assert.That(result, Is.EqualTo(new List<string> { kOtherGroupDn, kUserDn }));
            Assert.That(client.ReadCalls, Is.EqualTo(new List<string> { kGroupDn }));
        }

        [Test]
        public async Task GetMembers_PagesTheOrderedMembersAndSetsTheHasMoreHeader()
        {
            GroupController firstPageController = CreatePagingController(CreateMembersClient(), new SimulatedGlobalConfig());
            GroupController secondPageController = CreatePagingController(CreateMembersClient(), new SimulatedGlobalConfig());

            ActionResult<List<string>> firstPage = await firstPageController.GetMembers(new GroupMemberGetParameters { GroupDn = kGroupDn, Limit = 1 });
            ActionResult<List<string>> secondPage = await secondPageController.GetMembers(new GroupMemberGetParameters { GroupDn = kGroupDn, Limit = 1, Offset = 1 });

            Assert.Multiple(() =>
            {
                Assert.That(firstPage.Value, Is.EqualTo(kFirstMemberPage));
                Assert.That(HasMoreHeader(firstPageController), Is.EqualTo("true"));
                Assert.That(secondPage.Value, Is.EqualTo(kSecondMemberPage));
                Assert.That(HasMoreHeader(secondPageController), Is.EqualTo("false"));
            });
        }

        [Test]
        public async Task ListEndpoints_RejectALimitAboveTheConfiguredMaximum()
        {
            SimulatedGlobalConfig globalConfig = new() { MaxRestResponseObjectLimit = 1 };
            GroupController controller = CreatePagingController(CreateMembersClient(), globalConfig);

            ActionResult<List<GroupGetReturnParameters>> groups = await controller.Get(new PagingParameters { Limit = 2 });
            ActionResult<List<string>> members = await controller.GetMembers(new GroupMemberGetParameters { GroupDn = kGroupDn, Limit = 2 });
            ActionResult<List<string>> memberships = await controller.GetMemberships(new GroupMembershipGetParameters { UserDn = kUserDn, Offset = -1 });
            ActionResult<List<string>> resolved = await controller.ResolveMembers(new GroupResolveParameters { Dns = new List<string> { kDirectUserDn }, Limit = 0 });

            Assert.Multiple(() =>
            {
                Assert.That(groups.Result, Is.TypeOf<BadRequestObjectResult>());
                Assert.That(members.Result, Is.TypeOf<BadRequestObjectResult>());
                Assert.That(memberships.Result, Is.TypeOf<BadRequestObjectResult>());
                Assert.That(resolved.Result, Is.TypeOf<BadRequestObjectResult>());
                Assert.That(((ValidationProblemDetails)((BadRequestObjectResult)members.Result!).Value!).Errors["limit"][0], Does.Contain("between 1 and 1"));
            });
        }

        [Test]
        public async Task SearchGroups_ReportsAnUnknownLdapAndAnInvalidLimitTogether()
        {
            GroupController controller = CreatePagingController(CreateMembersClient(), new SimulatedGlobalConfig());

            ActionResult<List<string>> result = await controller.Get(new GroupGetParameters { LdapId = 99, Limit = 0 });

            ValidationProblemDetails problem = (ValidationProblemDetails)((BadRequestObjectResult)result.Result!).Value!;
            Assert.That(problem.Errors.Keys, Is.EquivalentTo(kLdapIdAndLimitKeys));
        }

        [Test]
        public async Task GetMemberships_FallsBackToMemberDnSearchWhenUserEntryHasNoMemberships()
        {
            RecordingLdapClient client = new()
            {
                ReadResult = LdapTestSupport.CreateEntry(
                    kUserDn,
                    new LdapAttribute("uid", kUserValues),
                    new LdapAttribute("memberOf", kGroupDn))
            };
            MiddlewareLdap ldap = CreateInternalGroupLdap(client);
            GroupController controller = new(new List<MiddlewareLdap> { ldap }, new SessionRevocationApiConnection(), new SimulatedGlobalConfig());

            List<string> result = (await controller.GetMemberships(new GroupMembershipGetParameters
            {
                UserDn = kUserDn
            })).Value!;

            Assert.That(result, Is.EqualTo(new List<string> { kGroupDn }));
            Assert.That(client.ReadCalls, Is.EqualTo(new List<string> { kUserDn }));
            Assert.That(client.SearchCalls, Is.Empty);
        }

        [Test]
        public async Task ResolveMembers_ReturnsResolvedUsersAndKeepsDirectDns()
        {
            RecordingLdapClient client = new()
            {
                ReadResultsByDn =
                {
                    [kGroupDn] = LdapTestSupport.CreateEntry(
                        kGroupDn,
                        new LdapAttribute("uniqueMember", kGroupMemberValues))
                }
            };
            MiddlewareLdap ldap = CreateInternalGroupLdap(client);
            GroupController controller = new(new List<MiddlewareLdap> { ldap }, new SessionRevocationApiConnection(), new SimulatedGlobalConfig());

            List<string> result = (await controller.ResolveMembers(new GroupResolveParameters
            {
                Dns = new List<string> { kGroupDn, kDirectUserDn }
            })).Value!;

            Assert.That(result, Is.EquivalentTo(new List<string> { kGroupMemberDn, kDirectUserDn }));
            Assert.That(result, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task ResolveMembers_PagesTheOrderedUserDns()
        {
            RecordingLdapClient client = new()
            {
                ReadResultsByDn =
                {
                    [kGroupDn] = LdapTestSupport.CreateEntry(
                        kGroupDn,
                        new LdapAttribute("uniqueMember", kGroupMemberValues))
                }
            };
            GroupController controller = CreatePagingController(client, new SimulatedGlobalConfig());

            ActionResult<List<string>> result = await controller.ResolveMembers(new GroupResolveParameters
            {
                Dns = new List<string> { kGroupDn, kDirectUserDn },
                Limit = 1
            });

            Assert.Multiple(() =>
            {
                Assert.That(result.Value, Is.EqualTo(kFirstResolvedPage));
                Assert.That(HasMoreHeader(controller), Is.EqualTo("true"));
            });
        }

        private static RecordingLdapClient CreateMembersClient()
        {
            return new RecordingLdapClient
            {
                ReadResult = LdapTestSupport.CreateEntry(
                    kGroupDn,
                    new LdapAttribute("uniqueMember", kMembersValues))
            };
        }

        private static GroupController CreatePagingController(RecordingLdapClient client, SimulatedGlobalConfig globalConfig)
        {
            return new GroupController(new List<MiddlewareLdap> { CreateInternalGroupLdap(client) }, new SessionRevocationApiConnection(), globalConfig)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
        }

        private static string HasMoreHeader(ControllerBase controller)
        {
            return controller.HttpContext.Response.Headers["X-Has-More"].ToString();
        }

        [Test]
        public async Task AddUser_ReturnsTrueWhenWritableInternalGroupLdapMatches()
        {
            RecordingLdapClient client = new()
            {
                ReadResult = LdapTestSupport.CreateEntry(kGroupDn)
            };
            MiddlewareLdap ldap = CreateInternalWritableGroupLdap(client);
            GroupController controller = new(new List<MiddlewareLdap> { ldap }, new SessionRevocationApiConnection(), new SimulatedGlobalConfig());

            bool result = await controller.AddUser(new GroupAddDeleteUserParameters
            {
                UserDn = kUserDn,
                GroupDn = kGroupDn
            });

            Assert.That(result, Is.True);
            Assert.That(client.ModifyCalls, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task RemoveUser_ReturnsTrueWhenWritableInternalGroupLdapMatches()
        {
            RecordingLdapClient client = new()
            {
                ReadResult = LdapTestSupport.CreateEntry(
                    kGroupDn,
                    new LdapAttribute("uniqueMember", kUniqueMemberValues))
            };
            MiddlewareLdap ldap = CreateInternalWritableGroupLdap(client);
            GroupController controller = new(new List<MiddlewareLdap> { ldap }, new SessionRevocationApiConnection(), new SimulatedGlobalConfig());

            bool result = await controller.RemoveUser(new GroupAddDeleteUserParameters
            {
                UserDn = kUserDn,
                GroupDn = kGroupDn
            });

            Assert.That(result, Is.True);
            Assert.That(client.ModifyCalls, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Create_ReturnsGroupDnWhenWritableInternalGroupLdapAddsEntry()
        {
            RecordingLdapClient client = new();
            MiddlewareLdap ldap = CreateInternalWritableGroupLdap(client);
            GroupController controller = new(new List<MiddlewareLdap> { ldap }, new SessionRevocationApiConnection(), new SimulatedGlobalConfig());

            string result = await controller.Create(new GroupAddDeleteParameters
            {
                GroupName = "AppOwners",
                OwnerGroup = true
            });

            Assert.That(result, Is.EqualTo(kGroupDn));
            Assert.That(client.AddedEntries, Has.Count.EqualTo(1));
            Assert.That(client.AddedEntries[0].Dn, Is.EqualTo(kGroupDn));
        }

        [Test]
        public async Task Edit_ReturnsUpdatedGroupDnWhenWritableInternalGroupLdapRenamesEntry()
        {
            RecordingLdapClient client = new();
            MiddlewareLdap ldap = CreateInternalWritableGroupLdap(client);
            GroupController controller = new(new List<MiddlewareLdap> { ldap }, new SessionRevocationApiConnection(), new SimulatedGlobalConfig());

            string result = await controller.Edit(new GroupEditParameters
            {
                OldGroupName = "OldName",
                NewGroupName = "NewName"
            });

            Assert.That(result, Is.EqualTo("cn=NewName,ou=groups,dc=fworch,dc=internal"));
            Assert.That(client.RenameCalls, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Delete_ReturnsTrueWhenWritableInternalGroupLdapDeletesEntry()
        {
            RecordingLdapClient client = new();
            MiddlewareLdap ldap = CreateInternalWritableGroupLdap(client);
            GroupController controller = new(new List<MiddlewareLdap> { ldap }, new SessionRevocationApiConnection(), new SimulatedGlobalConfig());

            bool result = await controller.Delete(new GroupAddDeleteParameters
            {
                GroupName = "AppOwners"
            });

            Assert.That(result, Is.True);
            Assert.That(client.DeletedDns, Is.EqualTo(new List<string> { kGroupDn }));
        }

        [Test]
        public async Task SearchGroups_SearchesOnlyTheNamedLdap()
        {
            RecordingLdapClient namedClient = new()
            {
                SearchResults = LdapTestSupport.CreateSearchResults(LdapTestSupport.CreateEntry(kGroupDn))
            };
            RecordingLdapClient otherClient = new();
            MiddlewareLdap otherLdap = CreateInternalGroupLdap(otherClient);
            otherLdap.Id = 2;
            List<MiddlewareLdap> ldaps = [CreateInternalGroupLdap(namedClient), otherLdap];
            GroupController controller = new(ldaps, new SessionRevocationApiConnection(), new SimulatedGlobalConfig());

            ActionResult<List<string>> result = await controller.Get(new GroupGetParameters { LdapId = 1, SearchPattern = "" });

            Assert.Multiple(() =>
            {
                Assert.That(result.Value, Is.EqualTo(new List<string> { kGroupDn }));
                Assert.That(otherClient.SearchCalls, Is.Empty);
            });
        }

        [TestCase(0)]
        [TestCase(5)]
        public async Task SearchGroups_RejectsAnLdapIdThatNamesNoConnectedLdap(int ldapId)
        {
            RecordingLdapClient client = new();
            List<MiddlewareLdap> ldaps = [CreateInternalGroupLdap(client)];
            GroupController controller = new(ldaps, new SessionRevocationApiConnection(), new SimulatedGlobalConfig());

            ActionResult<List<string>> result = await controller.Get(new GroupGetParameters { LdapId = ldapId, SearchPattern = "" });

            Assert.Multiple(() =>
            {
                Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
                Assert.That(client.SearchCalls, Is.Empty);
            });
        }

        [Test]
        public async Task RemoveUser_EndsTheSessionsOfTheRemovedUser()
        {
            RecordingLdapClient client = new()
            {
                ReadResult = LdapTestSupport.CreateEntry(kGroupDn, new LdapAttribute("uniqueMember", kUniqueMemberValues))
            };
            SessionRevocationApiConnection apiConnection = new();
            apiConnection.LocalUsers.Add(new UiUser { DbId = 21, Dn = kUserDn });
            GroupController controller = new(new List<MiddlewareLdap> { CreateInternalWritableGroupLdap(client) }, apiConnection, new SimulatedGlobalConfig());

            bool result = await controller.RemoveUser(new GroupAddDeleteUserParameters { UserDn = kUserDn, GroupDn = kGroupDn });

            Assert.That(result, Is.True);
            Assert.That(apiConnection.RevokedUserIds, Is.EqualTo(new List<List<int>> { new() { 21 } }));
        }

        [Test]
        public async Task Delete_EndsTheSessionsOfTheGroupMembers()
        {
            RecordingLdapClient client = new()
            {
                ReadResultsByDn =
                {
                    [kGroupDn] = LdapTestSupport.CreateEntry(kGroupDn, new LdapAttribute("uniqueMember", kGroupMemberValues))
                }
            };
            SessionRevocationApiConnection apiConnection = new();
            apiConnection.LocalUsers.Add(new UiUser { DbId = 22, Dn = kGroupMemberDn });
            apiConnection.LocalUsers.Add(new UiUser { DbId = 23, Dn = kDirectUserDn });
            GroupController controller = new(new List<MiddlewareLdap> { CreateInternalWritableGroupLdap(client) }, apiConnection, new SimulatedGlobalConfig());

            bool result = await controller.Delete(new GroupAddDeleteParameters { GroupName = "AppOwners" });

            Assert.That(result, Is.True);
            Assert.That(apiConnection.RevokedUserIds, Is.EqualTo(new List<List<int>> { new() { 22 } }));
        }

        [Test]
        public async Task Delete_KeepsTheSessionsWhenTheGroupWasNotDeleted()
        {
            SessionRevocationApiConnection apiConnection = new();
            apiConnection.LocalUsers.Add(new UiUser { DbId = 22, Dn = kGroupMemberDn });
            GroupController controller = new(new List<MiddlewareLdap> { CreateInternalGroupLdap(new RecordingLdapClient()) }, apiConnection, new SimulatedGlobalConfig());

            bool result = await controller.Delete(new GroupAddDeleteParameters { GroupName = "AppOwners" });

            Assert.That(result, Is.False);
            Assert.That(apiConnection.RevokedUserIds, Is.Empty);
        }

        private static TestableLdap CreateInternalGroupLdap(RecordingLdapClient client)
        {
            return new TestableLdap(client)
            {
                Id = 1,
                Addresses = ["ldap.example.test"],
                Port = 389,
                SearchUser = kSearchUser,
                SearchUserPwd = kSearchPassword,
                UserSearchPath = kInternalUserSearchPath,
                GroupSearchPath = kGroupSearchPath,
                GroupWritePath = kGroupWritePath
            };
        }

        private static TestableLdap CreateInternalWritableGroupLdap(RecordingLdapClient client)
        {
            return new TestableLdap(client)
            {
                Id = 1,
                Addresses = ["ldap.example.test"],
                Port = 389,
                SearchUser = kSearchUser,
                SearchUserPwd = kSearchPassword,
                WriteUser = kWriteUser,
                WriteUserPwd = "writepwd",
                UserSearchPath = kInternalUserSearchPath,
                GroupSearchPath = kGroupSearchPath,
                GroupWritePath = kGroupWritePath
            };
        }
    }
}

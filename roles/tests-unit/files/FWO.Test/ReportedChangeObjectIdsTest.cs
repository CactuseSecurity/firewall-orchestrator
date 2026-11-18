using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Report;
using FWO.Report;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class ReportedChangeObjectIdsTest
    {
        private static readonly long[] kAllNetworkObjectIds = [10, 11, 20, 21, 30, 31, 40, 41];
        private static readonly long[] kAllServiceIds = [50, 51, 52, 60, 61];
        private static readonly long[] kAllUserIds = [70, 80, 81];
        private static readonly long[] kInsertedObjectIds = [5, 6];
        private static readonly long[] kMissingMemberObjectIds = [12, 13];
        private static readonly long[] kMissingMemberServiceIds = [62];
        private static readonly long[] kMissingMemberUserIds = [91];

        [Test]
        public void CollectContainsObjectsOfOldAndNewRuleVersionsAndChangedObjects()
        {
            ReportedChangeObjectIds ids = ReportedChangeObjectIds.Collect(BuildManagement());

            Assert.That(ids.NetworkObjectIds, Is.EquivalentTo(kAllNetworkObjectIds));
            Assert.That(ids.ServiceIds, Is.EquivalentTo(kAllServiceIds));
            Assert.That(ids.UserIds, Is.EquivalentTo(kAllUserIds));
            Assert.That(ids.IsEmpty, Is.False);
        }

        [Test]
        public void CollectIgnoresMissingRuleVersionsAndPlaceholderIds()
        {
            ManagementReport management = new()
            {
                RuleChanges =
                [
                    new RuleChange { ChangeAction = 'I', OldRule = null!, NewRule = new Rule { Froms = [new NetworkLocation(new NetworkUser(), new NetworkObject { Id = 5 })] } },
                    new RuleChange { ChangeAction = 'D', OldRule = new Rule { NatData = null! }, NewRule = null! }
                ],
                ObjectChanges = [new ObjectChange { ChangeAction = 'I', OldObject = null!, NewObject = new NetworkObject { Id = 6 } }]
            };

            ReportedChangeObjectIds ids = ReportedChangeObjectIds.Collect(management);

            Assert.That(ids.NetworkObjectIds, Is.EquivalentTo(kInsertedObjectIds));
            Assert.That(ids.ServiceIds, Is.Empty);
            Assert.That(ids.UserIds, Is.Empty, "a user placeholder without id must not be collected");
        }

        [Test]
        public void CollectOfManagementWithoutChangesIsEmpty()
        {
            ReportedChangeObjectIds ids = ReportedChangeObjectIds.Collect(new ManagementReport());

            Assert.That(ids.IsEmpty, Is.True);
        }

        [Test]
        public void IdsAreOnlyReturnedForRequestedCategory()
        {
            ReportedChangeObjectIds ids = ReportedChangeObjectIds.Collect(BuildManagement());

            Assert.That(ids.GetNetworkObjectIds(ObjCategory.nobj), Is.EqualTo(kAllNetworkObjectIds));
            Assert.That(ids.GetServiceIds(ObjCategory.nobj), Is.Empty);
            Assert.That(ids.GetUserIds(ObjCategory.nobj), Is.Empty);
            Assert.That(ids.GetServiceIds(ObjCategory.nsrv), Is.EqualTo(kAllServiceIds));
            Assert.That(ids.GetUserIds(ObjCategory.user), Is.EqualTo(kAllUserIds));
            Assert.That(ids.GetNetworkObjectIds(ObjCategory.all), Has.Count.EqualTo(kAllNetworkObjectIds.Length));
            Assert.That(ids.GetServiceIds(ObjCategory.all), Has.Count.EqualTo(kAllServiceIds.Length));
            Assert.That(ids.GetUserIds(ObjCategory.all), Has.Count.EqualTo(kAllUserIds.Length));
        }

        [Test]
        public void ReturnedIdListsAreIndependentCopies()
        {
            ReportedChangeObjectIds ids = ReportedChangeObjectIds.Collect(BuildManagement());

            List<long> emptyIds = ids.GetUserIds(ObjCategory.nobj);
            emptyIds.Add(99);

            Assert.That(ids.GetServiceIds(ObjCategory.nobj), Is.Empty);
            Assert.That(ids.UserIds, Does.Not.Contain(99L));
        }

        [Test]
        public void CollectMissingGroupMembersReturnsDirectAndFlatMembersNotKnownYet()
        {
            ReportedChangeObjectIds knownIds = ReportedChangeObjectIds.Collect(BuildManagement());
            ManagementReport fetched = new()
            {
                ReportObjects =
                [
                    new NetworkObject
                    {
                        Id = 10,
                        ObjectGroups = [new Group<NetworkObject> { Object = new NetworkObject { Id = 11 } }, new Group<NetworkObject> { Object = new NetworkObject { Id = 12 } }, new Group<NetworkObject>()],
                        ObjectGroupFlats = [new GroupFlat<NetworkObject> { Object = new NetworkObject { Id = 12 } }, new GroupFlat<NetworkObject> { Object = new NetworkObject { Id = 13 } }]
                    }
                ],
                ReportServices = [new NetworkService { Id = 50, ServiceGroupFlats = [new GroupFlat<NetworkService> { Object = new NetworkService { Id = 62 } }, new GroupFlat<NetworkService> { Object = new NetworkService { Id = 51 } }] }],
                ReportUsers = [new NetworkUser { Id = 70, UserGroups = [new Group<NetworkUser> { Object = new NetworkUser { Id = 91 } }, new Group<NetworkUser> { Object = new NetworkUser() }] }]
            };

            ReportedChangeObjectIds memberIds = ReportedChangeObjectIds.CollectMissingGroupMembers(fetched, knownIds);

            Assert.That(memberIds.NetworkObjectIds, Is.EquivalentTo(kMissingMemberObjectIds), "11 is already known, 12 is listed once");
            Assert.That(memberIds.ServiceIds, Is.EquivalentTo(kMissingMemberServiceIds));
            Assert.That(memberIds.UserIds, Is.EquivalentTo(kMissingMemberUserIds), "members without object or id are ignored");
        }

        [Test]
        public void CollectMissingGroupMembersOfObjectsWithoutMembersIsEmpty()
        {
            ManagementReport fetched = new() { ReportObjects = [new NetworkObject { Id = 10 }] };

            Assert.That(ReportedChangeObjectIds.CollectMissingGroupMembers(fetched, new ReportedChangeObjectIds()).IsEmpty, Is.True);
        }

        [Test]
        public void ChangeReportRuleFragmentsSelectManagementIdForObjectLinks()
        {
            // the object links of a change report are built from the management id of the changed rules
            Assert.That(CountOccurrences(RuleQueries.ruleOverviewForChangeReportFragments, "mgm_id"), Is.GreaterThanOrEqualTo(2));
            Assert.That(CountOccurrences(RuleQueries.ruleDetailsForChangeReportFragments, "mgm_id"), Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void ObjectDetailsByIdsQueryRequiresIdLists()
        {
            // a missing (null) id list would make the api return all objects of the management
            Assert.That(ObjectQueries.getObjectDetailsByIds, Does.Contain("$nwObjIds: [bigint!]!"));
            Assert.That(ObjectQueries.getObjectDetailsByIds, Does.Contain("$svcIds: [bigint!]!"));
            Assert.That(ObjectQueries.getObjectDetailsByIds, Does.Contain("$userIds: [bigint!]!"));
        }

        private static int CountOccurrences(string text, string token)
        {
            return text.Split(token).Length - 1;
        }

        private static ManagementReport BuildManagement()
        {
            Rule oldRule = new()
            {
                Froms = [new NetworkLocation(new NetworkUser { Id = 70 }, new NetworkObject { Id = 10 })],
                Tos = [new NetworkLocation(new NetworkUser(), new NetworkObject { Id = 11 })],
                Services = [new ServiceWrapper { Content = new NetworkService { Id = 50 } }]
            };
            Rule newRule = new()
            {
                Froms = [new NetworkLocation(new NetworkUser(), new NetworkObject { Id = 10 })],
                Tos = [new NetworkLocation(new NetworkUser(), new NetworkObject { Id = 20 })],
                Services = [new ServiceWrapper { Content = new NetworkService { Id = 51 } }],
                NatData = new NatData
                {
                    TranslatedFroms = [new NetworkLocation(new NetworkUser(), new NetworkObject { Id = 21 })],
                    TranslatedTos = [new NetworkLocation(new NetworkUser(), new NetworkObject { Id = 30 })],
                    TranslatedServices = [new ServiceWrapper { Content = new NetworkService { Id = 52 } }]
                }
            };
            return new ManagementReport
            {
                Id = 1,
                RuleChanges = [new RuleChange { ChangeAction = 'C', OldRule = oldRule, NewRule = newRule }],
                ObjectChanges = [new ObjectChange { ChangeAction = 'C', OldObject = new NetworkObject { Id = 31 }, NewObject = new NetworkObject { Id = 40 } },
                                 new ObjectChange { ChangeAction = 'D', OldObject = new NetworkObject { Id = 41 }, NewObject = null! }],
                ServiceChanges = [new ServiceChange { ChangeAction = 'C', OldService = new NetworkService { Id = 60 }, NewService = new NetworkService { Id = 61 } }],
                UserChanges = [new UserChange { ChangeAction = 'C', OldUser = new NetworkUser { Id = 80 }, NewUser = new NetworkUser { Id = 81 } }]
            };
        }
    }
}

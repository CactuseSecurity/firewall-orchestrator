using FWO.Data.Flow;
using FWO.Data;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class FlowAdminHelperTest
    {
        [Test]
        public void BuildDuplicateGroups_FindsInactiveConflicts()
        {
            List<FlowNwObject> flowObjects =
            [
                new FlowNwObject
                {
                    Id = 1,
                    Name = "duplicate-flow",
                    Objects =
                    [
                        new NetworkObject
                        {
                            Id = 100,
                            Name = "obj-a",
                            FlowNetworkObjectId = 1,
                            FlowActive = false
                        },
                        new NetworkObject
                        {
                            Id = 200,
                            Name = "obj-b",
                            FlowNetworkObjectId = 1,
                            FlowActive = false
                        }
                    ]
                }
            ];

            List<FlowNwObjectDuplicateGroup> groups = FlowAdminHelper.BuildDuplicateGroups(flowObjects);

            Assert.That(groups, Has.Count.EqualTo(1));
            Assert.That(groups[0].FlowNwObjectId, Is.EqualTo(1));
            Assert.That(groups[0].Objects, Has.Count.EqualTo(2));
        }

        [Test]
        public void BuildDuplicateGroups_SkipsActiveMappings()
        {
            List<FlowNwObject> flowObjects =
            [
                new FlowNwObject
                {
                    Id = 1,
                    Name = "non-duplicate-flow",
                    Objects =
                    [
                        new NetworkObject
                        {
                            Id = 100,
                            Name = "obj-a",
                            FlowNetworkObjectId = 1,
                            FlowActive = true
                        },
                        new NetworkObject
                        {
                            Id = 200,
                            Name = "obj-b",
                            FlowNetworkObjectId = 1,
                            FlowActive = false
                        }
                    ]
                }
            ];

            List<FlowNwObjectDuplicateGroup> groups = FlowAdminHelper.BuildDuplicateGroups(flowObjects);

            Assert.That(groups, Is.Empty);
        }

        [Test]
        public void BuildDuplicateGroups_FindsInactiveFlowNwGroupConflicts()
        {
            List<FlowNwGroup> flowGroups =
            [
                new FlowNwGroup
                {
                    Id = 11,
                    Name = "group-1"
                }
            ];
            List<Management> managements =
            [
                new Management
                {
                    Id = 1,
                    Name = "mgm-1",
                    Objects =
                    [
                        new NetworkObject { Id = 101, Name = "obj-a", FlowNetworkGroupId = 11, FlowActive = false },
                        new NetworkObject { Id = 102, Name = "obj-b", FlowNetworkGroupId = 11, FlowActive = false }
                    ]
                }
            ];

            List<FlowNwGroupDuplicateGroup> groups = FlowAdminHelper.BuildDuplicateGroups(flowGroups, managements);

            Assert.That(groups, Has.Count.EqualTo(1));
            Assert.That(groups[0].FlowNwGroupId, Is.EqualTo(11));
            Assert.That(groups[0].Objects, Has.Count.EqualTo(2));
        }

        [Test]
        public void FormatFlowNwGroupTechnicalDetails_UsesLocalizedMembersLabel()
        {
            FlowNwGroup candidate = new()
            {
                Id = 11,
                Name = "group-1",
                NwGroupMembers =
                [
                    new FlowNwGroupMember { NwObject = new FlowNwObject { Id = 101, Name = "obj-a" } },
                    new FlowNwGroupMember { NwObject = new FlowNwObject { Id = 102, Name = "obj-b" } }
                ]
            };

            string details = FlowAdminHelper.FormatFlowNwGroupTechnicalDetails(candidate, "Members");

            Assert.That(details, Is.EqualTo("2 Members"));
        }

        [Test]
        public void BuildDuplicateGroups_FindsInactiveFlowSvcObjectConflicts()
        {
            List<FlowSvcObject> flowObjects =
            [
                new FlowSvcObject
                {
                    Id = 21,
                    Name = "svc-object-1"
                }
            ];
            List<Management> managements =
            [
                new Management
                {
                    Id = 2,
                    Name = "mgm-2",
                    Services =
                    [
                        new NetworkService
                        {
                            Id = 201,
                            Name = "svc-a",
                            FlowServiceObjectId = 21,
                            FlowActive = false,
                            Protocol = new NetworkProtocol { Id = 6, Name = "TCP" }
                        },
                        new NetworkService
                        {
                            Id = 202,
                            Name = "svc-b",
                            FlowServiceObjectId = 21,
                            FlowActive = false,
                            Protocol = new NetworkProtocol { Id = 6, Name = "TCP" }
                        }
                    ]
                }
            ];

            List<FlowSvcObjectDuplicateGroup> groups = FlowAdminHelper.BuildDuplicateGroups(flowObjects, managements);

            Assert.That(groups, Has.Count.EqualTo(1));
            Assert.That(groups[0].FlowSvcObjectId, Is.EqualTo(21));
            Assert.That(groups[0].Services, Has.Count.EqualTo(2));
        }

        [Test]
        public void FormatFlowSvcGroupTechnicalDetails_UsesLocalizedMembersLabel()
        {
            FlowSvcGroup candidate = new()
            {
                Id = 31,
                Name = "svc-group-1",
                SvcGroupMembers =
                [
                    new FlowSvcGroupMember { SvcObject = new FlowSvcObject { Id = 301, Name = "svc-a" } },
                    new FlowSvcGroupMember { SvcObject = new FlowSvcObject { Id = 302, Name = "svc-b" } }
                ]
            };

            string details = FlowAdminHelper.FormatFlowSvcGroupTechnicalDetails(candidate, "Members");

            Assert.That(details, Is.EqualTo("2 Members"));
        }

        [Test]
        public void BuildDuplicateGroups_FindsInactiveFlowSvcObjectConflictsAcrossMultipleManagements()
        {
            List<FlowSvcObject> flowObjects =
            [
                new FlowSvcObject
                {
                    Id = 21,
                    Name = "svc-object-1"
                }
            ];
            List<Management> managements =
            [
                new Management
                {
                    Id = 2,
                    Name = "mgm-2",
                    Services =
                    [
                        new NetworkService
                        {
                            Id = 201,
                            Name = "svc-a",
                            FlowServiceObjectId = 21,
                            FlowActive = false,
                            Protocol = new NetworkProtocol { Id = 6, Name = "TCP" }
                        },
                        new NetworkService
                        {
                            Id = 202,
                            Name = "svc-b",
                            FlowServiceObjectId = 21,
                            FlowActive = false,
                            Protocol = new NetworkProtocol { Id = 6, Name = "TCP" }
                        }
                    ]
                },
                new Management
                {
                    Id = 3,
                    Name = "mgm-3",
                    Services =
                    [
                        new NetworkService
                        {
                            Id = 301,
                            Name = "svc-c",
                            FlowServiceObjectId = 21,
                            FlowActive = false,
                            Protocol = new NetworkProtocol { Id = 17, Name = "UDP" }
                        },
                        new NetworkService
                        {
                            Id = 302,
                            Name = "svc-d",
                            FlowServiceObjectId = 21,
                            FlowActive = false,
                            Protocol = new NetworkProtocol { Id = 17, Name = "UDP" }
                        }
                    ]
                }
            ];

            List<FlowSvcObjectDuplicateGroup> groups = FlowAdminHelper.BuildDuplicateGroups(flowObjects, managements);

            Assert.That(groups, Has.Count.EqualTo(2));
            Assert.That(groups.Select(group => group.ManagementName), Is.EqualTo(new[] { "mgm-2", "mgm-3" }));
        }

        [Test]
        public void BuildDuplicateGroups_FindsInactiveFlowSvcGroupConflicts()
        {
            List<FlowSvcGroup> flowGroups =
            [
                new FlowSvcGroup
                {
                    Id = 31,
                    Name = "svc-group-1"
                }
            ];
            List<Management> managements =
            [
                new Management
                {
                    Id = 3,
                    Name = "mgm-3",
                    Services =
                    [
                        new NetworkService
                        {
                            Id = 301,
                            Name = "svc-a",
                            FlowServiceGroupId = 31,
                            FlowActive = false,
                            Protocol = new NetworkProtocol { Id = 6, Name = "TCP" }
                        },
                        new NetworkService
                        {
                            Id = 302,
                            Name = "svc-b",
                            FlowServiceGroupId = 31,
                            FlowActive = false,
                            Protocol = new NetworkProtocol { Id = 17, Name = "UDP" }
                        }
                    ]
                }
            ];

            List<FlowSvcGroupDuplicateGroup> groups = FlowAdminHelper.BuildDuplicateGroups(flowGroups, managements);

            Assert.That(groups, Has.Count.EqualTo(1));
            Assert.That(groups[0].FlowSvcGroupId, Is.EqualTo(31));
            Assert.That(groups[0].Services, Has.Count.EqualTo(2));
        }

        [Test]
        public void BuildDuplicateGroups_FindsInactiveFlowTimeObjectConflicts()
        {
            List<FlowTimeObject> flowObjects =
            [
                new FlowTimeObject
                {
                    Id = 41,
                    Name = "time-object-1"
                }
            ];
            List<Management> managements =
            [
                new Management
                {
                    Id = 4,
                    Name = "mgm-4",
                    TimeObjects =
                    [
                        new TimeObject
                        {
                            Id = 401,
                            Name = "time-a",
                            Uid = "uid-401",
                            FlowTimeObjectId = 41,
                            FlowActive = false
                        },
                        new TimeObject
                        {
                            Id = 402,
                            Name = "time-b",
                            Uid = "uid-402",
                            FlowTimeObjectId = 41,
                            FlowActive = false
                        }
                    ]
                }
            ];

            List<FlowTimeObjectDuplicateGroup> groups = FlowAdminHelper.BuildDuplicateGroups(flowObjects, managements);

            Assert.That(groups, Has.Count.EqualTo(1));
            Assert.That(groups[0].FlowTimeObjectId, Is.EqualTo(41));
            Assert.That(groups[0].TimeObjects, Has.Count.EqualTo(2));
        }

        [Test]
        public void FilterCustomObjectCandidates_FindsMatchesAcrossRelevantFields()
        {
            List<NetworkObject> candidates =
            [
                new NetworkObject
                {
                    Id = 10,
                    Name = "thisisatestobject",
                    IP = "10.0.0.1",
                    IpEnd = "",
                    Uid = "uid-10",
                    Active = false,
                    Type = new NetworkObjectType { Id = 1, Name = "host" }
                },
                new NetworkObject
                {
                    Id = 20,
                    Name = "another-object",
                    IP = "192.0.2.1",
                    IpEnd = "",
                    Uid = "uid-20",
                    Active = true,
                    Type = new NetworkObjectType { Id = 2, Name = "network" }
                }
            ];

            List<NetworkObject> byName = FlowAdminHelper.FilterCustomObjectCandidates(candidates, "test");
            List<NetworkObject> byIp = FlowAdminHelper.FilterCustomObjectCandidates(candidates, "10.0.0.1");
            List<NetworkObject> byUid = FlowAdminHelper.FilterCustomObjectCandidates(candidates, "uid-10");
            List<NetworkObject> byType = FlowAdminHelper.FilterCustomObjectCandidates(candidates, "host");

            Assert.That(byName, Has.Count.EqualTo(1));
            Assert.That(byName[0].Id, Is.EqualTo(10));
            Assert.That(byIp, Has.Count.EqualTo(1));
            Assert.That(byIp[0].Id, Is.EqualTo(10));
            Assert.That(byUid, Has.Count.EqualTo(1));
            Assert.That(byUid[0].Id, Is.EqualTo(10));
            Assert.That(byType, Has.Count.EqualTo(1));
            Assert.That(byType[0].Id, Is.EqualTo(10));
        }

        [Test]
        public void HasNoTechnicalAddress_OnlyReturnsTrueForObjectsWithoutIpData()
        {
            NetworkObject customObject = new()
            {
                Id = 100,
                IP = "",
                IpEnd = "",
                Uid = "fw-uid"
            };
            NetworkObject technicalObject = new()
            {
                Id = 200,
                IP = "192.0.2.10",
                IpEnd = "",
                Uid = "fw-uid-2"
            };

            Assert.That(FlowAdminHelper.HasNoTechnicalAddress(customObject), Is.True);
            Assert.That(FlowAdminHelper.HasNoTechnicalAddress(technicalObject), Is.False);
        }

        [Test]
        public void FormatNetworkObjectTechnicalDetails_IncludesTheTechnicalIdentifier()
        {
            NetworkObject candidate = new()
            {
                Id = 42,
                Name = "candidate",
                IP = "",
                IpEnd = "",
                Uid = "uid-42"
            };

            string details = FlowAdminHelper.FormatNetworkObjectTechnicalDetails(candidate);

            Assert.That(details, Does.Contain("candidate"));
            Assert.That(details, Does.Contain("uid-42"));
        }

        [Test]
        public void FormatNetworkObjectTechnicalDetails_UsesTechnicalIdentifierForNoIpObjects()
        {
            NetworkObject candidate = new()
            {
                Id = 42,
                Name = "",
                IP = "",
                IpEnd = "",
                Uid = "uid-42"
            };

            string details = FlowAdminHelper.FormatNetworkObjectTechnicalDetails(candidate);

            Assert.That(details, Is.EqualTo("uid-42"));
        }

        [Test]
        public void FormatFlowNwObjectTechnicalDetails_UsesIpRangeWhenAvailable()
        {
            FlowNwObject candidate = new()
            {
                Id = 42,
                Name = "flow-candidate",
                IpStart = "192.0.2.10",
                IpEnd = "",
                Hash = "hash-42"
            };

            string details = FlowAdminHelper.FormatFlowNwObjectTechnicalDetails(candidate);

            Assert.That(details, Is.EqualTo("192.0.2.10"));
        }

        [Test]
        public void FormatFlowNwObjectTechnicalDetails_ReturnsEmptyForObjectsWithoutIpData()
        {
            FlowNwObject candidate = new()
            {
                Id = 42,
                Name = "flow-candidate",
                IpStart = "",
                IpEnd = "",
                Hash = "hash-42"
            };

            string details = FlowAdminHelper.FormatFlowNwObjectTechnicalDetails(candidate);

            Assert.That(details, Is.EqualTo(""));
        }

        [Test]
        public void FormatFlowSvcObjectTechnicalDetails_UsesPortRange()
        {
            FlowSvcObject candidate = new()
            {
                Id = 52,
                Name = "svc-object",
                PortStart = 80,
                PortEnd = 443,
                ProtoId = 6
            };

            string details = FlowAdminHelper.FormatFlowSvcObjectTechnicalDetails(candidate);

            Assert.That(details, Does.Contain("80-443"));
            Assert.That(details, Does.Contain("6"));
        }

        [Test]
        public void FormatFlowSvcObjectTechnicalDetails_UsesProtocolNameWhenAvailable()
        {
            FlowSvcObject candidate = new()
            {
                Id = 53,
                Name = "svc-object",
                PortStart = 80,
                PortEnd = 443,
                ProtoId = 6
            };

            string details = FlowAdminHelper.FormatFlowSvcObjectTechnicalDetails(candidate, [new IpProtocol { Id = 6, Name = "TCP" }]);

            Assert.That(details, Does.Contain("80-443"));
            Assert.That(details, Does.Contain("TCP"));
            Assert.That(details, Does.Not.Contain("6"));
        }

        [Test]
        public void FormatTimeObjectTechnicalDetails_IncludesTechnicalIdentifier()
        {
            TimeObject candidate = new()
            {
                Id = 62,
                Name = "time-object",
                Uid = "uid-62",
                StartTime = new DateTime(2026, 06, 01, 8, 0, 0),
                EndTime = new DateTime(2026, 06, 01, 18, 0, 0)
            };

            string details = FlowAdminHelper.FormatTimeObjectTechnicalDetails(candidate);

            Assert.That(details, Does.Contain("uid-62"));
            Assert.That(details, Does.Contain("2026-06-01"));
        }

        [Test]
        public void FormatDuplicateObjectSummary_TruncatesLongLists()
        {
            List<NetworkObject> candidates =
            [
                new NetworkObject { Id = 1, Name = "one", IP = "", IpEnd = "", Uid = "uid-1" },
                new NetworkObject { Id = 2, Name = "two", IP = "", IpEnd = "", Uid = "uid-2" },
                new NetworkObject { Id = 3, Name = "three", IP = "", IpEnd = "", Uid = "uid-3" }
            ];

            string summary = FlowAdminHelper.FormatDuplicateObjectSummary(candidates, 2, "None", "... and @@COUNT@@ more");

            Assert.That(summary, Is.EqualTo("one [uid-1], two [uid-2], ... and 1 more"));
        }

        [Test]
        public void FormatDuplicateObjectSummary_ReturnsPlaceholderForEmptyLists()
        {
            string summary = FlowAdminHelper.FormatDuplicateObjectSummary(Array.Empty<NetworkObject>(), 2, "None", "... and @@COUNT@@ more");

            Assert.That(summary, Is.EqualTo("None"));
        }
    }
}

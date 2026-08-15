using FWO.Data.Flow;
using FWO.Data;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class FlowAdminHelperTest
    {
        private static readonly long[] kInactiveConflictObjectIds = [100L, 200L];
        private static readonly string[] kManagementNames = ["mgm-a", "mgm-b"];

        [Test]
        public void BuildDuplicateGroups_FindsInactiveConflicts()
        {
            List<FlowNwObject> flowObjects =
            [
                new FlowNwObject
                {
                    Id = 1,
                    Name = "duplicate-flow"
                }
            ];
            List<Management> managements =
            [
                new Management
                {
                    Id = 10,
                    Name = "mgm-a",
                    Objects =
                    [
                        new NetworkObject
                        {
                            Id = 200,
                            Name = "obj-b",
                            FlowNetworkObjectId = 1,
                            FlowActive = false
                        },
                        new NetworkObject
                        {
                            Id = 100,
                            Name = "obj-a",
                            FlowNetworkObjectId = 1,
                            FlowActive = false
                        }
                    ]
                }
            ];

            List<FlowNwObjectDuplicateGroup> groups = FlowAdminHelper.BuildDuplicateGroups(flowObjects, managements);

            Assert.That(groups, Has.Count.EqualTo(1));
            Assert.That(groups[0].FlowNwObjectId, Is.EqualTo(1));
            Assert.That(groups[0].ManagementId, Is.EqualTo(10));
            Assert.That(groups[0].ManagementName, Is.EqualTo("mgm-a"));
            Assert.That(groups[0].Objects, Has.Count.EqualTo(2));
            Assert.That(groups[0].Objects.Select(nwObject => nwObject.Id), Is.EqualTo(kInactiveConflictObjectIds));
        }

        [Test]
        public void BuildDuplicateGroups_SkipsActiveMappings()
        {
            List<FlowNwObject> flowObjects =
            [
                new FlowNwObject
                {
                    Id = 1,
                    Name = "non-duplicate-flow"
                }
            ];
            List<Management> managements =
            [
                new Management
                {
                    Id = 10,
                    Name = "mgm-a",
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

            List<FlowNwObjectDuplicateGroup> groups = FlowAdminHelper.BuildDuplicateGroups(flowObjects, managements);

            Assert.That(groups, Is.Empty);
        }

        [Test]
        public void BuildDuplicateGroups_CreatesSeparateGroupsPerManagement()
        {
            List<FlowNwObject> flowObjects =
            [
                new FlowNwObject
                {
                    Id = 1,
                    Name = "duplicate-flow"
                }
            ];
            List<Management> managements =
            [
                new Management
                {
                    Id = 20,
                    Name = "mgm-b",
                    Objects =
                    [
                        new NetworkObject { Id = 201, Name = "obj-c", FlowNetworkObjectId = 1, FlowActive = false },
                        new NetworkObject { Id = 202, Name = "obj-d", FlowNetworkObjectId = 1, FlowActive = false }
                    ]
                },
                new Management
                {
                    Id = 10,
                    Name = "mgm-a",
                    Objects =
                    [
                        new NetworkObject { Id = 101, Name = "obj-a", FlowNetworkObjectId = 1, FlowActive = false },
                        new NetworkObject { Id = 102, Name = "obj-b", FlowNetworkObjectId = 1, FlowActive = false }
                    ]
                }
            ];

            List<FlowNwObjectDuplicateGroup> groups = FlowAdminHelper.BuildDuplicateGroups(flowObjects, managements);

            Assert.That(groups, Has.Count.EqualTo(2));
            Assert.That(groups.Select(group => group.ManagementName), Is.EqualTo(kManagementNames));
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
        public void FormatFlowNwGroupMemberDetails_ListsNetworkMembers()
        {
            FlowNwGroup candidate = new()
            {
                Id = 11,
                Name = "group-1",
                NwGroupMembers =
                [
                    new FlowNwGroupMember
                    {
                        NwObject = new FlowNwObject
                        {
                            Id = 101,
                            IpStart = "192.0.2.10",
                            IpEnd = "192.0.2.10"
                        }
                    },
                    new FlowNwGroupMember
                    {
                        NwObject = new FlowNwObject
                        {
                            Id = 102,
                            IpStart = "198.51.100.0",
                            IpEnd = "198.51.100.255"
                        }
                    }
                ]
            };

            string details = FlowAdminHelper.FormatFlowNwGroupMemberDetails(candidate, 5, "None", "... and @@COUNT@@ more");

            Assert.That(details, Does.Contain("192.0.2.10"));
            Assert.That(details, Does.Contain("198.51.100.0/24"));
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
        public void FormatFlowSvcGroupMemberDetails_ListsServiceMembers()
        {
            FlowSvcGroup candidate = new()
            {
                Id = 31,
                Name = "svc-group-1",
                SvcGroupMembers =
                [
                    new FlowSvcGroupMember
                    {
                        SvcObject = new FlowSvcObject
                        {
                            Id = 301,
                            PortStart = 80,
                            PortEnd = 80,
                            ProtoId = 6
                        }
                    },
                    new FlowSvcGroupMember
                    {
                        SvcObject = new FlowSvcObject
                        {
                            Id = 302,
                            PortStart = 25565,
                            PortEnd = 25568,
                            ProtoId = 6
                        }
                    }
                ]
            };

            string details = FlowAdminHelper.FormatFlowSvcGroupMemberDetails(
                candidate,
                5,
                "None",
                "... and @@COUNT@@ more",
                [new IpProtocol { Id = 6, Name = "TCP" }]);

            Assert.That(details, Does.Contain("80/TCP"));
            Assert.That(details, Does.Contain("25565-25568/TCP"));
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
            Assert.That(groups[0].ManagementName, Is.EqualTo("mgm-2"));
            Assert.That(groups[1].ManagementName, Is.EqualTo("mgm-3"));
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
        public void FormatNetworkObjectTechnicalDetails_CanSuppressTechnicalIdentifier()
        {
            NetworkObject candidate = new()
            {
                Id = 42,
                Name = "candidate",
                IP = "",
                IpEnd = "",
                Uid = "uid-42"
            };

            string details = FlowAdminHelper.FormatNetworkObjectTechnicalDetails(candidate, false);

            Assert.That(details, Is.EqualTo("candidate"));
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
        public void FormatFlowNwObjectTechnicalDetails_UsesDisplayIpFormattingForNetworks()
        {
            FlowNwObject candidate = new()
            {
                Id = 42,
                Name = "flow-candidate",
                IpStart = "0.0.0.0/0",
                IpEnd = "255.255.255.255/0",
                Hash = "hash-42"
            };

            string details = FlowAdminHelper.FormatFlowNwObjectTechnicalDetails(candidate);

            Assert.That(details, Is.EqualTo("0.0.0.0/0"));
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
        public void FormatNetworkServiceTechnicalDetails_CanSuppressTechnicalIdentifier()
        {
            NetworkService candidate = new()
            {
                Id = 54,
                Name = "https",
                Uid = "uid-54",
                DestinationPort = 443,
                DestinationPortEnd = 443,
                Protocol = new NetworkProtocol { Id = 6, Name = "TCP" }
            };

            string details = FlowAdminHelper.FormatNetworkServiceTechnicalDetails(candidate, false);

            Assert.That(details, Is.EqualTo("https (443/TCP)"));
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

        [Test]
        public void MergeNetworkObjectMappingUpdate_ClearsCachedGroupMapping()
        {
            NetworkObject cachedObject = new()
            {
                Id = 42,
                Name = "cached",
                IP = "",
                IpEnd = "",
                Uid = "cached-uid",
                Active = true,
                FlowActive = false,
                FlowNetworkObjectId = null,
                FlowNetworkGroupId = 700
            };
            NetworkObject updatedObject = new()
            {
                Id = 42,
                Name = "updated",
                IP = "",
                IpEnd = "",
                Uid = "updated-uid",
                Active = true,
                FlowActive = true,
                FlowNetworkObjectId = 800
            };

            FlowAdminHelper.MergeNetworkObjectMappingUpdate(cachedObject, updatedObject);

            Assert.That(cachedObject.FlowNetworkObjectId, Is.EqualTo(800));
            Assert.That(cachedObject.FlowNetworkGroupId, Is.Null);
            Assert.That(cachedObject.FlowActive, Is.True);
        }

        [Test]
        public void MergeNetworkGroupMappingUpdate_ClearsCachedObjectMapping()
        {
            NetworkObject cachedObject = new()
            {
                Id = 42,
                Name = "cached",
                IP = "",
                IpEnd = "",
                Uid = "cached-uid",
                Active = true,
                FlowActive = false,
                FlowNetworkObjectId = 700,
                FlowNetworkGroupId = null
            };
            NetworkObject updatedObject = new()
            {
                Id = 42,
                Name = "updated",
                IP = "",
                IpEnd = "",
                Uid = "updated-uid",
                Active = true,
                FlowActive = true,
                FlowNetworkGroupId = 800
            };

            FlowAdminHelper.MergeNetworkGroupMappingUpdate(cachedObject, updatedObject);

            Assert.That(cachedObject.FlowNetworkGroupId, Is.EqualTo(800));
            Assert.That(cachedObject.FlowNetworkObjectId, Is.Null);
            Assert.That(cachedObject.FlowActive, Is.True);
        }

        [Test]
        public void MergeServiceObjectMappingUpdate_ClearsCachedGroupMapping()
        {
            NetworkService cachedService = new()
            {
                Id = 52,
                Name = "cached",
                Uid = "cached-uid",
                DestinationPort = 80,
                DestinationPortEnd = 80,
                Active = true,
                FlowActive = false,
                FlowServiceObjectId = null,
                FlowServiceGroupId = 900
            };
            NetworkService updatedService = new()
            {
                Id = 52,
                Name = "updated",
                Uid = "updated-uid",
                DestinationPort = 443,
                DestinationPortEnd = 443,
                Active = true,
                FlowActive = true,
                FlowServiceObjectId = 901
            };

            FlowAdminHelper.MergeServiceObjectMappingUpdate(cachedService, updatedService);

            Assert.That(cachedService.FlowServiceObjectId, Is.EqualTo(901));
            Assert.That(cachedService.FlowServiceGroupId, Is.Null);
            Assert.That(cachedService.FlowActive, Is.True);
        }

        [Test]
        public void MergeServiceGroupMappingUpdate_ClearsCachedObjectMapping()
        {
            NetworkService cachedService = new()
            {
                Id = 52,
                Name = "cached",
                Uid = "cached-uid",
                DestinationPort = 80,
                DestinationPortEnd = 80,
                Active = true,
                FlowActive = false,
                FlowServiceObjectId = 900,
                FlowServiceGroupId = null
            };
            NetworkService updatedService = new()
            {
                Id = 52,
                Name = "updated",
                Uid = "updated-uid",
                DestinationPort = 443,
                DestinationPortEnd = 443,
                Active = true,
                FlowActive = true,
                FlowServiceGroupId = 901
            };

            FlowAdminHelper.MergeServiceGroupMappingUpdate(cachedService, updatedService);

            Assert.That(cachedService.FlowServiceGroupId, Is.EqualTo(901));
            Assert.That(cachedService.FlowServiceObjectId, Is.Null);
            Assert.That(cachedService.FlowActive, Is.True);
        }
    }
}

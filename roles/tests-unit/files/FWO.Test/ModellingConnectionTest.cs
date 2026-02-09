using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Modelling;
using System.Collections.Generic;
using System.Linq;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ModellingConnectionTest
    {
        [Test]
        public void SyncState_InterfaceRequestedSetsRequestedFlag()
        {
            ModellingConnection conn = new()
            {
                IsInterface = true,
                IsRequested = true,
                IsPublished = false
            };

            conn.SyncState(0);

            ClassicAssert.IsTrue(conn.GetBoolProperty(ConState.Requested.ToString()));
            ClassicAssert.IsFalse(conn.GetBoolProperty(ConState.Decommissioned.ToString()));
        }

        [Test]
        public void SyncState_InterfaceRejectedSkipsRequestedFlag()
        {
            ModellingConnection conn = new()
            {
                IsInterface = true,
                IsRequested = true,
                IsPublished = false
            };
            conn.AddProperty(ConState.Rejected.ToString());

            conn.SyncState(0);

            ClassicAssert.IsTrue(conn.GetBoolProperty(ConState.Rejected.ToString()));
            ClassicAssert.IsFalse(conn.GetBoolProperty(ConState.Requested.ToString()));
        }

        [Test]
        public void SyncState_InterfaceUserSetsRequestedAndDecommissionedFlags()
        {
            ModellingConnection conn = new()
            {
                IsInterface = false,
                UsedInterfaceId = 99,
                InterfaceIsRequested = true,
                InterfaceIsDecommissioned = true
            };

            conn.SyncState(0);

            ClassicAssert.IsTrue(conn.GetBoolProperty(ConState.InterfaceRequested.ToString()));
            ClassicAssert.IsTrue(conn.GetBoolProperty(ConState.InterfaceDecommissioned.ToString()));
            ClassicAssert.IsFalse(conn.GetBoolProperty(ConState.InterfaceRejected.ToString()));
        }

        [Test]
        public void SyncState_InterfaceUserRejectedOverridesRequested()
        {
            ModellingConnection conn = new()
            {
                IsInterface = false,
                UsedInterfaceId = 99,
                InterfaceIsRequested = true,
                InterfaceIsRejected = true
            };

            conn.SyncState(0);

            ClassicAssert.IsTrue(conn.GetBoolProperty(ConState.InterfaceRejected.ToString()));
            ClassicAssert.IsFalse(conn.GetBoolProperty(ConState.InterfaceRequested.ToString()));
        }

        [Test]
        public void SyncState_MemberIssuesAndDocumentationOnlySet()
        {
            ModellingAppRole emptyRole = new() { Id = 1, Name = "AR1", AppServers = [] };
            ModellingServiceGroup emptySvcGrp = new() { Id = 2, Name = "SG1", Services = [] };
            ModellingNetworkArea deletedArea = new() { Id = 3, Name = "Area", IsDeleted = true };

            ModellingConnection conn = new()
            {
                SourceAppRoles = [new ModellingAppRoleWrapper { Content = emptyRole }],
                ServiceGroups = [new ModellingServiceGroupWrapper { Content = emptySvcGrp }],
                SourceAreas = [new ModellingNetworkAreaWrapper { Content = deletedArea }],
                ExtraConfigs = [new ModellingExtraConfig { ExtraConfigType = GlobalConst.kDoku_ + "text" }]
            };

            conn.SyncState(0);

            ClassicAssert.IsTrue(conn.GetBoolProperty(ConState.EmptyAppRoles.ToString()));
            ClassicAssert.IsTrue(conn.GetBoolProperty(ConState.EmptySvcGrps.ToString()));
            ClassicAssert.IsTrue(conn.GetBoolProperty(ConState.DeletedObjects.ToString()));
            ClassicAssert.IsTrue(conn.GetBoolProperty(ConState.DocumentationOnly.ToString()));
        }

        [Test]
        public void SyncState_IgnoresDummyAppRoleForEmptyCheck()
        {
            ModellingAppRole dummyRole = new() { Id = 99, Name = "Dummy", AppServers = [] };
            ModellingConnection conn = new()
            {
                SourceAppRoles = [new ModellingAppRoleWrapper { Content = dummyRole }]
            };

            conn.SyncState(99);

            ClassicAssert.IsFalse(conn.GetBoolProperty(ConState.EmptyAppRoles.ToString()));
        }

        [Test]
        public void ToRule_MapsSourcesDestinationsAndServices()
        {
            ModellingConnection conn = new()
            {
                Name = "Conn1",
                SourceAreas = [new ModellingNetworkAreaWrapper { Content = new ModellingNetworkArea { Id = 1, Name = "Area1", IdString = "NA1" } }],
                SourceOtherGroups = [new ModellingNwGroupWrapper { Content = new ModellingNwGroup { Id = 2, Name = "Group1", IdString = "GR1" } }],
                SourceAppRoles = [new ModellingAppRoleWrapper { Content = new ModellingAppRole { Id = 3, Name = "AR1", IdString = "AR1" } }],
                SourceAppServers = [new ModellingAppServerWrapper { Content = new ModellingAppServer { Id = 4, Name = "Server1", Ip = "10.0.0.1" } }],
                DestinationAreas = [new ModellingNetworkAreaWrapper { Content = new ModellingNetworkArea { Id = 5, Name = "Area2", IdString = "NA2" } }],
                DestinationOtherGroups = [new ModellingNwGroupWrapper { Content = new ModellingNwGroup { Id = 6, Name = "Group2", IdString = "GR2" } }],
                DestinationAppRoles = [new ModellingAppRoleWrapper { Content = new ModellingAppRole { Id = 7, Name = "AR2", IdString = "AR2" } }],
                DestinationAppServers = [new ModellingAppServerWrapper { Content = new ModellingAppServer { Id = 8, Name = "Server2", Ip = "10.0.0.2" } }],
                ServiceGroups = [new ModellingServiceGroupWrapper { Content = new ModellingServiceGroup { Id = 9, Name = "SvcGrp1", Services = [] } }],
                Services = [new ModellingServiceWrapper { Content = new ModellingService { Id = 10, Name = "Svc1", ProtoId = 6, Port = 80 } }]
            };

            Rule rule = conn.ToRule();

            ClassicAssert.AreEqual("Conn1", rule.Name);
            ClassicAssert.AreEqual(4, rule.Froms.Count());
            ClassicAssert.AreEqual(4, rule.Tos.Count());
            ClassicAssert.AreEqual(2, rule.Services.Count());
        }

        [Test]
        public void Sanitize_ShortensFields()
        {
            ModellingConnection conn = new()
            {
                Name = " Name ",
                Reason = "Reason\u0000",
                Creator = " Creator ",
                Properties = "  { }  ",
                ExtraParams = "  { }  "
            };

            bool shortened = conn.Sanitize();

            ClassicAssert.IsTrue(shortened);
            ClassicAssert.IsFalse(conn.Name?.Contains(" ") ?? false);
            ClassicAssert.IsFalse(conn.Creator?.Contains(" ") ?? false);
            ClassicAssert.AreEqual("Reason\0", conn.Reason);
        }
    }
}

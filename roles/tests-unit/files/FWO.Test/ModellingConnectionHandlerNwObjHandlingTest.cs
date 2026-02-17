using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Services;
using FWO.Services.Modelling;
using FWO.Basics;
using FWO.Config.Api.Data;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ModellingConnectionHandlerNwObjHandlingTest
    {
        static readonly SimulatedUserConfig userConfig = new();
        static readonly Action<Exception?, string, string, bool> DisplayMessageInUi = DefaultInit.DoNothing;
        static readonly FwoOwner Application = new() { Id = 1, Name = "TestApp" };

        [Test]
        public void RefreshSelectableNwObjects_IncludesCommonSelectedRolesAndServers()
        {
            ModellingConnection connection = new() { Id = 1 };
            SimulatedUserConfig localConfig = new() { AllowServerInConn = true };
            ModellingConnectionHandler handler = CreateHandler(connection, localConfig);

            handler.AvailableCommonAreas =
            [
                new ModellingNetworkAreaWrapper { Content = new ModellingNetworkArea { Id = 10, Name = "Area1", IdString = "NA10", GroupType = (int)ModellingTypes.ModObjectType.NetworkArea } }
            ];
            handler.AvailableSelectedObjects =
            [
                new ModellingNwGroupWrapper { Content = new ModellingNwGroup { Id = 20, Name = "Group1", GroupType = (int)ModellingTypes.ModObjectType.Network } }
            ];
            handler.AvailableAppRoles =
            [
                new ModellingAppRole { Id = 30, Name = "Role1" }
            ];
            handler.AvailableAppServers =
            [
                new ModellingAppServer { Id = 40, Name = "Srv1", IsDeleted = false },
                new ModellingAppServer { Id = 41, Name = "Srv2", IsDeleted = true }
            ];

            bool result = handler.RefreshSelectableNwObjects();

            ClassicAssert.IsTrue(result);
            ClassicAssert.AreEqual(4, handler.AvailableNwElems.Count);
            ClassicAssert.IsTrue(handler.AvailableNwElems.Contains(new KeyValuePair<int, long>((int)ModellingTypes.ModObjectType.NetworkArea, 10)));
            ClassicAssert.IsTrue(handler.AvailableNwElems.Contains(new KeyValuePair<int, long>((int)ModellingTypes.ModObjectType.Network, 20)));
            ClassicAssert.IsTrue(handler.AvailableNwElems.Contains(new KeyValuePair<int, long>((int)ModellingTypes.ModObjectType.AppRole, 30)));
            ClassicAssert.IsTrue(handler.AvailableNwElems.Contains(new KeyValuePair<int, long>((int)ModellingTypes.ModObjectType.AppServer, 40)));
        }

        [Test]
        public void SyncSrcChanges_AddsAndRemovesObjects()
        {
            ModellingConnection connection = new()
            {
                Id = 2,
                SourceAppServers = [WrapAppServer(1, "SrvOld")],
                SourceAppRoles = [WrapAppRole(2, "RoleOld")],
                SourceAreas = [WrapArea(3, "AreaOld")],
                SourceOtherGroups = [WrapNwGroup(4, "GroupOld")]
            };
            ModellingConnectionHandler handler = CreateHandler(connection);

            handler.SrcAppServerToDelete.Add(new ModellingAppServer { Id = 1 });
            handler.SrcAppRolesToDelete.Add(new ModellingAppRole { Id = 2 });
            handler.SrcAreasToDelete.Add(new ModellingNetworkArea { Id = 3 });
            handler.SrcNwGroupsToDelete.Add(new ModellingNwGroup { Id = 4 });

            handler.SrcAppServerToAdd.Add(new ModellingAppServer { Id = 11, Name = "SrvNew" });
            handler.SrcAppRolesToAdd.Add(new ModellingAppRole { Id = 12, Name = "RoleNew" });
            handler.SrcAreasToAdd.Add(new ModellingNetworkArea { Id = 13, Name = "AreaNew", IdString = "NA13" });
            handler.SrcNwGroupsToAdd.Add(new ModellingNwGroup { Id = 14, Name = "GroupNew", IdString = "GR14" });

            InvokePrivateVoid(handler, "SyncSrcChanges");

            ClassicAssert.AreEqual(1, handler.ActConn.SourceAppServers.Count);
            ClassicAssert.AreEqual(11, handler.ActConn.SourceAppServers[0].Content.Id);
            ClassicAssert.AreEqual(1, handler.ActConn.SourceAppRoles.Count);
            ClassicAssert.AreEqual(12, handler.ActConn.SourceAppRoles[0].Content.Id);
            ClassicAssert.AreEqual(1, handler.ActConn.SourceAreas.Count);
            ClassicAssert.AreEqual(13, handler.ActConn.SourceAreas[0].Content.Id);
            ClassicAssert.AreEqual(1, handler.ActConn.SourceOtherGroups.Count);
            ClassicAssert.AreEqual(14, handler.ActConn.SourceOtherGroups[0].Content.Id);
        }

        [Test]
        public void SyncDstChanges_AddsAndRemovesObjects()
        {
            ModellingConnection connection = new()
            {
                Id = 3,
                DestinationAppServers = [WrapAppServer(21, "SrvOld")],
                DestinationAppRoles = [WrapAppRole(22, "RoleOld")],
                DestinationAreas = [WrapArea(23, "AreaOld")],
                DestinationOtherGroups = [WrapNwGroup(24, "GroupOld")]
            };
            ModellingConnectionHandler handler = CreateHandler(connection);

            handler.DstAppServerToDelete.Add(new ModellingAppServer { Id = 21 });
            handler.DstAppRolesToDelete.Add(new ModellingAppRole { Id = 22 });
            handler.DstAreasToDelete.Add(new ModellingNetworkArea { Id = 23 });
            handler.DstNwGroupsToDelete.Add(new ModellingNwGroup { Id = 24 });

            handler.DstAppServerToAdd.Add(new ModellingAppServer { Id = 31, Name = "SrvNew" });
            handler.DstAppRolesToAdd.Add(new ModellingAppRole { Id = 32, Name = "RoleNew" });
            handler.DstAreasToAdd.Add(new ModellingNetworkArea { Id = 33, Name = "AreaNew", IdString = "NA33" });
            handler.DstNwGroupsToAdd.Add(new ModellingNwGroup { Id = 34, Name = "GroupNew", IdString = "GR34" });

            InvokePrivateVoid(handler, "SyncDstChanges");

            ClassicAssert.AreEqual(1, handler.ActConn.DestinationAppServers.Count);
            ClassicAssert.AreEqual(31, handler.ActConn.DestinationAppServers[0].Content.Id);
            ClassicAssert.AreEqual(1, handler.ActConn.DestinationAppRoles.Count);
            ClassicAssert.AreEqual(32, handler.ActConn.DestinationAppRoles[0].Content.Id);
            ClassicAssert.AreEqual(1, handler.ActConn.DestinationAreas.Count);
            ClassicAssert.AreEqual(33, handler.ActConn.DestinationAreas[0].Content.Id);
            ClassicAssert.AreEqual(1, handler.ActConn.DestinationOtherGroups.Count);
            ClassicAssert.AreEqual(34, handler.ActConn.DestinationOtherGroups[0].Content.Id);
        }

        [Test]
        public void NetworkAreaUseAllowed_BlocksWhenOtherSideHasAreas()
        {
            ModellingConnection connection = new()
            {
                Id = 4,
                DestinationAreas = [WrapArea(10, "AreaDst")]
            };
            ModellingConnectionHandler handler = CreateHandler(connection);

            bool result = handler.NetworkAreaUseAllowed([new ModellingNetworkArea { Id = 1 }], Direction.Source, out var reason);

            ClassicAssert.IsFalse(result);
            ClassicAssert.AreEqual("Edit Connection", reason.Title);
            ClassicAssert.AreEqual("Network areas on other side", reason.Text);
        }

        [Test]
        public void NetworkAreaUseAllowed_BlocksForInterfaces()
        {
            ModellingConnection connection = new()
            {
                Id = 5,
                IsInterface = true
            };
            ModellingConnectionHandler handler = CreateHandler(connection);

            bool result = handler.NetworkAreaUseAllowed([new ModellingNetworkArea { Id = 1 }], Direction.Source, out var reason);

            ClassicAssert.IsFalse(result);
            ClassicAssert.AreEqual("Edit Interface", reason.Title);
            ClassicAssert.AreEqual("Interfaces cannot use areas", reason.Text);
        }

        [Test]
        public void NetworkAreaUseAllowed_AllowsCommonServiceWithoutCommonAreas()
        {
            ModellingConnection connection = new()
            {
                Id = 6,
                IsCommonService = true
            };
            ModellingConnectionHandler handler = CreateHandler(connection);
            handler.CommonAreaConfigItems = [new CommonAreaConfig { AreaId = 100 }];

            bool result = handler.NetworkAreaUseAllowed([new ModellingNetworkArea { Id = 1 }], Direction.Source, out var reason);

            ClassicAssert.IsTrue(result);
            ClassicAssert.AreEqual("Edit Common Service", reason.Title);
            ClassicAssert.AreEqual("", reason.Text);
        }

        [Test]
        public void NetworkAreaUseAllowed_AllowsCommonAreasOnNormalConnection()
        {
            ModellingConnection connection = new() { Id = 7 };
            ModellingConnectionHandler handler = CreateHandler(connection);
            handler.CommonAreaConfigItems = [new CommonAreaConfig { AreaId = 1 }];

            bool result = handler.NetworkAreaUseAllowed([new ModellingNetworkArea { Id = 1 }], Direction.Source, out var reason);

            ClassicAssert.IsTrue(result);
            ClassicAssert.AreEqual("Edit Connection", reason.Title);
        }

        [Test]
        public void NetworkAreaUseAllowed_BlocksWhenCommonAreasNotAllowed()
        {
            ModellingConnection connection = new() { Id = 8 };
            ModellingConnectionHandler handler = CreateHandler(connection);
            handler.CommonAreaConfigItems = [new CommonAreaConfig { AreaId = 1 }];

            bool result = handler.NetworkAreaUseAllowed([new ModellingNetworkArea { Id = 2 }], Direction.Source, out var reason);

            ClassicAssert.IsFalse(result);
            ClassicAssert.AreEqual("Common areas not allowed", reason.Text);
        }

        [Test]
        public void AppServerToSource_AddsNonDeletedAndSkipsExisting()
        {
            ModellingConnection connection = new()
            {
                Id = 9,
                SourceAppServers = [WrapAppServer(1, "SrvExisting")]
            };
            ModellingConnectionHandler handler = CreateHandler(connection);
            handler.SrcReadOnly = false;

            handler.AppServerToSource(
            [
                new ModellingAppServer { Id = 1, Name = "SrvExisting", IsDeleted = false },
                new ModellingAppServer { Id = 2, Name = "SrvNew", IsDeleted = false },
                new ModellingAppServer { Id = 3, Name = "SrvDeleted", IsDeleted = true }
            ]);

            ClassicAssert.AreEqual(1, handler.SrcAppServerToAdd.Count);
            ClassicAssert.AreEqual(2, handler.SrcAppServerToAdd[0].Id);
        }

        [Test]
        public void AppServerToDestination_AddsNonDeletedAndSkipsExisting()
        {
            ModellingConnection connection = new()
            {
                Id = 10,
                DestinationAppServers = [WrapAppServer(1, "SrvExisting")]
            };
            ModellingConnectionHandler handler = CreateHandler(connection);
            handler.DstReadOnly = false;

            handler.AppServerToDestination(
            [
                new ModellingAppServer { Id = 1, Name = "SrvExisting", IsDeleted = false },
                new ModellingAppServer { Id = 2, Name = "SrvNew", IsDeleted = false },
                new ModellingAppServer { Id = 3, Name = "SrvDeleted", IsDeleted = true }
            ]);

            ClassicAssert.AreEqual(1, handler.DstAppServerToAdd.Count);
            ClassicAssert.AreEqual(2, handler.DstAppServerToAdd[0].Id);
        }

        [Test]
        public void AreasToSource_AddsWhenAllowedAndNotExisting()
        {
            ModellingConnection connection = new()
            {
                Id = 11,
                SourceAreas = [WrapArea(1, "AreaExisting")]
            };
            ModellingConnectionHandler handler = CreateHandler(connection);
            handler.SrcReadOnly = false;

            handler.AreasToSource(
            [
                new ModellingNetworkArea { Id = 1, Name = "AreaExisting", IdString = "NA1" },
                new ModellingNetworkArea { Id = 2, Name = "AreaNew", IdString = "NA2" }
            ]);

            ClassicAssert.AreEqual(1, handler.SrcAreasToAdd.Count);
            ClassicAssert.AreEqual(2, handler.SrcAreasToAdd[0].Id);
        }

        [Test]
        public void AreasToSource_SkipsWhenCommonAreaConfigDisallows()
        {
            ModellingConnection connection = new()
            {
                Id = 12
            };
            ModellingConnectionHandler handler = CreateHandler(connection);
            handler.SrcReadOnly = false;
            handler.CommonAreaConfigItems = [new CommonAreaConfig { AreaId = 2, UseInSrc = false }];

            handler.AreasToSource(
            [
                new ModellingNetworkArea { Id = 2, Name = "AreaBlocked", IdString = "NA2" }
            ]);

            ClassicAssert.AreEqual(0, handler.SrcAreasToAdd.Count);
        }

        [Test]
        public void AreasToDestination_AddsWhenAllowedAndNotExisting()
        {
            ModellingConnection connection = new()
            {
                Id = 13,
                DestinationAreas = [WrapArea(1, "AreaExisting")]
            };
            ModellingConnectionHandler handler = CreateHandler(connection);
            handler.DstReadOnly = false;

            handler.AreasToDestination(
            [
                new ModellingNetworkArea { Id = 1, Name = "AreaExisting", IdString = "NA1" },
                new ModellingNetworkArea { Id = 2, Name = "AreaNew", IdString = "NA2" }
            ]);

            ClassicAssert.AreEqual(1, handler.DstAreasToAdd.Count);
            ClassicAssert.AreEqual(2, handler.DstAreasToAdd[0].Id);
        }

        [Test]
        public void AreasToDestination_SkipsWhenCommonAreaConfigDisallows()
        {
            ModellingConnection connection = new()
            {
                Id = 14
            };
            ModellingConnectionHandler handler = CreateHandler(connection);
            handler.DstReadOnly = false;
            handler.CommonAreaConfigItems = [new CommonAreaConfig { AreaId = 3, UseInSrc = false }];

            handler.AreasToDestination(
            [
                new ModellingNetworkArea { Id = 3, Name = "AreaBlocked", IdString = "NA3" }
            ]);

            ClassicAssert.AreEqual(0, handler.DstAreasToAdd.Count);
        }

        [Test]
        public void NwGroupToSource_AddsWhenAllowedAndNotExisting()
        {
            ModellingConnection connection = new()
            {
                Id = 15,
                SourceOtherGroups = [WrapNwGroup(1, "GroupExisting")]
            };
            ModellingConnectionHandler handler = CreateHandler(connection);
            handler.SrcReadOnly = false;

            handler.NwGroupToSource(
            [
                new ModellingNwGroup { Id = 1, Name = "GroupExisting", IdString = "GR1" },
                new ModellingNwGroup { Id = 2, Name = "GroupNew", IdString = "GR2" }
            ]);

            ClassicAssert.AreEqual(1, handler.SrcNwGroupsToAdd.Count);
            ClassicAssert.AreEqual(2, handler.SrcNwGroupsToAdd[0].Id);
        }

        [Test]
        public void NwGroupToSource_SkipsWhenCommonAreaConfigDisallows()
        {
            ModellingConnection connection = new()
            {
                Id = 16
            };
            ModellingConnectionHandler handler = CreateHandler(connection);
            handler.SrcReadOnly = false;
            handler.CommonAreaConfigItems = [new CommonAreaConfig { AreaId = 2, UseInSrc = false }];

            handler.NwGroupToSource(
            [
                new ModellingNwGroup { Id = 2, Name = "GroupBlocked", IdString = "GR2" }
            ]);

            ClassicAssert.AreEqual(0, handler.SrcNwGroupsToAdd.Count);
        }

        [Test]
        public void NwGroupToDestination_AddsWhenAllowedAndNotExisting()
        {
            ModellingConnection connection = new()
            {
                Id = 17,
                DestinationOtherGroups = [WrapNwGroup(1, "GroupExisting")]
            };
            ModellingConnectionHandler handler = CreateHandler(connection);
            handler.DstReadOnly = false;

            handler.NwGroupToDestination(
            [
                new ModellingNwGroup { Id = 1, Name = "GroupExisting", IdString = "GR1" },
                new ModellingNwGroup { Id = 2, Name = "GroupNew", IdString = "GR2" }
            ]);

            ClassicAssert.AreEqual(1, handler.DstNwGroupsToAdd.Count);
            ClassicAssert.AreEqual(2, handler.DstNwGroupsToAdd[0].Id);
        }

        [Test]
        public void NwGroupToDestination_SkipsWhenCommonAreaConfigDisallows()
        {
            ModellingConnection connection = new()
            {
                Id = 18
            };
            ModellingConnectionHandler handler = CreateHandler(connection);
            handler.DstReadOnly = false;
            handler.CommonAreaConfigItems = [new CommonAreaConfig { AreaId = 2, UseInDst = false }];

            handler.NwGroupToDestination(
            [
                new ModellingNwGroup { Id = 2, Name = "GroupBlocked", IdString = "GR2" }
            ]);

            ClassicAssert.AreEqual(0, handler.DstNwGroupsToAdd.Count);
        }

        [Test]
        public void AppRolesToSource_AddsWhenNotExisting()
        {
            ModellingConnection connection = new()
            {
                Id = 19,
                SourceAppRoles = [WrapAppRole(1, "RoleExisting")]
            };
            ModellingConnectionHandler handler = CreateHandler(connection);
            handler.SrcReadOnly = false;

            handler.AppRolesToSource(
            [
                new ModellingAppRole { Id = 1, Name = "RoleExisting" },
                new ModellingAppRole { Id = 2, Name = "RoleNew" }
            ]);

            ClassicAssert.AreEqual(1, handler.SrcAppRolesToAdd.Count);
            ClassicAssert.AreEqual(2, handler.SrcAppRolesToAdd[0].Id);
        }

        [Test]
        public void AppRolesToDestination_AddsWhenNotExisting()
        {
            ModellingConnection connection = new()
            {
                Id = 20,
                DestinationAppRoles = [WrapAppRole(1, "RoleExisting")]
            };
            ModellingConnectionHandler handler = CreateHandler(connection);
            handler.DstReadOnly = false;

            handler.AppRolesToDestination(
            [
                new ModellingAppRole { Id = 1, Name = "RoleExisting" },
                new ModellingAppRole { Id = 2, Name = "RoleNew" }
            ]);

            ClassicAssert.AreEqual(1, handler.DstAppRolesToAdd.Count);
            ClassicAssert.AreEqual(2, handler.DstAppRolesToAdd[0].Id);
        }

        private static ModellingConnectionHandler CreateHandler(ModellingConnection connection)
        {
            return new ModellingConnectionHandler(new ModellingHandlerTestApiConn(), userConfig, Application, [connection], connection, false, false, DisplayMessageInUi, DefaultInit.DoNothing, true);
        }

        private static ModellingConnectionHandler CreateHandler(ModellingConnection connection, SimulatedUserConfig config)
        {
            return new ModellingConnectionHandler(new ModellingHandlerTestApiConn(), config, Application, [connection], connection, false, false, DisplayMessageInUi, DefaultInit.DoNothing, true);
        }

        private static void InvokePrivateVoid(ModellingConnectionHandler handler, string methodName)
        {
            MethodInfo? method = typeof(ModellingConnectionHandler).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            ClassicAssert.IsNotNull(method, $"Expected to find method '{methodName}'.");
            method!.Invoke(handler, null);
        }

        private static ModellingNetworkAreaWrapper WrapArea(long id, string name)
        {
            return new ModellingNetworkAreaWrapper { Content = new ModellingNetworkArea { Id = id, Name = name, IdString = $"NA{id}" } };
        }

        private static ModellingAppServerWrapper WrapAppServer(long id, string name)
        {
            return new ModellingAppServerWrapper { Content = new ModellingAppServer { Id = id, Name = name } };
        }

        private static ModellingAppRoleWrapper WrapAppRole(long id, string name)
        {
            return new ModellingAppRoleWrapper { Content = new ModellingAppRole { Id = id, Name = name } };
        }

        private static ModellingNwGroupWrapper WrapNwGroup(long id, string name)
        {
            return new ModellingNwGroupWrapper { Content = new ModellingNwGroup { Id = id, Name = name, IdString = $"GR{id}" } };
        }
    }
}

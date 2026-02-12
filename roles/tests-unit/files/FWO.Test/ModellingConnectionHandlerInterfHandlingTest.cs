using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Services;
using FWO.Basics;
using System.Collections.Generic;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ModellingConnectionHandlerInterfHandlingTest
    {
        static readonly SimulatedUserConfig userConfig = new();
        static readonly Action<Exception?, string, string, bool> DisplayMessageInUi = DefaultInit.DoNothing;
        static readonly FwoOwner Application = new() { Id = 1, Name = "TestApp" };

        [Test]
        public void InterfaceToConn_UsesInterfaceSourceWhenSourceFilled()
        {
            ModellingConnection connection = new()
            {
                Id = 1,
                SourceAppServers = [WrapAppServer(10, "AS_old")],
                SourceAppRoles = [WrapAppRole(20, "AR_old")],
                SourceAreas = [WrapArea(30, "Area_old")],
                SourceOtherGroups = [WrapNwGroup(40, "Group_old")],
                DestinationAppServers = [WrapAppServer(110, "AS_dst_old")],
                DestinationAppRoles = [WrapAppRole(120, "AR_dst_old")],
                DestinationAreas = [WrapArea(130, "Area_dst_old")],
                DestinationOtherGroups = [WrapNwGroup(140, "Group_dst_old")]
            };

            ModellingConnection interf = new()
            {
                Id = 2,
                Name = "Interf1",
                IsRequested = true,
                SourceAppServers = [WrapAppServer(11, "AS_new")],
                SourceAppRoles = [WrapAppRole(21, "AR_new")],
                SourceAreas = [WrapArea(31, "Area_new")],
                SourceOtherGroups = [WrapNwGroup(41, "Group_new")],
                Services = [WrapService(51, "Svc_new")],
                ServiceGroups = [WrapServiceGroup(61, "SvcGrp_new")],
                ExtraConfigs = [new ModellingExtraConfig() { ExtraConfigType = "X", ExtraConfigText = "Y" }]
            };

            ModellingConnectionHandler handler = CreateHandler(connection);

            handler.InterfaceToConn(interf);

            ClassicAssert.AreEqual("Interf1", handler.InterfaceName);
            ClassicAssert.IsTrue(handler.SrcReadOnly);
            ClassicAssert.IsFalse(handler.DstReadOnly);
            ClassicAssert.IsTrue(handler.SvcReadOnly);
            ClassicAssert.IsTrue(handler.ActConn.SrcFromInterface);
            ClassicAssert.IsFalse(handler.ActConn.DstFromInterface);
            ClassicAssert.AreEqual(interf.Id, handler.ActConn.UsedInterfaceId);
            ClassicAssert.AreEqual(1, handler.SrcAppServerToDelete.Count);
            ClassicAssert.AreEqual(10, handler.SrcAppServerToDelete[0].Id);
            ClassicAssert.AreEqual(1, handler.SrcAppRolesToDelete.Count);
            ClassicAssert.AreEqual(20, handler.SrcAppRolesToDelete[0].Id);
            ClassicAssert.AreEqual(1, handler.SrcAreasToDelete.Count);
            ClassicAssert.AreEqual(30, handler.SrcAreasToDelete[0].Id);
            ClassicAssert.AreEqual(1, handler.SrcNwGroupsToDelete.Count);
            ClassicAssert.AreEqual(40, handler.SrcNwGroupsToDelete[0].Id);
            ClassicAssert.AreEqual(11, handler.ActConn.SourceAppServers[0].Content.Id);
            ClassicAssert.AreEqual(21, handler.ActConn.SourceAppRoles[0].Content.Id);
            ClassicAssert.AreEqual(31, handler.ActConn.SourceAreas[0].Content.Id);
            ClassicAssert.AreEqual(41, handler.ActConn.SourceOtherGroups[0].Content.Id);
            ClassicAssert.AreEqual(110, handler.ActConn.DestinationAppServers[0].Content.Id);
            ClassicAssert.AreEqual(1, handler.ActConn.Services.Count);
            ClassicAssert.AreEqual(51, handler.ActConn.Services[0].Content.Id);
            ClassicAssert.AreEqual(1, handler.ActConn.ServiceGroups.Count);
            ClassicAssert.AreEqual(61, handler.ActConn.ServiceGroups[0].Content.Id);
            ClassicAssert.AreEqual(1, handler.ActConn.ExtraConfigsFromInterface.Count);
        }

        [Test]
        public void InterfaceToConn_UsesInterfaceDestinationWhenDestinationFilled()
        {
            ModellingConnection connection = new()
            {
                Id = 3,
                SourceAppServers = [WrapAppServer(210, "AS_src_old")],
                DestinationAppServers = [WrapAppServer(310, "AS_dst_old")],
                DestinationAppRoles = [WrapAppRole(320, "AR_dst_old")],
                DestinationAreas = [WrapArea(330, "Area_dst_old")],
                DestinationOtherGroups = [WrapNwGroup(340, "Group_dst_old")]
            };

            ModellingConnection interf = new()
            {
                Id = 4,
                Name = "Interf2",
                DestinationAppServers = [WrapAppServer(311, "AS_dst_new")],
                DestinationAppRoles = [WrapAppRole(321, "AR_dst_new")],
                DestinationAreas = [WrapArea(331, "Area_dst_new")],
                DestinationOtherGroups = [WrapNwGroup(341, "Group_dst_new")]
            };

            ModellingConnectionHandler handler = CreateHandler(connection);

            handler.InterfaceToConn(interf);

            ClassicAssert.IsFalse(handler.SrcReadOnly);
            ClassicAssert.IsTrue(handler.DstReadOnly);
            ClassicAssert.IsTrue(handler.ActConn.DstFromInterface);
            ClassicAssert.IsFalse(handler.ActConn.SrcFromInterface);
            ClassicAssert.AreEqual(1, handler.DstAppServerToDelete.Count);
            ClassicAssert.AreEqual(310, handler.DstAppServerToDelete[0].Id);
            ClassicAssert.AreEqual(1, handler.DstAppRolesToDelete.Count);
            ClassicAssert.AreEqual(320, handler.DstAppRolesToDelete[0].Id);
            ClassicAssert.AreEqual(1, handler.DstAreasToDelete.Count);
            ClassicAssert.AreEqual(330, handler.DstAreasToDelete[0].Id);
            ClassicAssert.AreEqual(1, handler.DstNwGroupsToDelete.Count);
            ClassicAssert.AreEqual(340, handler.DstNwGroupsToDelete[0].Id);
            ClassicAssert.AreEqual(311, handler.ActConn.DestinationAppServers[0].Content.Id);
            ClassicAssert.AreEqual(321, handler.ActConn.DestinationAppRoles[0].Content.Id);
            ClassicAssert.AreEqual(331, handler.ActConn.DestinationAreas[0].Content.Id);
            ClassicAssert.AreEqual(341, handler.ActConn.DestinationOtherGroups[0].Content.Id);
            ClassicAssert.AreEqual(210, handler.ActConn.SourceAppServers[0].Content.Id);
        }

        [Test]
        public void RemoveInterf_ClearsInterfaceState()
        {
            ModellingConnection connection = new()
            {
                Id = 5,
                UsedInterfaceId = 7,
                InterfaceIsRequested = true,
                InterfaceIsRejected = true,
                InterfaceIsDecommissioned = true,
                TicketId = 99,
                SourceAppServers = [WrapAppServer(1, "AS_src")],
                DestinationAppServers = [WrapAppServer(2, "AS_dst")],
                Services = [WrapService(3, "Svc")],
                ServiceGroups = [WrapServiceGroup(4, "SvcGrp")],
                ExtraConfigsFromInterface = [new ModellingExtraConfig() { ExtraConfigType = "X", ExtraConfigText = "Y" }],
                SrcFromInterface = true,
                DstFromInterface = true
            };

            ModellingConnectionHandler handler = CreateHandler(connection);
            handler.InterfaceName = "Interf";
            handler.SrcReadOnly = true;
            handler.DstReadOnly = true;
            handler.SvcReadOnly = true;

            handler.RemoveInterf();

            ClassicAssert.AreEqual("", handler.InterfaceName);
            ClassicAssert.AreEqual(0, handler.ActConn.SourceAppServers.Count);
            ClassicAssert.AreEqual(0, handler.ActConn.DestinationAppServers.Count);
            ClassicAssert.AreEqual(0, handler.ActConn.Services.Count);
            ClassicAssert.AreEqual(0, handler.ActConn.ServiceGroups.Count);
            ClassicAssert.AreEqual(0, handler.ActConn.ExtraConfigsFromInterface.Count);
            ClassicAssert.IsNull(handler.ActConn.UsedInterfaceId);
            ClassicAssert.IsFalse(handler.ActConn.InterfaceIsRequested);
            ClassicAssert.IsFalse(handler.ActConn.InterfaceIsRejected);
            ClassicAssert.IsFalse(handler.ActConn.InterfaceIsDecommissioned);
            ClassicAssert.IsNull(handler.ActConn.TicketId);
            ClassicAssert.IsFalse(handler.SrcReadOnly);
            ClassicAssert.IsFalse(handler.DstReadOnly);
            ClassicAssert.IsFalse(handler.SvcReadOnly);
            ClassicAssert.IsFalse(handler.ActConn.SrcFromInterface);
            ClassicAssert.IsFalse(handler.ActConn.DstFromInterface);
        }

        [Test]
        public void PreparePublishInterface_PublishesRequestedInterface()
        {
            ModellingConnection connection = new()
            {
                Id = 6,
                IsInterface = true,
                IsRequested = true,
                IsPublished = false,
                InterfacePermission = InterfacePermissions.Public.ToString(),
                AppId = null,
                ProposedAppId = 77
            };

            ModellingConnectionHandler handler = CreateHandler(connection);
            userConfig.User.Name = "Tester";

            bool result = handler.PreparePublishInterface();

            ClassicAssert.IsTrue(result);
            ClassicAssert.AreEqual("Tester", handler.ActConn.Creator);
            ClassicAssert.IsFalse(handler.ActConn.IsRequested);
            ClassicAssert.IsTrue(handler.ActConn.IsPublished);
            ClassicAssert.AreEqual(77, handler.ActConn.AppId);
            ClassicAssert.IsNull(handler.ActConn.ProposedAppId);
        }

        [Test]
        public void PreparePublishInterface_PrivateDoesNothing()
        {
            ModellingConnection connection = new()
            {
                Id = 7,
                IsInterface = true,
                IsRequested = true,
                IsPublished = false,
                InterfacePermission = InterfacePermissions.Private.ToString(),
                AppId = null,
                ProposedAppId = 88
            };

            ModellingConnectionHandler handler = CreateHandler(connection);
            userConfig.User.Name = "Tester";

            bool result = handler.PreparePublishInterface();

            ClassicAssert.IsFalse(result);
            ClassicAssert.IsTrue(handler.ActConn.IsRequested);
            ClassicAssert.IsFalse(handler.ActConn.IsPublished);
            ClassicAssert.IsNull(handler.ActConn.AppId);
            ClassicAssert.AreEqual(88, handler.ActConn.ProposedAppId);
        }

        [Test]
        public async Task RequestReplaceInterface_MismatchShowsErrorAndDoesNotEnableReplace()
        {
            bool messageRaised = false;
            Action<Exception?, string, string, bool> displayMessage = (_, __, ___, ____) => messageRaised = true;

            ModellingConnection connection = new()
            {
                Id = 8,
                SourceAppServers = [WrapAppServer(1, "AS_src")]
            };

            ModellingConnection interf = new()
            {
                Id = 9,
                DestinationAppServers = [WrapAppServer(2, "AS_dst")]
            };

            ModellingConnectionHandler handler = new ModellingConnectionHandler(
                new ModellingHandlerTestApiConn(),
                userConfig,
                Application,
                [connection],
                connection,
                false,
                false,
                displayMessage,
                DefaultInit.DoNothing,
                true);

            await handler.RequestReplaceInterface(interf);

            ClassicAssert.IsTrue(messageRaised);
            ClassicAssert.IsFalse(handler.ReplaceMode);
            ClassicAssert.IsFalse(handler.DisplaySelectedInterfaceMode);
            ClassicAssert.IsNull(handler.IntConnHandler);
        }

        [Test]
        public void DisplayInterface_UsesOwnerForDisplayName()
        {
            ModellingConnection connection = new() { Id = 10 };
            ModellingConnection interf = new() { Id = 11, Name = "InterfName", AppId = 5 };

            ModellingConnectionHandler handler = CreateHandler(connection);
            handler.AllApps = [new FwoOwner { Id = 5, Name = "OwnerName", ExtAppId = "APP-5" }];

            string result = handler.DisplayInterface(interf);

            ClassicAssert.AreEqual("InterfName (APP-5:OwnerName)", result);
        }

        [Test]
        public void DisplayInterface_FallsBackToNameWhenOwnerMissing()
        {
            ModellingConnection connection = new() { Id = 12 };
            ModellingConnection interf = new() { Id = 13, Name = "InterfOnly", AppId = 5 };

            ModellingConnectionHandler handler = CreateHandler(connection);
            handler.AllApps = [new FwoOwner { Id = 6, Name = "OtherOwner", ExtAppId = "APP-6" }];

            string result = handler.DisplayInterface(interf);

            ClassicAssert.AreEqual("InterfOnly", result);
        }

        [Test]
        public void DisplayInterface_ReturnsEmptyWhenNull()
        {
            ModellingConnection connection = new() { Id = 14 };
            ModellingConnectionHandler handler = CreateHandler(connection);

            ClassicAssert.AreEqual("", handler.DisplayInterface(null));
        }

        [Test]
        public void InterfaceAllowedWithNetworkArea_AllowsWhenInterfaceFlagSet()
        {
            ModellingConnection connection = new()
            {
                Id = 15,
                IsInterface = true,
                AppId = 1,
                SourceAreas = [WrapArea(1, "Area_src")]
            };
            ModellingConnectionHandler handler = CreateHandler(connection);
            handler.DstAreasToAdd.Add(WrapArea(2, "Area_dst").Content);

            ModellingConnection interf = new() { AppId = 99 };

            ClassicAssert.IsTrue(handler.InterfaceAllowedWithNetworkArea(interf));
        }

        [Test]
        public void InterfaceAllowedWithNetworkArea_BlocksDifferentAppWhenAreasPresent()
        {
            ModellingConnection connection = new()
            {
                Id = 16,
                AppId = 1,
                SourceAreas = [WrapArea(1, "Area_src")]
            };
            ModellingConnectionHandler handler = CreateHandler(connection);

            ModellingConnection interf = new() { AppId = 2 };

            ClassicAssert.IsFalse(handler.InterfaceAllowedWithNetworkArea(interf));
        }

        [Test]
        public void InterfaceAllowedWithNetworkArea_AllowsDifferentAppWhenNoAreasPresent()
        {
            ModellingConnection connection = new()
            {
                Id = 17,
                AppId = 1
            };
            ModellingConnectionHandler handler = CreateHandler(connection);

            ModellingConnection interf = new() { AppId = 2 };

            ClassicAssert.IsTrue(handler.InterfaceAllowedWithNetworkArea(interf));
        }

        [Test]
        public void IsNotInterfaceForeignToApp_BlocksWhenPreselectedInterfaceFromOtherApp()
        {
            ModellingConnection connection = new()
            {
                Id = 18,
                AppId = 1,
                UsedInterfaceId = 42
            };
            ModellingConnectionHandler handler = CreateHandler(connection);
            handler.PreselectedInterfaces = [new ModellingConnection { Id = 42, AppId = 2 }];

            ClassicAssert.IsFalse(handler.IsNotInterfaceForeignToApp());
        }

        [Test]
        public void IsNotInterfaceForeignToApp_AllowsWhenPreselectedInterfaceFromSameApp()
        {
            ModellingConnection connection = new()
            {
                Id = 19,
                AppId = 1,
                UsedInterfaceId = 42
            };
            ModellingConnectionHandler handler = CreateHandler(connection);
            handler.PreselectedInterfaces = [new ModellingConnection { Id = 42, AppId = 1 }];

            ClassicAssert.IsTrue(handler.IsNotInterfaceForeignToApp());
        }

        private static ModellingConnectionHandler CreateHandler(ModellingConnection connection)
        {
            return new ModellingConnectionHandler(new ModellingHandlerTestApiConn(), userConfig, Application, [connection], connection, false, false, DisplayMessageInUi, DefaultInit.DoNothing, true);
        }

        private static ModellingAppServerWrapper WrapAppServer(long id, string name)
        {
            return new ModellingAppServerWrapper { Content = new ModellingAppServer { Id = id, Name = name } };
        }

        private static ModellingAppRoleWrapper WrapAppRole(long id, string name)
        {
            return new ModellingAppRoleWrapper { Content = new ModellingAppRole { Id = id, Name = name } };
        }

        private static ModellingNetworkAreaWrapper WrapArea(long id, string name)
        {
            return new ModellingNetworkAreaWrapper { Content = new ModellingNetworkArea { Id = id, Name = name, IdString = $"NA{id}" } };
        }

        private static ModellingNwGroupWrapper WrapNwGroup(long id, string name)
        {
            return new ModellingNwGroupWrapper { Content = new ModellingNwGroup { Id = id, Name = name, IdString = $"GR{id}" } };
        }

        private static ModellingServiceWrapper WrapService(int id, string name)
        {
            return new ModellingServiceWrapper { Content = new ModellingService { Id = id, Name = name } };
        }

        private static ModellingServiceGroupWrapper WrapServiceGroup(int id, string name)
        {
            return new ModellingServiceGroupWrapper { Content = new ModellingServiceGroup { Id = id, Name = name } };
        }
    }
}
